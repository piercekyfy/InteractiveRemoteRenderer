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
        public fixed ushort PressedKeys[6];
        public fixed ushort ReleasedKeys[6];
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

        public ushort[] PressedKeys { get {
                ushort[] pressed = new ushort[packet.PressedCount];
                for (int i = 0; i < packet.PressedCount; i++)
                    unsafe { pressed[i] = packet.PressedKeys[i]; }
                return pressed;
            } 
        }

        public ushort[] ReleasedKeys
        {
            get
            {
                ushort[] released = new ushort[packet.ReleasedCount];
                for (int i = 0; i < packet.ReleasedCount; i++)
                    unsafe { released[i] = packet.ReleasedKeys[i]; }
                return released;
            }
        }

        public void SetKeys(ushort[] pressed, ushort[] released)
        {
            packet.PressedCount = 0;
            packet.ReleasedCount = 0;

            for (int i = 0; i < 6; i++)
            {
                if (pressed.Length > i)
                {
                    unsafe { packet.PressedKeys[i] = pressed[i]; }
                    packet.PressedCount = packet.PressedCount + 1;
                }
                if (released.Length > i)
                {
                    unsafe { packet.ReleasedKeys[i] = released[i]; }
                    packet.ReleasedCount = packet.ReleasedCount + 1;
                }
            }
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
