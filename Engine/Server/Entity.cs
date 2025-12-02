using System.Reflection;
namespace Patchwork;

public partial class Entity : IDisposable
{
    public static void SetupPackets()
    {
        GameServer.PacketReceived += (player, packetType, reader) =>
        {
            try
            {
                if (packetType == (uint)PacketType.EntityMessage)
                {
                    uint ID = reader.ReadUInt32();
                    string name = reader.ReadString();
                    Entity? entity = TryGetEntity(ID);
                    if (entity == null) return;
                    lock (entity)
                    {
                        entity.MessageRecieved(name, reader);
                        entity.MessageRecievedServer(player, name, reader);
                    }
                }
            }
            catch (EndOfStreamException) { }
        };
        GameServer.PlayerJoined += (player) =>
        {
            ClearAllState();
        };
    }
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
            DisposeTimer -= DeltaTime;
            if (DisposeTimer <= 0)
            {
                Dispose();
                return;
            }
        }
        if (Model is ModelFile model)
        {
            using BinaryReader reader = new(DriveMounts.FileStream(model.Path));
            Model = (IModel)(Serializer.Deserialize(reader) ?? throw new InvalidDataException("Deserialized static model for some fucking reason, how does this happen lol."));
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
        lock (this)
        {
            Initialize();
            Server();
        }
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
        NextId = 1;
        FreeIds.Clear();
        GameServer.SendToAll((uint)PacketType.Clear, (BinaryWriter writer) => { return true; });
    }
    private static readonly Stack<uint> FreeIds = new();
    private static uint NextId = 1;
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
        lock (PreviousNetworkState)
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
    }
    public static void SyncAll()
    {
        GameServer.SendToAll((uint)PacketType.Destroy, (BinaryWriter writer) =>
        {
            while (DisposedIDs.TryPop(out uint ID))
                writer.Write(ID);
            return true;
        });
        GameServer.SendToAll((uint)PacketType.Entity, (BinaryWriter writer) =>
        {
            bool done = false;
            foreach (Entity entity in Entities.Values.ToArray())
                done |= entity.Sync(writer);
            return done;
        });
    }
    public static void ClearAllState()
    {
        foreach (Entity entity in Entities.Values.ToArray())
            entity.ClearState();
    }
    public void MessagePlayer(string player, string name, Action<BinaryWriter> writePayload)
    {
        GameServer.Send(player, (uint)PacketType.EntityMessage, (BinaryWriter writer) =>
        {
            writer.Write(ID);
            writer.Write(name);
            writePayload(writer);
            return true;
        });
    }
    public void Message(string name, Action<BinaryWriter> writePayload)
    {
        GameServer.SendToAll((uint)PacketType.EntityMessage, (BinaryWriter writer) =>
        {
            writer.Write(ID);
            writer.Write(name);
            writePayload(writer);
            return true;
        });
    }
    public bool Sync(BinaryWriter writer)
    {
        SyncTimer -= DeltaTime;
        if (SyncTimer > 0) return false;
        SyncTimer = SyncInterval;
        List<string> payload = GetNetworkPayload();
        if (payload.Count == 0) return false;
        writer.Write(ID);
        Serializer.Serialize(writer, this, ["runtime"], payload);
        return true;
    }
    public void ClearState()
    {
        lock (PreviousNetworkState)
            PreviousNetworkState.Clear();
    }
}
