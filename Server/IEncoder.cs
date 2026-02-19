namespace IRR.Server
{
    public interface IEncoder : IDisposable
    {
        public void Encode(Frame frame, long index);
        public void Finish();
    }
}
