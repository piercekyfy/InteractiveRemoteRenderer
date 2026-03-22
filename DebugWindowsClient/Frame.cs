using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WindowsClient
{
    public class Frame : IDisposable
    {
        public IntPtr Data { get; private set; }
        public int Size { get; private set; }

        public Frame(IntPtr data, int size)
        {
            Data = data;
            Size = size;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Data);
        }
    }
}
