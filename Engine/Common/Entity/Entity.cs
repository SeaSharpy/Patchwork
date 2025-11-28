namespace Patchwork;

public interface IModel : ISerializable { }
public class ModelFile : IModel
{
    [SerializedMember] public string Path;
}
public class Model : IModel
{
    [SerializedMember] public string DataPath;
    [SerializedMember] public ITexture? Albedo;
    [SerializedMember] public Vector3? ConstantAlbedo;
    [SerializedMember] public ITexture? MetallicRoughnessAO;
    [SerializedMember] public Vector3? ConstantMetallicRoughnessAO;
    [SerializedMember] public ITexture? Emissive;
    [SerializedMember] public Vector3? ConstantEmissive;
    [SerializedMember] public ITexture? Normal;
}
public interface ITexture : ISerializable
{
}
public class PathTexture : ITexture
{
    [SerializedMember] public string Path;
    [SerializedMember] public Vector2 Scale;
    [SerializedMember] public Vector2 Offset;
}
public class RenderTexture : ITexture
{
    [SerializedMember] public uint Camera { get; }
    [SerializedMember] public Vector2 Scale;
    [SerializedMember] public Vector2 Offset;
}
public abstract partial class Entity : IDisposable, ISerializable
{
    [SerializedMember] public string Name;
    [SerializedMember] public IModel Model;
    [SerializedMember] public Connection[] Connections;
    [SerializedMember] public Vector3 Position;
    [SerializedMember] public Quaternion Rotation;
    [SerializedMember] public float Scale;
}
