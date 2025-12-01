using System.Net;
using System.Net.Sockets;

namespace Patchwork.Net;

public static class GameServer
{
    public static event Action<string, uint, BinaryReader>? PacketReceived;

    private class ClientConnection
    {
        public TcpClient TcpClient { get; }
        public NetworkStream Stream { get; }
        public string? PlayerName { get; set; }

        public ClientConnection(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
            Stream = tcpClient.GetStream();
        }
    }

    private static TcpListener? Listener;
    private static readonly List<ClientConnection> Clients = new();
    private static readonly Dictionary<string, ClientConnection> ClientsByName = new();
    private static CancellationTokenSource? CancellationSource;

    public static async Task StartAsync(int port)
    {
        CancellationSource?.Cancel();
        CancellationSource?.Dispose();
        CancellationSource = new CancellationTokenSource();
        CancellationToken token = CancellationSource.Token;

        Listener = new TcpListener(IPAddress.Any, port);
        Listener.Start();

        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = await Listener.AcceptTcpClientAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                ClientConnection connection = new ClientConnection(tcpClient);

                lock (Clients)
                    Clients.Add(connection);

                _ = HandleClientAsync(connection, token);
            }
        }
        finally
        {
            if (Listener != null)
            {
                Listener.Stop();
                Listener = null;
            }
        }
    }

    private static async Task HandleClientAsync(ClientConnection connection, CancellationToken token)
    {
        NetworkStream stream = connection.Stream;
        byte[] headerBuffer = new byte[8];

        try
        {
            while (!token.IsCancellationRequested)
            {
                await stream.ReadExactlyAsync(headerBuffer, 8);
                uint packetType = BitConverter.ToUInt32(headerBuffer, 0);
                int length = BitConverter.ToInt32(headerBuffer, 4);

                if (length < 0)
                    throw new InvalidDataException("Negative packet length.");

                byte[] payload = new byte[length];
                if (length > 0)
                    await stream.ReadExactlyAsync(payload, length);

                if (connection.PlayerName == null)
                {
                    if (packetType != (uint)PacketType.Auth)
                        throw new InvalidDataException("First packet from client must be auth (type 0).");

                    using MemoryStream ms = new MemoryStream(payload, writable: false);
                    using BinaryReader authReader = new BinaryReader(ms);
                    string playerName = authReader.ReadString();

                    lock (ClientsByName)
                    {
                        if (ClientsByName.ContainsKey(playerName))
                            throw new InvalidDataException($"Player name '{playerName}' is already in use.");

                        connection.PlayerName = playerName;
                        ClientsByName[playerName] = connection;
                    }

                    continue;
                }

                if (PacketReceived != null)
                {
                    MemoryStream ms = new MemoryStream(payload, writable: false);
                    BinaryReader reader = new BinaryReader(ms);
                    PacketReceived.Invoke(connection.PlayerName, packetType, reader);
                }
            }
        }
        catch
        {
            lock (Clients)
                Clients.Remove(connection);

            if (connection.PlayerName != null)
            {
                lock (ClientsByName)
                    ClientsByName.Remove(connection.PlayerName);
            }

            connection.TcpClient.Close();
        }
    }

    public static async Task SendAsync(string playerName, uint packetType, Func<BinaryWriter, bool> writePayload)
    {
        ClientConnection? connection;
        lock (ClientsByName)
        {
            if (!ClientsByName.TryGetValue(playerName, out connection))
                throw new KeyNotFoundException($"Player '{playerName}' not connected.");
        }

        using MemoryStream ms = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bool goThrough = writePayload(writer);
            if (!goThrough) return;
        }

        byte[] payload = ms.ToArray();
        await SendRawAsync(connection, packetType, payload);
    }

    public static void Send(string playerName, uint packetType, Func<BinaryWriter, bool> writePayload)
    {
        SendAsync(playerName, packetType, writePayload).GetAwaiter().GetResult();
    }

    private static async Task SendRawAsync(ClientConnection connection, uint packetType, byte[] payload)
    {
        if (!connection.TcpClient.Connected)
            return;

        NetworkStream stream = connection.Stream;

        int length = payload.Length;
        byte[] header = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(packetType), 0, header, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(length), 0, header, 4, 4);

        try
        {
            await stream.WriteAsync(header, 0, header.Length);
            if (length > 0)
                await stream.WriteAsync(payload, 0, length);
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException)
        {
            lock (Clients)
                Clients.Remove(connection);

            if (connection.PlayerName != null)
            {
                lock (ClientsByName)
                    ClientsByName.Remove(connection.PlayerName);
            }

            connection.TcpClient.Close();
        }
    }

    public static async Task SendToAllAsync(uint packetType, Func<BinaryWriter, bool> writePayload)
    {
        byte[] payload;
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                bool goThrough = writePayload(writer);
                if (!goThrough) return;
            }

            payload = ms.ToArray();
        }

        List<ClientConnection> targets;
        lock (Clients)
        {
            targets = new List<ClientConnection>(Clients.Count);
            foreach (ClientConnection client in Clients)
            {
                if (client.TcpClient.Connected && client.PlayerName != null)
                    targets.Add(client);
            }
        }

        List<Task> sendTasks = new List<Task>(targets.Count);
        foreach (ClientConnection connection in targets)
        {
            sendTasks.Add(SendRawAsync(connection, packetType, payload));
        }

        await Task.WhenAll(sendTasks);
    }

    public static void SendToAll(uint packetType, Func<BinaryWriter, bool> writePayload)
    {
        SendToAllAsync(packetType, writePayload).GetAwaiter().GetResult();
    }

    public static void Dispose()
    {
        CancellationSource?.Cancel();

        lock (Clients)
        {
            foreach (ClientConnection client in Clients)
            {
                client.TcpClient.Close();
            }

            Clients.Clear();
        }

        lock (ClientsByName)
        {
            ClientsByName.Clear();
        }

        Listener?.Stop();
        Listener = null;

        CancellationSource?.Dispose();
        CancellationSource = null;
    }
}
