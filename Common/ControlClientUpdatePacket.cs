using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct ControlClientUpdatePacket
    {
        public float CursorX { get; set; }
        public float CursorY { get; set; }
        public byte CursorPressedState { get; set; }
        public int PressedCount { get; set; }
        public int ReleasedCount { get; set; }
    }

    public class ControlClientUpdatePackerWrapper
    {
        public ref ControlClientUpdatePacket Packet => ref packet;
        private ControlClientUpdatePacket packet;

        public bool LeftPressed => (packet.CursorPressedState & 0x01) != 0;
        public bool MiddlePressed => (packet.CursorPressedState & 0x02) != 0;
        public bool RightPressed => (packet.CursorPressedState & 0x04) != 0;

        public ControlClientUpdatePackerWrapper(ControlClientUpdatePacket packet)
        {
            this.packet = packet;
        }

        public void SetKeyPressedCounts(int pressed, int released)
        {
            packet.PressedCount = pressed;
            packet.ReleasedCount = released;
        }

        public void SetPressedState(bool leftPressed, bool middlePressed, bool rightPressed)
        {
            packet.CursorPressedState = (byte)(
                (leftPressed ? 1 : 0) |
                (middlePressed ? 1 : 0) << 1 |
                (rightPressed ? 1 : 0) << 2);
        }
    }
}
