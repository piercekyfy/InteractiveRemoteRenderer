namespace IRR.Server
{
    public class Frame : IDisposable
    {
        public IntPtr Data { get; private set; }
        public int Size { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stride { get; private set; }
        // TODO: Timestamp

        private readonly FramePool pool;
        private int disposed;

        public Frame(nint data, int size, int width, int height, int stride, FramePool pool)
        {
            Data = data;
            Size = size;
            Width = width;
            Height = height;
            Stride = stride;
            this.pool = pool;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                pool.Return(this);
        }
    }
}
