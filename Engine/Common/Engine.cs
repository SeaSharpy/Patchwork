using System.Buffers;
using System.Numerics;
namespace Patchwork;

public partial class Engine
{
    public float DeltaTime { get; private set; }
    public float Time => (float)TimeDouble;
    private double TimeDouble = 0;
    public Vector3 Gravity = new Vector3(0, -10, 0);
    public void CommonLoad()
    {
        DriveMounts.Mount("C", new PhysicalFileSystem("."));
        Helper.Init(this);
        TestEntity entity = (TestEntity)Entity.Create(typeof(TestEntity));
        entity.Name = "Test";
        entity.Position = new Vector3(0, 0, 0);
        entity.Connections = [];
    }
    public void Update(double dt)
    {
        DeltaTime = (float)dt;
        TimeDouble += dt;
        Serializer.FlushQueue("physics");
        PhysicsManager.Update();
        Entity.TickAll();
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
    public static void Init(Engine engine)
    {
        if (Instance != null)
            throw new InvalidOperationException("Engine helper already initialized.");
        Instance = engine;
    }
    public static float Time => Instance.Time;
    public static float DeltaTime => Instance.DeltaTime;
    public static Vector3 Gravity
    {
        get => Instance.Gravity;
        set => Instance.Gravity = value;
    }
    public static void Close() => Instance.Close();
}
