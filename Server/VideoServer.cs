using IRR.Server;
using SharpGen.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class VideoServer
    {
        private readonly int sourceWidth;
        private readonly int sourceHeight;
        private readonly int displayWidth;
        private readonly int displayHeight;
        private readonly int gpu;
        private readonly int display;
        private readonly int fps;
        private TcpListener listener;
        private CancellationTokenSource cts;

        private Task handleTask;

        public VideoServer(int port, int sourceWidth, int sourceHeight, int displayWidth, int displayHeight, int gpu, int display, int fps, CancellationToken ct = default)
        {
            this.sourceWidth = sourceWidth;
            this.sourceHeight = sourceHeight;
            this.displayWidth = displayWidth;
            this.displayHeight = displayHeight;
            this.gpu = gpu;
            this.display = display;
            this.fps = fps;

            listener = new TcpListener(IPAddress.Any, port);

            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            listener.Start();

            handleTask = Task.Run(() => HandleSingle(cts.Token));
        }

        private async Task HandleSingle(CancellationToken ct)
        {
            TcpClient? client = null;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Log("Accepting...");
                    client = await listener.AcceptTcpClientAsync();
                    client.SendTimeout = 100;
                    var stream = client.GetStream();
                    Log("Accepted!");

                    await using var captureChannel = new CaptureChannel(new DXCapture(20, gpu, display), fps);
                    var reader = captureChannel.Start(ct);

                    using var fe = new FrameEncoder(stream, sourceWidth, sourceHeight, displayWidth, displayHeight, fps);
                    await using var encodeChannel = new EncodeChannel(reader, fe);
                    encodeChannel.Start(ct);

                    await encodeChannel.Join();
                }
                catch (SharpGenException)
                {
                    Log("Failed to initialize DXGI capture. Do the specified GPU and Display indices exist?");
                    break;
                }

                catch (Exception ex)
                {
                    Log($"Connected ended! Restarting... Exception: {ex.Message}");
                }
            }
        }

        private void Log(string message)
        {
            Console.WriteLine($"[VideoServer]: {message}");
        }
    }
}
