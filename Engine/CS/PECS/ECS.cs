namespace Patchwork.PECS;

public class ECS : IDisposable
{
    private static ECS Instance = null!;

    public ECS()
    {
        if (Instance != null)
            throw new InvalidOperationException("ECS already initialized.");
        Instance = this;
        Entity.Instance = this;
    }

    public readonly List<Entity> Entities = new();
    public readonly List<IDataComponent> Components = new();
    public readonly List<ISystem> Systems = new();
    public readonly Dictionary<ISystem, bool> SystemInitialized = new();
    private IRenderSystem? RenderSystem;

    public void RegisterEntity(Entity entity)
    {
        if (Entities.Contains(entity))
            throw new InvalidOperationException("Entity already exists");
        Entities.Add(entity);
    }

    public void DestroyEntity(Entity entity) => Entities.Remove(entity);

    private void IRegisterSystem(ISystem system)
    {
        if (system is IRenderSystem renderSystem)
        {
            if (RenderSystem != null)
                throw new InvalidOperationException("Only one render system is allowed");
            RenderSystem = renderSystem;
        }
        Systems.Add(system);
        SystemInitialized[system] = false;
    }
    public static void RegisterSystem(ISystem system) => Instance.IRegisterSystem(system);

    public void Update()
    {
        foreach (ISystem system in Systems.ToArray())
        {
            if (!SystemInitialized[system])
            {
                SystemInitialized[system] = true;
                system.Load();
            }
            system.Update();
        }

        foreach (Entity entity in Entities.ToArray())
            entity.Update();
    }
    public void IAddComponent(IDataComponent component) => Components.Add(component);
    public static void AddComponent(IDataComponent component) => Instance.IAddComponent(component);
    public void IRemoveComponent(IDataComponent component) => Components.Remove(component);
    public static void RemoveComponent(IDataComponent component) => Instance.IRemoveComponent(component);
    public IEnumerable<T> IGetComponents<T>() where T : IDataComponent => Components.OfType<T>();
    public static IEnumerable<T> GetComponents<T>() where T : IDataComponent => Instance.IGetComponents<T>();

    public void Render()
    {
        if (RenderSystem != null)
            RenderSystem.Render();
    }
    public void Dispose()
    {
        foreach (ISystem system in Systems.ToArray())
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
    public static ECS Instance { private get; set; } = null!;
    public Transform Transform = new();
    public Matrix4 TransformMatrix => Transform.Matrix();
    public Matrix4 TransformMatrixWith(Vector3 position, Vector3 scale) => Transform.WithOffset(position).WithScale(scale).Matrix();
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
        Instance.RegisterEntity(this);
        Entities.Add(this);
    }

    public static Entity[] Create(int count, Func<int, string> nameFunc)
    {
        Entity[] result = new Entity[count];
        for (int i = 0; i < count; i++)
            result[i] = new Entity(nameFunc(i));
        return result;
    }
    public static Entity Create(string name) => new Entity(name);

    public void AddComponent<T>(T component) where T : IDataComponent
    {
        Type type = typeof(T);
        if (!Components.TryGetValue(type, out List<IDataComponent>? list))
        {
            list = [];
            Components[type] = list;
        }
        if (component.Entity != null)
            throw new InvalidOperationException("Component already has an entity.");
        component.Entity = this;
        list.Add(component);
        ECS.AddComponent(component);
    }

    public void RemoveComponent<T>(T component) where T : IDataComponent
    {
        if (Components.TryGetValue(typeof(T), out List<IDataComponent>? list))
        {
            list.Remove(component);
            component.Entity = null!;
            ECS.RemoveComponent(component);
            if (list.Count == 0)
                Components.Remove(typeof(T));
        }
    }

    public IEnumerable<T> GetComponents<T>() where T : IDataComponent
    {
        if (Components.TryGetValue(typeof(T), out List<IDataComponent>? list))
            return list.Cast<T>();

        return Enumerable.Empty<T>();
    }

    public T GetComponent<T>() where T : IDataComponent
    {
        if (Components.TryGetValue(typeof(T), out List<IDataComponent>? list))
            return (T)list.First();

        return default!;
    }

    public IDataComponent GetComponent(Type type)
    {
        if (Components.TryGetValue(type, out List<IDataComponent>? list))
            return list.First();

        return default!;
    }

    public bool HasComponent<T>() where T : IDataComponent
        => Components.ContainsKey(typeof(T));

    public void Update()
    {
        foreach (List<IDataComponent>? list in Components.Values.ToArray())
            foreach (IDataComponent? component in list.ToArray())
                if (component is IUpdateComponent c)
                    c.Update();
    }

    public static List<Entity> Entities = new();
    public static void DisposeAll()
    {
        foreach (Entity entity in Entities.ToArray())
            entity.Dispose();
        Entities.Clear();
    }

    public void Dispose()
    {
        foreach (List<IDataComponent>? list in Components.Values.ToArray())
            foreach (IDataComponent? component in list.ToArray())
                component.Entity = null!;
        Instance.DestroyEntity(this);
        Entities.Remove(this);
    }
}
