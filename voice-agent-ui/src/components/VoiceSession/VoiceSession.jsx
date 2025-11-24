import { useEffect } from 'react';
import { useWebSocket, useAudioCapture, useAudioPlayback } from '../../hooks';
import { SESSION_STATUS } from '../../utils/constants';
import './VoiceSession.module.css';

export default function VoiceSession({ backendWsUrl, onSessionEnd }) {
  const {
    status,
    error,
    messages,
    sendAudio,
    endSession,
    isConnected,
    onAudioData,
  } = useWebSocket(backendWsUrl);

  const {
    isMuted,
    startCapture,
    stopCapture,
    pauseCapture,
    resumeCapture,
    toggleMute,
  } = useAudioCapture();

  const { isPlaying, playAudio } = useAudioPlayback();

  // Start audio capture when WebSocket connects
  useEffect(() => {
    if (status === SESSION_STATUS.CONNECTED) {
      handleStartAudioCapture();
    }

    return () => {
      stopCapture();
    };
  }, [status]);

  // Register audio data callback from server
  useEffect(() => {
    onAudioData((audioBuffer) => {
      playAudio(
        audioBuffer,
        () => {
          // On playback start - pause recording to avoid feedback
          pauseCapture();
        },
        () => {
          // On playback end - resume recording
          resumeCapture();
        }
      );
    });
  }, [onAudioData, playAudio, pauseCapture, resumeCapture]);

  const handleStartAudioCapture = async () => {
    try {
      await startCapture((audioData) => {
        // Send audio data to server via WebSocket
        if (isConnected()) {
          sendAudio(audioData);
        }
      });
    } catch (err) {
      console.error('[VoiceSession] Failed to start audio capture:', err);
    }
  };

  const handleEndSession = () => {
    stopCapture();
    endSession();
    if (onSessionEnd) {
      onSessionEnd();
    }
  };

  const getStatusColor = () => {
    switch (status) {
      case SESSION_STATUS.LISTENING:
        return '#4CAF50';
      case SESSION_STATUS.SPEAKING:
        return '#2196F3';
      case SESSION_STATUS.CONNECTED:
        return '#FFC107';
      case SESSION_STATUS.ERROR:
        return '#F44336';
      default:
        return '#9E9E9E';
    }
  };

  const getStatusText = () => {
    switch (status) {
      case SESSION_STATUS.LISTENING:
        return 'Listening...';
      case SESSION_STATUS.SPEAKING:
        return 'AI Speaking...';
      case SESSION_STATUS.CONNECTED:
        return 'Connected';
      case SESSION_STATUS.CONNECTING:
        return 'Connecting...';
      case SESSION_STATUS.ERROR:
        return 'Error';
      case SESSION_STATUS.DISCONNECTED:
        return 'Disconnected';
      default:
        return status;
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

      {messages.length > 0 && (
        <div className="conversation">
          {messages.map((msg, index) => (
            <div key={index} className={`message ${msg.role}`}>
              <div className="message-header">
                <strong>{msg.role === 'user' ? 'You' : 'AI Assistant'}</strong>
              </div>
              <div className="message-text">{msg.text}</div>
            </div>
          ))}
        </div>
      )}

      <div className="controls">
        <button
          className={`control-button ${isMuted ? 'muted' : ''}`}
          onClick={toggleMute}
          disabled={status === SESSION_STATUS.ERROR || status === SESSION_STATUS.DISCONNECTED}
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
        <p className="tech-info">
          Using AudioWorklet for direct PCM streaming
          {isPlaying && ' • Playing response...'}
        </p>
      </div>
    </div>
  );
}
