namespace RemoteSpeak.Models;

/// <summary>
/// 一次播报的附加信息：来自哪条 Token、要不要出声。
/// 没有 Token 校验时用 <see cref="Default"/>。
/// </summary>
public sealed record SpeakOptions(bool NotifyOnly, string TokenName)
{
    public static readonly SpeakOptions Default = new(false, "");
}
