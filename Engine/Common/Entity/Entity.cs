using BepuPhysics;
using BepuPhysics.Collidables;
namespace Patchwork;

public interface IModel : ISerializable
{
}
public class ModelFile : IModel
{
    [SerializedMember] public string Path;
}
public class Model : IModel
{
    [SerializedMember] public string DataPath;
    public BinaryReader DataReader
    {
        get
        {
            using MemoryStream ms = new MemoryStream();
            using FileStream fileStream = DriveMounts.FileStream(DataPath);
            fileStream.CopyTo(ms);
            MemoryStream msForReader = new MemoryStream(ms.ToArray(), writable: false);
            return new BinaryReader(msForReader);
        }
    }
    [SerializedMember] public ITexture? Albedo;
    [SerializedMember] public Vector4? ConstantAlbedo;
    [SerializedMember] public ITexture? MetallicRoughnessAO;
    [SerializedMember] public Vector3? ConstantMetallicRoughnessAO;
    [SerializedMember] public ITexture? Emissive;
    [SerializedMember] public Vector3? ConstantEmissive;
    [SerializedMember] public ITexture? Normal;
    private static readonly Dictionary<string, (ConvexHull hull, TypedIndex index)> Shapes = new();
    public TypedIndex Index
    {
        get
        {
            if (Shapes.TryGetValue(DataPath, out (ConvexHull hull, TypedIndex index) shape))
                return shape.index;
            Shapes[DataPath] = PhysicsManager.CreateShape(this);
            return Shapes[DataPath].index;
        }
    }
    public ConvexHull Hull
    {
        get
        {
            if (Shapes.TryGetValue(DataPath, out (ConvexHull hull, TypedIndex index) shape))
                return shape.hull;
            Shapes[DataPath] = PhysicsManager.CreateShape(this);
            return Shapes[DataPath].hull;
        }
    }
}
public interface ITexture : ISerializable
{

}
public class PathTexture : ITexture
{
    [SerializedMember] public string Path;
    [SerializedMember] public Vector2 Scale = Vector2.One;
    [SerializedMember] public Vector2 Offset = Vector2.Zero;
}
public class RenderTexture : ITexture
{
    [SerializedMember] public uint Camera { get; }
    [SerializedMember] public Vector2 Scale;
    [SerializedMember] public Vector2 Offset;
}
public partial class Entity : IDisposable, ISerializable
{

    public struct CamData : ISerializable
    {
        [SerializedMember] public Vector2 Size;
    }
    public CamData? Cam = null;
    [SerializedMember] public string Name;
    [SerializedMember] public IModel Model;
    [SerializedMember] public uint Layers;
    [SerializedMember] public Connection[] Connections;
    private Vector3 PositionInternal = Vector3.Zero;

    [SerializedMember(queue: "physics")]
    public Vector3 Position
    {
        get => PositionInternal;
        set
        {
            PositionInternal = value;
            if (Physics && Handle.HasValue)
                PhysicsManager.PositionUpdate(Handle.Value, value);
        }
    }
    private Quaternion RotationInternal = Quaternion.Identity;
    [SerializedMember(queue: "physics")]
    public Quaternion Rotation
    {
        get => RotationInternal;
        set
        {
            RotationInternal = value;
            if (Physics && Handle.HasValue)
                PhysicsManager.OrientationUpdate(Handle.Value, value);
        }
    }
    [SerializedMember] public float Scale;
    private float MassInternal = 1;
    [SerializedMember(queue: "physics")]
    public float Mass
    {
        get => MassInternal;
        set
        {
            MassInternal = value;
            if (Physics && Handle.HasValue)
                PhysicsManager.MassUpdate(Handle.Value, value);
        }
    }
    [SerializedMember] public bool Physics { get; init; } = false;
    [SerializedMember] public bool Static { get; init; } = false;
    private Vector3 VelocityInternal = Vector3.Zero;
    [SerializedMember(queue: "physics")]
    public Vector3 Velocity
    {
        get => VelocityInternal;
        set
        {
            VelocityInternal = value;
            if (Physics && Handle.HasValue)
                PhysicsManager.VelocityUpdate(Handle.Value, value);
        }
    }
    private Vector3 AngularVelocityInternal = Vector3.Zero;
    [SerializedMember(queue: "physics")]
    public Vector3 AngularVelocity
    {
        get => AngularVelocityInternal;
        set
        {
            AngularVelocityInternal = value;
            if (Physics && Handle.HasValue)
                PhysicsManager.AngularVelocityUpdate(Handle.Value, value);
        }
    }
    public void PhysicsUpdate(Vector3 position, Quaternion orientation, Vector3 velocity, Vector3 angularVelocity)
    {
        PositionInternal = position;
        RotationInternal = orientation;
        VelocityInternal = velocity;
        AngularVelocityInternal = angularVelocity;
    }
    public BodyHandle? Handle;
}
