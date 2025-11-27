using System.Numerics;
namespace Patchwork;

public partial class Engine
{
    public float DeltaTime { get; private set; }
    public float Time => (float)TimeDouble;
    private double TimeDouble = 0;
    public void NoWindowLoad()
    {
        DriveMounts.Mount("C", new PhysicalFileSystem("."));
        Helper.Init(this);
        WriteLine("Test");
        TestEntity entity = (TestEntity)Entity.Create(typeof(TestEntity));
        entity.Name = "Test";
        entity.Position = new Vector3(0, 0, 0);
        entity.Connections = [];
    }
    public void Update(double dt)
    {
        DeltaTime = (float)dt;
        TimeDouble += dt;
        Entity.TickAll();
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
