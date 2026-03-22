using FFmpeg.AutoGen;
using System.Net;
using System.Net.Sockets;

namespace WindowsClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ffmpeg.RootPath = "C:\\Users\\pierc\\Downloads\\ffmpeg\\bin";

            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5000));

            var stream = tcp.GetStream();

            using var decoder = new FrameDecoder(1920, 1080);
            var lenBuf = new byte[4];

            var output = File.OpenWrite("test.h264");

            while (true)
            {
                try
                {
                    await stream.ReadExactlyAsync(lenBuf);
                    int size = BitConverter.ToInt32(lenBuf);
                    Console.WriteLine($"Reading {size}...");
                    var buf = new byte[size];
                    await stream.ReadExactlyAsync(buf);

                    await output.WriteAsync(buf, 0, size);
                    Console.WriteLine("Got frame.");
                } catch (EndOfStreamException) {
                    Console.WriteLine("Finished.");
                    break;
                } catch (IOException)
                {
                    Console.WriteLine("Disconnected unexpectedly");
                    break;
                }
                
                //decoder.In(buf, size);

                //if (decoder.Reader.TryRead(out Frame? frame))
                //{
                //    Console.WriteLine("Got Frame");

                //    using var bmp = new Bitmap(1920, 1080, 1920 * 4, System.Drawing.Imaging.PixelFormat.Format32bppArgb, frame.Data);
                //    bmp.Save("frame.png", System.Drawing.Imaging.ImageFormat.Png);
                //}
            }
        }
    }
}
