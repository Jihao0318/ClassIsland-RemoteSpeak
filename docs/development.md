# 开发说明

## 环境

- .NET 8 SDK
- Windows（插件运行在 ClassIsland 里；编译本身跨平台）

## 构建

```bash
git clone https://github.com/Jihao0318/ClassIsland-RemoteSpeak.git
cd ClassIsland-RemoteSpeak

dotnet build RemoteSpeak/RemoteSpeak.csproj -c Release -p:CreateCipx=true -p:GenerateHashSummary=false
```

产物：`RemoteSpeak/cipx/RemoteSpeak.cipx`，用 ClassIsland 的「从本地文件安装」装上。

`-p:GenerateHashSummary=false` 不能省：SDK 打包完会调 `pwsh` 生成 MD5，只装了 Windows PowerShell 5.1 的机器没有 `pwsh`，构建会在这里失败。写在 csproj 里不生效——SDK 的 targets 会把它覆盖回 `true`，必须从命令行传。

## 测试

```bash
dotnet run -c Release --project ServerCheck
```

58 项断言覆盖 HTTP 协议、Token 校验、CORS、错误日志、自愈重绑与时长估算。Windows 和 Linux 都能全绿。

**打断本身不在无头覆盖范围内**：它需要真实的提醒工作线程和语音提供方，编译只能证明 API 用法对，效果得装到 ClassIsland 上连发两条实测。

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
docs/                                 接口、排障、开发说明
```

## CI 与发版

`.github/workflows/build.yml`：

- push 到 `main`/`master` 或发 PR：构建、跑测试、上传 `.cipx` artifact
- 推送版本 tag：额外发布 Release，附 `.cipx` 和 `checksums.md`

发版前把 `RemoteSpeak/manifest.yml` 与 `RemoteSpeak/RemoteSpeak.csproj` 里的版本改成同一个四段版本号，再打 tag：

```bash
git tag v1.0.0.0
git push origin v1.0.0.0
```

tag 与两处版本不一致时工作流会直接失败，不会发出一个版本号对不上的包。`checksums.md` 是上架 ClassIsland 插件市场用的 MD5 校验信息，从 Release 本地安装不读它。

## 三个坑

- 提醒只能由提供方实例本身发出，且 `ShowNotification` 往下会构造 Avalonia 控件，**必须回到 UI 线程**（`Dispatcher.UIThread.InvokeAsync`）；在线程池上调偶尔能过，偶尔就是被 `catch` 吞掉的跨线程异常。
- 打断旧播报时得**先关掉旧请求两段的 `IsSpeechEnabled`，再 `Cancel()`**：宿主的 `PopRequests` 只按状态跳过 `Cancelled/Completed`，还在排队的请求被取消后状态仍是 `Queued`，照样会被取出——而工作线程取出它的第一件事就是把它的内容塞进语音队列。顺序反了就会先念完旧句子再念新的。
- 改端口或 Token 会走 `Stop()` → `Start()`。旧监督循环可能在 `Cancel()` 生效前从 `GetContextAsync` 抛出来，光看 `_wantRunning` 会以为监听真断了，转头把刚绑好的新监听器关掉。现在每个循环带一个 `generation`，代次对不上就直接退出。
