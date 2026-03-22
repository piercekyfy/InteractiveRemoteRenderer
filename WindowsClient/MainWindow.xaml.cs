using FFmpeg.AutoGen;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WindowsClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? renderCts = null;
        Task? renderTask;

        private WriteableBitmap bitmap;

        public MainWindow()
        {
            ffmpeg.RootPath = "C:\\Users\\pierc\\Downloads\\ffmpeg\\bin";

            InitializeComponent();

            bitmap = new WriteableBitmap(1920, 1080, 96, 96, PixelFormats.Bgr32, null);
            VideoIn.Source = bitmap;
        }

        private async Task RenderLoop(CancellationToken ct = default)
        {
            TcpClient tcp = new TcpClient();
            await tcp.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5000));

            var stream = tcp.GetStream();

            using var decoder = new FrameDecoder(1920, 1080);
            var lenBuf = new byte[4];

            Dispatcher.Invoke(() => statusSquare.Fill = new SolidColorBrush(Colors.Green));

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await stream.ReadExactlyAsync(lenBuf, ct);
                    int size = BitConverter.ToInt32(lenBuf);
                    Console.WriteLine($"Reading {size}...");
                    var buf = new byte[size];
                    
                    await stream.ReadExactlyAsync(buf, ct);
                    Console.WriteLine("Got Frame");

                    decoder.In(buf, size);

                    if (decoder.Reader.TryRead(out Frame? frame))
                    {
                        Console.WriteLine("Decoded Frame");

                        Render(frame);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Finished.");
                    break;
                }
                catch (EndOfStreamException)
                {
                    Console.WriteLine("Finished.");
                    break;
                }
                catch (IOException)
                {
                    Console.WriteLine("Disconnected unexpectedly");
                    break;
                }
            }

            Dispatcher.Invoke(() => statusSquare.Fill = new SolidColorBrush(Colors.Red));
            renderTask = null;
        }

        private void Render(WindowsClient.Frame frame)
        {
            Dispatcher.Invoke(() =>
            {
                bitmap.Lock();
                
                unsafe
                {
                    Buffer.MemoryCopy((void*)frame.Data, (void*)bitmap.BackBuffer, frame.Size, frame.Size);

                    bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
                    bitmap.Unlock();
                }
            });
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (renderTask != null)
                return;

            renderCts = new CancellationTokenSource();
            renderTask = Task.Run(() => RenderLoop(renderCts.Token));
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if(renderTask != null)
            {
                statusSquare.Fill = new SolidColorBrush(Colors.Yellow);
                renderCts?.Cancel();
            }
        }
    }
}