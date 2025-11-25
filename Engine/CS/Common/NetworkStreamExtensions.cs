using System.Net.Sockets;

public static class NetworkStreamExtensions
{
    public static async Task ReadExactlyAsync(this NetworkStream stream, byte[] buffer, int length)
    {
        int offset = 0;
        int remaining = length;

        while (remaining > 0)
        {
            int read = await stream.ReadAsync(buffer, offset, remaining);
            if (read <= 0)
                throw new IOException("Remote closed connection.");

            offset += read;
            remaining -= read;
        }
    }
}
