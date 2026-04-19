using FFmpeg.AutoGen;
using Server;
using SharpGen.Runtime;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Vortice.DXGI;

namespace IRR.Server
{
    public class Program
    {

        public static void EnumerateDisplays()
        {
            using IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            uint gpuIndex = 0;
            while (factory.EnumAdapters1(gpuIndex, out IDXGIAdapter1? adapter).Success)
            {
                using(adapter)
                {
                    Console.WriteLine($"> GPU ({gpuIndex}): {adapter.Description1.Description}");

                    uint displayIndex = 0;
                    while(adapter.EnumOutputs(displayIndex, out IDXGIOutput? output).Success)
                    {
                        using (output)
                        {
                            Console.WriteLine($"\tDisplay ({displayIndex}): {output.Description.DeviceName}");
                        }
                        displayIndex++;
                    }
                }
                gpuIndex++;
            }
        }

        // arg0: control-port (integer), arg1: video-port (integer)
        public static async Task Main(string[] args)
        {
            // env

            string? ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG_ROOT_PATH");

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new Exception("FFMPEG not found. Is the environment variable FFMPEG_ROOT_PATH specified?");
            }

            ffmpeg.RootPath = ffmpegPath;

            // args

            int controlPort = int.Parse(args[0]);
            int videoPort = int.Parse(args[1]);

            Console.WriteLine(controlPort + " , " + videoPort);

            // display

            int fps = 30;

            EnumerateDisplays();
            Console.WriteLine();

            bool initializedDisplay = false;
            int gpuIndex = -1;
            int displayIndex = -1;
            DisplayInfo displayInfo = default;
            while (!initializedDisplay)
            {
                Console.Write("Use GPU (index): ");
                gpuIndex = int.Parse(Console.ReadLine() ?? "0");
                Console.Write("\nUse Display (index): ");
                displayIndex = int.Parse(Console.ReadLine() ?? "0");
                Console.WriteLine("...");

                // Test Display and extract parameters
                try
                {
                    using var captureTest = new DXCapture(1, gpuIndex, displayIndex);
                    displayInfo = captureTest.DisplayInfo;
                    initializedDisplay = true;
                }
                catch (SharpGenException ex)
                {
                    Console.WriteLine($"Failed to initialize DXGI capture. Do the specified GPU and Display indices exist?\nMessage: {ex.Message}");
                }
            }

            // control

            using var cts = new CancellationTokenSource();

            ServerInfo configuration = new ServerInfo()
            {
                DisplayIndex = displayIndex,
                DisplayWidth = displayInfo.Width,
                DisplayHeight = displayInfo.Height,
                VirtualDisplayOffsetX = displayInfo.VirtualLeft,
                VirtualDisplayOffsetY = displayInfo.VirtualTop,
                ControlPort = controlPort,
                VideoPort = videoPort
            };


            ControlServer controlServer = new ControlServer(configuration, cts.Token);
            VideoServer videoServer = new VideoServer(
                videoPort,
                configuration.DisplayWidth, 
                configuration.DisplayHeight,  
                1920, 
                1080, 
                gpuIndex, displayIndex, fps, cts.Token);

            while(true) { await Task.Delay(10); }
        }
    }
}

