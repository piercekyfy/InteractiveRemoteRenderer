import { useEffect, useRef } from "react";

export default function App() { /* As mentioned in the Server, the latency of WebRTC is too high. This example is left-over incase it is ever useful. */
  const videoRef = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    const ws = new WebSocket('ws://localhost:5000/ws');
    const pc = new RTCPeerConnection({ iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] });

    pc.ontrack = e => {
      if (videoRef.current)
        videoRef.current.srcObject = e.streams[0];
    };

    pc.onicecandidate = e => {
      if (e.candidate)
        ws.send(JSON.stringify({ type: 'candidate', candidate: e.candidate.candidate }));
    };

    ws.onmessage = async ({ data }) => {
      const msg = JSON.parse(data);
      if (msg.type === 'answer')
        await pc.setRemoteDescription({ type: 'answer', sdp: msg.sdp });
      else if (msg.type === 'candidate')
        await pc.addIceCandidate({ candidate: msg.candidate });
    };

    ws.onopen = async () => {
      pc.addTransceiver('video', { direction: 'recvonly' });
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      ws.send(JSON.stringify({ type: 'offer', sdp: offer.sdp }));
    };

    return () => {
      pc.close();
      ws.close();
    };
  }, []);

  return <video ref={videoRef} autoPlay playsInline />;
}