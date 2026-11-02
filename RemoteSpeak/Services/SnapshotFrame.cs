namespace RemoteSpeak.Services;

/// <summary>一帧取景结果：编码好的 JPEG 和它的来源设备。</summary>
public sealed record SnapshotFrame(byte[] Jpeg, string DeviceName, int Width, int Height);

/// <summary>摄像头拍不出图（没设备、被占用、系统隐私开关关了），对应 503 而不是 500。</summary>
public sealed class SnapshotUnavailableException : Exception
{
    public SnapshotUnavailableException(string message) : base(message)
    {
    }

    public SnapshotUnavailableException(string message, Exception inner) : base(message, inner)
    {
    }
}
