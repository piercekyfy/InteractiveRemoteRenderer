using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ControlServer
    {
        private readonly ServerInfo configuration;
        private TcpListener listener;
        private TcpClient? client;
        private CancellationTokenSource cts;

        private ControlClientUpdatePackerWrapper lastUpdate = new(default);

        public ControlServer(ServerInfo configuration, CancellationToken ct = default)
        {
            this.configuration = configuration;

            listener = new TcpListener(IPAddress.Any, configuration.ControlPort);

            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            listener.Start();

            BeginAccept(cts.Token);
        }

        public void Stop()
        {
            cts.Cancel();
        }

        private async Task HandleClient(CancellationToken ct)
        {
            if (client == null)
                throw new InvalidOperationException();

            int clientUpdateSize = Marshal.SizeOf<ControlClientUpdatePacket>();
            int max = new int[] { 
                clientUpdateSize
            }.Max();

            
            byte[] buffer = new byte[max];
            using var stream = client.GetStream();

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await stream.ReadExactlyAsync(buffer.AsMemory(0, clientUpdateSize), ct);
                    ControlClientUpdatePackerWrapper update = new ControlClientUpdatePackerWrapper(MemoryMarshal.Read<ControlClientUpdatePacket>(buffer.AsSpan(0, clientUpdateSize)));

                    // Update mouse pos
                    if (
                        !(lastUpdate.Packet.CursorX == update.Packet.CursorX && lastUpdate.Packet.CursorY == update.Packet.CursorY)
                        && update.Packet.CursorX > 0 && update.Packet.CursorY > 0)
                    {
                        WindowsAPI.MoveMouse(configuration.VirtualDisplayOffsetX + (int)update.Packet.CursorX, configuration.VirtualDisplayOffsetY + (int)update.Packet.CursorY, configuration.VirtualDisplayOffsetX + configuration.DisplayWidth, configuration.VirtualDisplayOffsetY + configuration.DisplayHeight);
                    }

                    // Update mouse click state
                    if (lastUpdate.LeftPressed != update.LeftPressed)
                        WindowsAPI.UpdateMouseLeft(update.LeftPressed);

                    if (lastUpdate.MiddlePressed != update.MiddlePressed)
                        WindowsAPI.UpdateMouseMiddle(update.MiddlePressed);

                    if (lastUpdate.RightPressed != update.RightPressed)
                        WindowsAPI.UpdateMouseRight(update.RightPressed);

                    foreach (ushort key in update.PressedKeys)
                        WindowsAPI.UpdateKey(key, true);

                    foreach (ushort key in update.ReleasedKeys)
                        WindowsAPI.UpdateKey(key, false);

                    lastUpdate = update;

                } catch (Exception ex)
                {
                    Log("Disconnected. Ex: " + ex.Message);
                    client = null;
                    BeginAccept(ct);
                    return;
                }
            }
        }

        private void BeginAccept(CancellationToken ct)
        {
            _ = Task.Run(() => AcceptOne(ct));
        }

        private async Task AcceptOne(CancellationToken ct)
        {
            while (client == null && !cts.IsCancellationRequested)
            {
                try
                {
                    Log("Accepting...");
                    TcpClient connecting = await listener.AcceptTcpClientAsync();
                    Log("Accepted! Sending Init...");
                    connecting.ReceiveTimeout = 3000;
                    connecting.SendTimeout = 3000;
                    NetworkStream stream = connecting.GetStream();

                    int initSize = Marshal.SizeOf<ControlServerInitPacket>();
                    int replySize = Marshal.SizeOf<ControlClientInitReply>();
                    byte[] buffer = new byte[Math.Max(initSize, replySize)];

                    ControlServerInitPacket initPacket = new ControlServerInitPacket()
                    {
                        DisplayIndex = configuration.DisplayIndex,
                        DisplayWidth = configuration.DisplayWidth,
                        DisplayHeight = configuration.DisplayHeight,
                        VideoPort = configuration.VideoPort
                    };
                    MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref initPacket, 1)).CopyTo(buffer);

                    await stream.WriteAsync(buffer.AsMemory(0, initSize), ct);
                    await stream.ReadExactlyAsync(buffer.AsMemory(0, replySize), ct);

                    // Bytes to struct
                    ControlClientInitReply reply = MemoryMarshal.Read<ControlClientInitReply>(buffer.AsSpan(0, replySize));

                    if(reply.ACK == 0x06)
                    {
                        Log("Initialized client.");
                        client = connecting;
                    }

                } catch (OperationCanceledException)
                {
                    return;
                } catch (Exception)
                {
                    continue;
                }

            }

            _ = Task.Run(() => HandleClient(ct));
        }

        private void Log(string message)
        {
            Console.WriteLine($"[ControlServer]: {message}");
        }
    }
}
