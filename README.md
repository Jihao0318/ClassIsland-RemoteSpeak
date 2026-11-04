<div align="center">

<img src="Icon.svg" width="112" alt="RemoteSpeak">

<h1>RemoteSpeak 远程播报</h1>

<p>人在别处，也能让教室里的 ClassIsland 替你开口。</p>

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
  <a href="#功能">功能</a> ·
  <a href="#安装">安装</a> ·
  <a href="#配置">配置</a> ·
  <a href="#构建">构建</a> ·
  <a href="#反馈">反馈</a> ·
  <a href="docs/http-api.md">接口文档</a> ·
  <a href="docs/troubleshooting.md">排障</a>
</p>

</div>

---

## 功能

- **远程喊话**：安卓控制端连上教室电脑，人在哪都能发，不用回教室
- **网页 / 脚本接入**：互动页、看板、定时任务也能触发，人不在也能自动播
- **先看画面再喊**：打开摄像头取景，喊之前先确认教室里有谁，避免对着空教室喊
- **同一句念几遍**：重要通知可以连念 1–5 遍，画面只显示一次
- **只通知不朗读**：需要安静时只弹提醒，不出声
- **打断式播报**：新消息直接顶掉上一条，不会排一长串
- **多 Token 权限**：给不同页面、不同人分配不同 Token，分别控制能不能通知、能不能朗读、能不能看画面
- **使用记录**：每条 Token 记今日播报次数、取景次数和最近使用时间
- **日志**：每条请求都留一行，出错能回头查

## 安装

1. 从 [Releases](../../releases/latest) 下载 `RemoteSpeak.cipx`。
2. ClassIsland → **设置 → 插件 → 从本地文件安装**，选择该文件。
3. 重启 ClassIsland。
4. **设置 → 提醒 → 启用提醒语音**（默认关着，不开只有画面）。
5. **设置 → 外部设置 → 远程播报**，打开 HTTP 服务。

手机端在同一个 Release 里下载 `RemoteBroadcast_*.apk`，按引导填教室电脑的地址和 Token 即可。

装完没声音或连不上，看 [排障说明](docs/troubleshooting.md)。

## 配置

| 项目 | 默认 | 说明 |
| --- | --- | --- |
| HTTP 服务 | 关闭 | 关闭时完全不监听，也不会被外部调用 |
| 端口 | `8765` | 被占用时换一个，服务会自动重试 |
| 访问 Token | 空 | 可建多条，分别控制通知/朗读/取景权限；一条都没有时不校验 |
| 遮罩文字 | `远程播报` | 提醒遮罩上显示的短语 |
| 遮罩时长 | `2` 秒 | |
| 语速 | `4.5` 字/秒 | 只用来估算正文停留时间 |
| 重复遍数 | `1` | 同一句念几遍（1–5）；调用时也可以单独指定 |
| 正文最长 | `120` 秒 | 长文本不会一直占着画面 |
| 摄像头取景 | 关闭 | 开启后可以远程看一帧教室画面；可选摄像头与出图尺寸 |
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

推送到 `main` 或发 PR 时 CI 会构建并打包；推送 `v1.3.2.0` 这类 tag 会自动发布 Release。更多见 [开发说明](docs/development.md)。

## 反馈

遇到问题、想加功能，欢迎提 [Issue](https://github.com/Jihao0318/ClassIsland-RemoteSpeak/issues)。

## 许可证

[MIT](LICENSE)
