using Microsoft.Extensions.Hosting;
using RemoteSpeak.Models;

namespace RemoteSpeak.Services;

/// <summary>
/// 把 <see cref="RemoteSpeakServer"/> 挂到宿主生命周期上，并让端口、Token、总开关在设置里改完立刻生效。
/// 服务起不来只记录原因给设置页看，绝不把插件本身拖崩。
/// </summary>
public sealed class RemoteSpeakHostService : IHostedService, IDisposable
{
    private readonly SpeakState _state;
    private readonly Settings _settings;
    private readonly RemoteSpeakServer _server;
    private readonly object _sync = new();

    public RemoteSpeakHostService(SpeakState state, Settings settings, ErrorLog log)
    {
        _state = state;
        _settings = settings;
        _server = new RemoteSpeakServer(Speak, log);
        // 后台自己恢复（重绑定成功、监听中断）也要反映到设置页，不能只在改设置时更新一次。
        _server.Changed += (_, _) => _state.SetStatus(_server.Address, _server.LastError);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        Apply();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        lock (_sync)
        {
            _server.Stop();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _settings.PropertyChanged -= OnSettingsPropertyChanged;
        lock (_sync)
        {
            _server.Dispose();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(Settings.HttpServiceEnabled) or nameof(Settings.HttpPort) or nameof(Settings.HttpToken)))
        {
            return;
        }

        // 绑定回调整个跑在 UI 线程上，重监听涉及关旧监听器，挪到线程池去做。
        Task.Run(Apply);
    }

    private void Apply()
    {
        lock (_sync)
        {
            string error;
            if (!_settings.HttpServiceEnabled)
            {
                _server.Stop();
                error = string.Empty;
            }
            else
            {
                // 先停再起，端口和 Token 一起按最新值生效。
                _server.Stop();
                error = _server.Start(_settings.HttpPort, _settings.HttpToken) ?? string.Empty;
            }

            _state.SetStatus(_server.Address, error);
        }
    }

    private async Task<bool> Speak(string text)
    {
        var provider = _state.Provider;
        if (provider == null)
        {
            return false;
        }

        try
        {
            await provider.SpeakAsync(text).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            // 发不出去也要在设置页留一行字，否则调用方只会看到一个没有下文的 500。
            lock (_sync)
            {
                _state.SetStatus(_server.Address, $"发送失败：{ex.GetType().Name} {ex.Message}");
            }

            throw;
        }
    }
}
