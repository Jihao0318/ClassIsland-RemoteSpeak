using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using RemoteSpeak.Models;

namespace RemoteSpeak.Services;

/// <summary>
/// 只监听环回的 HTTP 服务：网页 POST 一段文本，插件就把这条提醒塞进宿主的提醒队列。
/// 这里只懂 HTTP，不认识界面和提醒——解析出文本就交给回调，任何解析失败只回错误码，不会把接受循环打断。
/// 服务只要没被显式关掉就一直试着保持监听：绑定失败、监听中断都按退避在后台自动重试。
/// </summary>
public sealed class RemoteSpeakServer : IDisposable
{
    private const int MaxBodyBytes = 64 * 1024;
    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);

    /// <summary>收到文本就交给提醒队列，第二个参数是接口的 repeat（没给就是 null，听本地设置的）；返回 false 表示插件还没就绪。</summary>
    private readonly Func<string, int?, SpeakOptions, Task<bool>> _onText;

    /// <summary>取景回调；没接上时 /snapshot 一律回 503。</summary>
    private readonly Func<CancellationToken, Task<SnapshotFrame>>? _onSnapshot;

    private readonly RequestLog _log;

    private readonly object _sync = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _supervisorCts;
    private int _port;
    private IReadOnlyList<ApiToken> _tokens = [];
    private bool _wantRunning;
    private bool _snapshotEnabled;

    /// <summary>同一句话反复出现就不再重复记（端口一直被占不该把 300 条日志刷满）。</summary>
    private string _lastIssue = string.Empty;

    /// <summary>上一次记探活的时间：网页轮询时一秒只留一行。</summary>
    private DateTime _lastHealthLogged = DateTime.MinValue;

    /// <summary>
    /// 每次 Start 递增。旧监督循环发现代次变了就直接退出：
    /// Stop() 之后、Cancel() 之前它可能刚好从 GetContextAsync 里抛出来，
    /// 光看 _wantRunning 会误以为是"监听断了"，转头把新监听器关掉。
    /// </summary>
    private int _generation;

    public RemoteSpeakServer(
        Func<string, int?, SpeakOptions, Task<bool>> onText,
        RequestLog log,
        Func<CancellationToken, Task<SnapshotFrame>>? onSnapshot = null)
    {
        _onText = onText;
        _log = log;
        _onSnapshot = onSnapshot;
    }

    /// <summary>正在监听的前缀；没起来时为空。</summary>
    public string Address { get; private set; } = string.Empty;

    /// <summary>最近一次失败的原因；正常监听时为空。后台重试期间这里会是「正在自动重试」那句话。</summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>地址或错误变了（含后台自动恢复）时触发。</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 按给定端口和 Token 列表起服务。返回 null 表示当场就绑上了；
    /// 返回失败原因时后台仍会按退避继续重试，直到绑上或者被 <see cref="Stop"/>。
    /// </summary>
    public string? Start(int port, IReadOnlyList<ApiToken> tokens, bool snapshotEnabled = false)
    {
        Stop();

        lock (_sync)
        {
            _port = port;
            _tokens = tokens;
            _snapshotEnabled = snapshotEnabled;
            _wantRunning = true;
            _lastIssue = string.Empty;
            // 先当场试一次，设置页好立刻给出成没成；没成也不放弃，交给监督循环。
            BindLocked();
            var generation = ++_generation;
            var cts = new CancellationTokenSource();
            _supervisorCts = cts;
            _ = Task.Run(() => SuperviseAsync(cts.Token, generation));
            return LastError.Length > 0 ? LastError : null;
        }
    }

    /// <summary>兼容只传一个 token 的老调用方（ServerCheck 用）。</summary>
    public string? Start(int port, string token, bool snapshotEnabled = false) =>
        Start(
            port,
            token.Length == 0
                ? []
                : [new ApiToken { Name = "默认", Value = token.Trim() }],
            snapshotEnabled);

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            _wantRunning = false;
            cts = _supervisorCts;
            _supervisorCts = null;
            CloseListenerLocked();
            Address = string.Empty;
            LastError = string.Empty;
        }

        cts?.Cancel();
        cts?.Dispose();
    }

    public void Dispose() => Stop();

    /// <summary>必须在 <see cref="_sync"/> 内调用。</summary>
    private void BindLocked()
    {
        // 只绑环回，普通权限就能监听；HttpListener 按前缀里的 Host 逐个匹配，
        // 而调用方有的写 localhost 有的写 127.0.0.1，所以两种写法都绑上。
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{_port}/");
        listener.Prefixes.Add($"http://127.0.0.1:{_port}/");

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            listener.Close();
            Address = string.Empty;
            LastError = DescribeStartFailure(ex, _port);
            LogIssueLocked(LastError);
            return;
        }
        catch (Exception ex)
        {
            listener.Close();
            Address = string.Empty;
            LastError = $"启动失败：{ex.Message}";
            LogIssueLocked(LastError);
            return;
        }

        _listener = listener;
        Address = $"http://localhost:{_port}/";
        LastError = string.Empty;
        _lastIssue = string.Empty; // 恢复了，下次再坏要重新记一笔
    }

    /// <summary>同一条问题反复出现只记一次，端口被长期占用不该把 100 条日志刷满。必须在锁内调用。</summary>
    private void LogIssueLocked(string message)
    {
        if (string.Equals(message, _lastIssue, StringComparison.Ordinal))
        {
            return;
        }

        _lastIssue = message;
        _log.Add("监听", message);
    }

    /// <summary>必须在 <see cref="_sync"/> 内调用。关掉旧监听器，顺带掐断挂起的 GetContextAsync。</summary>
    private void CloseListenerLocked()
    {
        if (_listener == null)
        {
            return;
        }

        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (HttpListenerException)
        {
            // 已经关掉了。
        }
        catch (ObjectDisposedException)
        {
        }

        _listener = null;
    }

    /// <summary>
    /// 监督循环：绑定没成就一直重试，监听中途断了也重来。
    /// 睡眠唤醒、端口被别的程序抢走、http.sys 抽风都会让 GetContextAsync 抛异常，
    /// 以前这些情况等于服务静悄悄消失，现在都会自己长回来。
    /// </summary>
    private async Task SuperviseAsync(CancellationToken cancellationToken, int generation)
    {
        var delay = FirstRetryDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            var listener = Volatile.Read(ref _listener);
            if (listener == null)
            {
                bool bound;
                lock (_sync)
                {
                    // 代次对不上说明已经被新的 Start 接管，这里不能再碰监听器。
                    if (!_wantRunning || generation != _generation)
                    {
                        return;
                    }

                    BindLocked();
                    PublishChanged();
                    delay = _listener == null ? delay : FirstRetryDelay;
                    bound = _listener != null;
                }

                if (!bound)
                {
                    // 还没绑上（多半是端口被占），下一轮再来；占用方一让开就能接住。
                    if (await WaitAsync(delay, cancellationToken))
                    {
                        return;
                    }

                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
                    continue;
                }

                listener = Volatile.Read(ref _listener)!;
            }

            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Stop() 掐监听器也走这里，所以先看取消标志；没被取消就是真断了。
                lock (_sync)
                {
                    // Stop() 之后、Cancel() 之前也会走到这里；代次变了就不是"真断了"。
                    if (!_wantRunning || generation != _generation)
                    {
                        return;
                    }

                    CloseListenerLocked();
                    LastError = "监听中断，正在自动重试。";
                    LogIssueLocked(LastError);
                    PublishChanged();
                    delay = FirstRetryDelay;
                }

                continue;
            }
            catch (Exception)
            {
                return;
            }

            _ = HandleAsync(context, cancellationToken);
        }
    }

    /// <summary>可取消的等待；返回 true 表示该退出了。</summary>
    private static async Task<bool> WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return true;
        }

        return false;
    }

    private void PublishChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string DescribeStartFailure(HttpListenerException ex, int port)
    {
        // 环回前缀基本只可能撞端口占用这一种。错误码按平台各认一套：
        // Windows 32/183/10048，Linux 98（EADDRINUSE），macOS 48（EADDRINUSE）。
        if (ex.ErrorCode is 32 or 183 or 10048 or 98 or 48)
        {
            return $"端口 {port} 暂时被其他程序占用，正在自动重试。";
        }

        if (ex.ErrorCode == 5)
        {
            return $"端口 {port} 被系统保留（可能落在 Windows 的排除端口段里），换个端口。";
        }

        return $"启动失败：{ex.Message}（错误码 {ex.ErrorCode}），正在自动重试。";
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        try
        {
            // 读写超时由 http.sys 兜着（默认 30s / 120s），客户端吊着不写完会自己断；这里只顺着请求复用连接。
            response.KeepAlive = request.KeepAlive;

            AddCors(response);

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            var path = request.Url?.AbsolutePath ?? "/";
            if (path is "/health" or "/")
            {
                LogHealth(request, path);
                await WriteAsync(response, HttpStatusCode.OK, "{\"ok\":true,\"name\":\"远程播报\"}").ConfigureAwait(false);
                return;
            }

            if (path == "/permissions")
            {
                await HandlePermissionsAsync(request, response).ConfigureAwait(false);
                return;
            }

            if (path == "/snapshot")
            {
                await HandleSnapshotAsync(request, response, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (path != "/speak")
            {
                await FailAsync(request, response, HttpStatusCode.NotFound, "没有这个接口").ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod != "POST")
            {
                await FailAsync(request, response, HttpStatusCode.MethodNotAllowed, "请用 POST").ConfigureAwait(false);
                return;
            }

            var speakToken = CheckToken(request, needNotify: true, needSpeak: false, needSnapshot: false);
            if (!speakToken.Accepted)
            {
                await FailAsync(
                    request,
                    response,
                    speakToken.Forbidden ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized,
                    speakToken.Forbidden ? "这个 Token 没有通知权限" : "Token 不对").ConfigureAwait(false);
                return;
            }

            var payload = await ReadSpeakAsync(request).ConfigureAwait(false);
            if (payload == null)
            {
                await FailAsync(request, response, HttpStatusCode.RequestEntityTooLarge, "文本太长了").ConfigureAwait(false);
                return;
            }

            if (payload.Error != null)
            {
                await FailAsync(request, response, HttpStatusCode.BadRequest, payload.Error).ConfigureAwait(false);
                return;
            }

            if (payload.Text.Length == 0)
            {
                await FailAsync(request, response, HttpStatusCode.BadRequest, "body 里要有非空的 text").ConfigureAwait(false);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await FailAsync(request, response, HttpStatusCode.ServiceUnavailable, "服务正在停止，稍后再试").ConfigureAwait(false);
                return;
            }

            var allowSpeak = speakToken.Token?.AllowSpeak ?? true;
            // mode=notify 一律只通知；mode=speak 但这条 Token 没朗读权限时也回退成通知。
            var notifyOnly = payload.Mode == SpeakRequest.ModeNotify || !allowSpeak;
            var options = new SpeakOptions(
                NotifyOnly: notifyOnly,
                TokenName: speakToken.Token?.Name ?? "");
            if (!await _onText(payload.Text, payload.Repeat, options).ConfigureAwait(false))
            {
                await FailAsync(request, response, HttpStatusCode.ServiceUnavailable, "插件正在启动，稍后再试").ConfigureAwait(false);
                return;
            }

            // 受理成功也留一行，回看才知道网页到底递进来了什么；正文只留前 20 字。
            // 先记再回响应：真正的成功是提供方接下了，回写只是管道。
            var times = payload.Repeat == null ? "" : $" ×{payload.Repeat}";
            var who = speakToken.Token == null ? "" : $" · token={speakToken.Token.Name}";
            speakToken.Token?.RecordUse(TokenUseKind.Speak);
            _log.AddRequest("HTTP 202",
                $"POST /speak{who} · {payload.Text.Length} 字{times} · {RequestLog.Summarize(payload.Text)}");
            await WriteAsync(response, HttpStatusCode.Accepted, "{\"ok\":true}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 吞掉异常只会让人对着 500 干瞪眼，这里把原因原样带回去，并留在日志里。
            var reason = $"内部错误：{ex.GetType().Name} {ex.Message}";
            _log.Add("HTTP 500", $"{request.HttpMethod} {request.Url?.AbsolutePath ?? "/"}：{reason}");
            try
            {
                await WriteAsync(response, HttpStatusCode.InternalServerError, ErrorJson(reason)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 客户端已经断开，没什么可做的。
            }
        }
        finally
        {
            try
            {
                response.OutputStream.Close();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// 取景接口：开关没开就等于不存在；开了也要过 Token 这一关。
    /// 图是一次性的，回 no-store，免得中间层把教室画面存下来。
    /// </summary>
    private async Task HandleSnapshotAsync(HttpListenerRequest request, HttpListenerResponse response, CancellationToken cancellationToken)
    {
        if (!_snapshotEnabled)
        {
            await FailAsync(request, response, HttpStatusCode.NotFound, "取景功能未开启").ConfigureAwait(false);
            return;
        }

        if (request.HttpMethod != "GET")
        {
            await FailAsync(request, response, HttpStatusCode.MethodNotAllowed, "请用 GET").ConfigureAwait(false);
            return;
        }

        var snapshotToken = CheckToken(request, needNotify: false, needSpeak: false, needSnapshot: true);
        if (!snapshotToken.Accepted)
        {
            await FailAsync(
                request,
                response,
                snapshotToken.Forbidden ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized,
                snapshotToken.Forbidden ? "这个 Token 没有取景权限" : "Token 不对").ConfigureAwait(false);
            return;
        }

        if (_onSnapshot == null)
        {
            await FailAsync(request, response, HttpStatusCode.ServiceUnavailable, "取景功能不可用").ConfigureAwait(false);
            return;
        }

        SnapshotFrame frame;
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            frame = await _onSnapshot(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 改端口、关服务、插件卸载都会走到这里：正在拍的那张被掐了，属于本服务在收摊，
            // 回 500 只会让人以为取景有 bug。
            await FailAsync(request, response, HttpStatusCode.ServiceUnavailable, "服务正在停止，稍后再试").ConfigureAwait(false);
            return;
        }
        catch (SnapshotUnavailableException ex)
        {
            // 没摄像头、被别的程序占着、系统隐私开关关了——这是设备状态，不是本服务的 bug，
            // 回 503 并把原因带回去，调用方才看得懂该去开哪个开关。
            await FailAsync(request, response, HttpStatusCode.ServiceUnavailable, ex.Message).ConfigureAwait(false);
            return;
        }

        var seconds = Stopwatch.GetElapsedTime(startedAt, Stopwatch.GetTimestamp()).TotalSeconds;
        var who = snapshotToken.Token == null ? "" : $" · token={snapshotToken.Token.Name}";
        snapshotToken.Token?.RecordUse(TokenUseKind.Snapshot);
        _log.AddRequest("HTTP 200",
            $"GET /snapshot{who} · {frame.DeviceName} · {frame.Width}×{frame.Height} · {frame.Jpeg.Length / 1024} KB · {seconds:0.0} 秒");
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "image/jpeg";
        response.ContentLength64 = frame.Jpeg.Length;
        response.AddHeader("Cache-Control", "no-store");
        await response.OutputStream.WriteAsync(frame.Jpeg, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>探活是网页轮询用的，一秒内重复的只留一行，不然列表会被它刷满。</summary>
    private void LogHealth(HttpListenerRequest request, string path)
    {
        if (!_log.LogRequests)
        {
            // 不记就别占住这一秒的名额，否则开关刚打开那一下反而被去重吃掉。
            return;
        }

        var now = DateTime.Now;
        if (RequestLog.HealthAlreadyLogged(_lastHealthLogged, now))
        {
            return;
        }

        _lastHealthLogged = now;
        _log.AddRequest("HTTP 200", $"{request.HttpMethod} {path}");
    }

    /// <summary>回错误码的同时记进日志——出错得有个能回头查的地方。</summary>
    private async Task FailAsync(HttpListenerRequest request, HttpListenerResponse response, HttpStatusCode status, string reason)
    {
        _log.Add($"HTTP {(int)status}", $"{request.HttpMethod} {request.Url?.AbsolutePath ?? "/"}：{reason}");
        await WriteAsync(response, status, ErrorJson(reason)).ConfigureAwait(false);
    }

    /// <summary>一次校验的结果：命中的 token、是否放行、是不是权限不够。</summary>
    private sealed record TokenCheck(ApiToken? Token, bool Accepted, bool Forbidden);

    /// <summary>
    /// 没启用任何 token 时整台本机都不校验（和旧版「Token 留空」一致）；
    /// 一旦有启用的 token，就必须带对，而且得有所需权限。
    /// </summary>
    private TokenCheck CheckToken(
        HttpListenerRequest request,
        bool needNotify,
        bool needSpeak,
        bool needSnapshot)
    {
        var active = _tokens.Where(t => t.Enabled && t.Value.Length > 0).ToList();
        if (active.Count == 0)
        {
            return new TokenCheck(null, true, false);
        }

        var presented = request.Headers["X-Token"] ?? "";
        var token = active.FirstOrDefault(t => string.Equals(t.Value, presented, StringComparison.Ordinal));
        if (token == null)
        {
            return new TokenCheck(null, false, false);
        }

        // 能朗读就默认能弹通知；这里再兜一层，防止手改配置文件把通知关掉。
        if ((needNotify && !(token.AllowNotify || token.AllowSpeak))
            || (needSpeak && !token.AllowSpeak)
            || (needSnapshot && !token.AllowSnapshot))
        {
            return new TokenCheck(token, false, true);
        }

        return new TokenCheck(token, true, false);
    }

    /// <summary>
    /// 让调用方先问清楚这条 Token 能干什么；APP 靠它决定显示哪些卡片。
    /// 只要 Token 有效就回 200，权限不够就把对应字段置 false。
    /// </summary>
    private async Task HandlePermissionsAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        var check = CheckToken(request, needNotify: false, needSpeak: false, needSnapshot: false);
        if (!check.Accepted)
        {
            await FailAsync(request, response, HttpStatusCode.Unauthorized, "Token 不对").ConfigureAwait(false);
            return;
        }

        var token = check.Token;
        var json = JsonSerializer.Serialize(new
        {
            ok = true,
            name = token?.Name ?? "",
            notify = token?.AllowNotify ?? true,
            speak = token?.AllowSpeak ?? true,
            snapshot = token?.AllowSnapshot ?? true,
        });
        await WriteAsync(response, HttpStatusCode.OK, json).ConfigureAwait(false);
    }

    /// <summary>读 body 并解析；body 超限返回 null，格式不对或空白按老规矩交空文本。</summary>
    private static async Task<SpeakRequest?> ReadSpeakAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 > MaxBodyBytes)
        {
            return null;
        }

        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        string body;
        try
        {
            body = await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            // 超时或客户端半路断开，按空白文本回 400。
            return new SpeakRequest(string.Empty, null, null, null);
        }

        return SpeakRequest.Parse(body);
    }

    private static void AddCors(HttpListenerResponse response)
    {
        // 网页里的 fetch 打本地端口必然过 CORS 这一关，缺了这些头浏览器就直接拦掉。
        response.AddHeader("Access-Control-Allow-Origin", "*");
        response.AddHeader("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-Token");
        response.AddHeader("Access-Control-Max-Age", "600");
    }

    private static readonly JsonSerializerOptions ErrorOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>错误响应统一走这里：异常信息里带引号换行也不能把 JSON 拼坏。</summary>
    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(new { ok = false, error = message }, ErrorOptions);

    private static async Task WriteAsync(HttpListenerResponse response, HttpStatusCode status, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = (int)status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }
}
