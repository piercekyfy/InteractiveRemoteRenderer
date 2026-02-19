using FFmpeg.AutoGen;
using System.Text;

namespace IRR.Server
{
    public unsafe class FrameEncoder : IEncoder
    {
        private readonly string codecName;

        private readonly int width;
        private readonly int height;
        private readonly int fps;
        private readonly Stream output;

        private AVCodecContext* av; // encoder state
        private AVFrame* frame; // frame buffer
        private AVPacket* packet; // output buffer
        private SwsContext* sws;

        public FrameEncoder(Stream output, int width, int height, int fps, string codecName = "h264_nvenc")
        {
            this.output = output;   
            this.width = width;
            this.height = height;
            this.fps = fps;
            this.codecName = codecName;

            InitializeAVResources();
        }

        public void Encode(Frame frame, long index)
        {
            ffmpeg.av_frame_make_writable(this.frame);

            byte* data = (byte*)frame.Data;
            int stride = frame.Stride;

            ffmpeg.sws_scale(sws, [data], [stride], 0, height, this.frame->data, this.frame->linesize); // convert from bgra to yuv420p
            this.frame->pts = index; // presentation timestamp (time_Base = 1/fps, so 0 is frame 0, etc.)

            ffmpeg.avcodec_send_frame(av, this.frame);
            DrainPacket();
        }

        public void Finish()
        {
            ffmpeg.avcodec_send_frame(av, null); // end-of-stream
            DrainPacket();
        }

        public void Dispose()
        {
            if (av != null)
                fixed (AVCodecContext** ptr = &av) { ffmpeg.avcodec_free_context(ptr); }

            if (frame != null)
                fixed (AVFrame** ptr = &frame) { ffmpeg.av_frame_free(ptr); }

            if (packet != null)
                fixed (AVPacket** ptr = &packet) { ffmpeg.av_packet_free(ptr); }

            if (sws != null)
                ffmpeg.sws_freeContext(sws); 

        }

        private void DrainPacket()
        {
            while (true) // from encoder into packet
            {
                int ret = ffmpeg.avcodec_receive_packet(av, packet);
                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                    break;
                ThrowFFmpegError(ret);

                output.Write(new Span<byte>(packet->data, packet->size));
                ffmpeg.av_packet_unref(packet);
            }
        }

        private void InitializeAVResources()
        {
            AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name(codecName);
            if (codec == null)
                throw new Exception($"Codec {codecName} not found.");

            av = ffmpeg.avcodec_alloc_context3(codec);
            av->width = width;
            av->height = height;
            av->time_base = new AVRational { num = 1, den = fps };
            av->framerate = new AVRational { num = fps, den = 1 };
            av->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            av->bit_rate = 8000000;

            if (codecName == "h264_nvenc")
            {
                ffmpeg.av_opt_set(av->priv_data, "preset", "p4", 0); // nvenc middle ground speed/quality
                ffmpeg.av_opt_set(av->priv_data, "tune", "ll", 0); // tune for low latency
                ffmpeg.av_opt_set(av->priv_data, "zerolatency", "1", 0); // no frame buffering
                av->max_b_frames = 0; // no b-frames
                av->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
            }

            int ret = ffmpeg.avcodec_open2(av, codec, null);
            ThrowFFmpegError(ret);

            frame = ffmpeg.av_frame_alloc();
            frame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
            frame->width = width;
            frame->height = height;
            ret = ffmpeg.av_frame_get_buffer(frame, 0);
            ThrowFFmpegError(ret);

            packet = ffmpeg.av_packet_alloc();

            sws = ffmpeg.sws_getContext(width, height, AVPixelFormat.AV_PIX_FMT_BGRA, width, height, AVPixelFormat.AV_PIX_FMT_YUV420P, 0, null, null, null);

            if (sws == null)
                throw new Exception("Failed to create swscale context.");
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
