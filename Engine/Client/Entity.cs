using OpenTK.Mathematics;

namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public Matrix4 Transform => Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(TK(Rotation)) * Matrix4.CreateTranslation(TK(Position));
    public static void SetupPackets()
    {
        GameClient.PacketReceived += (packetType, reader) =>
        {
            try
            {
                if (packetType == (uint)PacketType.Entity)
                {
                    while (true)
                    {
                        uint ID = reader.ReadUInt32();
                        Entity? entity = TryGetEntity(ID);
                        if (entity == null)
                        {
                            Create(reader, ID);
                            return;
                        }
                        lock (entity)
                            Serializer.Deserialize(reader, entity);
                    }
                }
                else if (packetType == (uint)PacketType.Destroy)
                {
                    while (true)
                    {
                        uint ID = reader.ReadUInt32();
                        Entity? entity = TryGetEntity(ID);
                        if (entity == null)
                            continue;
                        lock (entity)
                            entity.Dispose();
                    }
                }
                else if (packetType == (uint)PacketType.Clear)
                {
                    DisposeAll();
                }
                else if (packetType == (uint)PacketType.EntityMessage)
                {
                    uint ID = reader.ReadUInt32();
                    string name = reader.ReadString();
                    Entity? entity = TryGetEntity(ID);
                    if (entity == null) return;
                    lock (entity)
                    {
                        entity.MessageRecieved(name, reader);
                        entity.MessageRecievedClient(name, reader);
                    }
                }
            }
            catch (EndOfStreamException) { }
        };
    }
    public void Message(string name, Action<BinaryWriter> writePayload)
    {
        GameClient.Send((uint)PacketType.EntityMessage, (BinaryWriter writer) =>
        {
            writer.Write(ID);
            writer.Write(name);
            writePayload(writer);
        });
    }
    public void DisposeExtras()
    {
        FreeIds.Push(ID);
    }

    public static void DisposeAllExtras()
    {
        NextId = uint.MaxValue;
        FreeIds.Clear();
    }
    private static readonly Stack<uint> FreeIds = new();
    private static uint NextId = uint.MaxValue;
    private static uint GetID()
    {
        checked
        {
            if (FreeIds.Count > 0)
                return FreeIds.Pop();

            return NextId--;
        }
    }
    public static void TickAll()
    {
        foreach (Entity entity in Entities.Values.ToArray())
            entity.Tick();
    }
    public void Tick()
    {
        lock (this)
        {
            Initialize();
            Client();
        }
    }
}