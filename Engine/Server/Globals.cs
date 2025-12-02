using System.Reflection;
namespace Patchwork;

public static partial class Globals
{
    public static Dictionary<string, object> PreviousNetworkState { get; } = new();
    public static List<string> GetNetworkPayload()
    {
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(typeof(Globals));
        List<string> payload = new();
        foreach (KeyValuePair<string, SerializedMember> member in members)
        {
            object? value = null;
            if (member.Value.Member is PropertyInfo property)
                value = property.GetValue(null);
            else if (member.Value.Member is FieldInfo field)
                value = field.GetValue(null);
            if (value == null) continue;
            if (PreviousNetworkState.TryGetValue(member.Key, out object? previousValue))
                if (value.Equals(previousValue)) continue;
            payload.Add(member.Key);
            PreviousNetworkState[member.Key] = value;
        }
        return payload;
    }
    public static void Sync()
    {
        GameServer.SendToAll((uint)PacketType.Globals, (BinaryWriter writer) =>
        {
            Serializer.Serialize(writer, typeof(Globals), ["runtime"], GetNetworkPayload());
            return true;
        });
    }

    public static void SetupPackets()
    {
        GameServer.PlayerJoined += (player) =>
        {
            PreviousNetworkState.Clear();
        };
    }
}
