using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Patchwork.FileSystem;
public interface IEditorSerializable
{
    string Serialize();
    void Deserialize(string data);
}
public interface IModel : IEditorSerializable
{
    
}
public abstract class Entity : IDisposable
{
    [EditorField] private string NameInternal;
    public string Name => NameInternal;
    public uint ID { get; private set; } = 0;
    public bool Dead { get; private set; } = false;
    public bool Disposed { get; private set; }
    public bool Disposing { get; private set; }
    public float DisposeTimer { get; private set; }
    [EditorField] public IModel Model;
    private World? WorldInternal;
    public World World
    {
        get => WorldInternal ?? throw new InvalidOperationException("Entity not added to world.");
        set
        {
            if (WorldInternal != null)
                WorldInternal.Entities.Remove(this);
            value.Entities.Add(this);
            WorldInternal = value;
        }
    }
    public Entity GetEntityFromWorld(string name) => World.GetEntity(name);
    public Dictionary<string, Entity> EntityByName = new();
    public Entity GetEntity(string name)
    {
        lock (EntityByName)
            return EntityByName.TryGetValue(name, out Entity? entity) ? entity : throw new KeyNotFoundException($"Entity '{name}' not found.");
    }
    [EditorButton] public void Kill() => Dead = true;
    [EditorButton] public void Dispose()
    {
        Kill();
        if (Disposed) return;
        Disposed = true;
        Disposing = false;

        lock (Entities)
        {
            if (Entities.Remove(ID))
                FreeIds.Push(ID);
        }
        lock (World.Entities)
            World.Entities.Remove(this);
        lock (EntityByName)
            EntityByName.Remove(Name);
    }
    public void DisposeIn(float time)
    {
        Kill();
        if (Disposed || (Disposing && time > DisposeTimer)) return;
        Disposing = true;
        DisposeTimer = time;
    }
    private static readonly Dictionary<uint, Entity> Entities = new();
    private static readonly Stack<uint> FreeIds = new();
    private static uint NextId = 0;
    protected Entity()
    {
        lock (Entities)
        {
            if (FreeIds.Count > 0)
            {
                ID = FreeIds.Pop();
            }
            else
            {
                if (NextId == uint.MaxValue)
                {
                    Console.WriteLine("What the fuck are you doing to have 4 billion entities across all loaded worlds?");
                    Close();
                    Kill();
                    Disposed = true;
                    return;
                }

                ID = NextId++;
            }

            Entities.Add(ID, this);
        }
    }
    public Entity(World world) : this()
    {
        World = world;
    }
    
    public static Entity Create(Type type, SettingsDefinition entityDefinition, World world)
    {
        Entity created = SettingsDefinition.Create<Entity>(type, entityDefinition);
        created.World = world;
        return created;
    }
    public abstract IEnumerable<string> Inputs { get; }
    public struct Connection : IEditorSerializable
    {
        public string InputObjectName;
        public string InputName;
        public string Output;
        public void Deserialize(string data)
        {
            string[] parts = data.Split(':');
            if (parts.Length != 3)
                throw new InvalidDataException("Connection data must be in the format 'object:input:output'.");
            InputObjectName = parts[0];
            InputName = parts[1];
            Output = parts[2];
        }
        public string Serialize()
        {
            return $"{InputObjectName}:{InputName}:{Output}";
        }
    }
    [EditorField] public Connection[] Connections;
    public abstract IEnumerable<string> Outputs { get; }
    public abstract void Input(string name);
    public List<(Connection connection, float wait)> QueuedOutputs = new();
    public void Output(string name, float wait = 0)
    {
        Connection connection = Connections.First(c => c.Output == name);
        if (string.IsNullOrWhiteSpace(connection.InputObjectName))
            throw new InvalidDataException("Connection has no input object.");
        QueuedOutputs.Add((connection, wait));
    }
    public abstract void Think();
    public void Tick(bool running = true)
    {
        if (Disposed) return;
        if (Disposing)
        {
            DisposeTimer -= DeltaTime;
            if (DisposeTimer <= 0)
                Dispose();
        }
        lock (QueuedOutputs)
            if (QueuedOutputs.Count > 0)
                for (int i = 0; i < QueuedOutputs.Count; i++)
                    if (QueuedOutputs[i].wait <= 0)
                    {
                        Connection connection = QueuedOutputs[i].connection;
                        Entity entity = GetEntity(connection.InputObjectName);
                        entity.Input(connection.InputName);
                        QueuedOutputs.RemoveAt(i);
                        i--;
                    }
                    else
                        QueuedOutputs[i] = QueuedOutputs[i] with { wait = QueuedOutputs[i].wait - DeltaTime };
        if (running)
            Think();
    }
}
public struct SettingsDefinition
{
    public Dictionary<string, string> Settings;
    private static ConcurrentDictionary<Type, EditorCache> FieldCaches = new();
    private struct EditorCache
    {
        public Dictionary<string, FieldInfo> EditorFields { get; init; }
        public Dictionary<string, MethodInfo> EditorButtons { get; init; }
    }
    public SettingsDefinition(string definition)
    {
        Settings = new Dictionary<string, string>();
        string[] lines = definition.Split(';');
        foreach (string line in lines)
        {
            string[] parts = line.Split(':');
            string key = parts[0];
            // 1 and onwards
            string value = string.Join(':', parts[1..]);
            Settings[key] = value;
        }
    }
    public static object? ConvertStringToValue(Type t, string s)
    {
        Type? underlying = Nullable.GetUnderlyingType(t);
        if (underlying != null)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;

            return ConvertStringToValue(underlying, s);
        }

        if (t.IsEnum)
        {
            if (!Enum.TryParse(t, s, ignoreCase: true, out object? result)) return null;
            return result;
        }

        if (t == typeof(string))
            return s;

        MethodInfo? tryParse = t.GetMethod(
            "TryParse",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), t.MakeByRefType() },
            modifiers: null
        );

        if (tryParse != null)
        {
            object[] args = new object[] { s, Activator.CreateInstance(t)! };

            bool success = (bool)tryParse.Invoke(null, args)!;
            if (success)
                return args[1];
        }

        MethodInfo? parse = t.GetMethod(
            "Parse",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null
        );

        if (parse != null)
            return parse.Invoke(null, new object[] { s });

        if (typeof(IConvertible).IsAssignableFrom(t))
            return Convert.ChangeType(s, t, CultureInfo.InvariantCulture);

        return null;
    }
    static object FromType(Type t, string s, int layer = 0)
    {
        if (t.IsAssignableTo(typeof(IEditorSerializable)))
        {
            IEditorSerializable value = (IEditorSerializable)(Activator.CreateInstance(t, nonPublic: true) ?? throw new InvalidDataException($"Failed to create instance of {t.Name}."));
            value.Deserialize(s);
            return value;
        }
        else if (t == typeof(string))
        {
            return s;
        }
        else if (t.IsArray)
        {
            string[] values = s.Split(layer == 0 ? ";" : ("#" + new string('^', layer) + "#"));
            Type elementType = t.GetElementType()!;
            Array array = Array.CreateInstance(elementType, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                object? result = FromType(elementType, values[i], layer + 1);
                if (result == null || result.GetType() != elementType)
                    throw new InvalidDataException($"Failed to convert string '{values[i]}' to value of type {elementType.Name}.");
                array.SetValue(result, i);
            }
            return array;
        }
        else if (t.IsValueType)
        {
            object? result = ConvertStringToValue(t, s);
            if (result == null || result.GetType() != t)
                throw new InvalidDataException($"Failed to convert string '{s}' to value of type {t.Name}.");
            return result;
        }
        else throw new InvalidDataException($"Type {t.Name} is not supported.");
    }
    public static T Create<T>(Type type, SettingsDefinition entityDefinition, int layer = 0)
    {
        T obj = (T)(Activator.CreateInstance(type, nonPublic: true) ?? throw new InvalidDataException($"Failed to create instance of {type.Name}."));
        EditorCache cache;
        if (!FieldCaches.TryGetValue(type, out cache))
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Dictionary<string, FieldInfo> editorFields = new();
            Dictionary<string, MethodInfo> editorButtons = new();
            foreach (FieldInfo field in fields)
                if (field.GetCustomAttribute<EditorFieldAttribute>() != null)
                    editorFields[field.Name] = field;
            foreach (MethodInfo method in methods)
                if (method.GetCustomAttribute<EditorButtonAttribute>() != null)
                    editorButtons[method.Name] = method;
            cache = new EditorCache
            {
                EditorFields = editorFields,
                EditorButtons = editorButtons
            };
            FieldCaches.TryAdd(type, cache);
        }
        int covered = 0;
        foreach (KeyValuePair<string, string> setting in entityDefinition.Settings)
        {
            if (!cache.EditorFields.TryGetValue(setting.Key, out FieldInfo? field))
                throw new InvalidDataException($"Entity {type.Name} has no field named '{setting.Key}'.");
            Type fieldType = field.FieldType;
            covered++;
            field.SetValue(obj, FromType(fieldType, setting.Value, layer));
        }
        return obj;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class EditorFieldAttribute : Attribute
{
    
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class EditorButtonAttribute : Attribute
{
    
}

public class World : IDisposable
{
    public static bool DisposedAll { get; private set; } = false;
    public bool Disposed { get; private set; } = false;
    public bool Running;
    public readonly List<Entity> Entities = new();
    private string? LastPath;
    public Entity GetEntity(string name)
    {
        if (Disposed || DisposedAll) return null!;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Entity name must not be empty.", nameof(name));
        lock (Entities)
            return Entities.FirstOrDefault(e => e.Name == name) ?? throw new KeyNotFoundException($"Entity '{name}' not found.");
    }
    private static readonly List<World> Worlds = new();
    public static void TickAll()
    {
        foreach (World world in Worlds.ToArray())
            world.Tick();
    }
    public World()
    {
        if (DisposedAll) throw new InvalidOperationException("Worlds cannot be created after all have been disposed.");
        lock (Worlds)
            Worlds.Add(this);
    }
    public void Make(string def)
    {
        if (!def.StartsWith("["))
            throw new InvalidDataException("Entity definition must start with a type name.");
        string typeName = def.Substring(1, def.IndexOf(']') - 1);
        Type? type = Type.GetType(typeName);
        if (type == null)
            throw new InvalidDataException($"Type '{typeName}' not found.");
        def = def.Substring(typeName.Length + 2).Trim();
        SettingsDefinition definition = new(def);
        Entity.Create(type, definition, this);
    }
    public void Load(string path)
    {
        if (Disposed || DisposedAll) return;
        Unload();
        LastPath = path;
        string content = DriveMounts.FileText(path);
        string[] entities = content.Split("=====");
        foreach (string entity in entities)
        {
            string text = entity.Trim();
            if (entity.StartsWith("FILE."))
            {
                Make(DriveMounts.FileText(text.Substring(5)));
            }
            else
                Make(text);
        }
    }
    public void Reload()
    {
        if (Disposed || DisposedAll) return;
        if (LastPath == null) throw new InvalidOperationException("World must be loaded before it can be reloaded.");
        Load(LastPath);
    }
    public void Unload()
    {
        foreach (Entity entity in Entities.ToArray())
            entity.Dispose();
        Entities.Clear();
    }
    public void Tick()
    {
        foreach (Entity entity in Entities.ToArray())
            entity.Tick(Running);
    }
    public void Dispose()
    {
        Disposed = true;
        Unload();
        lock (Worlds)
            Worlds.Remove(this);
    }
    public static void DisposeAll()
    {
        if (DisposedAll) return;
        foreach (World world in Worlds)
            world.Dispose();
        Worlds.Clear();
        DisposedAll = true;
    }
}