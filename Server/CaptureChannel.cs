using IRR.Server;
using System.Threading.Channels;

namespace Server
{
    public class CaptureChannel : IAsyncDisposable
    {
        private Channel<Frame>? frameChannel;

        private readonly ICapture capture;
        private double targetFramerate;
        private int timeout;
        private int maxTimeouts;

        private CancellationTokenSource? cts;
        private Task? captureTask;

        public CaptureChannel(ICapture capture, double targetFramerate = 60.0, int timeout = 1000, int maxTimeouts = 3)
        {
            this.capture = capture;
            this.targetFramerate = targetFramerate;
            this.timeout = timeout;
            this.maxTimeouts = maxTimeouts;

            if (capture.Capacity == 0)
                throw new InvalidOperationException("Supplied ICapture capacity is zero. Is it initialized?");
        }

        public ChannelReader<Frame> Start(CancellationToken ct = default)
        {
            if (cts != null)
                cts?.Dispose();

            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if(frameChannel != null)
                DisposeChannel();
            frameChannel = Channel.CreateBounded<Frame>(new BoundedChannelOptions(capture.Capacity - 2) { }); // -2 means the frame pool will never be empty

            captureTask = Task.Run(() => CaptureLoop(cts.Token));
            return frameChannel.Reader;
        }

        public async Task StopAsync()
        {
            cts?.Cancel();
            if(captureTask != null)
                await captureTask;
            DisposeChannel();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            capture?.Dispose();
            DisposeChannel();
        }

        protected async Task CaptureLoop(CancellationToken ct)
        {
            if (frameChannel == null)
                throw new InvalidOperationException("CaptureChannel be be started before CaptureLoop can execute.");

            double frames = 0;
            double lastTick = Environment.TickCount64;

            int timeouts = maxTimeouts;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        long now = Environment.TickCount64;
                        frames += now - lastTick;
                        lastTick = now;

                        if (frames < (1000.0 / targetFramerate))
                        {
                            await Task.Delay(1, ct);
                            continue;
                        }

                        frames = 0;

                        var frame = capture.CaptureFrame(timeout);

                        if (frame == null)
                            continue;

                        if (!frameChannel.Writer.TryWrite(frame))
                        {
                            var old = await frameChannel.Reader.ReadAsync();
                            old?.Dispose();

                            await frameChannel.Writer.WriteAsync(frame);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (TimeoutException)
                    {
                        if (--timeouts <= 0)
                            throw;
                    }
                }
            } finally
            {
                frameChannel.Writer.Complete();
            }
            
        }

        private void DisposeChannel()
        {
            if (frameChannel == null)
                return;

            while (frameChannel.Reader.TryRead(out Frame? f))
            {
                f?.Dispose();
            }

            frameChannel = null;
        }
    }
}
