using System.Text.Json;

namespace RemoteSpeak.Services;

/// <summary>
/// <c>POST /speak</c> 的 body：正文 + 可选的重复遍数。
/// 老调用方只写 text 时行为一字不变，所以 <see cref="Repeat"/> 是 <see cref="Nullable{Int32}"/>——
/// "没给"和"给了 1"是两回事，前者要听本地设置的。
/// </summary>
public sealed record SpeakRequest(string Text, int? Repeat, string? Error)
{
    public const int MinRepeat = 1;
    public const int MaxRepeat = 5;

    /// <summary>解析 body。JSON 不合法时按老规矩交空文本（由调用方回 400），不抛异常。</summary>
    public static SpeakRequest Parse(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return new SpeakRequest("", null, null);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new SpeakRequest("", null, null);
            }

            var text = root.TryGetProperty("text", out var textField) && textField.ValueKind == JsonValueKind.String
                ? (textField.GetString() ?? string.Empty).Trim()
                : string.Empty;

            if (!root.TryGetProperty("repeat", out var repeatField))
            {
                return new SpeakRequest(text, null, null);
            }

            // 越界、小数、字符串、布尔都直接拒：静默钳制会让调用方以为念了 10 遍，其实只念了 5 遍。
            if (repeatField.ValueKind != JsonValueKind.Number ||
                !repeatField.TryGetInt32(out var repeat) ||
                repeat is < MinRepeat or > MaxRepeat)
            {
                return new SpeakRequest(text, null, $"repeat 要是 {MinRepeat}-{MaxRepeat} 的整数");
            }

            return new SpeakRequest(text, repeat, null);
        }
    }

    /// <summary>接口传了听接口的，没传听本地设置的。</summary>
    public static int ResolveRepeat(int? fromApi, int fromSettings) =>
        fromApi ?? Math.Clamp(fromSettings, MinRepeat, MaxRepeat);
}
