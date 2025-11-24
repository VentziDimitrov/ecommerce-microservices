import { useState, useRef, useEffect } from 'react';

export default function VoiceSession({ backendWsUrl, onSessionEnd }) {
  const [status, setStatus] = useState('connecting');
  const [isMuted, setIsMuted] = useState(false);
  const [error, setError] = useState(null);
  const [transcript, setTranscript] = useState('');

  const wsRef = useRef(null);
  const streamRef = useRef(null);
  const audioContextRef = useRef(null);
  const audioWorkletNodeRef = useRef(null);
  const sourceNodeRef = useRef(null);
  const playbackAudioContextRef = useRef(null);
  const audioQueueRef = useRef([]);
  const isPlayingRef = useRef(false);
  const isRecordingRef = useRef(false);
  const audioChunksSentRef = useRef(0);

  useEffect(() => {
    connectWebSocket();
    return () => {
      cleanup();
    };
  }, [backendWsUrl]);

  const connectWebSocket = () => {
    try {
      const ws = new WebSocket(backendWsUrl);
      ws.binaryType = 'arraybuffer';

      ws.onopen = () => {
        console.log('[VoiceSession] WebSocket connected to:', backendWsUrl);
        setStatus('connected');
        setError(null);
        audioChunksSentRef.current = 0;
        startMic();
      };

      ws.onmessage = (evt) => {
        if (typeof evt.data === 'string') {
          console.log('[VoiceSession] Received text message:', evt.data.substring(0, 100));
          handleTextMessage(evt.data);
        } else {
          console.log('[VoiceSession] Received audio data:', evt.data.byteLength, 'bytes');
          handleAudioData(evt.data);
        }
      };

      ws.onerror = (err) => {
        console.error('WebSocket error', err);
        setError('WebSocket connection error');
        setStatus('error');
      };

      ws.onclose = () => {
        console.log('WebSocket closed');
        setStatus('disconnected');
      };

      wsRef.current = ws;
    } catch (err) {
      console.error('Failed to connect WebSocket', err);
      setError('Failed to connect to server');
      setStatus('error');
    }
  };

  const handleTextMessage = (data) => {
    try {
      const message = JSON.parse(data);
      console.log('[VoiceSession] Received message:', message);

      if (message.type === 'status') {
        console.log('[VoiceSession] Status update:', message.status);
        setStatus(message.status);
      } else if (message.type === 'error') {
        setError(message.error);
        console.error('[VoiceSession] Server error:', message.error);
      } else if (message.type === 'transcript') {
        console.log('[VoiceSession] Transcript:', message.text);
        setTranscript(message.text);
      }
    } catch (err) {
      console.error('[VoiceSession] Error parsing message', err);
    }
  };

  const handleAudioData = (audioBuffer) => {
    audioQueueRef.current.push(audioBuffer);
    if (!isPlayingRef.current) {
      playNextAudio();
    }
  };

  const playNextAudio = async () => {
    if (audioQueueRef.current.length === 0) {
      isPlayingRef.current = false;
      return;
    }

    isPlayingRef.current = true;
    const audioBuffer = audioQueueRef.current.shift();

    try {
      // Pause recording during playback to avoid feedback
      if (isRecordingRef.current && audioWorkletNodeRef.current) {
        audioWorkletNodeRef.current.port.postMessage({ type: 'pause' });
      }

      if (!playbackAudioContextRef.current) {
        playbackAudioContextRef.current = new (window.AudioContext || window.webkitAudioContext)();
      }

      const audioCtx = playbackAudioContextRef.current;
      const decodedBuffer = await audioCtx.decodeAudioData(audioBuffer);
      const source = audioCtx.createBufferSource();
      source.buffer = decodedBuffer;
      source.connect(audioCtx.destination);

      source.onended = () => {
        // Resume recording after playback
        if (isRecordingRef.current && audioWorkletNodeRef.current) {
          audioWorkletNodeRef.current.port.postMessage({ type: 'resume' });
        }
        playNextAudio();
      };

      source.start(0);
    } catch (err) {
      console.error('Audio playback error', err);
      setError('Audio playback error');
      // Resume recording even on error
      if (isRecordingRef.current && audioWorkletNodeRef.current) {
        audioWorkletNodeRef.current.port.postMessage({ type: 'resume' });
      }
      playNextAudio();
    }
  };

  const startMic = async () => {
    try {
      // Request microphone access
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
          sampleRate: 16000,
          channelCount: 1
        }
      });

      streamRef.current = stream;

      // Create AudioContext for recording
      // Note: Browser may not support 16kHz, it will use closest supported rate
      const audioContext = new (window.AudioContext || window.webkitAudioContext)({
        sampleRate: 16000  // Match Azure Speech requirements
      });
      audioContextRef.current = audioContext;

      console.log('[VoiceSession] AudioContext created with sample rate:', audioContext.sampleRate);
      if (audioContext.sampleRate !== 16000) {
        console.warn(`[VoiceSession] Browser using ${audioContext.sampleRate}Hz instead of 16000Hz. Backend may need to handle resampling.`);
      }

      // Load AudioWorklet processor
      await audioContext.audioWorklet.addModule('/pcm-processor.js');

      // Create audio source from microphone
      const source = audioContext.createMediaStreamSource(stream);
      sourceNodeRef.current = source;

      // Create AudioWorklet node
      const workletNode = new AudioWorkletNode(audioContext, 'pcm-processor');
      audioWorkletNodeRef.current = workletNode;

      // Handle PCM data from worklet
      workletNode.port.onmessage = (event) => {
        if (event.data.type === 'audio' && wsRef.current &&
            wsRef.current.readyState === WebSocket.OPEN) {
          // Send raw PCM data directly to WebSocket
          const bytesSent = event.data.data.byteLength;
          audioChunksSentRef.current++;
          if (audioChunksSentRef.current % 10 === 0) {
            console.log(`[VoiceSession] Sent ${audioChunksSentRef.current} chunks (latest: ${bytesSent} bytes)`);
          }
          wsRef.current.send(event.data.data);
        } else if (event.data.type === 'audio') {
          console.warn('[VoiceSession] Audio data received but WebSocket not open:', wsRef.current?.readyState);
        }
      };

      // Connect: microphone -> worklet -> (data sent via WebSocket)
      source.connect(workletNode);
      // Note: workletNode doesn't need to connect to destination since we're just capturing

      isRecordingRef.current = true;
      setStatus('listening');
      setError(null);
      console.log('[VoiceSession] AudioWorklet-based recording started');
      console.log('[VoiceSession] Actual sample rate:', audioContext.sampleRate);
      console.log('[VoiceSession] Microphone constraints:', stream.getAudioTracks()[0].getSettings());
    } catch (err) {
      console.error('[VoiceSession] Microphone access error', err);
      setError(`Could not access microphone: ${err.message}`);
      setStatus('error');
    }
  };

  const stopMic = () => {
    isRecordingRef.current = false;

    // Disconnect and cleanup audio nodes
    if (sourceNodeRef.current) {
      sourceNodeRef.current.disconnect();
      sourceNodeRef.current = null;
    }

    if (audioWorkletNodeRef.current) {
      audioWorkletNodeRef.current.disconnect();
      audioWorkletNodeRef.current.port.close();
      audioWorkletNodeRef.current = null;
    }

    if (audioContextRef.current) {
      audioContextRef.current.close();
      audioContextRef.current = null;
    }

    // Stop microphone stream
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(track => track.stop());
      streamRef.current = null;
    }
  };

  const handleMuteToggle = () => {
    const stream = streamRef.current;
    if (stream) {
      stream.getAudioTracks().forEach(track => {
        track.enabled = !track.enabled;
      });
      setIsMuted(!isMuted);
    }
  };

  const handleEndSession = () => {
    cleanup();
    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify({ type: 'endSession' }));
      wsRef.current.close();
    }
    onSessionEnd && onSessionEnd();
  };

  const cleanup = () => {
    stopMic();
    if (playbackAudioContextRef.current) {
      playbackAudioContextRef.current.close();
      playbackAudioContextRef.current = null;
    }
    audioQueueRef.current = [];
    isPlayingRef.current = false;
  };

  const getStatusColor = () => {
    switch (status) {
      case 'listening': return '#4CAF50';
      case 'speaking': return '#2196F3';
      case 'connected': return '#FFC107';
      case 'error': return '#F44336';
      default: return '#9E9E9E';
    }
  };

  const getStatusText = () => {
    switch (status) {
      case 'listening': return 'Listening...';
      case 'speaking': return 'AI Speaking...';
      case 'connected': return 'Connected';
      case 'connecting': return 'Connecting...';
      case 'error': return 'Error';
      case 'disconnected': return 'Disconnected';
      default: return status;
    }
  };

  return (
    <div className="voice-session">
      <div className="status-indicator" style={{ backgroundColor: getStatusColor() }}>
        <div className="status-dot"></div>
        <span className="status-text">{getStatusText()}</span>
      </div>

      {error && (
        <div className="error-message">
          {error}
        </div>
      )}

      {transcript && (
        <div className="transcript">
          <strong>You said:</strong> {transcript}
        </div>
      )}

      <div className="controls">
        <button
          className={`control-button ${isMuted ? 'muted' : ''}`}
          onClick={handleMuteToggle}
          disabled={status === 'error' || status === 'disconnected'}
        >
          {isMuted ? '🔇 Unmute' : '🎤 Mute'}
        </button>
        <button
          className="control-button end-button"
          onClick={handleEndSession}
        >
          End Session
        </button>
      </div>

      <div className="instructions">
        <p>Speak naturally - the AI will respond when you're done talking</p>
        <p className="tech-info">Using AudioWorklet for direct PCM streaming</p>
      </div>
    </div>
  );
}
