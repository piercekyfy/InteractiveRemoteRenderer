using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IRR.Server
{
    public class RTCR264Stream : Stream
    {
        public override bool CanWrite => true;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        private readonly RTCPeerConnection connection;
        private readonly uint timestampStep;

        private uint timestamp = 0;
        public RTCR264Stream(RTCPeerConnection connection, int clockrate, int fps)
        {
            this.connection = connection;
            this.timestampStep = (uint)(clockrate / fps);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            connection.SendVideo(timestamp, buffer[offset..(offset + count)]);
            timestamp += timestampStep;
        }
    }

    // Incredibly cool, but simply has too much latency for the purpose of this application
    public class WebRTCHost : IDisposable
    {
        public RTCPeerConnectionState State { get; private set; } = RTCPeerConnectionState.closed;
        private static SDPAudioVideoMediaFormat H264Format => new SDPAudioVideoMediaFormat(
            SDPMediaTypesEnum.video,
            96,
            "H264",
            90000,
            0,
            "packetization-mode=1;profile-level-id=42e01f" // fragmented NAL units, baseline profile, level 3.1
            );

        private readonly HttpListener listener;

        private RTCPeerConnection? peerConnection;

        public WebRTCHost(HttpListener listener)
        {
            this.listener = listener;
        }

        public async Task<RTCR264Stream> Start(int framerate, CancellationToken ct = default)
        {
            if (peerConnection != null)
                throw new InvalidOperationException("WebRTCHost is already running.");

            listener.Start();
            var listenerContext = await listener.GetContextAsync();

            try
            {
                var webSocketContext = await listenerContext.AcceptWebSocketAsync(null);

                peerConnection = new RTCPeerConnection();

                var videoTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.video,
                    false,
                    [H264Format],
                    MediaStreamStatusEnum.SendOnly);
                peerConnection.addTrack(videoTrack);

                var socket = webSocketContext.WebSocket;

                peerConnection.onicecandidate += candidate =>
                {
                    _ = Send(socket, 
                        JsonSerializer.Serialize(new { type = "candidate", candidate = candidate.ToString() }));
                };

                var returnOffer = JsonSerializer.Deserialize<JsonElement>(await Receive(socket, new byte[4096], 4096, ct));
                peerConnection.setRemoteDescription(new RTCSessionDescriptionInit()
                {
                    type = RTCSdpType.offer,
                    sdp = returnOffer.GetProperty("sdp").GetString()
                });

                var rtcAnswer = peerConnection.createAnswer();
                await peerConnection.setLocalDescription(rtcAnswer);
                await Send(socket, JsonSerializer.Serialize(new { type = "answer", sdp = rtcAnswer.sdp }), ct);

                _ = Task.Run(() => HandleICECandidate(peerConnection, socket, ct));

                peerConnection.onconnectionstatechange += state =>
                {
                    this.State = state;
                };

                return new RTCR264Stream(peerConnection, H264Format.ClockRate(), framerate);
            }
            catch (OperationCanceledException) {}
            return null;
        }

        public void Stop()
        {
            listener.Close();
            peerConnection?.close();
            peerConnection?.Dispose();
        }
        public void Dispose()
        {
            Stop();
        }

        private async Task HandleICECandidate(RTCPeerConnection peerConnection, WebSocket socket, CancellationToken ct = default)
        {
            byte[] buffer = new byte[4096];
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var message = JsonSerializer.Deserialize<JsonElement>(await Receive(socket, buffer, buffer.Length, ct));
                if (message.GetProperty("type").GetString() == "candidate")
                    peerConnection.addIceCandidate(new RTCIceCandidateInit() { candidate = message.GetProperty("candidate").GetString() });
            }
        }

        private static async Task Send(WebSocket socket, string content, CancellationToken ct = default)
        {
            await socket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(content)), 
                WebSocketMessageType.Text,
                true, 
                ct);
        }

        private static async Task<string> Receive(WebSocket socket, byte[] buffer, int size, CancellationToken ct = default)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, size), ct);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
    }
}
