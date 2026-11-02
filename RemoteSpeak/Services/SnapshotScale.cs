namespace RemoteSpeak.Services;

/// <summary>一张图缩放后的尺寸。</summary>
public readonly record struct SnapshotSize(int Width, int Height);

/// <summary>
/// 共享模式下改不了摄像头正在出的尺寸，只能在编码那一步缩小，
/// 所以这里按高度算出等比的目标宽度，不能拉变形。
/// </summary>
public static class SnapshotScale
{
    /// <summary>把 srcWidth×srcHeight 等比收到高度不超过 targetHeight；本来就不高就不放大。</summary>
    public static SnapshotSize For(int sourceWidth, int sourceHeight, int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || sourceHeight <= targetHeight)
        {
            return new SnapshotSize(sourceWidth, sourceHeight);
        }

        // 向下取到偶数：编码器对奇数宽高不一定买账，宁可少一列也别拉伸。
        var width = sourceWidth * targetHeight / sourceHeight & ~1;
        return new SnapshotSize(Math.Max(width, 2), targetHeight);
    }
}
