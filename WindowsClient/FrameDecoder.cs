using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WindowsClient
{
    public unsafe class FrameDecoder : IDisposable
    {
        public ChannelReader<Frame> Reader => frameChannel.Reader;

        private readonly int width;
        private readonly int height;

        private Channel<Frame> frameChannel;

        private AVCodecContext* av; // decoder context
        private AVPacket* packet;
        private AVFrame* frame;
        private SwsContext* sws;

        public FrameDecoder(int width, int height)
        {
            this.width = width;
            this.height = height;

            frameChannel = Channel.CreateBounded<Frame>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
            InitializeAVResources();
        }

        public void In(byte[] data, int length)
        {
            fixed (byte* ptr = data)
            {
                packet->data = ptr;
                packet->size = length;
                ffmpeg.avcodec_send_packet(av, packet);
                Decode();
            }
            packet->data = null;
            packet->size = 0;
        }

        private void Decode()
        {
            while (true)
            {
                int ret = ffmpeg.avcodec_receive_frame(av, frame);
                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                    break;
                ThrowFFmpegError(ret);


                int size = height * width * 4;
                IntPtr ptr = Marshal.AllocHGlobal(size);

                ffmpeg.sws_scale(sws, frame->data, frame->linesize, 0, frame->height, [(byte*)ptr], [width * 4]);

                var f = new Frame(ptr, size);

                frameChannel.Writer.WriteAsync(f).AsTask().GetAwaiter().GetResult();
            }
        }

        public void Dispose()
        {
            if (av != null)
                fixed (AVCodecContext** ptr = &av) { ffmpeg.avcodec_free_context(ptr); }

            if (frame != null)
                fixed (AVFrame** ptr = &frame) { ffmpeg.av_frame_free(ptr); }


            if (packet != null)
                fixed (AVPacket** ptr = &packet) { ffmpeg.av_packet_free(ptr); }

            while(frameChannel.Reader.TryRead(out Frame? f))
            {
                f?.Dispose();
            }
        }

        private void InitializeAVResources()
        {
            AVCodec* codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
            av = ffmpeg.avcodec_alloc_context3(codec);
            av->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
            av->flags |= ffmpeg.PARSER_FLAG_COMPLETE_FRAMES;

            ffmpeg.avcodec_open2(av, codec, null);

            packet = ffmpeg.av_packet_alloc();
            frame = ffmpeg.av_frame_alloc();

            sws = ffmpeg.sws_getContext(width, height, AVPixelFormat.AV_PIX_FMT_YUV420P, width, height, AVPixelFormat.AV_PIX_FMT_BGRA, 0, null, null, null);
        }

        private static void ThrowFFmpegError(int error)
        {
            if (error < 0)
            {
                byte[] buffer = new byte[1024];
                fixed (byte* b = buffer)
                    ffmpeg.av_strerror(error, b, (ulong)buffer.Length);
                string desc = Encoding.UTF8.GetString(buffer);
                throw new Exception($"FFmpeg Error: {desc} (code {error})");
            }
        }
    }
}
