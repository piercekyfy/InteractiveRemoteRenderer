using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class SocketUtils
    {
        public static async Task<int> ReadInt32(NetworkStream stream, Memory<byte> buffer, CancellationToken ct = default)
        {
            await stream.ReadExactlyAsync(buffer, ct);
            return BitConverter.ToInt32(buffer.Span);
        }
    }
}
