using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ServerInfo
    {
        public int DisplayIndex { get; set; }
        public required int DisplayWidth { get; set; }
        public required int DisplayHeight { get; set; }
        public required int VirtualDisplayOffsetX { get; set; }
        public required int VirtualDisplayOffsetY { get; set; }
        public required int VideoPort { get; set; }
        public required int ControlPort { get; set; }
    }
}
