using System;
using System.Collections.Concurrent;
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

        public CursorInfo CursorInfo
        {
            get { lock (cursorLock) return cursorInfo; }
            set { lock (cursorLock) cursorInfo = value; }
        }

        private readonly object cursorLock = new object();
        private CursorInfo cursorInfo = new CursorInfo();

        private ConcurrentQueue<ushort> pressedKeys = new ConcurrentQueue<ushort>();
        private ConcurrentQueue<ushort> releasedKeys = new ConcurrentQueue<ushort>();

        public void AddKeyPressed(ushort key) => pressedKeys.Enqueue(key);
        public void AddKeyReleased(ushort key) => releasedKeys.Enqueue(key);

        public ushort[] ConsumePressed() => Consume(pressedKeys);
        public ushort[] ConsumeReleased() => Consume(releasedKeys);

        private ushort[] Consume(ConcurrentQueue<ushort> queue)
        {
            var result = new List<ushort>();
            while (result.Count < 6 && queue.TryDequeue(out ushort key))
                result.Add(key);
            return result.ToArray();
        }
    }
}
