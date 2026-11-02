namespace RemoteSpeak.Services;

/// <summary>一条日志。落盘用制表符分隔，所以消息里不能有制表符，写之前会压平。</summary>
public sealed record LogEntry(DateTime Time, string Source, string Message)
{
    public string Display => $"{Time:MM-dd HH:mm:ss}  {Source}  {Message}";
}

/// <summary>
/// 环形日志：最多 300 条，超出丢最老的，同时写进插件配置目录下的 error.log（文件名沿用历史，里面现在也记正常请求）。
/// 错误无条件记；正常受理的请求由 <see cref="LogRequests"/> 决定，默认记。
/// Token 永远不进日志。
/// </summary>
public sealed class RequestLog
{
    public const int Capacity = 300;

    /// <summary>正文进日志时留这么多字，长句子不至于把列表刷爆。</summary>
    public const int SummaryLength = 20;

    private const char Separator = '\t';
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly object _sync = new();
    private readonly string _filePath;
    private readonly List<LogEntry> _entries = new();

    public RequestLog(string filePath) => _filePath = filePath;

    /// <summary>新写入一条或清空时触发，设置页用它刷新列表。</summary>
    public event EventHandler? Changed;

    public string FilePath => _filePath;

    /// <summary>关掉它就退回「只记错误」。</summary>
    public bool LogRequests { get; set; } = true;

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
    public List<LogEntry> Snapshot()
    {
        lock (_sync)
        {
            return [.. _entries];
        }
    }

    /// <summary>出错的事：不管开关怎么设都要留。</summary>
    public void Add(string source, string message)
    {
        var entry = new LogEntry(DateTime.Now, Clean(source), Clean(message));
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

    /// <summary>正常请求：只在「记录正常请求」开着时进。</summary>
    public void AddRequest(string source, string message)
    {
        if (LogRequests)
        {
            Add(source, message);
        }
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

                _entries.Add(new LogEntry(time, parts[1], parts[2]));
            }

            if (_entries.Count > Capacity)
            {
                _entries.RemoveRange(Capacity, _entries.Count - Capacity);
            }
        }
    }

    /// <summary>正文摘要：超过 20 字截断，回看时知道念的是哪句就够，不需要全文。</summary>
    public static string Summarize(string text) =>
        text.Length <= SummaryLength ? text : string.Concat(text.AsSpan(0, SummaryLength), "…");

    /// <summary>探活是给网页轮询用的，一秒内的重复只留一行。</summary>
    public static bool HealthAlreadyLogged(DateTime last, DateTime now) => (now - last).TotalSeconds < 1;

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
