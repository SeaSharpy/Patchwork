namespace Patchwork;
public static partial class Globals
{
    public static void SetupPackets()
    {
        GameClient.PacketReceived += (packetType, reader) =>
        {
            if (packetType == (uint)PacketType.Globals)
                Serializer.Deserialize(reader);
        };
    }
    public static uint Camera = 0;
    public static float FOV = 100;
}