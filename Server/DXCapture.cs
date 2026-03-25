using Common;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace IRR.Server
{

    public struct DisplayInfo
    {
        public int VirtualTop;
        public int VirtualLeft;
        public int Width;
        public int Height;
    }

    public class DXCapture : ICapture
    {
        public DisplayInfo DisplayInfo { get; private set; }
        public int Capacity => framePool == null ? 0 : framePool.Capacity;

        private IDXGIAdapter1? adapter;
        private ID3D11Device? device;
        private IDXGIOutput? output;
        private IDXGIOutput1? output1;
        private IDXGIOutputDuplication? duplication;

        private ID3D11Texture2D? stagingTexture;

        private FramePool? framePool;

        public DXCapture(int capacity, int gpu = 0, int display = 0)
        {
            InitializeDXResources(capacity, gpu, display);
        }

        public Frame? CaptureFrame(int timeout)
        {
            if (device == null || duplication == null || stagingTexture == null || framePool == null)
                throw new InvalidOperationException("Capture is not initialized.");

            Result res = duplication.AcquireNextFrame((uint)timeout, out OutduplFrameInfo frameInfo, out IDXGIResource? resource);
            if (res.Failure || resource == null) return null;

            // TODO: frameInfo has cursor info

            try // Move capture to staging
            {
                using ID3D11Texture2D texture = resource.QueryInterface<ID3D11Texture2D>();
                device.ImmediateContext.CopyResource(stagingTexture, texture);
            }
            finally
            {
                resource.Dispose();
                duplication.ReleaseFrame();
            }

            MappedSubresource mapped = device.ImmediateContext.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var description = stagingTexture.Description;
                return framePool.Rent(
                    mapped.DataPointer,
                    (int)mapped.DepthPitch,
                    (int)description.Width,
                    (int)description.Height,
                    (int)mapped.RowPitch
                );
            } finally
            {
                device.ImmediateContext.Unmap(stagingTexture, 0);
            }
        }

        public void Dispose()
        {
            framePool?.Dispose();
            DisposeDXResources();
        }

        private void InitializeDXResources(int capacity, int gpu, int display)
        {
            using IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            factory.EnumAdapters1((uint)gpu, out adapter).CheckError();
            D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.None, new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 }, out device).CheckError();

            if (device == null)
                throw new Exception("Failed to create D3 device.");

            adapter.EnumOutputs((uint)display, out output).CheckError();
            output1 = output.QueryInterface<IDXGIOutput1>();

            duplication = output1.DuplicateOutput(device);

            RectI bounds = output1.Description.DesktopCoordinates;

            DisplayInfo = new DisplayInfo()
            {
                VirtualLeft = bounds.Left,
                VirtualTop = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
            };

            stagingTexture = device.CreateTexture2D(new()
            {
                CPUAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm, // Blue, Green, Red, Alpha, 4 bytes per pixel.
                Width = (uint)bounds.Width,
                Height = (uint)bounds.Height,
                MiscFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging,
            });

            framePool = new FramePool(bounds.Width * bounds.Height * 4, capacity);
        }

        private void DisposeDXResources()
        {
            adapter?.Dispose();
            adapter = null;
            device?.ImmediateContext?.Dispose();
            device?.Dispose();
            device = null;
            output?.Dispose();
            output = null;
            output1?.Dispose();
            output1 = null;
            duplication?.Dispose();
            duplication = null;
            stagingTexture?.Dispose();
            stagingTexture = null;
        }
    }
}
