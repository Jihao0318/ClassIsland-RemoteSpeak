using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using RemoteSpeak.Services;

var passed = 0;
var failed = 0;
var received = new List<string>();
var ready = true;
var explode = false;

// 日志和测试文件都丢在临时目录里，跑完删掉
var tmpDir = Path.Combine(Path.GetTempPath(), $"remotespeak-check-{Guid.NewGuid():N}");
Directory.CreateDirectory(tmpDir);
string TmpFile(string name) => Path.Combine(tmpDir, name);

void Check(string name, bool ok, string? detail = null)
{
    if (ok) { passed++; Console.WriteLine($"PASS  {name}"); }
    else { failed++; Console.WriteLine($"FAIL  {name}  {detail}"); }
}

var mainLog = new RequestLog(TmpFile("main.log"));
var server = new RemoteSpeakServer((text, _, _) =>
{
    if (explode)
    {
        // 模拟跨线程构造控件那类真实崩溃：异常必须变成带原因的 500，不能是空白 500。
        throw new InvalidOperationException("Call from invalid thread: \"UserControl\" \n换行");
    }
    if (!ready) return Task.FromResult(false);
    received.Add(text);
    return Task.FromResult(true);
}, mainLog);

const int port = 18765;
const string token = "tk-1234";
var error = server.Start(port, token);
Check("启动监听", error == null, error);
Check("地址已回报", server.Address == $"http://localhost:{port}/", server.Address);

var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };

async Task<(int status, string body)> Send(
    string method, string path, string? bodyText = null, string? sentToken = null, string contentType = "application/json")
{
    var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (bodyText != null)
    {
        request.Content = new StringContent(bodyText, Encoding.UTF8, contentType);
    }
    if (sentToken != null)
    {
        request.Headers.Add("X-Token", sentToken);
    }

    var response = await client.SendAsync(request);
    return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
}

// 1. 健康检查不校验 Token
var health = await Send("GET", "/health");
Check("GET /health = 200", health.status == 200, health.status.ToString());
Check("健康检查带名字", health.body.Contains("远程播报"), health.body);

var root = await Send("GET", "/");
Check("GET / = 200", root.status == 200, root.status.ToString());

// 2. 正常播报
var speak = await Send("POST", "/speak", "{\"text\":\"上课了\"}", token);
Check("POST /speak 正确 Token = 202", speak.status == 202, $"{speak.status} {speak.body}");
Check("文本原样送达", received.Count == 1 && received[0] == "上课了", string.Join("|", received));

// 3. Token 相关
Check("缺少 Token = 401", (await Send("POST", "/speak", "{\"text\":\"x\"}")).status == 401);
Check("错误 Token = 401", (await Send("POST", "/speak", "{\"text\":\"x\"}", "wrong")).status == 401);

// 4. 文本校验
Check("空文本 = 400", (await Send("POST", "/speak", "{\"text\":\"   \"}", token)).status == 400);
Check("缺字段 = 400", (await Send("POST", "/speak", "{\"other\":1}", token)).status == 400);
Check("非法 JSON = 400", (await Send("POST", "/speak", "not json", token)).status == 400);
Check("text 非字符串 = 400", (await Send("POST", "/speak", "{\"text\":123}", token)).status == 400);

// 5. 路由与方法
Check("未知路径 = 404", (await Send("GET", "/nope", null, null)).status == 404);
Check("GET /speak = 405", (await Send("GET", "/speak")).status == 405);
var options = await Send("OPTIONS", "/speak");
Check("OPTIONS = 204", options.status == 204, options.status.ToString());

// 6. 体积上限
var big = "{\"text\":\"" + new string('啊', 40000) + "\"}";
Check("超过 64KB = 413", (await Send("POST", "/speak", big, token)).status == 413);

// 7. CORS 头
using (var probe = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") })
{
    var res = await probe.GetAsync("health");
    var acao = res.Headers.TryGetValues("Access-Control-Allow-Origin", out var v) ? string.Join(",", v) : "";
    var ach = res.Headers.TryGetValues("Access-Control-Allow-Headers", out var h) ? string.Join(",", h) : "";
    Check("允许跨域来源", acao.Contains("*"), acao);
    Check("允许 X-Token 头", ach.Contains("X-Token"), ach);
}

// 8. 提供方未就绪
ready = false;
var notReady = await Send("POST", "/speak", "{\"text\":\"喂\"}", token);
Check("未就绪 = 503", notReady.status == 503, $"{notReady.status} {notReady.body}");
ready = true;

// 7.5 回调抛异常 → 500 且带原因，JSON 不能被引号换行拼坏
explode = true;
var boom = await Send("POST", "/speak", "{\"text\":\"炸一下\"}", token);
explode = false;
Check("回调抛异常 = 500", boom.status == 500, boom.status.ToString());
Check("500 带异常类型和消息", boom.body.Contains("InvalidOperationException") && boom.body.Contains("invalid thread"), boom.body);
using (var doc = System.Text.Json.JsonDocument.Parse(boom.body))
{
    Check("异常里的引号换行被正确转义",
        doc.RootElement.GetProperty("error").GetString()!.Contains("\"UserControl\" \n换行"),
        boom.body);
    Check("ok 字段为 false", doc.RootElement.GetProperty("ok").GetBoolean() == false);
}

// 7.6 异常之后服务不受影响
Check("抛异常后仍能继续服务",
    (await Send("POST", "/speak", "{\"text\":\"还行\"}", token)).status == 202);

// 9. 中文与 UTF-8
received.Clear();
var utf8 = await Send("POST", "/speak", "{\"text\":\"第三节课\\n下课别跑\"}", token);
Check("UTF-8 换行原样保留", utf8.status == 202 && received.Count == 1 && received[0] == "第三节课\n下课别跑",
    received.Count > 0 ? received[0].Replace("\n", "\\n") : "没收到");

// 10. 改端口热重启
server.Stop();
Check("停止后地址清空", server.Address.Length == 0, server.Address);
var before = received.Count;
error = server.Start(port + 1, "");
Check("换端口重启", error == null, error);
var noTokenClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port + 1}/") };
var noToken = await noTokenClient.PostAsync("/speak", new StringContent("{\"text\":\"不带Token\"}", Encoding.UTF8, "application/json"));
Check("Token 留空时不校验", (int)noToken.StatusCode == 202, ((int)noToken.StatusCode).ToString());

// 11. 端口被占用：当场给出原因，后台还会一直重试到对方让开
server.Dispose();
using (var blocker = new TcpListener(IPAddress.Any, port + 2))
{
    blocker.Start();
    var busy = new RemoteSpeakServer((_, _, _) => Task.FromResult(true), new RequestLog(TmpFile("busy.log")));
    var retryStates = new List<string>();
    busy.Changed += (_, _) => retryStates.Add($"{busy.Address}|{busy.LastError}");
    var busyError = busy.Start(port + 2, "");
    Check("占用端口返回失败原因", busyError != null && busyError.Contains("占用"), busyError ?? "null");
    Check("占用时地址为空", busy.Address.Length == 0, busy.Address);

    // 腾出端口后不该等人重启 ClassIsland——服务自己长回来
    await Task.Delay(1200);
    blocker.Stop();
    var recovered = false;
    for (var i = 0; i < 60 && !recovered; i++)
    {
        await Task.Delay(500);
        try
        {
            using var probe = new HttpClient();
            recovered = (await probe.GetAsync($"http://127.0.0.1:{port + 2}/health")).StatusCode == HttpStatusCode.OK;
        }
        catch (Exception)
        {
        }
    }

    Check("占用方让开后自动恢复监听", recovered, busy.Address + " / " + busy.LastError);
    Check("恢复后清掉错误并且能收请求", busy.LastError.Length == 0, busy.LastError);
    using (var late = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port + 2}/") })
    {
        var lateResponse = await late.PostAsync("/speak", new StringContent("{\"text\":\"补上的请求\"}", Encoding.UTF8, "application/json"));
        Check("恢复后接口可用", (int)lateResponse.StatusCode == 202, ((int)lateResponse.StatusCode).ToString());
    }

    busy.Dispose();
    Check("停止后不再监听", busy.Address.Length == 0);
}

// 11.5 并发请求不能把接受循环卡住
using (var parallel = new RemoteSpeakServer((_, _, _) => Task.FromResult(true), new RequestLog(TmpFile("parallel.log"))))
{
    parallel.Start(port + 3, "");
    using var bulk = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port + 3}/") };
    var tasks = Enumerable.Range(0, 20)
        .Select(i => bulk.PostAsync("/speak", new StringContent("{\"text\":\"第" + i + "条\"}", Encoding.UTF8, "application/json")))
        .ToArray();
    var results = await Task.WhenAll(tasks);
    Check("20 条并发全部收下", results.All(r => (int)r.StatusCode == 202),
        string.Join(",", results.Select(r => (int)r.StatusCode)));
}

// 12. 没启动时不监听
var idle = new RemoteSpeakServer((_, _, _) => Task.FromResult(true), new RequestLog(TmpFile("idle.log")));
Check("未启动不监听", idle.Address.Length == 0);
idle.Dispose();

// 13. 显示时长与滚动判定
var s = new RemoteSpeak.Models.Settings();
Check("短句时长取朗读估算并封底 3 秒",
    Math.Abs(s.EstimateOverlayDuration("上课了").TotalSeconds - 3) < 0.01,
    s.EstimateOverlayDuration("上课了").ToString());
var long40 = new string('字', 40);
Check("40 字按 4.5 字/秒估（40/4.5+2 ≈ 10.9 秒）",
    Math.Abs(s.EstimateOverlayDuration(long40).TotalSeconds - (40 / 4.5 + 2)) < 0.01,
    s.EstimateOverlayDuration(long40).TotalSeconds.ToString());
var huge = new string('字', 600);
Check("超长句封顶最长时间", Math.Abs(s.EstimateOverlayDuration(huge).TotalSeconds - 120) < 0.01,
    s.EstimateOverlayDuration(huge).TotalSeconds.ToString());
s.WordsPerSecond = 9;
Check("语速调快后时长变短", Math.Abs(s.EstimateOverlayDuration(long40).TotalSeconds - (40 / 9.0 + 2)) < 0.01,
    s.EstimateOverlayDuration(long40).TotalSeconds.ToString());
s.WordsPerSecond = 0;
Check("语速设 0 被钳回默认", Math.Abs(s.WordsPerSecond - 4.5) < 0.01, s.WordsPerSecond.ToString());
s.MaxSpeechDurationSeconds = 1;
Check("最长时长设太小被钳回默认", s.MaxSpeechDurationSeconds == 120, s.MaxSpeechDurationSeconds.ToString());
s.HttpPort = 0;
Check("端口设 0 被钳回默认", s.HttpPort == 8765, s.HttpPort.ToString());

// 14. 旧配置文件里残留的滚动字段不影响加载，FixUp 只管还在的键
var legacy = new RemoteSpeak.Models.Settings { WordsPerSecond = 0.1, MaskText = "   " };
legacy.FixUp();
Check("FixUp 修掉过慢的语速", Math.Abs(legacy.WordsPerSecond - 4.5) < 0.01, legacy.WordsPerSecond.ToString());
Check("FixUp 补上空白遮罩文字", legacy.MaskText == "远程播报", legacy.MaskText);

// 15. 日志：出错和受理都记，正文只留前 20 字
var sources = string.Join(",", mainLog.Snapshot().Select(e => e.Source));
var okLines = string.Join(" / ", mainLog.Snapshot().Where(e => e.Source == "HTTP 202").Select(e => e.Message));
Check("401 记进了日志", mainLog.Snapshot().Any(e => e.Source == "HTTP 401" && e.Message.Contains("POST /speak")), sources);
Check("500 带异常类型进日志", mainLog.Snapshot().Any(e => e.Source == "HTTP 500" && e.Message.Contains("InvalidOperationException")), sources);
Check("404/405/413/503 都记了",
    new[] { "HTTP 404", "HTTP 405", "HTTP 413", "HTTP 503" }.All(code => mainLog.Snapshot().Any(e => e.Source == code)),
    sources);
Check("受理成功的 202 也记一行", mainLog.Snapshot().Any(e => e.Source == "HTTP 202"), sources);
Check("202 那行带方法、路径、字数和正文",
    mainLog.Snapshot().Any(e => e.Source == "HTTP 202" && e.Message.Contains("POST /speak")
        && e.Message.Contains("字") && e.Message.Contains("第三节课")), okLines);
Check("正文里的换行不会把日志撑成两行",
    !mainLog.Snapshot().Any(e => e.Source == "HTTP 202" && e.Message.Contains("下课别跑\n")), okLines);
Check("OPTIONS 预检不记，只记紧随的真实请求", !mainLog.Snapshot().Any(e => e.Source == "HTTP 204"), sources);

// 16. 环形上限、落盘回读、清空
var ring = new RequestLog(TmpFile("ring.log"));
ring.Load();
Check("日志文件不存在时读不炸", ring.Count == 0);
ring.Add("HTTP 401", "POST /speak：Token 不对");
ring.Add("HTTP 500", "内部错误：InvalidOperationException 第一行\n第二行");
Check("新的排在最前", ring.Count == 2 && ring.Snapshot()[0].Source == "HTTP 500", ring.Snapshot()[0].Display);
Check("异常里的换行被压成一行", !ring.Snapshot()[0].Message.Contains('\n') && ring.Snapshot()[0].Message.Contains("第二行"),
    ring.Snapshot()[0].Message);
var reread = new RequestLog(TmpFile("ring.log"));
reread.Load();
Check("落盘后能原样读回", reread.Count == 2 && reread.Snapshot()[1].Message.Contains("Token 不对"), reread.Count.ToString());
for (var i = 0; i < 450; i++)
{
    ring.Add($"测试 {i}", "x");
}

Check("最多只留 300 条", ring.Count == 300, ring.Count.ToString());
Check("丢的是最老的", ring.Snapshot()[0].Source == "测试 449", ring.Snapshot()[0].Source);
ring.Clear();
Check("清空后内存和文件都空", ring.Count == 0 && File.ReadAllLines(TmpFile("ring.log")).Length == 0);

// 17. 端口一直占着反复重试，不能把 100 条日志刷满
using (var blocker = new TcpListener(IPAddress.Any, port + 5))
{
    blocker.Start();
    var floodLog = new RequestLog(TmpFile("flood.log"));
    var stuck = new RemoteSpeakServer((_, _, _) => Task.FromResult(true), floodLog);
    stuck.Start(port + 5, "");
    await Task.Delay(6000); // 期间监督循环至少又重试了两轮
    Check("同一条故障只记一次", floodLog.Count == 1, string.Join(" / ", floodLog.Snapshot().Select(e => e.Display)));
    stuck.Dispose();
    blocker.Stop();
}

// 18. 取景接口 /snapshot：路由、鉴权、出图
var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x01, 0x02, 0x03 };
var snapLog = new RequestLog(TmpFile("snapshot.log"));
Func<CancellationToken, Task<SnapshotFrame>> snapFake = _ => Task.FromResult(new SnapshotFrame(jpeg, "测试摄像头", 1280, 720));
var snapServer = new RemoteSpeakServer((_, _, _) => Task.FromResult(true), snapLog, ct => snapFake(ct));
using var snapClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port + 6}/") };

async Task<(int status, string? contentType, string? cacheControl, byte[] body)> GetSnapshot(string? sentToken, string method = "GET")
{
    var req = new HttpRequestMessage(new HttpMethod(method), "snapshot");
    if (sentToken != null)
    {
        req.Headers.Add("X-Token", sentToken);
    }

    var res = await snapClient.SendAsync(req);
    return ((int)res.StatusCode,
        res.Content.Headers.ContentType?.ToString(),
        res.Headers.TryGetValues("Cache-Control", out var v) ? string.Join(",", v) : null,
        await res.Content.ReadAsByteArrayAsync());
}

// 开关默认关着：这条路由等于不存在
snapServer.Start(port + 6, "");
Check("取景未开启时 /snapshot = 404", (await GetSnapshot(null)).status == 404);

snapServer.Start(port + 6, "snap-tk", true);
Check("取景开启后缺 Token = 401", (await GetSnapshot(null)).status == 401);
Check("取景开启后 Token 不对 = 401", (await GetSnapshot("wrong")).status == 401);
var shot = await GetSnapshot("snap-tk");
Check("正确 Token 取到图 = 200", shot.status == 200, shot.status.ToString());
Check("取景成功也记一行，带尺寸和大小",
    snapLog.Snapshot().Any(e => e.Source == "HTTP 200" && e.Message.Contains("GET /snapshot")
        && e.Message.Contains("1280×720") && e.Message.Contains("KB")),
    string.Join(" / ", snapLog.Snapshot().Where(e => e.Source == "HTTP 200").Select(e => e.Message)));
Check("返回的字节就是抓到的那张图", shot.body.SequenceEqual(jpeg), $"{shot.body.Length} 字节");
Check("Content-Type 是 image/jpeg", shot.contentType == "image/jpeg", shot.contentType);
Check("取景图不许缓存", shot.cacheControl?.Contains("no-store") == true, shot.cacheControl);
Check("POST /snapshot = 405", (await GetSnapshot("snap-tk", "POST")).status == 405);

// 18.5 拍不出图要给出原因，不能让人对着空白 500 干瞪眼
snapFake = _ => Task.FromException<SnapshotFrame>(new SnapshotUnavailableException("系统相机隐私已关闭，去 设置 → 隐私 → 摄像头 里打开"));
var denied = await GetSnapshot("snap-tk");
Check("摄像头不可用 = 503", denied.status == 503, denied.status.ToString());
Check("503 带上失败原因", System.Text.Encoding.UTF8.GetString(denied.body).Contains("系统相机隐私已关闭"),
    System.Text.Encoding.UTF8.GetString(denied.body));
Check("503 记进了日志",
    snapLog.Snapshot().Any(e => e.Source == "HTTP 503" && e.Message.Contains("GET /snapshot")),
    string.Join(",", snapLog.Snapshot().Select(e => e.Source).Distinct()));

snapFake = _ => Task.FromException<SnapshotFrame>(new InvalidOperationException("0xC00D36B2"));
Check("取景里的意外异常 = 500", (await GetSnapshot("snap-tk")).status == 500);

// 18.7 取景途中服务被停掉：取消信号不该伪装成一个看起来像 bug 的 500
snapFake = _ => Task.FromException<SnapshotFrame>(new OperationCanceledException("停止"));
var cancelled = await GetSnapshot("snap-tk");
Check("取景途中被取消 = 503", cancelled.status == 503, cancelled.status.ToString());
Check("取消不回内部错误", !System.Text.Encoding.UTF8.GetString(cancelled.body).Contains("内部错误"),
    System.Text.Encoding.UTF8.GetString(cancelled.body));

snapFake = _ => Task.FromResult(new SnapshotFrame(jpeg, "测试摄像头", 1280, 720));
Check("异常之后取景还能用", (await GetSnapshot("snap-tk")).status == 200);

// 18.6 开关开着但没接上取景回调
var bareLog = new RequestLog(TmpFile("snapshot-bare.log"));
var bareServer = new RemoteSpeakServer((_, _, _) => Task.FromResult(true), bareLog);
bareServer.Start(port + 7, "", true);
using (var bareClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port + 7}/") })
{
    var bareRes = await bareClient.GetAsync("snapshot");
    Check("没接取景回调 = 503", (int)bareRes.StatusCode == 503, ((int)bareRes.StatusCode).ToString());
}

bareServer.Dispose();

snapServer.Dispose();

// 19. 取景设置：默认关、设备可留空、分辨率只有三档
var snapSettings = new RemoteSpeak.Models.Settings();
Check("取景默认关闭", snapSettings.SnapshotEnabled == false);
Check("没挑设备时留空", snapSettings.SnapshotDeviceId == "", $"[{snapSettings.SnapshotDeviceId}]");
Check("出图默认 720 高", snapSettings.SnapshotResolution == 720, snapSettings.SnapshotResolution.ToString());

snapSettings.SnapshotResolution = 1080;
Check("1080 是合法档位", snapSettings.SnapshotResolution == 1080, snapSettings.SnapshotResolution.ToString());
snapSettings.SnapshotResolution = 360;
Check("360 是合法档位", snapSettings.SnapshotResolution == 360, snapSettings.SnapshotResolution.ToString());

snapSettings.SnapshotResolution = 480;
snapSettings.FixUp();
Check("非法档位钳回 720", snapSettings.SnapshotResolution == 720, snapSettings.SnapshotResolution.ToString());

var snapLegacy = new RemoteSpeak.Models.Settings { SnapshotDeviceId = "  SC13A  ", SnapshotResolution = 500 };
snapLegacy.FixUp();
Check("设备 ID 去掉首尾空格", snapLegacy.SnapshotDeviceId == "SC13A", $"[{snapLegacy.SnapshotDeviceId}]");
snapLegacy.SnapshotDeviceId = "";
snapLegacy.FixUp();
Check("设备 ID 允许留空，交给自动挑选", snapLegacy.SnapshotDeviceId == "", $"[{snapLegacy.SnapshotDeviceId}]");

// 20. 缩放：共享模式下改不了摄像头的出图尺寸，只能在编码时按档位缩小，且不能变形
Check("16:9 的 4K 收到 720 档 = 1280x720",
    SnapshotScale.For(3840, 2160, 720) == new SnapshotSize(1280, 720),
    SnapshotScale.For(3840, 2160, 720).ToString());
Check("4:3 的画面按高度收，宽度跟着走",
    SnapshotScale.For(4672, 3504, 720) == new SnapshotSize(960, 720),
    SnapshotScale.For(4672, 3504, 720).ToString());
Check("1080 档收 4K = 1920x1080",
    SnapshotScale.For(3840, 2160, 1080) == new SnapshotSize(1920, 1080),
    SnapshotScale.For(3840, 2160, 1080).ToString());
Check("360 档收 1080p = 640x360",
    SnapshotScale.For(1920, 1080, 360) == new SnapshotSize(640, 360),
    SnapshotScale.For(1920, 1080, 360).ToString());
Check("本来就不高档位的不放大",
    SnapshotScale.For(1280, 720, 720) == new SnapshotSize(1280, 720)
    && SnapshotScale.For(640, 480, 720) == new SnapshotSize(640, 480),
    $"{SnapshotScale.For(1280, 720, 720)} / {SnapshotScale.For(640, 480, 720)}");
Check("除不尽时宽度取偶数",
    SnapshotScale.For(4096, 2160, 720) == new SnapshotSize(1364, 720),
    SnapshotScale.For(4096, 2160, 720).ToString());
Check("宽高为 0 的脏数据不炸也不放大",
    SnapshotScale.For(0, 0, 720) == new SnapshotSize(0, 0),
    SnapshotScale.For(0, 0, 720).ToString());

// 21. 「复制调用示例」生成的代码：三段都要在，Token 该有才有，括号得配平
static int CountOf(string text, char c) => text.Count(ch => ch == c);
var examplePlain = CallExample.Build(8765, "", false);
var exampleToken = CallExample.Build(8766, "tk-9", true);
Check("没填 Token 时示例里不出现 X-Token", !examplePlain.Contains("X-Token"), examplePlain);
Check("填了 Token 后 speak 带头", exampleToken.Contains("\"X-Token\": \"tk-9\""), exampleToken);
Check("示例三段都在",
    examplePlain.Contains("/health") && examplePlain.Contains("/speak") && examplePlain.Contains("/snapshot"),
    examplePlain);
Check("示例带上 repeat 的用法", examplePlain.Contains("repeat"), examplePlain);
Check("取景没开也把 /snapshot 段给出并提示 404",
    examplePlain.Contains("/snapshot") && examplePlain.Contains("404"),
    examplePlain);
Check("端口按当前设置写进每个 URL",
    exampleToken.Split("http://localhost:8766").Length - 1 == 3,
    (exampleToken.Split("http://localhost:8766").Length - 1).ToString());
Check("Content-Type 后面留了逗号",
    exampleToken.Contains("\"Content-Type\": \"application/json; charset=utf-8\","),
    exampleToken);
Check("没有残留的插值花括号", !exampleToken.Contains("{{") && !exampleToken.Contains("}}"), exampleToken);
Check("花括号配平", CountOf(examplePlain, '{') == CountOf(examplePlain, '}') && CountOf(exampleToken, '{') == CountOf(exampleToken, '}'),
    $"{CountOf(examplePlain, '{')}/{CountOf(examplePlain, '}')} {CountOf(exampleToken, '{')}/{CountOf(exampleToken, '}')}");
Check("圆括号配平", CountOf(examplePlain, '(') == CountOf(examplePlain, ')') && CountOf(exampleToken, '(') == CountOf(exampleToken, ')'),
    $"{CountOf(examplePlain, '(')}/{CountOf(examplePlain, ')')} {CountOf(exampleToken, '(')}/{CountOf(exampleToken, ')')}");
Check("Token 里的引号被转义，不会把示例代码拼坏",
    CallExample.Build(8765, "a\"b", true).Contains("\"X-Token\": \"a\\\"b\""),
    CallExample.Build(8765, "a\"b", true));

// 22. 日志的截断、探活去重，以及「记录正常请求」开关
var long25 = new string('字', 25);
Check("正文截到 20 字加省略号",
    RequestLog.Summarize(long25) == new string('字', 20) + "…", RequestLog.Summarize(long25));
Check("不足 20 字原样", RequestLog.Summarize("上课了") == "上课了", RequestLog.Summarize("上课了"));
Check("空正文不写省略号", RequestLog.Summarize("") == "", $"[{RequestLog.Summarize("")}]");
Check("同一秒内的探活算重复",
    RequestLog.HealthAlreadyLogged(new DateTime(2026, 11, 2, 10, 0, 0), new DateTime(2026, 11, 2, 10, 0, 0, 400)));
Check("过一秒的探活重新记",
    !RequestLog.HealthAlreadyLogged(new DateTime(2026, 11, 2, 10, 0, 0), new DateTime(2026, 11, 2, 10, 0, 1, 200)));
Check("默认就记录正常请求", new RequestLog(TmpFile("default-flag.log")).LogRequests);
Check("设置里默认开着记录正常请求", new RemoteSpeak.Models.Settings().LogRequests);

var quietLog = new RequestLog(TmpFile("quiet.log"));
quietLog.LogRequests = false;
var quietServer = new RemoteSpeakServer((_, _, _) => Task.FromResult(true), quietLog);
quietServer.Start(port + 8, "tk");
using (var quietClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port + 8}/") })
{
    quietClient.DefaultRequestHeaders.Add("X-Token", "tk");
    await quietClient.PostAsync("/speak", new StringContent("{\"text\":\"安静\"}", Encoding.UTF8, "application/json"));
    await quietClient.GetAsync("/health");
    Check("关掉开关后成功请求不进日志", quietLog.Count == 0,
        string.Join(" / ", quietLog.Snapshot().Select(e => e.Display)));

    quietLog.LogRequests = true;
    await quietClient.GetAsync("/health");
    await quietClient.GetAsync("/health");
    Check("连着两次探活只记一行",
        quietLog.Snapshot().Count(e => e.Message.Contains("GET /health")) == 1,
        string.Join(" / ", quietLog.Snapshot().Select(e => e.Display)));
    Check("重新打开后受理的行也进来了",
        quietLog.Snapshot().Any(e => e.Source == "HTTP 200" && e.Message.Contains("GET /health")),
        string.Join(" / ", quietLog.Snapshot().Select(e => e.Display)));

    quietClient.DefaultRequestHeaders.Remove("X-Token");
    quietClient.DefaultRequestHeaders.Add("X-Token", "wrong-again");
    await quietClient.PostAsync("/speak", new StringContent("{\"text\":\"还是安静\"}", Encoding.UTF8, "application/json"));
    Check("关掉与否都拦不住错误进日志",
        quietLog.Snapshot().Any(e => e.Source == "HTTP 401"),
        string.Join(" / ", quietLog.Snapshot().Select(e => e.Display)));
}

quietServer.Dispose();

// 23. 重复遍数：老 body 一个字节不用改，repeat 严格校验，接口压过本地
var plain = SpeakRequest.Parse("{\"text\":\"喂\"}");
Check("老 body 只有 text 照样能用",
    plain.Text == "喂" && plain.Repeat == null && plain.Error == null,
    $"{plain.Text}|{plain.Repeat?.ToString() ?? "null"}|{plain.Error ?? "null"}");
var withRepeat = SpeakRequest.Parse("{\"text\":\"喂\",\"repeat\":3}");
Check("repeat 读得出来", withRepeat.Repeat == 3 && withRepeat.Error == null, withRepeat.Error ?? "null");
Check("认不出的字段不影响", SpeakRequest.Parse("{\"text\":\"喂\",\"foo\":1}").Error == null);
foreach (var bad in new[] { "0", "-1", "6", "2.5", "\"3\"", "true", "null" })
{
    var parsed = SpeakRequest.Parse($"{{\"text\":\"喂\",\"repeat\":{bad}}}");
    Check($"repeat={bad} 直接拒并说明范围", parsed.Error != null && parsed.Error.Contains("1-5"),
        parsed.Error ?? "没报错");
}

Check("空 text 仍按老规矩算没给文本", SpeakRequest.Parse("{\"text\":\"  \"}").Text.Length == 0);
Check("不是 JSON 时不报 repeat 的错", SpeakRequest.Parse("喂").Error == null && SpeakRequest.Parse("喂").Text.Length == 0);
Check("接口没给遍数就用本地的", SpeakRequest.ResolveRepeat(null, 3) == 3, SpeakRequest.ResolveRepeat(null, 3).ToString());
Check("接口给了就压过本地", SpeakRequest.ResolveRepeat(2, 5) == 2, SpeakRequest.ResolveRepeat(2, 5).ToString());
Check("本地越界也钳进 1-5",
    SpeakRequest.ResolveRepeat(null, 0) == 1 && SpeakRequest.ResolveRepeat(null, 9) == 5,
    $"{SpeakRequest.ResolveRepeat(null, 0)}/{SpeakRequest.ResolveRepeat(null, 9)}");
Check("念一遍时朗读文本就是原文", SpeakScript.SpeechText("第三节课下课", 1) == "第三节课下课",
    SpeakScript.SpeechText("第三节课下课", 1));
Check("念两遍中间断开好停顿", SpeakScript.SpeechText("下课了", 2) == "下课了。\n下课了。",
    SpeakScript.SpeechText("下课了", 2).Replace("\n", "\\n"));
Check("原文带句末标点不重复补", SpeakScript.SpeechText("下课了。", 2) == "下课了。\n下课了。",
    SpeakScript.SpeechText("下课了。", 2).Replace("\n", "\\n"));
Check("问号也算句末", SpeakScript.SpeechText("下课了？", 2) == "下课了？\n下课了？",
    SpeakScript.SpeechText("下课了？", 2).Replace("\n", "\\n"));

var ds = new RemoteSpeak.Models.Settings { WordsPerSecond = 5 };
Check("一遍的时长跟以前完全一样",
    Math.Abs(ds.EstimateOverlayDuration("abcdefghij").TotalSeconds - 4) < 0.01,
    ds.EstimateOverlayDuration("abcdefghij").TotalSeconds.ToString());
Check("三遍按三倍字数估",
    Math.Abs(ds.EstimateOverlayDuration("abcdefghij", 3).TotalSeconds - 8) < 0.01,
    ds.EstimateOverlayDuration("abcdefghij", 3).TotalSeconds.ToString());
ds.MaxSpeechDurationSeconds = 10;
Check("再长也顶不过正文最长",
    Math.Abs(ds.EstimateOverlayDuration("abcdefghij", 5).TotalSeconds - 10) < 0.01,
    ds.EstimateOverlayDuration("abcdefghij", 5).TotalSeconds.ToString());
Check("本地默认只念一遍", new RemoteSpeak.Models.Settings().RepeatTimes == 1);
var badRepeat = new RemoteSpeak.Models.Settings { RepeatTimes = 0 };
badRepeat.FixUp();
Check("FixUp 把 0 遍钳回 1", badRepeat.RepeatTimes == 1, badRepeat.RepeatTimes.ToString());
var bigRepeat = new RemoteSpeak.Models.Settings { RepeatTimes = 9 };
bigRepeat.FixUp();
Check("FixUp 把 9 遍钳到 5", bigRepeat.RepeatTimes == 5, bigRepeat.RepeatTimes.ToString());

var repLog = new RequestLog(TmpFile("repeat.log"));
var repRepeats = new List<int?>();
var repServer = new RemoteSpeakServer((text, repeat, _) =>
{
    repRepeats.Add(repeat);
    return Task.FromResult(true);
}, repLog);
repServer.Start(port + 9, "");
using var repClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port + 9}/") };

async Task<(int status, string body)> PostSpeak(string json)
{
    var res = await repClient.PostAsync("/speak", new StringContent(json, Encoding.UTF8, "application/json"));
    return ((int)res.StatusCode, await res.Content.ReadAsStringAsync());
}

var r3 = await PostSpeak("{\"text\":\"重复三遍\",\"repeat\":3}");
Check("带 repeat 的播报 = 202", r3.status == 202, $"{r3.status} {r3.body}");
Check("遍数原样交给播报回调", repRepeats.Count > 0 && repRepeats[^1] == 3,
    repRepeats[^1]?.ToString() ?? "null");
var rBad = await PostSpeak("{\"text\":\"喂\",\"repeat\":0}");
Check("repeat 非法 → 400 并写明范围", rBad.status == 400 && rBad.body.Contains("1-5"), rBad.body);
var rOld = await PostSpeak("{\"text\":\"老调用方\"}");
Check("不带 repeat 时回调收到 null", rOld.status == 202 && repRepeats[^1] == null,
    repRepeats[^1]?.ToString() ?? "null");
Check("日志行里带上念了几遍",
    repLog.Snapshot().Any(e => e.Source == "HTTP 202" && e.Message.Contains("×3")),
    string.Join(" / ", repLog.Snapshot().Where(e => e.Source == "HTTP 202").Select(e => e.Message)));
Check("不带 repeat 时日志不硬编一个遍数",
    repLog.Snapshot().Any(e => e.Source == "HTTP 202" && !e.Message.Contains("×")),
    string.Join(" / ", repLog.Snapshot().Where(e => e.Source == "HTTP 202").Select(e => e.Message)));

repServer.Dispose();

Directory.Delete(tmpDir, true);

Console.WriteLine();
Console.WriteLine($"passed={passed} failed={failed}");
return failed == 0 ? 0 : 1;
