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

var mainLog = new ErrorLog(TmpFile("main.log"));
var server = new RemoteSpeakServer(text =>
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
    var busy = new RemoteSpeakServer(_ => Task.FromResult(true), new ErrorLog(TmpFile("busy.log")));
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
using (var parallel = new RemoteSpeakServer(_ => Task.FromResult(true), new ErrorLog(TmpFile("parallel.log"))))
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
var idle = new RemoteSpeakServer(_ => Task.FromResult(true), new ErrorLog(TmpFile("idle.log")));
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

// 15. 错误日志：出错的都进去了，成功的没进
var sources = string.Join(",", mainLog.Snapshot().Select(e => e.Source));
Check("401 记进了日志", mainLog.Snapshot().Any(e => e.Source == "HTTP 401" && e.Message.Contains("POST /speak")), sources);
Check("500 带异常类型进日志", mainLog.Snapshot().Any(e => e.Source == "HTTP 500" && e.Message.Contains("InvalidOperationException")), sources);
Check("404/405/413/503 都记了",
    new[] { "HTTP 404", "HTTP 405", "HTTP 413", "HTTP 503" }.All(code => mainLog.Snapshot().Any(e => e.Source == code)),
    sources);
Check("成功的 202/200 不留痕", !mainLog.Snapshot().Any(e => e.Source is "HTTP 202" or "HTTP 200" or "HTTP 204"), sources);

// 16. 环形上限、落盘回读、清空
var ring = new ErrorLog(TmpFile("ring.log"));
ring.Load();
Check("日志文件不存在时读不炸", ring.Count == 0);
ring.Add("HTTP 401", "POST /speak：Token 不对");
ring.Add("HTTP 500", "内部错误：InvalidOperationException 第一行\n第二行");
Check("新的排在最前", ring.Count == 2 && ring.Snapshot()[0].Source == "HTTP 500", ring.Snapshot()[0].Display);
Check("异常里的换行被压成一行", !ring.Snapshot()[0].Message.Contains('\n') && ring.Snapshot()[0].Message.Contains("第二行"),
    ring.Snapshot()[0].Message);
var reread = new ErrorLog(TmpFile("ring.log"));
reread.Load();
Check("落盘后能原样读回", reread.Count == 2 && reread.Snapshot()[1].Message.Contains("Token 不对"), reread.Count.ToString());
for (var i = 0; i < 150; i++)
{
    ring.Add($"测试 {i}", "x");
}

Check("最多只留 100 条", ring.Count == 100, ring.Count.ToString());
Check("丢的是最老的", ring.Snapshot()[0].Source == "测试 149", ring.Snapshot()[0].Source);
ring.Clear();
Check("清空后内存和文件都空", ring.Count == 0 && File.ReadAllLines(TmpFile("ring.log")).Length == 0);

// 17. 端口一直占着反复重试，不能把 100 条日志刷满
using (var blocker = new TcpListener(IPAddress.Any, port + 5))
{
    blocker.Start();
    var floodLog = new ErrorLog(TmpFile("flood.log"));
    var stuck = new RemoteSpeakServer(_ => Task.FromResult(true), floodLog);
    stuck.Start(port + 5, "");
    await Task.Delay(6000); // 期间监督循环至少又重试了两轮
    Check("同一条故障只记一次", floodLog.Count == 1, string.Join(" / ", floodLog.Snapshot().Select(e => e.Display)));
    stuck.Dispose();
    blocker.Stop();
}

Directory.Delete(tmpDir, true);

Console.WriteLine();
Console.WriteLine($"passed={passed} failed={failed}");
return failed == 0 ? 0 : 1;
