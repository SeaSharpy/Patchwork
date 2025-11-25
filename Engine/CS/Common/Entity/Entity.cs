using System.Numerics;
using System.Reflection;
using System.Collections.Concurrent;
namespace Patchwork;

public interface IModel
{

}
public interface ISerializable
{
    
}
public abstract partial class Entity : IDisposable, ISerializable
{
    [SerializedMember] public string Name;
    [SerializedMember] public IModel Model;
    [SerializedMember] public Connection[] Connections;
    [SerializedMember] public Vector3 Position;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SerializedMemberAttribute(bool save = true) : Attribute
{
    public bool Save { get; init; } = save;
}

public struct SerializedMember
{
    public MemberInfo Member;
    public Type Type;
    public bool Save;
}
public static class SerializationRegistry
{
    private static Dictionary<Type, Dictionary<string, SerializedMember>> Members = new();
    private static void Register(Type type)
    {
        Dictionary<string, SerializedMember> members = new();
        foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            SerializedMemberAttribute? attribute = member.GetCustomAttribute<SerializedMemberAttribute>();
            if (attribute == null) continue;
            Type memberType;
            if (member is PropertyInfo property)
            {
                if (!property.CanRead || !property.CanWrite) continue;
                memberType = property.PropertyType;
            }
            else if (member is FieldInfo field)
            {
                if (field.IsInitOnly) continue;
                memberType = field.FieldType;
            }
            else continue;
            members[member.Name] = new SerializedMember { Member = member, Type = memberType, Save = attribute.Save };
        }
        Members[type] = members;
    }
    public static Dictionary<string, SerializedMember> GetSerializationMembers(Type type)
    {
        if (!Members.ContainsKey(type)) Register(type);
        return Members[type];
    }
}

public static class EntitySerializer
{
    public static void Serialize(BinaryWriter writer, ISerializable obj, bool save = true)
    {
        Type type = obj.GetType();
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(type);

        writer.Write(type.AssemblyQualifiedName ?? throw new InvalidDataException($"Type {type.Name} doesn't have a full name for some fucking reason."));
        int actualCount = 0;
        foreach (KeyValuePair<string, SerializedMember> m in members)
            if (!save || m.Value.Save)
                actualCount++;
        writer.Write(actualCount);
        foreach (KeyValuePair<string, SerializedMember> member in members)
        {
            if (save && !member.Value.Save) continue;
            writer.Write(member.Key);
            object? value = null;
            if (member.Value.Member is PropertyInfo property)
                value = property.GetValue(obj);
            else if (member.Value.Member is FieldInfo field)
                value = field.GetValue(obj);
            WriteValue(writer, member.Value.Type, value, save);
        }
    }

    public static ISerializable Deserialize(BinaryReader reader)
    {
        string typeName = reader.ReadString();
        Type type = Type.GetType(typeName) ?? throw new InvalidDataException($"Type {typeName} not found.");
        if (!type.IsAssignableTo(typeof(ISerializable))) throw new InvalidDataException($"Type {typeName} is not serializable.");
        int memberCount = reader.ReadInt32();
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(type);
        ISerializable obj = (ISerializable)(Activator.CreateInstance(type, nonPublic: true)
                                            ?? throw new InvalidDataException($"Failed to create instance of type {type.FullName}."));
        for (int i = 0; i < memberCount; i++)
        {
            string memberName = reader.ReadString();
            if (!members.TryGetValue(memberName, out SerializedMember serializedMember)) throw new InvalidDataException($"Member {memberName} not found.");
            Type memberType = serializedMember.Type;
            object? value = ReadValue(reader, memberType);
            if (serializedMember.Member is PropertyInfo property)
                property.SetValue(obj, value);
            else if (serializedMember.Member is FieldInfo field)
                field.SetValue(obj, value);
        }
        return obj;
    }

    private static void WriteValue(BinaryWriter writer, Type type, object? value, bool save = true)
    {
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

        if (typeof(ISerializable).IsAssignableFrom(type) && value != null)
        {
            Serialize(writer, (ISerializable)value, save);
            return;
        }

        throw new NotSupportedException($"Cannot serialize field of type {type.FullName}.");
    }

    private static object ReadValue(BinaryReader reader, Type type)
    {
        if (type.IsEnum)
        {
            Type underlying = Enum.GetUnderlyingType(type);
            object underlyingValue = ReadValue(reader, underlying);
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
                object element = ReadValue(reader, elementType);
                array.SetValue(element, i);
            }
            return array;
        }

        if (typeof(ISerializable).IsAssignableFrom(type))
        {
            return Deserialize(reader);
        }

        throw new NotSupportedException($"Cannot deserialize field of type {type.FullName}.");
    }
}
