using System.Net;
using System.Text;
using System.Text.Json;

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

    /// <summary>收到文本就交给提醒队列；返回 false 表示插件还没就绪。</summary>
    private readonly Func<string, Task<bool>> _onText;

    private readonly ErrorLog _log;

    private readonly object _sync = new();

    private HttpListener? _listener;
    private CancellationTokenSource? _supervisorCts;
    private int _port;
    private string _token = string.Empty;
    private bool _wantRunning;

    /// <summary>同一句话反复出现就不再重复记（端口一直被占不该把 100 条日志刷满）。</summary>
    private string _lastIssue = string.Empty;

    /// <summary>
    /// 每次 Start 递增。旧监督循环发现代次变了就直接退出：
    /// Stop() 之后、Cancel() 之前它可能刚好从 GetContextAsync 里抛出来，
    /// 光看 _wantRunning 会误以为是"监听断了"，转头把新监听器关掉。
    /// </summary>
    private int _generation;

    public RemoteSpeakServer(Func<string, Task<bool>> onText, ErrorLog log)
    {
        _onText = onText;
        _log = log;
    }

    /// <summary>正在监听的前缀；没起来时为空。</summary>
    public string Address { get; private set; } = string.Empty;

    /// <summary>最近一次失败的原因；正常监听时为空。后台重试期间这里会是「正在自动重试」那句话。</summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>地址或错误变了（含后台自动恢复）时触发。</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 按给定端口和 Token 起服务。返回 null 表示当场就绑上了；
    /// 返回失败原因时后台仍会按退避继续重试，直到绑上或者被 <see cref="Stop"/>。
    /// </summary>
    public string? Start(int port, string token)
    {
        Stop();

        lock (_sync)
        {
            _port = port;
            _token = token.Trim();
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
        // 只绑环回，普通权限就能监听；调用方有的写 localhost 有的写 127.0.0.1，
        // Host 对不上会被 http.sys 直接拒掉，所以两个都认。
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
                await WriteAsync(response, HttpStatusCode.OK, "{\"ok\":true,\"name\":\"远程播报\"}").ConfigureAwait(false);
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

            if (!IsTokenAccepted(request))
            {
                await FailAsync(request, response, HttpStatusCode.Unauthorized, "Token 不对").ConfigureAwait(false);
                return;
            }

            var text = await ReadTextAsync(request).ConfigureAwait(false);
            if (text == null)
            {
                await FailAsync(request, response, HttpStatusCode.RequestEntityTooLarge, "文本太长了").ConfigureAwait(false);
                return;
            }

            if (text.Length == 0)
            {
                await FailAsync(request, response, HttpStatusCode.BadRequest, "body 里要有非空的 text").ConfigureAwait(false);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await FailAsync(request, response, HttpStatusCode.ServiceUnavailable, "服务正在停止，稍后再试").ConfigureAwait(false);
                return;
            }

            if (!await _onText(text).ConfigureAwait(false))
            {
                await FailAsync(request, response, HttpStatusCode.ServiceUnavailable, "插件正在启动，稍后再试").ConfigureAwait(false);
                return;
            }

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

    /// <summary>回错误码的同时记进日志——出错得有个能回头查的地方。</summary>
    private async Task FailAsync(HttpListenerRequest request, HttpListenerResponse response, HttpStatusCode status, string reason)
    {
        _log.Add($"HTTP {(int)status}", $"{request.HttpMethod} {request.Url?.AbsolutePath ?? "/"}：{reason}");
        await WriteAsync(response, status, ErrorJson(reason)).ConfigureAwait(false);
    }

    private bool IsTokenAccepted(HttpListenerRequest request)
    {
        if (_token.Length == 0)
        {
            return true;
        }

        return string.Equals(request.Headers["X-Token"], _token, StringComparison.Ordinal);
    }

    /// <summary>取 body 里的 text；body 超限返回 null，格式不对或空白返回空串。</summary>
    private static async Task<string?> ReadTextAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 > MaxBodyBytes)
        {
            return null;
        }

        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("text", out var field) && field.ValueKind == JsonValueKind.String)
            {
                return (field.GetString() ?? string.Empty).Trim();
            }
        }
        catch (JsonException)
        {
            // 不是合法 JSON，按空白文本回 400。
        }
        catch (IOException)
        {
            // 超时或客户端半路断开，按空白文本回 400。
        }

        return string.Empty;
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
