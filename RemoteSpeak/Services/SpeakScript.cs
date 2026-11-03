namespace RemoteSpeak.Services;

/// <summary>
/// 把同一句话拼成要念几遍的朗读文本。画面显示的那份字不动——
/// <c>NotificationContent</c> 的显示文本和朗读文本是两个字段，这里只改朗读那一份。
/// </summary>
public static class SpeakScript
{
    private const string SentenceEnders = "。！？；，、.!?,;:";

    public static string SpeechText(string text, int times)
    {
        if (times <= 1)
        {
            return text;
        }

        // 没有句末标点就补一个再断开，否则两遍会连成一句被念成「下课别跑下课别跑」。
        var one = text.Length > 0 && SentenceEnders.Contains(text[^1]) ? text : text + "。";
        return string.Join("\n", Enumerable.Repeat(one, times));
    }
}
