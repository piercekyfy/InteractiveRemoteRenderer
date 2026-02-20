using FFmpeg.AutoGen;
using Server;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

            using var cts = new CancellationTokenSource();
            int fps = 60;

            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/");

            using var rtc = new WebRTCHost(listener);
            using var rtcStream = await rtc.Start(fps, cts.Token);

            await using var capture = new CaptureChannel(new DXCapture(20), fps);
            var reader = capture.Start(cts.Token);

            await using var encoder = new EncodeChannel(reader, new FrameEncoder(rtcStream, 1920, 1080, fps));
            encoder.Start(cts.Token);

            await Task.Delay(20000);

            cts.Cancel();
        }
    }
}

