global using Patchwork.Serialization;
using System.Reflection;
namespace Patchwork.Serialization;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SerializedMemberAttribute(string[]? flags = null, string? queue = null) : Attribute
{
    public string[] Flags { get; init; } = flags ?? [];
    public string? Queue { get; init; } = queue;
}

public struct SerializedMember
{
    public MemberInfo Member;
    public Type Type;
    public string[] Flags;
    public string? Queue;
}
public static class SerializationRegistry
{
    public static readonly Type[] StaticSerializers = [
        typeof(Globals),
    ];
    public static void InitializeStaticSerializers()
    {
        foreach (Type type in StaticSerializers)
            Register(type, true);
    }
    private static Dictionary<Type, Dictionary<string, SerializedMember>> Members = new();
    private static void Register(Type type, bool static_ = false)
    {
        Dictionary<string, SerializedMember> members = new();
        foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | (static_ ? BindingFlags.Static : BindingFlags.Instance)))
        {
            SerializedMemberAttribute? attribute = member.GetCustomAttribute<SerializedMemberAttribute>();
            if (attribute == null) continue;
            Type memberType;
            if (member is PropertyInfo property)
            {
                if (!property.CanRead || !property.CanWrite) continue; 
                if (property.GetIndexParameters().Length > 0) continue;
                memberType = property.PropertyType;
            }
            else if (member is FieldInfo field)
            {
                if (field.IsInitOnly) continue;
                memberType = field.FieldType;
            }
            else continue;
            members[member.Name] = new SerializedMember { Member = member, Type = memberType, Flags = attribute.Flags, Queue = attribute.Queue };
        }
        Members[type] = members;
    }
    public static Dictionary<string, SerializedMember> GetSerializationMembers(Type type)
    {
        if (!Members.ContainsKey(type)) Register(type);
        return Members[type];
    }
}

public interface ISerializable
{

}
public static class Serializer
{
    public static Dictionary<string, Queue<(SerializedMember member, ISerializable? obj, object? value)>> Queues = new();
    public static void FlushQueue(string queueName)
    {
        if (!Queues.TryGetValue(queueName, out Queue<(SerializedMember member, ISerializable? obj, object? value)>? queue)) return;
        while (queue.TryDequeue(out (SerializedMember member, ISerializable? obj, object? value) item))
        {
            if (item.member.Member is PropertyInfo property)
                property.SetValue(item.obj, item.value);
            else if (item.member.Member is FieldInfo field)
                field.SetValue(item.obj, item.value);
        }
    }
    static bool IsValidSubset(string[] allowed, string[] subset)
    {
        HashSet<string> allowedSet = new(allowed);
        for (int i = 0; i < subset.Length; i++)
        {
            if (!allowedSet.Contains(subset[i]))
                return false;
        }
        return true;
    }
    public static void Serialize(BinaryWriter writer, ISerializable obj, string[]? flags = null, List<string>? enabled = null)
    {
        Type type = obj.GetType();
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(type);

        writer.Write(type.AssemblyQualifiedName ?? throw new InvalidDataException($"Type {type.Name} doesn't have a full name for some fucking reason."));

        int actualCount = 0;
        foreach (KeyValuePair<string, SerializedMember> m in members)
        {
            string name = m.Key;
            SerializedMember member = m.Value;

            if (enabled != null && !enabled.Contains(name))
                continue;
            if (flags != null && !IsValidSubset(flags, member.Flags))
                continue;

            actualCount++;
        }

        writer.Write(false);
        writer.Write(actualCount);

        foreach (KeyValuePair<string, SerializedMember> pair in members)
        {
            string name = pair.Key;
            SerializedMember member = pair.Value;

            if (enabled != null && !enabled.Contains(name))
                continue;
            if (flags != null && !IsValidSubset(flags, member.Flags))
                continue;

            writer.Write(name);

            object? value = null;
            if (member.Member is PropertyInfo property)
                value = property.GetValue(obj);
            else if (member.Member is FieldInfo field)
                value = field.GetValue(obj);

            WriteValue(writer, member.Type, value, flags);
        }
    }
    public static void Serialize(BinaryWriter writer, Type type, string[]? flags = null, List<string>? enabled = null)
    {
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(type);

        writer.Write(type.AssemblyQualifiedName ?? throw new InvalidDataException($"Type {type.Name} doesn't have a full name for some fucking reason."));

        int actualCount = 0;
        foreach (KeyValuePair<string, SerializedMember> m in members)
        {
            string name = m.Key;
            SerializedMember member = m.Value;

            if (enabled != null && !enabled.Contains(name))
                continue;
            if (flags != null && !IsValidSubset(flags, member.Flags))
                continue;

            actualCount++;
        }

        writer.Write(true);
        writer.Write(actualCount);

        foreach (KeyValuePair<string, SerializedMember> pair in members)
        {
            string name = pair.Key;
            SerializedMember member = pair.Value;

            if (enabled != null && !enabled.Contains(name))
                continue;
            if (flags != null && !IsValidSubset(flags, member.Flags))
                continue;

            writer.Write(name);

            object? value = null;
            if (member.Member is PropertyInfo property)
                value = property.GetValue(null);
            else if (member.Member is FieldInfo field)
                value = field.GetValue(null);

            WriteValue(writer, member.Type, value, flags);
        }
    }
    public static ISerializable? Deserialize(BinaryReader reader)
    {
        string typeName = reader.ReadString();
        Type type = Type.GetType(typeName) ?? throw new InvalidDataException($"Type {typeName} not found.");
        if (!type.IsAssignableTo(typeof(ISerializable))) throw new InvalidDataException($"Type {typeName} is not serializable.");
        bool static_ = reader.ReadBoolean();
        int memberCount = reader.ReadInt32();
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(type);
        ISerializable? obj = static_ ? null : (ISerializable)(Activator.CreateInstance(type, nonPublic: true)
                                            ?? throw new InvalidDataException($"Failed to create instance of type {type.FullName}."));
        for (int i = 0; i < memberCount; i++)
        {
            string memberName = reader.ReadString();
            if (!members.TryGetValue(memberName, out SerializedMember serializedMember)) throw new InvalidDataException($"Member {memberName} not found.");
            Type memberType = serializedMember.Type;
            object? value = ReadValue(reader, memberType);
            if (serializedMember.Queue != null)
            {
                if (!Queues.TryGetValue(serializedMember.Queue, out Queue<(SerializedMember member, ISerializable? obj, object? value)>? queue))
                    Queues[serializedMember.Queue] = queue = new();
                queue.Enqueue((serializedMember, obj, value));
                continue;
            }
            if (serializedMember.Member is PropertyInfo property)
                property.SetValue(obj, value);
            else if (serializedMember.Member is FieldInfo field)
                field.SetValue(obj, value);
        }
        return obj;
    }

    public static void Deserialize(BinaryReader reader, ISerializable obj)
    {
        string typeName = reader.ReadString();
        Type type = Type.GetType(typeName) ?? throw new InvalidDataException($"Type {typeName} not found.");
        if (type != obj.GetType()) throw new InvalidDataException($"Type {typeName} does not match object type.");
        if (!type.IsAssignableTo(typeof(ISerializable))) throw new InvalidDataException($"Type {typeName} is not serializable.");
        bool static_ = reader.ReadBoolean();
        if (static_) throw new InvalidDataException("Static deserialization to object is not supported.");
        int memberCount = reader.ReadInt32();
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(type);
        for (int i = 0; i < memberCount; i++)
        {
            string memberName = reader.ReadString();
            if (!members.TryGetValue(memberName, out SerializedMember serializedMember)) throw new InvalidDataException($"Member {memberName} not found.");
            Type memberType = serializedMember.Type;
            object? prevValue = null;
            {
                if (serializedMember.Member is PropertyInfo property)
                    prevValue = property.GetValue(obj);
                else if (serializedMember.Member is FieldInfo field)
                    prevValue = field.GetValue(obj);
            }
            object? value = ReadValueObj(reader, memberType, prevValue);
            if (serializedMember.Queue != null)
            {
                if (!Queues.TryGetValue(serializedMember.Queue, out Queue<(SerializedMember member, ISerializable? obj, object? value)>? queue))
                    Queues[serializedMember.Queue] = queue = new();
                queue.Enqueue((serializedMember, obj, value));
                continue;
            }
            {
                if (serializedMember.Member is PropertyInfo property)
                    property.SetValue(obj, value);
                else if (serializedMember.Member is FieldInfo field)
                    field.SetValue(obj, value);
            }
        }
    }
    private static object? ReadValueObj(BinaryReader reader, Type type, object? value)
    {
        bool hasValue = reader.ReadBoolean();
        if (!hasValue) return null;
        if (type.IsArray)
        {
            if (value is Array array && array.GetType().GetElementType() == type)
            {
                Array.Clear(array);
                int length = reader.ReadInt32();
                Type elementType = type.GetElementType()!;
                for (int i = 0; i < length; i++)
                {
                    object? element = ReadValue(reader, elementType);
                    array.SetValue(element, i);
                }
                return array;
            }
            else
            {
                int length = reader.ReadInt32();
                Type elementType = type.GetElementType()!;
                Array newArray = Array.CreateInstance(elementType, length);
                for (int i = 0; i < length; i++)
                {
                    object? element = ReadValue(reader, elementType);
                    newArray.SetValue(element, i);
                }
                return newArray;
            }
        }

        if (typeof(ISerializable).IsAssignableFrom(type))
        {
            if (value is ISerializable s)
                Deserialize(reader, s);
            else
                return Deserialize(reader);
            return s;
        }

        return ReadValue(reader, type, true);
    }
    private static void WriteValue(BinaryWriter writer, Type type, object? value, string[]? flags = null)
    {
        if (value == null)
        {
            writer.Write(false);
            return;
        }
        writer.Write(true);
        if (type.IsEnum)
        {
            Type underlying = Enum.GetUnderlyingType(type);
            object underlyingValue = Convert.ChangeType(value ?? 0, underlying);
            WriteValue(writer, underlying, underlyingValue);
            return;
        }

        if (type == typeof(int))
        {
            writer.Write((int)(value ?? 0));
            return;
        }
        if (type == typeof(uint))
        {
            writer.Write((uint)(value ?? 0));
            return;
        }
        if (type == typeof(float))
        {
            writer.Write((float)(value ?? 0f));
            return;
        }
        if (type == typeof(bool))
        {
            writer.Write((bool)(value ?? false));
            return;
        }
        if (type == typeof(string))
        {
            writer.Write(value as string ?? string.Empty);
            return;
        }

        if (type.IsArray)
        {
            Array? array = (Array?)value;
            int length = array?.Length ?? 0;
            writer.Write(length);

            Type elementType = type.GetElementType()!;
            for (int i = 0; i < length; i++)
            {
                object? element = array!.GetValue(i);
                WriteValue(writer, elementType, element);
            }
            return;
        }

        if (type.IsAssignableTo(typeof(ISerializable)))
        {
            Serialize(writer, (ISerializable)value, flags);
            return;
        }

        if (type.IsValueType)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsInitOnly) continue;
                object? val = field.GetValue(value);
                WriteValue(writer, field.FieldType, val, flags);
            }
            return;
        }

        throw new NotSupportedException($"Cannot serialize field of type {type.FullName}.");
    }

    private static object? ReadValue(BinaryReader reader, Type type, bool nullChecked = false)
    {
        if (!nullChecked)
        {
            bool hasValue = reader.ReadBoolean();
            if (!hasValue) return null;
        }
        if (type.IsEnum)
        {
            Type underlying = Enum.GetUnderlyingType(type);
            object underlyingValue = ReadValue(reader, underlying) ?? throw new InvalidDataException("Failed to read enum underlying value.");
            return Enum.ToObject(type, underlyingValue);
        }

        if (type == typeof(int))
            return reader.ReadInt32();
        if (type == typeof(uint))
            return reader.ReadUInt32();
        if (type == typeof(float))
            return reader.ReadSingle();
        if (type == typeof(bool))
            return reader.ReadBoolean();
        if (type == typeof(string))
            return reader.ReadString();

        if (type.IsArray)
        {
            int length = reader.ReadInt32();
            Type elementType = type.GetElementType()!;
            Array array = Array.CreateInstance(elementType, length);
            for (int i = 0; i < length; i++)
            {
                object? element = ReadValue(reader, elementType);
                array.SetValue(element, i);
            }
            return array;
        }

        if (typeof(ISerializable).IsAssignableFrom(type))
        {
            return Deserialize(reader);
        }

        if (type.IsValueType)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            object? value = Activator.CreateInstance(type, nonPublic: true) ?? throw new InvalidDataException($"Failed to create instance of type {type.FullName}.");
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsInitOnly) continue;
                object? val = ReadValue(reader, field.FieldType);
                field.SetValue(value, val);
            }
            return value;
        }

        throw new NotSupportedException($"Cannot deserialize field of type {type.FullName}.");
    }
}