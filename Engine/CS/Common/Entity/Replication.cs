using System.Reflection;
namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public Dictionary<string, object> PreviousNetworkState { get; } = new();
    public static Entity GetEntity(string name)
    {
        foreach (KeyValuePair<uint, Entity> kv in Entities)
            if (kv.Value.Name == name)
                return kv.Value;
        throw new KeyNotFoundException($"Entity '{name}' not found.");
    }
    public static Entity? TryGetEntity(string name)
    {
        foreach (KeyValuePair<uint, Entity> kv in Entities)
            if (kv.Value.Name == name)
                return kv.Value;
        return null;
    }
    public static Entity GetEntity(uint ID) => Entities.TryGetValue(ID, out Entity? entity) ? entity : throw new KeyNotFoundException($"Entity with ID {ID} not found.");
    public static Entity? TryGetEntity(uint ID) => Entities.TryGetValue(ID, out Entity? entity) ? entity : null;
    private static readonly Dictionary<uint, Entity> Entities = new();


    public static Entity Create(Type type, BinaryReader? data = null)
    {
        Entity created = (Entity)Activator.CreateInstance(type, nonPublic: true)!;
        created.AddToEntities();
        if (data != null)
            Serializer.Deserialize(data, created);
        return created;
    }
    private static Entity Create(BinaryReader data, uint? ID = null)
    {
        Entity entity = (Entity)Serializer.Deserialize(data);
        if (ID != null)
            entity.ID = ID.Value;
        entity.AddToEntities();
        return entity;
    }
    public static void DisposeAll()
    {
        foreach (Entity entity in Entities.Values.ToArray())
            entity.Dispose();
        DisposeAllExtras();
        Entities.Clear();
    }
    public static void Load(string path)
    {
        DisposeAll();
        string content = DriveMounts.FileText(path);
    }
}