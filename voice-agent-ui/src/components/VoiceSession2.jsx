import React, { useState, useRef, useEffect } from 'react';

const CHUNK_MS = 250;

export default function VoiceSession2({ backendWsUrl, onSessionEnd }) {
  const [status, setStatus] = useState('idle'); 
  const mediaRecorderRef = useRef(null);
  const wsRef = useRef(null);
  const streamRef = useRef(null);

  useEffect(() => {
    const ws = new WebSocket(backendWsUrl);
    ws.binaryType = 'arraybuffer';
    ws.onopen = () => {
      console.log('WS open');
      startMic();
    };
    ws.onmessage = (evt) => {
      playAudioChunk(evt.data);
    };
    ws.onerror = (err) => {
      console.error('WS error', err);
    };
    wsRef.current = ws;
    return () => {
      stopMic();
      ws.close();
    };
  }, [backendWsUrl]);

  const startMic = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true } });
      streamRef.current = stream;
      const mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm; codecs=opus' });
      mediaRecorderRef.current = mediaRecorder;
      mediaRecorder.ondataavailable = (e) => {
        if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
          wsRef.current.send(e.data);
        }
      };
      mediaRecorder.start(CHUNK_MS);
      setStatus('listening');
    } catch (err) {
      console.error('Mic access error', err);
    }
  };

  const stopMic = () => {
    setStatus('idle');
    const mediaRecorder = mediaRecorderRef.current;
    if (mediaRecorder && mediaRecorder.state !== 'inactive') {
      mediaRecorder.stop();
    }
    const stream = streamRef.current;
    if (stream) {
      stream.getTracks().forEach(track => track.stop());
    }
  };

  const playAudioChunk = (audioBuffer) => {
    setStatus('speaking');
    const audioContext = new AudioContext();
    audioContext.decodeAudioData(audioBuffer)
      .then(buffer => {
        const src = audioContext.createBufferSource();
        src.buffer = buffer;
        src.connect(audioContext.destination);
        src.onended = () => {
          // after playback, resume mic
          startMic();
        };
        // Stop mic first to avoid feedback
        stopMic();
        src.start(0);
      })
      .catch(err => {
        console.error('Audio decode error', err);
        startMic();
      });
  };

  const handleMuteToggle = () => {
    const stream = streamRef.current;
    if (stream) {
      stream.getAudioTracks().forEach(track => {
        track.enabled = !track.enabled;
      });
    }
  };

  const handleEndSession = () => {
    stopMic();
    if (wsRef.current) {
      wsRef.current.send(JSON.stringify({ type: 'endSession' }));
      wsRef.current.close();
    }
    onSessionEnd && onSessionEnd();
  };

  return (
    <div>
      <h2>Voice Session</h2>
      <p>Status: {status}</p>
      <button onClick={handleMuteToggle}>Mute/Unmute</button>
      <button onClick={handleEndSession}>End Session</button>
    </div>
  );
}
