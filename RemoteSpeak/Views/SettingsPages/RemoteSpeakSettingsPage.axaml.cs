using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using RemoteSpeak.Models;
using RemoteSpeak.Services;

namespace RemoteSpeak.Views.SettingsPages;

[SettingsPageInfo(
    "seewo.remotespeak.settings",
    "远程播报",
    "\uE4D0",
    "\uE4D0",
    SettingsPageCategory.External)]
public partial class RemoteSpeakSettingsPage : SettingsPageBase
{
    private SpeakState State => IAppHost.GetService<SpeakState>();

    private ErrorLog Log => IAppHost.GetService<ErrorLog>();

    public Settings Settings => IAppHost.GetService<Plugin>().Settings;

    public string AboutText =>
        $"远程播报\n版本：{IAppHost.GetService<Plugin>().Info.Manifest.Version}\n" +
        $"把网页、脚本发来的文字，用 ClassIsland 自己的提醒画面和语音引擎播出来。";

    public RemoteSpeakSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        State.StatusChanged += OnStatusChanged;
        Log.Changed += OnLogChanged;
        RefreshStatus();
        RefreshLog();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        State.StatusChanged -= OnStatusChanged;
        Log.Changed -= OnLogChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        // 状态是在线程池上改的，回到 UI 线程才能动控件。
        Dispatcher.UIThread.Post(RefreshStatus);
    }

    private void OnLogChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshLog);
    }

    private void RefreshStatus()
    {
        StatusTextBlock.Text = State.IsRunning
            ? $"正在监听 {State.Address}"
            : Settings.HttpServiceEnabled ? "未监听" : "已关闭";
        ErrorTextBlock.Text = State.LastError;
        ErrorTextBlock.IsVisible = State.LastError.Length > 0;
    }

    private void ButtonGenerateToken_OnClick(object? sender, RoutedEventArgs e)
    {
        Settings.HttpToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    private void ButtonClearToken_OnClick(object? sender, RoutedEventArgs e)
    {
        Settings.HttpToken = "";
    }

    private void RefreshLog()
    {
        var entries = Log.Snapshot();
        LogItemsControl.ItemsSource = entries;
        LogEmptyTextBlock.IsVisible = entries.Count == 0;
        LogCountTextBlock.Text = entries.Count == 0 ? "暂无记录" : $"共 {entries.Count} 条";
        LogHintTextBlock.Text = LogHint;
    }

    private const string LogHint =
        "画面和朗读是两码事：这里只记插件自己看得见的——请求被拒、发送提醒时抛的异常、监听中断与重绑失败。"
        + "Edge TTS 连不上、429、语音生成超时都发生在宿主的语音服务里，ISpeechService 不会把失败回调给插件，"
        + "那部分要点「打开 ClassIsland 日志目录」去宿主的日志里查。";

    private void ButtonClearLog_OnClick(object? sender, RoutedEventArgs e) => Log.Clear();

    private void ButtonOpenLog_OnClick(object? sender, RoutedEventArgs e)
    {
        // 还没出过错就没有这个文件，那时打开它所在的文件夹。
        Open(File.Exists(Log.FilePath) ? Log.FilePath : Path.GetDirectoryName(Log.FilePath) ?? Log.FilePath);
    }

    private void ButtonOpenHostLogs_OnClick(object? sender, RoutedEventArgs e) =>
        Open(ClassIsland.Core.CommonDirectories.AppLogFolderPath);

    private void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // 打不开也要说清楚，别让人对着没反应的按钮猜。
            LogHintTextBlock.Text = $"打不开 {path}：{ex.Message}";
        }
    }

    private async void ButtonCopyExample_OnClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            return;
        }

        await clipboard.SetTextAsync(BuildExampleText());
    }

    private string BuildExampleText()
    {
        var port = Settings.HttpPort;
        var token = Settings.HttpToken;
        var tokenHeader = token.Length == 0
            ? ""
            : $"\n    \"X-Token\": \"{token}\",";

        return
            $$"""
             // 1. 确认插件在跑
             await fetch("http://localhost:{{port}}/health").then(r => r.ok);

             // 2. 触发播报
             await fetch("http://localhost:{{port}}/speak", {
               method: "POST",
               headers: {
                 "Content-Type": "application/json; charset=utf-8",{{tokenHeader}}
               },
               body: JSON.stringify({ text: "这段话会被念出来" })
             });
             // 202 只代表已受理。新的播报会顶掉上一条：画面立刻收，念到一半的语音立刻掐。
             """;
    }
}
