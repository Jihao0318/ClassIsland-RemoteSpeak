using System.IO;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemoteSpeak.Models;
using RemoteSpeak.Services;
using RemoteSpeak.Views.SettingsPages;

namespace RemoteSpeak;

/// <summary>
/// 插件入口。远程播报只做一件事：在本机开一个 HTTP 接口，收到文本就以提醒的形式播出来。
/// </summary>
[PluginEntrance]
public class Plugin : PluginBase
{
    /// <summary>插件自己的设置对象，设置页和服务共用同一个实例。</summary>
    public Settings Settings { get; private set; } = new();

    /// <summary>设置文件的绝对路径，设置页的「打开配置文件夹」要用。</summary>
    public string ConfigPath { get; private set; } = "";

    /// <summary>日志文件的绝对路径，和设置文件放一起（文件名沿用 error.log）。</summary>
    public string LogPath { get; private set; } = "";

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ConfigPath = Path.Combine(PluginConfigFolder, "settings.json");
        Settings = ConfigureFileHelper.LoadConfig<Settings>(ConfigPath);
        Settings.FixUp();
        Settings.PropertyChanged += (_, _) => ConfigureFileHelper.SaveConfig(ConfigPath, Settings);

        LogPath = Path.Combine(PluginConfigFolder, "error.log");
        var log = new RequestLog(LogPath);
        log.Load();

        services.AddSingleton(Settings);
        services.AddSingleton(log);
        services.AddSingleton<CameraSnapshotter>();
        // 提醒提供方只注册为 IHostedService，具体类型拿不到，用这个单例把实例递出来。
        services.AddSingleton<SpeakState>();

        // 提供方先注册：宿主按注册顺序实例化 hosted service，这样服务起监听时提供方已就绪。
        services.AddNotificationProvider<RemoteSpeakProvider>();
        services.AddHostedService<RemoteSpeakHostService>();

        services.AddSettingsPage<RemoteSpeakSettingsPage>();
    }
}
