using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsClient
{
    public class CursorInfo
    {
        public float X { get; set; }
        public float Y { get; set; }
        public bool ButtonLeftPressed { get; set; }
        public bool ButtonMiddlePressed { get; set; }
        public bool ButtonRightPressed { get; set; }

        public CursorInfo ProjectTo(float sourceWidth, float sourceHeight, float targetWidth, float targetHeight)
        {
            return new CursorInfo
            {
                X = X * (targetWidth / sourceWidth),
                Y = Y * (targetHeight / sourceHeight),
                ButtonLeftPressed = ButtonLeftPressed,
                ButtonMiddlePressed = ButtonMiddlePressed,
                ButtonRightPressed = ButtonRightPressed
            };
        }
    }

    public class ClientState
    {
        public float WindowSizeX { get; set; }
        public float WindowSizeY { get; set; }
        public CursorInfo CursorInfo { get; set; } = new CursorInfo();

        private List<ushort> pressedKeys = new List<ushort>();
        private List<ushort> releasedKeys = new List<ushort>();

        public ClientState(float windowSizeX, float windowSizeY)
        {
            WindowSizeX = windowSizeX;
            WindowSizeY = windowSizeY;            
        }

        public void AddKeyPressed(ushort key)
        {
            pressedKeys.Add(key);
        }

        public void AddKeyReleased(ushort key)
        {
            releasedKeys.Add(key);
        }

        public ushort[] ConsumePressed()
        {
            ushort[] pressed = new ushort[Math.Min(6, pressedKeys.Count)];
            for (int i = 0; i < pressed.Length; i++)
            {
                pressed[i] = pressedKeys[i];
            }
            pressedKeys.RemoveRange(0, pressed.Length);
            return pressed;
        }

        public ushort[] ConsumeReleased()
        {
            ushort[] released = new ushort[Math.Min(6, releasedKeys.Count)];
            for (int i = 0; i < released.Length; i++)
            {
                released[i] = releasedKeys[i];
            }
            releasedKeys.RemoveRange(0, released.Length);
            return released;
        }
    }
}
