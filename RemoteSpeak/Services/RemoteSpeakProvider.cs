using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Shared;
using RemoteSpeak.Models;

namespace RemoteSpeak.Services;

/// <summary>
/// 远程播报的提醒提供方：把一段文本发成「遮罩 + 静态正文」的提醒，朗读挂在正文上。
/// 新的播报会顶掉上一条，不排队。
/// </summary>
[NotificationProviderInfo(
    "CBD60C4C-202A-41C0-B104-77D5D79389FC",
    "远程播报",
    "\uE025",
    "由本机 HTTP 接口触发的远程播报提醒")]
public sealed class RemoteSpeakProvider : NotificationProviderBase
{
    private readonly object _sync = new();
    private readonly List<NotificationRequest> _outstanding = [];
    private readonly Settings _settings;

    public RemoteSpeakProvider(SpeakState state, Settings settings)
    {
        _settings = settings;
        state.Provider = this;
    }

    /// <summary>
    /// 播出一条播报：先掐掉上一条的画面和语音，再发这一条。等排进队列才返回；宿主 UI 线程上炸了就原样抛给调用方。
    /// </summary>
    /// <param name="text">要念的正文，也是画面上显示的那一行。</param>
    /// <param name="apiRepeat">接口的 repeat；没给就用设置里的「重复遍数」。</param>
    public async Task SpeakAsync(string text, int? apiRepeat, SpeakOptions options)
    {
        var times = SpeakRequest.ResolveRepeat(apiRepeat, _settings.RepeatTimes);
        // 发送要落到宿主的 UI 线程上：ShowNotification 往下会碰提醒窗口的控件，
        // 在线程池上调偶尔能过，偶尔就是跨线程异常，没必要赌。
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SuppressOutstanding();
            IAppHost.GetService<ISpeechService>().ClearSpeechQueue();
            var request = BuildRequest(text, times, options);
            Track(request);
            ShowNotification(request);
        });
    }

    /// <summary>
    /// 取消已发出、还没播完的提醒。
    /// </summary>
    /// <remarks>
    /// 必须先关掉朗读再 Cancel：工作线程取出被取消的提醒时，第一件事仍是把它的文本塞进语音队列，
    /// 之后才因为取消令牌退出。顺序反了就会先念完旧句子才念新的。
    /// </remarks>
    private void SuppressOutstanding()
    {
        NotificationRequest[] outstanding;
        lock (_sync)
        {
            outstanding = [.. _outstanding];
            _outstanding.Clear();
        }

        foreach (var request in outstanding)
        {
            request.MaskContent.IsSpeechEnabled = false;
            if (request.OverlayContent != null)
            {
                request.OverlayContent.IsSpeechEnabled = false;
            }

            request.Cancel();
        }
    }

    private void Track(NotificationRequest request)
    {
        lock (_sync)
        {
            _outstanding.Add(request);
        }

        request.Canceled += OnRequestEnded;
        request.Completed += OnRequestEnded;
    }

    private void OnRequestEnded(object? sender, EventArgs e)
    {
        if (sender is NotificationRequest request)
        {
            lock (_sync)
            {
                _outstanding.Remove(request);
            }
        }
    }

    private NotificationRequest BuildRequest(string text, int times, SpeakOptions options)
    {
        // 标题统一是「遮罩文字：Token 昵称」，只通知只影响出不出声，不换标题。
        var maskText = string.IsNullOrWhiteSpace(options.TokenName)
            ? _settings.MaskText
            : $"{_settings.MaskText}：{options.TokenName}";

        return new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(
                maskText,
                hasRightIcon: false,
                factory: content =>
                {
                    content.Duration = TimeSpan.FromSeconds(_settings.MaskDurationSeconds);
                    // 朗读交给正文那一段，两段都开会把同一句话念两遍。
                    content.IsSpeechEnabled = false;
                }),
            OverlayContent = BuildOverlay(text, times, options.NotifyOnly)
        };
    }

    /// <summary>正文只做静态一行显示：摆不下就摆不下，念不能少。</summary>
    private NotificationContent BuildOverlay(string text, int times, bool notifyOnly) =>
        NotificationContent.CreateSimpleTextContent(
            text,
            content =>
            {
                content.Duration = _settings.EstimateOverlayDuration(text, times);
                // 画面上还是原文那一句，只有朗读文本按遍数重复——
                // NotificationContent 的显示文本（模板里的 Text）和 SpeechContent 是两个字段。
                content.IsSpeechEnabled = !notifyOnly;
                content.SpeechContent = notifyOnly ? text : SpeakScript.SpeechText(text, times);
            });
}
