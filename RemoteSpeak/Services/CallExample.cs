namespace RemoteSpeak.Services;

/// <summary>
/// 设置页「复制调用示例」生成的那段 fetch 代码：端口、Token 和取景状态都按当前设置填好，
/// 复制过去就能直接跑。放在不依赖界面的地方是为了能被无头断言验一遍——
/// 拼错一个逗号，用户粘到网页里就是 SyntaxError。
/// </summary>
public static class CallExample
{
    public static string Build(int port, string token, bool snapshotEnabled)
    {
        // Token 是用户手填的，里面的引号和反斜杠得转义，否则整段代码被拼坏。
        var safe = token.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
        var tokenHeader = safe.Length == 0 ? "" : $"\n    \"X-Token\": \"{safe}\",";
        var shotOptions = safe.Length == 0 ? "" : $", {{ headers: {{ \"X-Token\": \"{safe}\" }} }}";
        var snapshotNote = snapshotEnabled
            ? "，取景已开启"
            : "，「摄像头取景」还没打开——开了才有效，没开时回 404";

        return
            $$"""
             // 1. 确认插件在跑
             await fetch("http://localhost:{{port}}/health").then(r => r.ok);

             // 2. 触发播报（202 只代表已受理；新播报会顶掉上一条）
             await fetch("http://localhost:{{port}}/speak", {
               method: "POST",
               headers: {
                 "Content-Type": "application/json; charset=utf-8",{{tokenHeader}}
               },
               body: JSON.stringify({ text: "这段话会被念出来" })
             });

             // 3. 取一帧摄像头画面，成功回 image/jpeg{{snapshotNote}}
             const shot = await fetch("http://localhost:{{port}}/snapshot"{{shotOptions}});
             if (shot.ok) {
               const url = URL.createObjectURL(await shot.blob());
               document.querySelector("#cam").src = url; // 塞给页面上任意一个 <img>
             } else {
               console.warn(await shot.json()); // 拍不出来回 503，形如 { ok: false, error: "原因" }
             }
             """;
    }
}
