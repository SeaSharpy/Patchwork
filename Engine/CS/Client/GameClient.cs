
using System.Net.Sockets;
namespace Patchwork;
public class GameClient
{
    public event Action<uint, BinaryReader>? PacketReceived;

    private TcpClient TcpClient;
    private NetworkStream Stream;
    private string PlayerName;

    public async Task ConnectAsync(string host, int port, string playerName)
    {
        PlayerName = playerName;
        TcpClient = new TcpClient();
        await TcpClient.ConnectAsync(host, port);
        Stream = TcpClient.GetStream();

        // Send auth packet (type 0, payload: player name)
        await SendAsync(0, writer =>
        {
            writer.Write(PlayerName);
        });

        _ = ReadLoopAsync();
    }

    private async Task ReadLoopAsync()
    {
        byte[] headerBuffer = new byte[8];

        try
        {
            while (true)
            {
                await Stream.ReadExactlyAsync(headerBuffer, 8);
                uint packetType = BitConverter.ToUInt32(headerBuffer, 0);
                int length = BitConverter.ToInt32(headerBuffer, 4);

                if (length < 0)
                    throw new InvalidDataException("Negative packet length.");

                byte[] payload = new byte[length];
                if (length > 0)
                    await Stream.ReadExactlyAsync(payload, length);

                if (PacketReceived != null)
                {
                    MemoryStream ms = new MemoryStream(payload, writable: false);
                    BinaryReader reader = new BinaryReader(ms);
                    PacketReceived.Invoke(packetType, reader);
                }
            }
        }
        catch
        {
            TcpClient.Close();
        }
    }

    public async Task SendAsync(uint packetType, Action<BinaryWriter> writePayload)
    {
        if (TcpClient == null || !TcpClient.Connected)
            return;

        using MemoryStream ms = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writePayload(writer);
        }

        byte[] payload = ms.ToArray();
        int length = payload.Length;

        byte[] header = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(packetType), 0, header, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(length), 0, header, 4, 4);

        await Stream.WriteAsync(header, 0, header.Length);
        if (length > 0)
            await Stream.WriteAsync(payload, 0, length);
    }
}
