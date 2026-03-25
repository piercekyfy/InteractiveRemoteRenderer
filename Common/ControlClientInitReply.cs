using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ControlClientInitReply
    {
        public byte ACK = 0x06;

        public ControlClientInitReply() {}
    }
}
