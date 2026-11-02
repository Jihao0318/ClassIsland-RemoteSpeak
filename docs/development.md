# 开发说明

## 环境

- .NET 8 SDK
- Windows（插件运行在 ClassIsland 里；`EnableWindowsTargeting` 已打开，编译可以跨平台）

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

114 项断言覆盖 HTTP 协议、Token 校验、CORS、日志流水（正常请求 + 错误）、自愈重绑、时长估算、取景的尺寸换算，以及「复制调用示例」生成的那段代码（三段齐全、Token 该有才有、括号配平、Token 里的引号被转义）。Windows 和 Linux 都能全绿。

**摄像头和打断都不在无头覆盖范围内**：取景要真实设备，打断要真实的提醒工作线程与语音提供方。编译只能证明 API 用法对，效果得装到 ClassIsland 上用设置页的「试拍一张」实测。

所以 `ServerCheck` 只把不依赖 WinRT 的那几个文件编进去（`RemoteSpeakServer.cs`、`SnapshotFrame.cs`、`SnapshotScale.cs`、`CallExample.cs`、`RequestLog.cs`、`Settings.cs`），项目本身仍是 `net8.0`；`CameraSnapshotter.cs` 不在列表里，加了就会把 Windows SDK 投影拖进测试工程，Linux 上直接编不过。缩放、档位钳制、错误码到状态码的映射、示例代码的拼装都留在能跑断言的那一侧，WinRT 那一层只剩设备枚举、取帧、编码三件事。示例那段之所以值得测：拼错一个逗号，用户粘到网页里就是 `SyntaxError`，而它在设置页里根本看不见。

## 平台目标与打包

插件是 `net8.0-windows10.0.19041.0`——取景要用 `MediaCapture`，这是宿主自己的平台目标。

构建会把 `Microsoft.Windows.SDK.NET.dll` 和 `WinRT.Runtime.dll` 复制到输出目录，但打包前由 `StripWinRTProjection` 删掉：宿主的 `PluginLoadContext.Load` 对这两个名字故意 `return null`，插件绑的就是宿主目录里那两份，跟着打包只会让 `.cipx` 从 91 KB 涨到 6 MB。

代价是插件只能在 ClassIsland 里跑——用别的加载器单独调试这两个 DLL 得自己补上。

图标是 `RemoteSpeak/icon.png`（256×256，RGBA）。宿主的 `PluginManifest.Icon` 默认就叫 `icon.png`，插件列表和市场用 Avalonia 的 `Image` 显示它——**只认位图，SVG 渲染不出来**，所以仓库根那张 `Icon.svg` 不能直接打进包。改图形就改 SVG，然后重新光栅化一份 `icon.png`（`.github/workflows/build.yml` 的 Verify package 会检查它在不在包里，漏了直接失败）。

## 仓库结构

```
RemoteSpeak/                          插件本体（.NET 8 + ClassIsland.PluginSdk 2.1.0.1）
  Plugin.cs                           入口：注册设置、提醒提供方、后台服务、设置页
  Models/Settings.cs                  设置项、取值钳制与停留时长估算
  Services/RemoteSpeakServer.cs       HttpListener、协议处理、自愈重绑
  Services/RemoteSpeakHostService.cs  挂到宿主生命周期，改设置即时生效
  Services/RemoteSpeakProvider.cs     提醒提供方：打断式发送
  Services/CameraSnapshotter.cs       MediaCapture 取一帧、缩放到档位、编成 JPEG
  Services/SnapshotFrame.cs           取景结果与「拍不出图」异常：服务端只认这两个，不认 WinRT
  Services/SnapshotScale.cs           档位 → 实际出图尺寸的等比换算
  Services/CallExample.cs             「复制调用示例」的拼装，端口/Token/取景状态填进模板
  Services/RequestLog.cs                请求流水与错误：300 条环形，落盘 error.log
  Views/SettingsPages/                设置页
  icon.png                            插件图标（Icon.svg 光栅化来的，宿主只认位图）
ServerCheck/                          无头测试：把服务端、错误日志、设置对象编进控制台程序跑断言
docs/                                 接口、排障、开发说明
Icon.svg                              README 头图与图标的矢量源
```

## CI 与发版

`.github/workflows/build.yml`：

- push 到 `main`/`master` 或发 PR：构建、跑测试、上传 `.cipx` artifact
- 推送版本 tag：额外发布 Release，附 `.cipx` 和 `checksums.md`

发版前把 `RemoteSpeak/manifest.yml` 与 `RemoteSpeak/RemoteSpeak.csproj` 里的版本改成同一个四段版本号，再打 tag：

```bash
git tag v1.1.0.0
git push origin v1.1.0.0
```

tag 与两处版本不一致时工作流会直接失败，不会发出一个版本号对不上的包。`checksums.md` 是上架 ClassIsland 插件市场用的 MD5 校验信息，从 Release 本地安装不读它。

## 六个坑

- 提醒只能由提供方实例本身发出，且 `ShowNotification` 往下会构造 Avalonia 控件，**必须回到 UI 线程**（`Dispatcher.UIThread.InvokeAsync`）；在线程池上调偶尔能过，偶尔就是被 `catch` 吞掉的跨线程异常。
- 打断旧播报时得**先关掉旧请求两段的 `IsSpeechEnabled`，再 `Cancel()`**：宿主的 `PopRequests` 只按状态跳过 `Cancelled/Completed`，还在排队的请求被取消后状态仍是 `Queued`，照样会被取出——而工作线程取出它的第一件事就是把它的内容塞进语音队列。顺序反了就会先念完旧句子再念新的。
- 改端口或 Token 会走 `Stop()` → `Start()`。旧监督循环可能在 `Cancel()` 生效前从 `GetContextAsync` 抛出来，光看 `_wantRunning` 会以为监听真断了，转头把刚绑好的新监听器关掉。现在每个循环带一个 `generation`，代次对不上就直接退出。
- 取景只能用 `MediaCaptureMemoryPreference.Cpu` + `MediaFrameReader`，并且**头两张要丢掉**：自动曝光还没收敛，第一张平均亮度只有稳定后的一半不到，留着就是「拍出来是黑的」。想省事直接 `CapturePhotoToStreamAsync` 不行——它出的是全尺寸原图，档位没地方生效。
- 共享模式（`SharedReadOnly`）下 `SetFormatAsync` 会抛 `0x80070020`，想让摄像头直接出小图就得 `ExclusiveControl`，而独占会把正在用摄像头的程序踢掉。所以尺寸在 `BitmapEncoder.BitmapTransform` 这一步缩，别去改设备格式。
- 成功那行日志要在**回写响应之前**记。先写响应后记，客户端拿到 `202` 的那一刻列表里还是空的——设置页看起来像"请求成功了但没记录"，无头断言也会因为这一瞬间的先后关系时好时坏。
