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

    private RequestLog Log => IAppHost.GetService<RequestLog>();

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
        RefreshCameraOptions();
        _ = LoadDevicesAsync();
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

    private static readonly (string Label, int Height)[] Resolutions =
    [
        ("640×360", 360),
        ("1280×720", 720),
        ("1920×1080", 1080)
    ];

    private record DeviceOption(string Id, string Label);

    /// <summary>填控件时不要顺手把设置改回去。</summary>
    private bool _loadingCameraOptions;

    private CameraSnapshotter Camera => IAppHost.GetService<CameraSnapshotter>();

    private void RefreshCameraOptions()
    {
        _loadingCameraOptions = true;
        ResolutionComboBox.ItemsSource = Resolutions.Select(r => r.Label).ToList();
        ResolutionComboBox.SelectedIndex = Math.Max(Array.FindIndex(Resolutions, r => r.Height == Settings.SnapshotResolution), 0);
        _loadingCameraOptions = false;
    }

    private async Task LoadDevicesAsync()
    {
        _loadingCameraOptions = true;
        try
        {
            var devices = await Camera.ListDevicesAsync(CancellationToken.None);
            var options = new List<DeviceOption>
            {
                new("", devices.Any(d => d.IsDefault)
                    ? $"自动（系统默认：{devices.First(d => d.IsDefault).DisplayName}）"
                    : "自动（列表里第一台）")
            };
            options.AddRange(devices.Select(d => new DeviceOption(d.Id, d.IsDefault ? $"{d.DisplayName}（系统默认）" : d.DisplayName)));
            DeviceComboBox.ItemsSource = options;
            var picked = options.FindIndex(o => o.Id == Settings.SnapshotDeviceId);
            if (picked < 0)
            {
                // 记住的那台不在了（拔了、换口了）：下拉框显示「自动」，设置就得真的写回自动。
                Settings.SnapshotDeviceId = "";
            }

            DeviceComboBox.SelectedIndex = Math.Max(picked, 0);
        }
        catch (Exception ex)
        {
            // 枚举本身就能失败（系统关了相机权限），下拉框得说一句人话。
            DeviceComboBox.ItemsSource = new List<DeviceOption> { new("", "没扫到摄像头") };
            DeviceComboBox.SelectedIndex = 0;
            SnapshotResultTextBlock.Text = $"扫描摄像头失败：{ex.Message}";
        }
        finally
        {
            _loadingCameraOptions = false;
        }
    }

    private void DeviceComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingCameraOptions)
        {
            return;
        }

        if (DeviceComboBox.SelectedItem is DeviceOption option)
        {
            Settings.SnapshotDeviceId = option.Id;
        }
    }

    private void ResolutionComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingCameraOptions)
        {
            return;
        }

        var index = ResolutionComboBox.SelectedIndex;
        if (index is >= 0 and < 3)
        {
            Settings.SnapshotResolution = Resolutions[index].Height;
        }
    }

    private void ButtonRescanDevices_OnClick(object? sender, RoutedEventArgs e) => _ = LoadDevicesAsync();

    /// <summary>试拍图固定就那一张，每次覆盖：磁盘上不会堆图片。</summary>
    private static string PreviewPath => Path.Combine(Path.GetTempPath(), "RemoteSpeak 试拍.jpg");

    private async void ButtonTestSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        SnapshotResultTextBlock.Text = "正在取景…";
        try
        {
            var frame = await Camera.CaptureAsync(Settings.SnapshotDeviceId, Settings.SnapshotResolution, CancellationToken.None);
            try
            {
                File.WriteAllBytes(PreviewPath, frame.Jpeg);
            }
            catch (IOException)
            {
                // 图已经拍到了，只是默认查看器还占着那个文件名。
                SnapshotResultTextBlock.Text = $"已拍到 {frame.Width}×{frame.Height}，但那张 {PreviewPath} 正被其他程序占着写不进去——关掉查看器再试。";
                return;
            }

            SnapshotResultTextBlock.Text =
                $"{frame.DeviceName} · {frame.Width}×{frame.Height} · {frame.Jpeg.Length / 1024} KB　已存到临时目录，正在打开";
            Open(PreviewPath);
        }
        catch (Exception ex)
        {
            SnapshotResultTextBlock.Text = $"拍不出来：{ex.Message}";
        }
    }

    private void ButtonOpenSnapshotFolder_OnClick(object? sender, RoutedEventArgs e) =>
        Open(Path.GetDirectoryName(PreviewPath) ?? PreviewPath);

    private void ButtonDeleteSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!File.Exists(PreviewPath))
        {
            SnapshotResultTextBlock.Text = "没有试拍图要删。";
            return;
        }

        try
        {
            File.Delete(PreviewPath);
            SnapshotResultTextBlock.Text = "已删除试拍图。";
        }
        catch (IOException)
        {
            SnapshotResultTextBlock.Text = "删不掉——图片还在查看器里开着，关掉它再点一次。";
        }
    }

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

        await clipboard.SetTextAsync(CallExample.Build(Settings.HttpPort, Settings.HttpToken, Settings.SnapshotEnabled));
    }
}
