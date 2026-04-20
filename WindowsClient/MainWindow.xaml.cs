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
using System.Windows.Media.Animation;
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
        private bool panelMinimized = false;
        private bool logosRemoved = false;

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

                controlClient.UpdateScreenDimensions((float)VideoIn.ActualWidth, (float)VideoIn.ActualHeight);
                controlClient.UpdateCursorPosition((float)localMousePos.X, (float)localMousePos.Y);
            };

            VideoIn.MouseUp += (s, e) => controlClient.UpdateMouse(e.ChangedButton, true);
            VideoIn.MouseDown += (s, e) => controlClient.UpdateMouse(e.ChangedButton, false);
            VideoIn.PreviewKeyUp += (s, e) =>
            {
                if (!VideoIn.IsKeyboardFocusWithin)
                    return;

                e.Handled = true;
                controlClient.UpdateKey((ushort)KeyInterop.VirtualKeyFromKey(e.Key), true);
            };
            VideoIn.PreviewKeyDown += (s, e) =>
            {
                if (!VideoIn.IsKeyboardFocusWithin)
                    return;

                e.Handled = true;
                controlClient.UpdateKey((ushort)KeyInterop.VirtualKeyFromKey(e.Key), false);
            };

            VideoIn.MouseDown += (s, e) => VideoIn.Focus();
            Loaded += (s, e) => VideoIn.Focus();
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

                renderCts = new CancellationTokenSource();
                _ = Task.Run(() => RenderLoop(renderClient, renderCts.Token));

                Dispatcher.Invoke(() => {
                    statusSquare.Fill = new SolidColorBrush(Colors.Green);
                    RemoveLogos();

                    var initInfo = controlClient.GetInitPacket();

                    if (initInfo != null && !(Width == initInfo.Value.DisplayWidth && Height == initInfo.Value.DisplayHeight))
                    {
                        var result = MessageBox.Show($"Remote source is {initInfo.Value.DisplayWidth}x{initInfo.Value.DisplayHeight}.\n\nResize window to match?",
                            "Resize Window", MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            Width = initInfo.Value.DisplayWidth;
                            Height = initInfo.Value.DisplayHeight;
                        }
                    }

                });
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

        private void btnMinimizePanel_Click(object sender, RoutedEventArgs e)
        {
            panelMinimized = !panelMinimized;
            panelContent.Visibility = panelMinimized ? Visibility.Collapsed : Visibility.Visible;
            btnMinimizePanel.Content = panelMinimized ? "▶" : "◀";
        }

        private void RemoveLogos()
        {
            if (logosRemoved) return;
            logosRemoved = true;

            var fade = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
            watermark.BeginAnimation(OpacityProperty, fade);
            hintBoxConnect.BeginAnimation(OpacityProperty, fade);
            hintBoxStatus.BeginAnimation(OpacityProperty, fade);
        }
    }
}