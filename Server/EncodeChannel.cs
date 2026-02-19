using System.Threading.Channels;

namespace IRR.Server
{
    public class EncodeChannel : IAsyncDisposable
    {
        private ChannelReader<Frame> frameReader;
        private IEncoder encoder;

        private CancellationTokenSource? cts;
        private Task? encodeTask;

        public EncodeChannel(ChannelReader<Frame> frameReader, IEncoder encoder)
        {
            this.frameReader = frameReader;
            this.encoder = encoder;
        }

        public void Start(CancellationToken ct = default)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            encodeTask = Task.Run(() => EncodeLoop(cts.Token));
        }

        public async Task StopAsync()
        {
            cts?.Cancel();
            if (encodeTask != null)
                await encodeTask;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            encoder?.Dispose();
        }

        protected async Task EncodeLoop(CancellationToken ct)
        {
            long index = 0;
            try
            {
                await foreach (Frame frame in frameReader.ReadAllAsync(ct))
                {
                    using (frame)
                    {
                        encoder.Encode(frame, index++);
                    }
                }
            } catch (OperationCanceledException) {}
            finally
            {
                encoder.Finish();
            }
        }
    }
}
