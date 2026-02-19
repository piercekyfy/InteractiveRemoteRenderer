using FFmpeg.AutoGen;
using Server;

namespace IRR.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            ffmpeg.RootPath = "C:\\Users\\pierc\\Downloads\\ffmpeg\\bin";

            var output = File.OpenWrite("test.h264");
            await using var capture = new CaptureChannel(new DXCapture(20), 60);

            CancellationTokenSource cts = new CancellationTokenSource();

            var reader = capture.Start(cts.Token);
            await using var encoder = new EncodeChannel(reader, new FrameEncoder(output, 1920, 1080, 60));
            encoder.Start(cts.Token);

            await Task.Delay(5000);
            cts.Cancel();

            await capture.StopAsync();
            await encoder.StopAsync();
            output.Dispose();
        }


    }
}

