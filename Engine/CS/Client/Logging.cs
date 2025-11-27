
namespace Patchwork;

public static partial class Logging
{
#if DEBUG
    public static void Write(string text)
    {
        Console.Write(text);
    }

    public static void WriteLine(string text)
    {
        Console.WriteLine(text);
    }
#else
    private static readonly object Lock;
    private static readonly StreamWriter LatestWriter;
    private static readonly StreamWriter TimestampWriter;

    static Logging()
    {
        Lock = new object();

        string baseDir = AppContext.BaseDirectory;
        string logsDir = Path.Combine(baseDir, "Logs");

        Directory.CreateDirectory(logsDir);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string timestampPath = Path.Combine(logsDir, $"{timestamp}.log");
        string latestPath = Path.Combine(logsDir, "latest.log");

        TimestampWriter = new StreamWriter(timestampPath, false)
        {
            AutoFlush = true
        };

        LatestWriter = new StreamWriter(latestPath, false)
        {
            AutoFlush = true
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeWriters();
    }

    public static void Write(string text)
    {
        lock (Lock)
        {
            TimestampWriter.Write(text);
            LatestWriter.Write(text);
        }
    }

    public static void WriteLine(string text)
    {
        lock (Lock)
        {
            TimestampWriter.WriteLine(text);
            LatestWriter.WriteLine(text);
        }
    }

    private static void DisposeWriters()
    {
        lock (Lock)
        {
            try
            {
                TimestampWriter.Flush();
                LatestWriter.Flush();
            }
            catch
            {
            }

            try
            {
                TimestampWriter.Dispose();
            }
            catch
            {
            }

            try
            {
                LatestWriter.Dispose();
            }
            catch
            {
            }
        }
    }
#endif
}
