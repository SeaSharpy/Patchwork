using System.Diagnostics;
namespace Patchwork;

public partial class Engine
{
    public float DeltaTime { get; private set; }
    public float Time => (float)TimeDouble;
    private double TimeDouble = 0;    public bool Loaded = false;
    public int LoadingError = 0;
    public int LoadedAsync = 0;
    public bool Headless { get; init; } = false;
    public Engine()
    {
    }
    public void NoWindowLoad()
    {
        DriveMounts.Mount("C", new PhysicalFileSystem("."));
        Helper.Init(this);
    }
    private void LoadHeadlessAsync()
    {
        try
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            IncludedFiles.Init();

            stopwatch.Stop();
            WriteLine($"Loading async head took {stopwatch.ElapsedMilliseconds}ms.");
            Interlocked.Increment(ref LoadedAsync);
        }
        catch (Exception ex)
        {
            WriteLine("Engine load failed: " + ex);
            Interlocked.And(ref LoadingError, 1);
        }
    }
    private void LoadHeadAsync()
    {
        try
        {

            Stopwatch stopwatch = new();
            stopwatch.Start();

            stopwatch.Stop();
            WriteLine($"Loading async headless took {stopwatch.ElapsedMilliseconds}ms.");
            Interlocked.Increment(ref LoadedAsync);
        }
        catch (Exception ex)
        {
            WriteLine("Engine load failed: " + ex);
            Interlocked.And(ref LoadingError, 1);
        }
    }
    public void Update(double dt)
    {
        if (LoadingError == 1) Close();
        DeltaTime = (float)dt;
        TimeDouble += DeltaTime;
        if (Loaded) Entity.TickAll();
        else if (LoadedAsync == 2)
        {
            Stopwatch stopwatch = new();
            if (!Headless)
            {
                
            }
            stopwatch.Stop();
            WriteLine($"Loading sync took {stopwatch.ElapsedMilliseconds}ms.");
            Loaded = true;
        }
    }
    public void Render()
    {

    }
    public void NoWindowUnload()
    {
        DriveMounts.Dispose();
    }
    public class CloseException() : Exception("Engine is closed.");
    public class BubbleException() : Exception("Bubble exception.");
    public void Close() => throw new CloseException();
}

public static class Helper
{
    private static Engine Instance = null!;
    public static void Init(Engine engine)
    {
        if (Instance != null)
            throw new InvalidOperationException("Engine helper already initialized.");
        Instance = engine;
    }
    public static float Time => Instance.Time;
    public static float DeltaTime => Instance.DeltaTime;
    public static void Close() => Instance.Close();
}
