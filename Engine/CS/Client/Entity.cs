namespace Patchwork;

public abstract partial class Entity : IDisposable
{
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
            }
            catch (EndOfStreamException) { }
        };
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
            Client();
    }
}