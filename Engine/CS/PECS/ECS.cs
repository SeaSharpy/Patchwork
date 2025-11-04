using Patchwork.Render;
using OpenTK.Mathematics;
namespace Patchwork.PECS;

public class ECS : IDisposable
{
    static readonly ECS InstanceInternal = new();
    public static ECS Instance => InstanceInternal;
    public static ECS I => InstanceInternal;

    ECS() { }

    public readonly List<Entity> Entities = new();
    public readonly List<IDataComponent> Components = new();
    public readonly List<ISystem> Systems = new();
    public readonly Dictionary<ISystem, bool> SystemInitialized = new();
    IRenderSystem? RenderSystem;

    internal void RegisterEntity(Entity entity)
    {
        if (Entities.Contains(entity))
            throw new Exception("Entity already exists");
        Entities.Add(entity);
    }

    internal void DestroyEntity(Entity entity)
    {
        Entities.Remove(entity);
    }

    public void RegisterSystem(ISystem system)
    {
        if (system is IRenderSystem renderSystem)
        {
            if (RenderSystem != null)
                throw new Exception("Only one render system is allowed");
            RenderSystem = renderSystem;
        }
        Systems.Add(system);
        SystemInitialized[system] = false;
    }

    public void Update()
    {
        foreach (var system in Systems.ToArray())
        {
            if (!SystemInitialized[system])
            {
                SystemInitialized[system] = true;
                system.Load();
            }
            system.Update();
        }

        foreach (var entity in Entities.ToArray())
            entity.Update();
    }
    internal void AddComponent(IDataComponent component)
    {
        Components.Add(component);
    }
    internal void RemoveComponent(IDataComponent component)
    {
        Components.Remove(component);
    }
    public IEnumerable<T> GetComponents<T>() where T : IDataComponent
    {
        return Components.OfType<T>();
    }

    public void Render()
    {
        if (RenderSystem != null)
            RenderSystem.Render();
    }
    public void Dispose()
    {
        foreach (var system in Systems.ToArray())
            system.Dispose();
    }
}

public interface ISystem : IDisposable
{
    void Load() { }
    void Update() { }
    void Draw() { }
}

public class IDataComponent
{
    public Entity Entity { get; internal set; } = null!;
}

public class IUpdateComponent : IDataComponent
{
    public void Update() { }
}

public class Entity : IDisposable
{
    public Transform Transform = new();
    public Matrix4 TransformMatrix { get; private set; } = Matrix4.Identity;
    public string Name { get; private set; }
    public string? Marker { get; private set; }
    public string[] Layers { get; private set; }
    public readonly Dictionary<Type, List<IDataComponent>> Components = new();

    public IDataComponent this[Type type] => GetComponent(type);

    public Entity(string name, string[]? layers = null, string? marker = null)
    {
        Name = name;
        Marker = marker;
        Layers = layers ?? Array.Empty<string>();
        ECS.I.RegisterEntity(this);
        Entities.Add(this);
    }

    public static Entity[] Create(int count, Func<int, string> nameFunc)
    {
        var result = new Entity[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new Entity(nameFunc(i));
        }
        return result;
    }
    public static Entity Create(string name)
    {
        return new Entity(name);
    }

    public void AddComponent<T>(T component) where T : IDataComponent
    {
        var type = typeof(T);
        if (!Components.TryGetValue(type, out var list))
        {
            list = new List<IDataComponent>();
            Components[type] = list;
        }
        if (component.Entity != null)
            throw new Exception("Component already has an entity.");
        component.Entity = this;
        list.Add(component);
        ECS.I.AddComponent(component);
    }

    public void RemoveComponent<T>(T component) where T : IDataComponent
    {
        if (Components.TryGetValue(typeof(T), out var list))
        {
            list.Remove(component);
            component.Entity = null!;
            ECS.I.RemoveComponent(component);
            if (list.Count == 0)
                Components.Remove(typeof(T));
        }
    }

    public IEnumerable<T> GetComponents<T>() where T : IDataComponent
    {
        if (Components.TryGetValue(typeof(T), out var list))
            return list.Cast<T>();

        return Enumerable.Empty<T>();
    }

    public T GetComponent<T>() where T : IDataComponent
    {
        if (Components.TryGetValue(typeof(T), out var list))
            return (T)list.First();

        return default!;
    }

    public IDataComponent GetComponent(Type type)
    {
        if (Components.TryGetValue(type, out var list))
            return list.First();

        return default!;
    }

    public bool HasComponent<T>() where T : IDataComponent
        => Components.ContainsKey(typeof(T));

    public void Update()
    {
        foreach (var list in Components.Values.ToArray())
            foreach (var component in list.ToArray())
                if (component is IUpdateComponent c)
                    c.Update();
        TransformMatrix = Transform.Matrix();
    }

    public static List<Entity> Entities = new();
    public static void DisposeAll()
    {
        foreach (var entity in Entities.ToArray())
            entity.Dispose();
        Entities.Clear();
    }

    public void Dispose()
    {
        foreach (var list in Components.Values.ToArray())
            foreach (var component in list.ToArray())
                component.Entity = null!;
        ECS.I.DestroyEntity(this);
        Entities.Remove(this);
    }
}
