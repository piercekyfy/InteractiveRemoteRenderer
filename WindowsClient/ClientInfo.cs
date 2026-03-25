using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsClient
{
    public class ClientInfo
    {
        public double WindowSizeX { get; set; }
        public double WindowSizeY { get; set; }
        public double LocalCursorX { get; set; }
        public double LocalCursorY { get; set; }

        public ClientInfo(double windowSizeX, double windowSizeY, double localCursorX, double localCursorY)
        {
            this.WindowSizeX = windowSizeX;
            this.WindowSizeY = windowSizeY;
            this.LocalCursorX = localCursorX;
            this.LocalCursorY = localCursorY;
        }

        public double ProjectedCursorX(double targetWindowSizeX)
        {
            return this.LocalCursorX * (targetWindowSizeX / WindowSizeX);
        }

        public double ProjectedCursorY(double targetWindowSizeY)
        {
            return this.LocalCursorY * (targetWindowSizeY / WindowSizeY);
        }
    }
}
