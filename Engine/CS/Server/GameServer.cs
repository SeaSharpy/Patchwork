using System.Net;
using System.Net.Sockets;
namespace Patchwork;
public class GameServer
{
    public event Action<string, uint, BinaryReader>? PacketReceived;

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

    private TcpListener Listener;
    private readonly List<ClientConnection> Clients = new();
    private readonly Dictionary<string, ClientConnection> ClientsByName = new();

    public async Task StartAsync(int port)
    {
        Listener = new TcpListener(IPAddress.Any, port);
        Listener.Start();

        while (true)
        {
            TcpClient tcpClient = await Listener.AcceptTcpClientAsync();
            ClientConnection connection = new ClientConnection(tcpClient);

            lock (Clients)
                Clients.Add(connection);

            _ = HandleClientAsync(connection);
        }
    }

    private async Task HandleClientAsync(ClientConnection connection)
    {
        NetworkStream stream = connection.Stream;
        byte[] headerBuffer = new byte[8]; // 4 bytes type, 4 bytes length

        try
        {
            while (true)
            {
                await stream.ReadExactlyAsync(headerBuffer, 8);
                uint packetType = BitConverter.ToUInt32(headerBuffer, 0);
                int length = BitConverter.ToInt32(headerBuffer, 4);

                if (length < 0)
                    throw new InvalidDataException("Negative packet length.");

                byte[] payload = new byte[length];
                if (length > 0)
                    await stream.ReadExactlyAsync(payload, length);

                // Auth handling
                if (connection.PlayerName == null)
                {
                    if (packetType != 0)
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

                    // No PacketReceived event for auth by default
                    continue;
                }

                // Normal packets
                if (PacketReceived != null)
                {
                    MemoryStream ms = new MemoryStream(payload, writable: false);
                    BinaryReader reader = new BinaryReader(ms);
                    PacketReceived.Invoke(connection.PlayerName, packetType, reader);
                    // reader/ms will be cleaned up by GC; caller must finish inside callback
                }
            }
        }
        catch
        {
            // Disconnect cleanup
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

    public async Task SendAsync(string playerName, uint packetType, Action<BinaryWriter> writePayload)
    {
        ClientConnection connection;
        lock (ClientsByName)
        {
            if (!ClientsByName.TryGetValue(playerName, out connection))
                throw new KeyNotFoundException($"Player '{playerName}' not connected.");
        }

        using MemoryStream ms = new MemoryStream();
        using (BinaryWriter writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writePayload(writer);
        }

        byte[] payload = ms.ToArray();
        await SendRawAsync(connection, packetType, payload);
    }

    private async Task SendRawAsync(ClientConnection connection, uint packetType, byte[] payload)
    {
        if (!connection.TcpClient.Connected)
            return;

        NetworkStream stream = connection.Stream;

        int length = payload.Length;
        byte[] header = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(packetType), 0, header, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(length), 0, header, 4, 4);

        await stream.WriteAsync(header, 0, header.Length);
        if (length > 0)
            await stream.WriteAsync(payload, 0, length);
    }
}
