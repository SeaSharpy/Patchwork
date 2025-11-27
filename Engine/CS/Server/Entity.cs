using System.Reflection;
namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public float SyncTimer = 0;
    public static void TickAll()
    {
        foreach (Entity entity in Entities.Values.ToArray())
            entity.Tick();
        SyncAll();
    }
    public void Tick()
    {
        if (Disposed) return;
        if (Disposing)
        {
            DisposeTimer -= Helper.DeltaTime;
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
                        QueuedOutputs[i] = QueuedOutputs[i] with { wait = QueuedOutputs[i].wait - Helper.DeltaTime };
        lock (this)
            Server();
    }
    public static Stack<uint> DisposedIDs = new();
    public void DisposeExtras()
    {
        FreeIds.Push(ID);
        lock (DisposedIDs)
            DisposedIDs.Push(ID);
    }

    public static void DisposeAllExtras()
    {
        NextId = 0;
        FreeIds.Clear();
        GameServer.SendToAll((uint)PacketType.Clear, (BinaryWriter writer) => { });
    }
    private static readonly Stack<uint> FreeIds = new();
    private static uint NextId = 0;
    private static uint GetID()
    {
        checked
        {
            if (FreeIds.Count > 0)
                return FreeIds.Pop();
            return NextId++;
        }
    }
    public List<string> GetNetworkPayload()
    {
        Dictionary<string, SerializedMember> members = SerializationRegistry.GetSerializationMembers(typeof(Entity));
        List<string> payload = new();
        foreach (KeyValuePair<string, SerializedMember> member in members)
        {
            object? value = null;
            if (member.Value.Member is PropertyInfo property)
                value = property.GetValue(this);
            else if (member.Value.Member is FieldInfo field)
                value = field.GetValue(this);
            if (value == null) continue;
            if (PreviousNetworkState.TryGetValue(member.Key, out object? previousValue))
                if (value.Equals(previousValue)) continue;
            payload.Add(member.Key);
            PreviousNetworkState[member.Key] = value;
        }
        return payload;
    }
    public static float SyncTimer;
    public static void SyncAll()
    {
        GameServer.SendToAll((uint)PacketType.Destroy, (BinaryWriter writer) =>
        {
            while (DisposedIDs.TryPop(out uint ID))
                writer.Write(ID);
        });
        GameServer.SendToAll((uint)PacketType.Entity, (BinaryWriter writer) =>
        {
            foreach (Entity entity in Entities.Values.ToArray())
                entity.Sync(writer);
        });
    }
    public void Sync(BinaryWriter writer)
    {
        SyncTimer -= Helper.DeltaTime;
        if (SyncTimer > 0) return;
        SyncTimer = SyncInterval;
        List<string> payload = GetNetworkPayload();
        if (payload.Count == 0) return;
        writer.Write(ID);
        Serializer.Serialize(writer, this, false, payload);
    }
}