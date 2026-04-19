using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WindowsClient
{
    public class ControlClient
    {
        public bool Connected { get; private set; } = false;

        private ControlServerInitPacket initInfo;
        private TcpClient client;

        private volatile ClientState clientState = new ClientState();

        private CancellationTokenSource cts;

        private Task? handleTask;

        public ControlClient()
        {
            client = new TcpClient();
            cts = new CancellationTokenSource();
        }

        public async Task Connect(IPEndPoint endPoint, CancellationToken ct = default)
        {
            client = new TcpClient();
            await client.ConnectAsync(endPoint, ct);

            NetworkStream stream = client.GetStream();

            int initSize = Marshal.SizeOf<ControlServerInitPacket>();
            int replySize = Marshal.SizeOf<ControlClientInitReply>();
            byte[] buffer = new byte[Math.Max(initSize, replySize)];

            await stream.ReadExactlyAsync(buffer.AsMemory(0, initSize), ct);

            initInfo = MemoryMarshal.Read<ControlServerInitPacket>(buffer.AsSpan(0, initSize));

            ControlClientInitReply reply = new ControlClientInitReply();
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref reply, 1)).CopyTo(buffer.AsSpan(0, replySize));
            await stream.WriteAsync(buffer.AsMemory(0, replySize), ct);

            cts = new CancellationTokenSource();
            Connected = true;
            clientState = new ClientState();

            handleTask = Task.Run(() => Handle(cts.Token));
        }

        public void Update(ClientState info)
        {
            clientState = info;
        }

        public void Stop()
        {
            cts.Cancel();
            Connected = false;
        }

        private async Task Handle(CancellationToken ct)
        {
            int clientUpdateSize = Marshal.SizeOf<ControlClientUpdatePacket>();
            int max = new int[] {
                clientUpdateSize
            }.Max();

            byte[] buffer = new byte[max];
            using var stream = client.GetStream();

            while (!ct.IsCancellationRequested)
            {
                CursorInfo cInfo = clientState.CursorInfo.ProjectTo(clientState.WindowSizeX, clientState.WindowSizeY, initInfo.DisplayWidth, initInfo.DisplayHeight);

                ControlClientUpdatePackerWrapper update = new ControlClientUpdatePackerWrapper(new ControlClientUpdatePacket()
                {
                    CursorX = cInfo.X,
                    CursorY = cInfo.Y
                });

                update.SetPressedState(cInfo.ButtonLeftPressed, cInfo.ButtonMiddlePressed, cInfo.ButtonRightPressed);

                ushort[] pressedKeys = clientState.ConsumePressed();
                ushort[] releasedKeys = clientState.ConsumeReleased();

                update.SetKeyPressedCounts(pressedKeys.Length, releasedKeys.Length);

                byte[] pressedBuffer = new byte[pressedKeys.Length * sizeof(ushort)];
                byte[] releasedBuffer = new byte[releasedKeys.Length * sizeof(ushort)];

                for (int i = 0; i < pressedKeys.Length; i++)
                    BitConverter.GetBytes(pressedKeys[i]).CopyTo(pressedBuffer, i * sizeof(ushort));

                for (int i = 0; i < releasedKeys.Length; i++)
                    BitConverter.GetBytes(releasedKeys[i]).CopyTo(releasedBuffer, i * sizeof(ushort));

                MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref update.Packet, 1)).CopyTo(buffer);

                await stream.WriteAsync(buffer.AsMemory(0, clientUpdateSize), ct);
                await stream.WriteAsync(pressedBuffer);
                await stream.WriteAsync(releasedBuffer);

                await Task.Delay(1);
            }
        }

        public async Task<TcpClient> GetVideoClient(CancellationToken ct = default)
        {
            if (!Connected)
                throw new InvalidOperationException("ControlClient is not connected.");
            using var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct);

            TcpClient tcp = new TcpClient();

            IPAddress address = ((IPEndPoint?)client?.Client?.RemoteEndPoint)?.Address ?? IPAddress.None;
            await tcp.ConnectAsync(new IPEndPoint(address, initInfo.VideoPort));

            return tcp;
        }

        public ControlServerInitPacket? GetInitPacket()
        {
            if (!Connected)
                return null;

            return initInfo;
        }

        public void UpdateScreenDimensions(float width, float height)
        {
            clientState.WindowSizeX = width;
            clientState.WindowSizeY = height;
        }

        public void UpdateCursorPosition(float x, float y)
        {
            clientState.CursorInfo.X = x;
            clientState.CursorInfo.Y = y;
        }

        public void UpdateMouse(MouseButton button, bool up)
        {
            switch(button)
            {
                case MouseButton.Left:
                    clientState.CursorInfo.ButtonLeftPressed = !up; break;
                case MouseButton.Middle:
                    clientState.CursorInfo.ButtonMiddlePressed = !up; break;
                case MouseButton.Right:
                    clientState.CursorInfo.ButtonRightPressed = !up; break;
            }
        }

        public void UpdateKey(ushort key, bool up)
        {
            if (up)
                clientState.AddKeyReleased(key);
            else
                clientState.AddKeyPressed(key);
            
        }
    }
}
