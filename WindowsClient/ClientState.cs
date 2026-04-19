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

        public ClientState(float windowSizeX, float windowSizeY)
        {
            WindowSizeX = windowSizeX;
            WindowSizeY = windowSizeY;            
        }
    }
}
