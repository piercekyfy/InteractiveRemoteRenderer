using FFmpeg.AutoGen;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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

        private WriteableBitmap bitmap;

        ControlClient controlClient;

        public MainWindow()
        {
            InitializeComponent();

            bitmap = new WriteableBitmap(1920, 1080, 96, 96, PixelFormats.Bgr32, null);
            VideoIn.Source = bitmap;

            txtHost.Text = "127.0.0.1";
            txtPort.Text = "5830";

            controlClient = new ControlClient();

            CompositionTarget.Rendering += (s, e) =>
            {
                System.Windows.Point localMousePos = Mouse.GetPosition(VideoIn);
                controlClient.Update(new ClientInfo(VideoIn.ActualWidth, VideoIn.ActualHeight, localMousePos.X, localMousePos.Y));
            };
        }

        private async Task RenderLoop(TcpClient client, CancellationToken ct = default)
        {
            var stream = client.GetStream();

            using var decoder = new FrameDecoder(1920, 1080);
            var lenBuf = new byte[4];

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await stream.ReadExactlyAsync(lenBuf, ct);
                    int size = BitConverter.ToInt32(lenBuf);
                    var buf = new byte[size];
                    
                    await stream.ReadExactlyAsync(buf, ct);

                    decoder.In(buf, size);

                    if (decoder.Reader.TryRead(out Frame? frame))
                    {
                        Render(frame);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }
            }
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
            if (controlClient.Connected)
                return;
            
            IPAddress host = IPAddress.Parse(txtHost.Text);
            int port = int.Parse(txtPort.Text);

            _ = Task.Run(async () =>
            {
                await controlClient.Connect(new IPEndPoint(host, port));
                Dispatcher.Invoke(() => statusSquare.Fill = new SolidColorBrush(Colors.Yellow));
                TcpClient renderClient = await controlClient.GetVideoClient();
                Dispatcher.Invoke(() => statusSquare.Fill = new SolidColorBrush(Colors.Green));

                renderCts = new CancellationTokenSource();
                _ = Task.Run(() => RenderLoop(renderClient, renderCts.Token));
            });
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if(controlClient.Connected)
            {
                controlClient.Stop();
                statusSquare.Fill = new SolidColorBrush(Colors.Red);
                renderCts?.Cancel();
            }
        }
    }
}