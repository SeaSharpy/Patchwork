
namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public static Entity GetEntity(string name)
    {
        foreach (KeyValuePair<uint, Entity> kv in Entities)
        {
            if (kv.Value.Name == name)
                return kv.Value;
        }
        throw new KeyNotFoundException($"Entity '{name}' not found.");
    }
    private static readonly Dictionary<uint, Entity> Entities = new();
    private static readonly Stack<uint> FreeIds = new();
    private static uint NextId = 0;
    public static void TickAll()
    {
        foreach (Entity entity in Entities.Values.ToArray())
            entity.Tick();
    }
    private static uint GetID()
    {
        checked
        {
            if (FreeIds.Count > 0)
                return FreeIds.Pop();

            return NextId++;
        }
    }

    public static Entity Create(Type type)
    {
        Entity created = (Entity)Activator.CreateInstance(type, nonPublic: true)!;
        return created;
    }
    public static void DisposeAll()
    {
        foreach (Entity entity in Entities.Values.ToArray())
            entity.Dispose();
        NextId = 0;
        Entities.Clear();
        FreeIds.Clear();
    }
    public static void Load(string path)
    {
        DisposeAll();
        string content = DriveMounts.FileText(path);
    }
}