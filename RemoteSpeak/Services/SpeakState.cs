namespace RemoteSpeak.Services;

/// <summary>
/// 跨组件共享的运行时状态。
/// 提醒只能由提供方实例本身发出，而 AddNotificationProvider 只把它注册成 IHostedService，
/// 按具体类型从容器里取是取不到的；服务状态同理——设置页是临时实例，不能自己存。
/// </summary>
public sealed class SpeakState
{
    public RemoteSpeakProvider? Provider { get; set; }

    /// <summary>正在监听的前缀；没起来时为空。</summary>
    public string Address { get; private set; } = string.Empty;

    /// <summary>最近一次启动失败的原因；正常时为空。</summary>
    public string LastError { get; private set; } = string.Empty;

    public bool IsRunning => Address.Length > 0;

    /// <summary>状态变化后触发，设置页用它刷新那行红字。</summary>
    public event EventHandler? StatusChanged;

    public void SetStatus(string address, string lastError)
    {
        Address = address;
        LastError = lastError;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
