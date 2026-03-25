using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ControlServerInitPacket
    {
        public int DisplayIndex { get; set; }
        public int DisplayWidth { get; set; }
        public int DisplayHeight { get; set; }
        public int VideoPort { get; set; }
    }
}
