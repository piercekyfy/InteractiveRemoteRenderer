namespace IRR.Server
{
    public interface ICapture : IDisposable
    {
        public int Capacity { get; }
        public Frame? CaptureFrame(int timeout);
    }
}
