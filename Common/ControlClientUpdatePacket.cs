using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ControlClientUpdatePacket
    {
        public double CursorX { get; set; }
        public double CursorY { get; set; }
    }
}
