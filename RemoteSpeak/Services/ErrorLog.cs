namespace RemoteSpeak.Services;

/// <summary>一条错误日志。落盘用制表符分隔，所以消息里不能有制表符，写之前会压平。</summary>
public sealed record ErrorEntry(DateTime Time, string Source, string Message)
{
    public string Display => $"{Time:MM-dd HH:mm:ss}  {Source}  {Message}";
}

/// <summary>
/// 只记错误的环形日志：最多 100 条，超出丢最老的，同时写进插件配置目录下的文件，重启也还在。
/// 成功的事情不进这里——念对了没有回看的价值，念错了才需要留着。
/// </summary>
public sealed class ErrorLog
{
    public const int Capacity = 100;
    private const char Separator = '\t';
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly List<ErrorEntry> _entries = new();

    public ErrorLog(string filePath) => _filePath = filePath;

    /// <summary>新写入一条或清空时触发，设置页用它刷新列表。</summary>
    public event EventHandler? Changed;

    public string FilePath => _filePath;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>最近的记录，新的在前。</summary>
    public List<ErrorEntry> Snapshot()
    {
        lock (_sync)
        {
            return [.. _entries];
        }
    }

    public void Add(string source, string message)
    {
        var entry = new ErrorEntry(DateTime.Now, Clean(source), Clean(message));
        if (entry.Message.Length == 0)
        {
            return;
        }

        lock (_sync)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > Capacity)
            {
                _entries.RemoveRange(Capacity, _entries.Count - Capacity);
            }

            SaveLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
            SaveLocked();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>把上一次留下的文件读回来；格式不对的行直接丢掉，不抛。</summary>
    public void Load()
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(_filePath);
        }
        catch (Exception)
        {
            return;
        }

        lock (_sync)
        {
            _entries.Clear();
            // 文件本身就是新的在前，照顺序读回来即可。
            foreach (var line in lines)
            {
                var parts = line.Split(Separator, 3);
                if (parts.Length < 3 || !DateTime.TryParseExact(parts[0], TimeFormat, null,
                        System.Globalization.DateTimeStyles.None, out var time))
                {
                    continue;
                }

                _entries.Add(new ErrorEntry(time, parts[1], parts[2]));
            }

            if (_entries.Count > Capacity)
            {
                _entries.RemoveRange(Capacity, _entries.Count - Capacity);
            }
        }
    }

    /// <summary>写文件失败也不能把插件带崩，日志只是尽力而为。</summary>
    private void SaveLocked()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            File.WriteAllLines(_filePath, _entries.Select(e => $"{e.Time.ToString(TimeFormat)}{Separator}{e.Source}{Separator}{e.Message}"));
        }
        catch (Exception)
        {
        }
    }

    /// <summary>异常消息里带换行很常见，压成一行才不破坏一行一条的格式。</summary>
    private static string Clean(string text) =>
        text.Replace("\r", " ").Replace("\n", " ").Replace(Separator, ' ').Trim();
}
