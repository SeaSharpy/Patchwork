using System.Net.Sockets;
using System.Text;
namespace Patchwork.Net;
public static class GameClient
{
    public static event Action<uint, BinaryReader>? PacketReceived;

    private const uint AuthPacketType = 0;

    private static TcpClient? Client;
    private static NetworkStream? Stream;
    private static string? PlayerName;

    private static readonly object SendLock = new();
    private static CancellationTokenSource? ReceiveCts;
    private static Task? ReceiveTask;

    public static bool Connected => Client != null && Client.Connected;

    public static void Connect(string host, int port, string playerName)
    {
        PlayerName = playerName;

        Client = new TcpClient();
        Client.Connect(host, port);
        Stream = Client.GetStream();

        Send(AuthPacketType, writer =>
        {
            writer.Write(PlayerName);
        });

        StartReceiveLoop();
    }

    public static async Task ConnectAsync(string host, int port, string playerName)
    {
        PlayerName = playerName;

        Client = new TcpClient();
        await Client.ConnectAsync(host, port);
        Stream = Client.GetStream();

        await SendAsync(AuthPacketType, writer =>
        {
            writer.Write(PlayerName);
        });

        StartReceiveLoop();
    }

    public static void Disconnect()
    {
        ReceiveCts?.Cancel();

        try
        {
            Stream?.Close();
            Client?.Close();
        }
        catch
        {
        }
    }

    public static void Send(uint packetType, Action<BinaryWriter> writePayload)
    {
        if (!Connected || Stream == null) return;

        byte[] payload;
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                writePayload(writer);
            }

            payload = ms.ToArray();
        }

        byte[] header = new byte[8];
        int length = payload.Length;

        Buffer.BlockCopy(BitConverter.GetBytes(packetType), 0, header, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(length), 0, header, 4, 4);

        lock (SendLock)
        {
            Stream.Write(header, 0, header.Length);
            if (length > 0)
                Stream.Write(payload, 0, length);
        }
    }

    public static async Task SendAsync(uint packetType, Action<BinaryWriter> writePayload)
    {
        if (!Connected || Stream == null) return;

        byte[] payload;
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                writePayload(writer);
            }

            payload = ms.ToArray();
        }

        byte[] header = new byte[8];
        int length = payload.Length;

        Buffer.BlockCopy(BitConverter.GetBytes(packetType), 0, header, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(length), 0, header, 4, 4);

        lock (SendLock)
        {
            Stream.Write(header, 0, header.Length);
            if (length > 0)
                Stream.Write(payload, 0, length);
        }
    }

    private static void StartReceiveLoop()
    {
        if (Stream == null || Client == null) return;

        ReceiveCts?.Cancel();
        ReceiveCts = new CancellationTokenSource();
        CancellationToken token = ReceiveCts.Token;

        ReceiveTask = Task.Run(async () =>
        {
            byte[] headerBuffer = new byte[8];

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await ReadExactlyAsync(Stream, headerBuffer, 8, token);

                    uint packetType = BitConverter.ToUInt32(headerBuffer, 0);
                    int length = BitConverter.ToInt32(headerBuffer, 4);

                    if (length < 0)
                        throw new InvalidDataException("Negative packet length.");

                    byte[] payload = new byte[length];
                    if (length > 0)
                        await ReadExactlyAsync(Stream, payload, length, token);

                    if (PacketReceived != null)
                    {
                        using MemoryStream ms = new MemoryStream(payload, writable: false);
                        using BinaryReader reader = new BinaryReader(ms, Encoding.UTF8);
                        PacketReceived.Invoke(packetType, reader);
                    }
                }
            }
            catch
            {
                try
                {
                    Client.Close();
                }
                catch
                {
                }
            }
        }, token);
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int length, CancellationToken token)
    {
        int offset = 0;
        int remaining = length;

        while (remaining > 0)
        {
            int read = await stream.ReadAsync(buffer, offset, remaining, token);
            if (read <= 0)
                throw new IOException("Remote closed connection.");

            offset += read;
            remaining -= read;
        }
    }
}
