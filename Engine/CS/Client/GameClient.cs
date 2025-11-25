using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class GameClient
{
    public event Action<uint, BinaryReader>? PacketReceived;

    private const uint AuthPacketType = 0;

    private TcpClient Client;
    private NetworkStream Stream;
    private string PlayerName;

    private readonly object SendLock = new();
    private CancellationTokenSource? ReceiveCts;
    private Task? ReceiveTask;

    public bool Connected => Client != null && Client.Connected;

    // Sync connect
    public void Connect(string host, int port, string playerName)
    {
        PlayerName = playerName;

        Client = new TcpClient();
        Client.Connect(host, port);
        Stream = Client.GetStream();

        // auth packet
        Send(AuthPacketType, writer =>
        {
            writer.Write(PlayerName);
        });

        StartReceiveLoop();
    }

    // Async connect
    public async Task ConnectAsync(string host, int port, string playerName)
    {
        PlayerName = playerName;

        Client = new TcpClient();
        await Client.ConnectAsync(host, port);
        Stream = Client.GetStream();

        // auth packet
        await SendAsync(AuthPacketType, writer =>
        {
            writer.Write(PlayerName);
        });

        StartReceiveLoop();
    }

    public void Disconnect()
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

    // Sync send
    public void Send(uint packetType, Action<BinaryWriter> writePayload)
    {
        if (!Connected) return;

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

    // Async send
    public async Task SendAsync(uint packetType, Action<BinaryWriter> writePayload)
    {
        if (!Connected) return;

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

        // If you really want the write itself to be async instead of sync-under-lock,
        // you can replace the body with an async lock or a dedicated send loop later.
    }

    private void StartReceiveLoop()
    {
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
                // disconnected or cancelled
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
