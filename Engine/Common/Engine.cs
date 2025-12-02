namespace Patchwork;

public partial class Engine
{
    public float DeltaTime { get; private set; }
    public float Time => (float)TimeDouble;
    private double TimeDouble = 0;
    public void CommonLoad()
    {
        DriveMounts.Mount("C", new PhysicalFileSystem("."));
        HelperInit(this);
    }
    public void Update(double dt)
    {
        DeltaTime = (float)dt;
        TimeDouble += dt;
        UpdateExtrasPre();
        Serializer.FlushQueue("physics");
        PhysicsManager.Update();
        Entity.TickAll();
        UpdateExtrasPost();
    }
    public void CommonUnload()
    {
        DriveMounts.Dispose();
        PhysicsManager.Dispose();
    }
    public class CloseException() : Exception("Engine is closed.");
    public class BubbleException() : Exception("Bubble exception.");
    public void Close() => throw new CloseException();
}

public static class Helper
{
    private static Engine Instance = null!;
    public static void HelperInit(Engine engine)
    {
        if (Instance != null)
            throw new InvalidOperationException("Engine helper already initialized.");
        Instance = engine;
    }
    public static float Time => Instance.Time;
    public static float DeltaTime => Instance.DeltaTime;
    public static void Close() => Instance.Close();
}
