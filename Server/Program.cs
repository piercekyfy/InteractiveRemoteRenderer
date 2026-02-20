using FFmpeg.AutoGen;
using Server;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Net.Sockets;


namespace IRR.Server
{
    public class Program
    {

        public static async Task Main(string[] args)
        {
            ffmpeg.RootPath = "C:\\Users\\pierc\\Downloads\\ffmpeg\\bin";

            //var output = File.OpenWrite("test.h264");
            //await using var capture = new CaptureChannel(new DXCapture(20), 60);

            //CancellationTokenSource cts = new CancellationTokenSource();

            //var reader = capture.Start(cts.Token);
            //await using var encoder = new EncodeChannel(reader, new FrameEncoder(output, 1920, 1080, 60));
            //encoder.Start(cts.Token);

            //await Task.Delay(10000);
            //cts.Cancel();

            //await capture.StopAsync();
            //await encoder.StopAsync();
            //output.Dispose();

            //var listener = new HttpListener();
            //listener.Prefixes.Add("http://localhost:5000/");

            //using var rtc = new WebRTCHost(listener);
            //using var rtcStream = await rtc.Start(fps, cts.Token);


            using var cts = new CancellationTokenSource();
            int fps = 60;

            var tcp = new TcpListener(IPAddress.Any, 5000);
            tcp.Start();
            var client = await tcp.AcceptTcpClientAsync();
            var stream = client.GetStream();

            await using var capture = new CaptureChannel(new DXCapture(20), fps);
            var reader = capture.Start(cts.Token);

            var fe = new FrameEncoder(stream, 1920, 1080, fps);
            await using var encoder = new EncodeChannel(reader, fe);
            encoder.Start(cts.Token);

            await Task.Delay(50000);

            cts.Cancel();
        }
    }
}

