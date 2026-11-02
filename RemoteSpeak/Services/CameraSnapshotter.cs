using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage.Streams;

namespace RemoteSpeak.Services;

/// <summary>
/// 抓一帧本机摄像头的画面并编成 JPEG。
/// 只用共享模式：教学机上可能正有人开着视频会议或直播，独占模式会把对方直接踢掉。
/// 共享模式改不了摄像头正在出的尺寸，所以按设置里的档位缩在编码这一步。
/// </summary>
public sealed class CameraSnapshotter
{
    /// <summary>头两张自动曝光还没稳，明显偏暗，丢掉。</summary>
    private const int DiscardedFrames = 2;

    private const int FramePollIntervalMs = 20;

    /// <summary>从开摄像头到拿到合格画面的总预算；超了基本等于设备坏了或被独占。</summary>
    private const int CaptureBudgetMs = 4000;

    private readonly RequestLog _log;

    /// <summary>同一时刻只让一个请求碰摄像头，后到的当场回忙——网页轮询不该把摄像头排成一串。</summary>
    private readonly SemaphoreSlim _cameraLock = new(1, 1);

    public CameraSnapshotter(RequestLog log)
    {
        _log = log;
    }

    /// <summary>设置页列可选摄像头用。默认那台标出来，好让人知道「自动」会挑到谁。</summary>
    public async Task<IReadOnlyList<SnapshotDevice>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync().AsTask(cancellationToken).ConfigureAwait(false);
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(cancellationToken)
                .ConfigureAwait(false);
            var defaultId = devices.FirstOrDefault(d => d.IsDefault)?.Id ?? string.Empty;
            return groups
                .Where(g => g.SourceInfos.Any(s => s.SourceKind == MediaFrameSourceKind.Color))
                .Select(g => new SnapshotDevice(g.Id, g.DisplayName,
                    string.Equals(g.Id, defaultId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        catch (Exception ex) when (ex is not SnapshotUnavailableException and not OperationCanceledException)
        {
            throw Translate(ex, "摄像头列表");
        }
    }

    /// <summary>
    /// 拍一张。设置里的设备 ID 找不到就退回第一个能出画面的摄像头——
    /// 换个 USB 口就 503 属于自己给自己找麻烦。拍不出来抛 <see cref="SnapshotUnavailableException"/>。
    /// 上一次还没拍完就直接回忙，不让网页把摄像头排成一串。
    /// </summary>
    public async Task<SnapshotFrame> CaptureAsync(string deviceId, int targetHeight, CancellationToken cancellationToken)
    {
        if (!await _cameraLock.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            throw new SnapshotUnavailableException("上一次取景还没结束。");
        }

        try
        {
            return await CaptureCoreAsync(deviceId, targetHeight, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    private async Task<SnapshotFrame> CaptureCoreAsync(string deviceId, int targetHeight, CancellationToken cancellationToken)
    {
        List<MediaFrameSourceGroup> groups;
        try
        {
            var found = await MediaFrameSourceGroup.FindAllAsync().AsTask(cancellationToken).ConfigureAwait(false);
            groups = found.Where(g => g.SourceInfos.Any(s => s.SourceKind == MediaFrameSourceKind.Color)).ToList();
        }
        catch (Exception ex) when (ex is not SnapshotUnavailableException and not OperationCanceledException)
        {
            throw Translate(ex, "摄像头列表");
        }

        if (groups.Count == 0)
        {
            throw new SnapshotUnavailableException("没有检测到能出画面的摄像头。");
        }

        var group = PickGroup(groups, await ResolveDeviceIdAsync(deviceId, cancellationToken).ConfigureAwait(false));
        using var capture = new MediaCapture();
        try
        {
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = group,
                VideoDeviceId = group.Id,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                AudioDeviceId = string.Empty,
            }).AsTask(cancellationToken).ConfigureAwait(false);

            var source = capture.FrameSources.Values.FirstOrDefault(f => f.Info.SourceKind == MediaFrameSourceKind.Color)
                         ?? throw new SnapshotUnavailableException($"{group.DisplayName} 没有彩色画面源。");

            var reader = await capture.CreateFrameReaderAsync(source).AsTask(cancellationToken).ConfigureAwait(false);
            try
            {
                await reader.StartAsync().AsTask(cancellationToken).ConfigureAwait(false);
                // 帧要一直攥到位图转完：SoftwareBitmap 指的是帧自己的缓冲，帧一收它就空了。
                using var frame = await ReadFrameAsync(reader, cancellationToken).ConfigureAwait(false);
                var bitmap = frame.VideoMediaFrame!.SoftwareBitmap!;
                var size = SnapshotScale.For(bitmap.PixelWidth, bitmap.PixelHeight, targetHeight);
                using var bgra = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
                var jpeg = await EncodeJpegAsync(bgra, size, cancellationToken).ConfigureAwait(false);
                return new SnapshotFrame(jpeg, group.DisplayName, size.Width, size.Height);
            }
            finally
            {
                reader.Dispose();
            }
        }
        catch (Exception ex) when (ex is not SnapshotUnavailableException and not OperationCanceledException)
        {
            throw Translate(ex, group.DisplayName);
        }
    }

    private static async Task<MediaFrameReference> ReadFrameAsync(MediaFrameReader reader, CancellationToken cancellationToken)
    {
        var deadline = Environment.TickCount64 + CaptureBudgetMs;
        var kept = 0;

        while (Environment.TickCount64 < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = reader.TryAcquireLatestFrame();
            var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap == null)
            {
                frame?.Dispose();
                await Task.Delay(FramePollIntervalMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            kept++;
            if (kept <= DiscardedFrames)
            {
                // 曝光还没收敛，这张拍出来偏黑，留着只会让人以为摄像头坏了。
                frame!.Dispose();
                await Task.Delay(FramePollIntervalMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            return frame!;
        }

        throw new SnapshotUnavailableException("摄像头没有出画面，可能正被其他程序占用。");
    }

    private static async Task<byte[]> EncodeJpegAsync(SoftwareBitmap source, SnapshotSize size, CancellationToken cancellationToken)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream).AsTask(cancellationToken)
            .ConfigureAwait(false);
        if (size.Width != source.PixelWidth || size.Height != source.PixelHeight)
        {
            // 共享模式没法让摄像头直接出小图，只能在编码时缩；比例已经算好了，不会变形。
            encoder.BitmapTransform.ScaledWidth = (uint)size.Width;
            encoder.BitmapTransform.ScaledHeight = (uint)size.Height;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        encoder.SetSoftwareBitmap(source);
        await encoder.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size).AsTask(cancellationToken).ConfigureAwait(false);
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// 「自动」这一档问的是系统默认摄像头——笔记本上那就是内置摄像头，选它不会错。
    /// 本机这种两台都没被设成默认的情况只能挑第一台，所以设置页里最好自己点一台。
    /// </summary>
    private static async Task<string> ResolveDeviceIdAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (deviceId.Length > 0)
        {
            return deviceId;
        }

        try
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(cancellationToken)
                .ConfigureAwait(false);
            var watcher = devices.FirstOrDefault(d => d.IsDefault);
            return watcher?.Id ?? string.Empty;
        }
        catch (Exception)
        {
            // 默认设备查不到不算错，退到「第一台」继续拍。
            return string.Empty;
        }
    }

    private static MediaFrameSourceGroup PickGroup(List<MediaFrameSourceGroup> groups, string deviceId)
    {
        if (deviceId.Length > 0)
        {
            var picked = groups.FirstOrDefault(g =>
                string.Equals(g.Id, deviceId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(g.DisplayName, deviceId, StringComparison.OrdinalIgnoreCase));
            if (picked != null)
            {
                return picked;
            }
        }

        return groups[0];
    }

    /// <summary>把 WinRT 的错误码翻译成人话；回一句 500 谁看得懂。</summary>
    private SnapshotUnavailableException Translate(Exception ex, string what)
    {
        _log.Add("取景", $"{what}：{Describe(ex)}");
        return new SnapshotUnavailableException(Describe(ex), ex);
    }

    private static string Describe(Exception ex)
    {
        var reason = ex.HResult switch
        {
            // E_ACCESSDENIED：系统在 设置 → 隐私和安全 → 相机 里把桌面应用的权限关了。
            unchecked((int)0x80070005) => "系统已禁止桌面应用访问摄像头，去 设置 → 隐私和安全 → 相机 里打开。",
            // MF_E_HW_MFT_FAILED_START_STREAMING：设备被别的程序占着。
            unchecked((int)0xC00D3705) => "摄像头正在被其他程序使用。",
            _ => null
        };

        if (reason == null && ex is UnauthorizedAccessException)
        {
            reason = "系统已禁止桌面应用访问摄像头，去 设置 → 隐私和安全 → 相机 里打开。";
        }

        return reason ?? $"取景失败：{ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}";
    }
}

/// <summary>设置页里可选的一个摄像头。</summary>
public sealed record SnapshotDevice(string Id, string DisplayName, bool IsDefault);
