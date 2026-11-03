<div align="center">

<img src="Icon.svg" width="112" alt="RemoteSpeak">

<h1>RemoteSpeak 远程播报</h1>

<p>在教室电脑上开一个本机 HTTP 接口，网页或脚本 POST 一段文本，ClassIsland 立刻弹出提醒并朗读。</p>

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
  <a href="#配置">配置</a> ·
  <a href="#构建">构建</a> ·
  <a href="#反馈">反馈</a> ·
  <a href="docs/http-api.md">接口文档</a> ·
  <a href="docs/troubleshooting.md">排障</a>
</p>

</div>

---

课堂互动页、值班看板、外部脚本想让教室里的 ClassIsland 出声，一个 `fetch` 就够了：

```js
await fetch("http://localhost:8765/speak", {
  method: "POST",
  headers: { "Content-Type": "application/json; charset=utf-8" },
  body: JSON.stringify({ text: "第三节课下课，请同学们有序离开教室。" })
});
```

朗读交给 ClassIsland 已经选好的语音引擎，不用再装第二个朗读程序。

## 特性

- **只绑环回**：监听 `localhost` 与 `127.0.0.1`，默认 `8765` 端口，普通权限即可。
- **打断式播报**：新播报顶掉上一条，旧画面立刻收，念到一半的语音立刻掐。
- **可以念几遍**：接口 `repeat` 或设置里的重复遍数，1–5 遍，接口优先；画面只显示一次原文。
- **朗读交给宿主**：用 ClassIsland 选定的语音引擎与音色，插件只负责发提醒。
- **服务自愈**：端口被占或休眠断连，按 1s→15s 退避在后台重绑。
- **可选 Token**：填了就必须带 `X-Token`；留空表示本机程序都能触发。
- **摄像头取景**：`GET /snapshot` 取一帧当前画面，默认关闭；图只在内存里过一遍，不落盘。
- **日志**：每条请求都记一行，播报带字数和正文前 20 字，错误永远记，滚动保留 300 条。

## 安装

1. 从 [Releases](../../releases/latest) 下载 `RemoteSpeak.cipx`。
2. ClassIsland → **设置 → 插件 → 从本地文件安装**，选择该文件。
3. 重启 ClassIsland。
4. **设置 → 提醒 → 启用提醒语音**（默认关着，不开只有画面）。
5. **设置 → 外部设置 → 远程播报**，打开 HTTP 服务。

装完没声音或端口起不来，看 [排障说明](docs/troubleshooting.md)。

## 配置

| 项目 | 默认 | 说明 |
| --- | --- | --- |
| HTTP 服务 | 关闭 | 关闭时一个端口都不监听 |
| 端口 | `8765` | 被占用时换一个，服务会自动重试 |
| 访问 Token | 空 | 留空不校验；填了必须带 `X-Token` |
| 遮罩文字 | `远程播报` | 提醒遮罩上显示的短语 |
| 遮罩时长 | `2` 秒 | |
| 语速 | `4.5` 字/秒 | 只用来估算正文停留时间 |
| 重复遍数 | `1` | 同一句念几遍（1–5）；接口带 `repeat` 时以接口为准 |
| 正文最长 | `120` 秒 | 长文本不会一直占着画面 |
| 摄像头取景 | 关闭 | 开启后 `GET /snapshot` 回一帧 JPEG；可选摄像头与出图尺寸 |
| 记录正常请求 | 开启 | 关掉只记错误；日志最多 300 条，Token 永不记录 |

接口、状态码、跨域和调用示例见 [HTTP 接口文档](docs/http-api.md)。

## 构建

需要 .NET 8 SDK。

```bash
git clone https://github.com/Jihao0318/ClassIsland-RemoteSpeak.git
cd ClassIsland-RemoteSpeak

dotnet build RemoteSpeak/RemoteSpeak.csproj -c Release -p:CreateCipx=true -p:GenerateHashSummary=false
```

产物：`RemoteSpeak/cipx/RemoteSpeak.cipx`。

推送到 `main` 或发 PR 时 CI 会构建并打包；推送 `v1.2.0.0` 这类 tag 会自动发布 Release。更多见 [开发说明](docs/development.md)。

## 反馈

遇到问题、想加功能，欢迎提 [Issue](https://github.com/Jihao0318/ClassIsland-RemoteSpeak/issues)。

## 许可证

[MIT](LICENSE)
