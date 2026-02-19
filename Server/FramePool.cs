using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace IRR.Server
{
    public class FramePool : IDisposable
    {
        public int Capacity => allocations.Count;

        private readonly ConcurrentBag<IntPtr> buffers = new ConcurrentBag<IntPtr>(); // available
        private readonly List<IntPtr> allocations = new List<IntPtr>(); // all
        private readonly int bufferSize;
        private int disposed;

        public FramePool(int bufferSize, int capacity)
        {
            this.bufferSize = bufferSize;
            for (int i = 0; i < capacity; i++)
            {
                IntPtr buffer;
                unsafe
                {
                   buffer = (nint)NativeMemory.AlignedAlloc((nuint)bufferSize, 64); // Aligned for SIMD
                }
                allocations.Add(buffer);
                buffers.Add(buffer);
            }
        }

        public Frame Rent(IntPtr data, int size, int width, int height, int stride)
        {
            if(!buffers.TryTake(out IntPtr buffer))
                throw new InvalidOperationException("Pool is empty.");

            unsafe
            {
                Buffer.MemoryCopy((void*)data, (void*)buffer, bufferSize, size);
            }

            return new Frame(buffer, size, width, height, stride, this);
        }

        public void Return(Frame frame)
        {
            if (disposed == 1)
                return;

            buffers.Add(frame.Data);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                foreach (IntPtr buffer in allocations)
                    unsafe { NativeMemory.AlignedFree((void*)buffer); };
        }
    }
}
