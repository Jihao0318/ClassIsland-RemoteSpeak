using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RemoteSpeak.Models;

/// <summary>一次调用属于哪个接口。</summary>
public enum TokenUseKind
{
    Speak,
    Snapshot,
}

/// <summary>
/// 一条访问 Token。<see cref="Value"/> 是调用方要带在 X-Token 里的串，
/// <see cref="Name"/> 只用于设置页和日志里认人。
/// </summary>
public sealed partial class ApiToken : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = "新 Token";

    [ObservableProperty]
    private string _value = "";

    /// <summary>是否允许 POST /speak。</summary>
    [ObservableProperty]
    private bool _allowSpeak = true;

    /// <summary>是否允许 GET /snapshot。</summary>
    [ObservableProperty]
    private bool _allowSnapshot = true;

    /// <summary>只弹提醒，不朗读。</summary>
    [ObservableProperty]
    private bool _notifyOnly;

    /// <summary>关掉后这条 token 立刻失效，不用删。</summary>
    [ObservableProperty]
    private bool _enabled = true;

    // 统计字段用普通属性：请求在后台线程命中，不能在这里直接发通知，
    // 否则设置页的绑定会跨线程炸掉。通知走 UsageUpdated。
    public DateTime? LastUsedAt { get; set; }

    public int TodaySpeakCount { get; set; }

    public int TodaySnapshotCount { get; set; }

    public string UsageDate { get; set; } = "";

    /// <summary>设置页那一行说明：播报和取景分开数，最后什么时候用过。</summary>
    public string UsageText => LastUsedAt == null
        ? "还没用过"
        : $"播报 {TodaySpeakCount} · 取景 {TodaySnapshotCount} · 最近 {LastUsedAt:MM-dd HH:mm}";

    /// <summary>由插件入口在 UI 线程上接过去，负责刷新界面并保存统计。</summary>
    public Action? UsageUpdated { get; set; }

    /// <summary>请求命中时记一笔。跨天自动把两类今日次数都清零。</summary>
    public void RecordUse(TokenUseKind kind)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (UsageDate != today)
        {
            UsageDate = today;
            TodaySpeakCount = 0;
            TodaySnapshotCount = 0;
        }

        if (kind == TokenUseKind.Speak)
        {
            TodaySpeakCount++;
        }
        else
        {
            TodaySnapshotCount++;
        }

        LastUsedAt = DateTime.Now;
        UsageUpdated?.Invoke();
    }

    /// <summary>统计变了以后刷新 <see cref="UsageText"/>。</summary>
    public void RaiseUsageChanged() => OnPropertyChanged(nameof(UsageText));

    /// <summary>生成一条新的随机 token，32 个十六进制字符。</summary>
    public static string NewValue() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
