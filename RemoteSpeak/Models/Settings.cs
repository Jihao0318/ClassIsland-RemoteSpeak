using CommunityToolkit.Mvvm.ComponentModel;

namespace RemoteSpeak.Models;

/// <summary>
/// 插件设置。改动即写入插件目录下的 settings.json，无需手动保存。
/// </summary>
public partial class Settings : ObservableObject
{
    /// <summary>HTTP 服务总开关。关闭时完全不监听任何端口。</summary>
    [ObservableProperty]
    private bool _httpServiceEnabled = false;

    [ObservableProperty]
    private int _httpPort = 8765;

    /// <summary>访问 Token；留空则不校验请求头。</summary>
    [ObservableProperty]
    private string _httpToken = "";

    /// <summary>遮罩上显示的短语。</summary>
    [ObservableProperty]
    private string _maskText = "远程播报";

    [ObservableProperty]
    private int _maskDurationSeconds = 2;

    /// <summary>估算朗读时长用的语速，单位「字/秒」。</summary>
    [ObservableProperty]
    private double _wordsPerSecond = 4.5;

    /// <summary>单条播报允许占用画面的最长时间。</summary>
    [ObservableProperty]
    private int _maxSpeechDurationSeconds = 120;

    /// <summary>取景接口总开关。关闭时 /snapshot 这条路由等于不存在。</summary>
    [ObservableProperty]
    private bool _snapshotEnabled = false;

    /// <summary>挑中的摄像头设备 ID；留空表示用第一个可用的。</summary>
    [ObservableProperty]
    private string _snapshotDeviceId = "";

    /// <summary>出图高度档位，只认 360 / 720 / 1080。</summary>
    [ObservableProperty]
    private int _snapshotResolution = 720;

    /// <summary>正常请求要不要也写进日志；关掉就只留错误。</summary>
    [ObservableProperty]
    private bool _logRequests = true;

    private static readonly int[] SnapshotResolutions = [360, 720, 1080];

    /// <summary>NumberBox 被清空时会被绑成 0，这里挡回去，免得服务起在无效端口上。</summary>
    partial void OnHttpPortChanged(int value)
    {
        if (value is < 1 or > 65535)
        {
            HttpPort = 8765;
        }
    }

    /// <summary>语速为 0 会让时长估算除以零，直接钳回默认值。</summary>
    partial void OnWordsPerSecondChanged(double value)
    {
        if (value < 0.5 || double.IsNaN(value) || double.IsInfinity(value))
        {
            WordsPerSecond = 4.5;
        }
    }

    partial void OnMaskDurationSecondsChanged(int value)
    {
        if (value is < 1 or > 30)
        {
            MaskDurationSeconds = 2;
        }
    }

    partial void OnMaxSpeechDurationSecondsChanged(int value)
    {
        if (value is < 5 or > 600)
        {
            MaxSpeechDurationSeconds = 120;
        }
    }

    /// <summary>摄像头未必供得起任意尺寸，档位只能在三档里挑。</summary>
    partial void OnSnapshotResolutionChanged(int value)
    {
        if (Array.IndexOf(SnapshotResolutions, value) < 0)
        {
            SnapshotResolution = 720;
        }
    }

    /// <summary>旧版本配置文件里缺字段时补默认值。</summary>
    public void FixUp()
    {
        MaskText = string.IsNullOrWhiteSpace(MaskText) ? "远程播报" : MaskText.Trim();
        HttpToken = HttpToken?.Trim() ?? "";
        HttpPort = HttpPort is >= 1 and <= 65535 ? HttpPort : 8765;
        WordsPerSecond = WordsPerSecond > 0.5 ? WordsPerSecond : 4.5;
        MaskDurationSeconds = MaskDurationSeconds is >= 1 and <= 30 ? MaskDurationSeconds : 2;
        MaxSpeechDurationSeconds = MaxSpeechDurationSeconds is >= 5 and <= 600 ? MaxSpeechDurationSeconds : 120;
        SnapshotDeviceId = SnapshotDeviceId?.Trim() ?? "";
        SnapshotResolution = Array.IndexOf(SnapshotResolutions, SnapshotResolution) >= 0 ? SnapshotResolution : 720;
    }

    /// <summary>
    /// 正文该占多久：按字数估算念完要多久。画面一结束宿主就清空语音队列，所以只放宽不收窄。
    /// </summary>
    public TimeSpan EstimateOverlayDuration(string text)
    {
        var seconds = text.Length / WordsPerSecond + 2;
        return TimeSpan.FromSeconds(Math.Min(Math.Max(seconds, 3), MaxSpeechDurationSeconds));
    }
}
