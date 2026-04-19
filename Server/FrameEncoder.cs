using FFmpeg.AutoGen;
using System.Diagnostics;
using System.Text;

namespace IRR.Server
{
    public unsafe class FrameEncoder : IEncoder
    {
        private static readonly string NVIDIA_CODEC = "h264_nvenc";
        private static readonly string SOFTWARE_CODEC = "libx264";

        private bool usingNvidia => codecName == NVIDIA_CODEC;
        private string? codecName = null;

        private readonly int sourceWidth;
        private readonly int sourceHeight;
        private readonly int targetWidth;
        private readonly int targetHeight;
        private readonly int fps;
        private readonly Stream output;

        private AVCodecContext* av; // encoder state
        private AVFrame* frame; // frame buffer
        private AVPacket* packet; // output buffer
        private SwsContext* sws;

        public Stopwatch EncodeTime = new Stopwatch();
        public Stopwatch DrainTime = new Stopwatch();

        public FrameEncoder(Stream output, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, int fps)
        {
            this.output = output;
            this.sourceWidth = sourceWidth;
            this.sourceHeight = sourceHeight;
            this.targetWidth = targetWidth;
            this.targetHeight = targetHeight;
            this.fps = fps;

            InitializeAVResources();
        }

        public void Encode(Frame frame, long index)
        {
            EncodeTime.Start();
            ffmpeg.av_frame_make_writable(this.frame);

            byte* data = (byte*)frame.Data;
            int stride = frame.Stride;

            ffmpeg.sws_scale(sws, [data], [stride], 0, sourceHeight, this.frame->data, this.frame->linesize); // convert from bgra to yuv420p
            this.frame->pts = index; // presentation timestamp (time_Base = 1/fps, so 0 is frame 0, etc.)

            ffmpeg.avcodec_send_frame(av, this.frame);
            EncodeTime.Stop();
            DrainTime.Start();
            DrainPacket();
            DrainTime.Stop();
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

                // TEMPORARY PREFIX
                output.Write(BitConverter.GetBytes(packet->size));

                output.Write(new Span<byte>(packet->data, packet->size));
                ffmpeg.av_packet_unref(packet);
            }
        }

        private void InitializeAVResources()
        {
            AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name(NVIDIA_CODEC);
            AVCodecContext* testCodec = null;

            if(codec != null)
            {
                testCodec = ffmpeg.avcodec_alloc_context3(codec);
                testCodec->width = targetWidth;
                testCodec->height = targetHeight;
                testCodec->time_base = new AVRational { num = 1, den = fps };
                testCodec->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

                if (ffmpeg.avcodec_open2(testCodec, codec, null) < 0)
                {
                    ffmpeg.avcodec_free_context(&testCodec);
                    codec = null;
                }
            }

            if (codec == null)
            {
                Console.WriteLine($"Codec {NVIDIA_CODEC} not found, trying {SOFTWARE_CODEC} instead...");
                codec = ffmpeg.avcodec_find_encoder_by_name(SOFTWARE_CODEC);
                if (codec == null)
                    throw new Exception($"Failed to find codec ${SOFTWARE_CODEC}");

                codecName = SOFTWARE_CODEC;
                av = ffmpeg.avcodec_alloc_context3(codec);
            } else
            {
                codecName = NVIDIA_CODEC;
                av = testCodec;
            }
            
            av->width = targetWidth;
            av->height = targetHeight;
            av->time_base = new AVRational { num = 1, den = fps };
            av->framerate = new AVRational { num = fps, den = 1 };
            av->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            av->bit_rate = 8000000;            
            av->rc_max_rate = 8000000;
            av->rc_buffer_size = 8000000 * 2; // 2 seconds buffer
            av->gop_size = fps / 3; // How often a key-frame is produced
            av->max_b_frames = 0;
            av->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;

            ffmpeg.av_opt_set(av->priv_data, "profile", "high", 0); // main, high, baseline(worst)
            ffmpeg.av_opt_set(av->priv_data, "repeat-headers", "1", 0);

            if (usingNvidia)
            {
                ffmpeg.av_opt_set(av->priv_data, "rc", "cbr", 0); // constant bandwidth mode (keep bandwidth predictable)
 
                //ffmpeg.av_opt_set(av->priv_data, "preset", "p4", 0); // nvenc middle ground speed/quality
                ffmpeg.av_opt_set(av->priv_data, "preset", "p1", 0); // fastest, low quality
                ffmpeg.av_opt_set(av->priv_data, "rc-lookahead", "0", 0); // disable lookahead (also disabled by tune ll)
                ffmpeg.av_opt_set(av->priv_data, "tune", "ll", 0); // tune for low latency 
                ffmpeg.av_opt_set(av->priv_data, "zerolatency", "1", 0); // no frame buffering
            }
            else
            {
                ffmpeg.av_opt_set(av->priv_data, "preset", "ultrafast", 0);
                ffmpeg.av_opt_set(av->priv_data, "tune", "zerolatency", 0);
                av->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
            }

            int ret = ffmpeg.avcodec_open2(av, codec, null);
            ThrowFFmpegError(ret);

            frame = ffmpeg.av_frame_alloc();
            frame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
            frame->width = targetWidth;
            frame->height = targetHeight;
            ret = ffmpeg.av_frame_get_buffer(frame, 0);
            ThrowFFmpegError(ret);

            packet = ffmpeg.av_packet_alloc();

            sws = ffmpeg.sws_getContext(sourceWidth, sourceHeight, AVPixelFormat.AV_PIX_FMT_BGRA, targetWidth, targetHeight, AVPixelFormat.AV_PIX_FMT_YUV420P, 0, null, null, null);

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
