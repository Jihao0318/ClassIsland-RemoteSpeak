<div align="center">

<img src="Icon.svg" width="112" alt="RemoteSpeak">

<h1>RemoteSpeak 远程播报</h1>

<p>在本机开一个 HTTP 接口，网页或脚本 POST 一段文本，ClassIsland 立刻弹出提醒并朗读。</p>

<p>
  <img alt="ClassIsland 2.1+" src="https://img.shields.io/badge/ClassIsland-2.1%2B-2ea44f?style=flat-square">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square">
  <img alt="Windows" src="https://img.shields.io/badge/Platform-Windows-0078D4?style=flat-square">
  <img alt="MIT" src="https://img.shields.io/badge/License-MIT-3DA639?style=flat-square">
</p>

<p>
  <a href="https://github.com/Jihao0318/ClassIsland-RemoteSpeak/actions/workflows/build.yml"><img alt="Build" src="https://github.com/Jihao0318/ClassIsland-RemoteSpeak/actions/workflows/build.yml/badge.svg"></a>
  <a href="https://github.com/Jihao0318/ClassIsland-RemoteSpeak/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/Jihao0318/ClassIsland-RemoteSpeak?style=flat-square"></a>
</p>

<p>
  <a href="#安装">安装</a> ·
  <a href="#快速开始">快速开始</a> ·
  <a href="#http-接口">HTTP 接口</a> ·
  <a href="#配置">配置</a> ·
  <a href="#没有声音">没有声音</a> ·
  <a href="#构建">构建</a>
</p>

</div>

---

课堂互动页、值班看板、外部脚本想让教室里的 ClassIsland「出声」，一个 `fetch` 就够了——不用再装第二个朗读程序，也不给 ClassIsland 塞第二套 TTS 通道。

```js
await fetch("http://localhost:8765/speak", {
  method: "POST",
  headers: { "Content-Type": "application/json; charset=utf-8" },
  body: JSON.stringify({ text: "第三节课下课，请同学们有序离开教室。" })
});
```

## 特性

- **只绑环回**：监听 `localhost` 与 `127.0.0.1`，默认 `8765` 端口，普通权限即可，不需要管理员。
- **打断式播报**：新播报顶掉上一条——旧画面立刻收，念到一半的语音立刻掐，连发多条只有最后一条念完。
- **朗读交给宿主**：用 ClassIsland 选定的语音引擎与音色，插件只负责发提醒和估算画面停留时长。
- **服务自愈**：端口被别的程序占住、或休眠唤醒后监听断了，按 1s→15s 指数退避在后台重绑，占用方一让开就恢复，不用重启 ClassIsland。
- **可选 Token**：填了就必须带 `X-Token` 请求头；留空表示本机程序都能触发。
- **错误日志**：只记出错的那一面（请求被拒、发送抛异常、监听中断），滚动保留 100 条，落在插件配置目录的 `error.log`。

## 安装

1. 从 [Releases](../../releases/latest) 下载 `RemoteSpeak.cipx`。
2. 打开 ClassIsland → **设置 → 插件 → 从本地文件安装**，选择该文件。
3. 重启 ClassIsland。
4. **设置 → 提醒 → 启用提醒语音**（ClassIsland 默认关着，不开只有画面没有声音）。
5. **设置 → 外部设置 → 远程播报**，打开 HTTP 服务，按需设置端口与 Token。

装好后设置页会显示当前监听地址；`GET /health` 能探活。

## 快速开始

```bash
curl http://localhost:8765/health
# {"ok":true,"name":"远程播报"}

curl -X POST http://localhost:8765/speak \
  -H "Content-Type: application/json; charset=utf-8" \
  -d '{"text":"上课了，请回到座位。"}'
# {"ok":true}   ← 202，只代表已受理
```

带 Token 时加一行请求头：

```bash
curl -X POST http://localhost:8765/speak \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "X-Token: 你的Token" \
  -d '{"text":"这段话会被念出来"}'
```

设置页的「复制调用示例」会按当前端口和 Token 生成一份现成的 `fetch` 代码。

## HTTP 接口

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| `GET` | `/health`、`/` | 探活，不校验 Token |
| `POST` | `/speak` | 播报，Body 为 `{"text": "要被朗读的文本"}` |
| `OPTIONS` | 任意 | CORS 预检，返回 `204` |

| 状态码 | 含义 |
| --- | --- |
| `200` | 健康检查通过 |
| `202` | 已受理（不代表念完） |
| `400` | `text` 为空、缺失或 JSON 格式不对 |
| `401` | Token 不对 |
| `404` | 路径不存在 |
| `405` | 方法不对 |
| `413` | 请求体超过 64 KB |
| `503` | 插件启动中或停止中 |
| `500` | 内部错误，响应里带异常类型与原因 |

- 请求体上限 **64 KB**，按 UTF-8 解析 JSON。
- 错误响应统一是 `{"ok":false,"error":"原因"}`，不会留一个空白 500。
- CORS 已放行所有来源与 `X-Token` 头，网页可以直接调用。

## 配置

| 项目 | 默认 | 说明 |
| --- | --- | --- |
| HTTP 服务 | 关闭 | 关闭时一个端口都不监听 |
| 端口 | `8765` | 被占用时换一个，服务会在后台自动重试 |
| 访问 Token | 空 | 留空不校验；填了必须带 `X-Token` |
| 遮罩文字 | `远程播报` | 提醒遮罩上显示的短语 |
| 遮罩时长 | `2` 秒 | |
| 语速 | `4.5` 字/秒 | 只用来估算正文要停留多久 |
| 正文最长 | `120` 秒 | 长文本也不会一直占着画面 |

停留时间按「字数 ÷ 语速 + 2 秒」估算，下限 3 秒。画面一收宿主就清空语音队列，所以宁可估长一点。

## 注意

- **202 只代表受理**：若 **设置 → 提醒** 里的「启用提醒」总开关是关的，画面会被直接丢弃，但接口照样回 `202`。
- **打断范围比想象中大**：ClassIsland 的语音队列全局只有一条，掐断旧播报时会把宿主或其他插件排队的语音一起清空。
- **会排队**：如果这一刻别的提醒（比如上课提醒）正占着画面，本条会排在它后面显示，语音也随它延后。
- **只监听本机**：其他设备连不上，跨网怎么打通由调用方自己处理（反代、内网穿透都行）。

## 没有声音

按顺序检查：

1. **设置 → 提醒 → 启用提醒语音** 是否打开。
2. **设置 → 提醒 → 提醒管理 → 远程播报**：如果这里开了「对此提醒来源启用特殊高级设置」，它以本条自己的「启用提醒语音」为准，而这个子开关默认是关的。不需要差异化就别开高级设置。
3. 播报内容是「遮罩段不发声 + 正文段发声」，这是故意的，避免同一句话念两遍。
4. 念出来用的是宿主选定的语音引擎。Edge TTS 连不上、429、合成超时都发生在宿主里，插件看不到——去 **设置页 → 错误日志 → 打开 ClassIsland 日志目录** 查。

插件自己的错误日志只记它看得见的：请求被拒、发送提醒时抛的异常、监听中断与重绑失败。

## 构建

需要 .NET 8 SDK。

```bash
git clone https://github.com/Jihao0318/ClassIsland-RemoteSpeak.git
cd ClassIsland-RemoteSpeak

dotnet build RemoteSpeak/RemoteSpeak.csproj -c Release -p:CreateCipx=true -p:GenerateHashSummary=false
```

产物：`RemoteSpeak/cipx/RemoteSpeak.cipx`，用 ClassIsland 的「从本地文件安装」装上。

> `-p:GenerateHashSummary=false` 不能省：SDK 打包完会调 `pwsh` 生成 MD5，只装了 Windows PowerShell 5.1 的机器没有 `pwsh`，构建会在这里失败。CI 里同样显式关掉，再由脚本单独生成 `checksums.md`。

跑测试：

```bash
dotnet run -c Release --project ServerCheck
```

58 项断言覆盖 HTTP 协议、Token 校验、CORS、错误日志、自愈重绑与时长估算。**打断本身不在无头覆盖范围内**：它需要真实的提醒工作线程和语音提供方，编译只能证明 API 用法对，效果得装到 ClassIsland 上连发两条实测。

## 持续集成

`.github/workflows/build.yml` 会在 push / PR 时构建并跑测试，把 `.cipx` 作为 artifact 上传；推送版本 tag 时自动创建 Release。

发版前把 `RemoteSpeak/manifest.yml` 与 `RemoteSpeak/RemoteSpeak.csproj` 里的版本改成同一个四段版本号，再打 tag：

```bash
git tag v1.0.0.0
git push origin v1.0.0.0
```

tag 与两处版本不一致时工作流会直接失败，不会发出一个版本号对不上的包。

工作流定义见 [`.github/workflows/build.yml`](.github/workflows/build.yml)，运行记录在 [Actions](https://github.com/Jihao0318/ClassIsland-RemoteSpeak/actions)。

## 仓库结构

```
RemoteSpeak/                          插件本体（.NET 8 + ClassIsland.PluginSdk 2.1.0.1）
  Plugin.cs                           入口：注册设置、提醒提供方、后台服务、设置页
  Models/Settings.cs                  设置项与停留时长估算
  Services/RemoteSpeakServer.cs       HttpListener、协议处理、自愈重绑
  Services/RemoteSpeakHostService.cs  挂到宿主生命周期，改设置即时生效
  Services/RemoteSpeakProvider.cs     提醒提供方：打断式发送
  Services/ErrorLog.cs                只记错误的滚动日志
  Views/SettingsPages/                设置页
ServerCheck/                          无头测试：把服务端、错误日志、设置对象编进控制台程序跑断言
.github/workflows/build.yml           构建、测试、tag 发版
```

## 写给后来人的三个坑

- 提醒只能由提供方实例本身发出，且 `ShowNotification` 往下会构造 Avalonia 控件，**必须回到 UI 线程**（`Dispatcher.UIThread.InvokeAsync`）；在线程池上调偶尔能过，偶尔就是被 `catch` 吞掉的跨线程异常。
- 打断旧播报时得**先关掉旧请求两段的 `IsSpeechEnabled`，再 `Cancel()`**：宿主的 `PopRequests` 只按状态跳过 `Cancelled/Completed`，还在排队的请求被取消后状态仍是 `Queued`，照样会被取出——而工作线程取出它的第一件事就是把它的内容塞进语音队列。顺序反了就会先念完旧句子再念新的。
- 改端口或 Token 会走 `Stop()` → `Start()`。旧监督循环可能在 `Cancel()` 生效前从 `GetContextAsync` 抛出来，光看 `_wantRunning` 会以为监听真断了，转头把刚绑好的新监听器关掉。现在每个循环带一个 `generation`，代次对不上就直接退出。

## 反馈

遇到问题、想加功能，欢迎提 [Issue](https://github.com/Jihao0318/ClassIsland-RemoteSpeak/issues)。

## 许可证

[MIT](LICENSE)
