import { useState, useRef, useCallback } from 'react';
import AudioCaptureService from '../services/audio/AudioCaptureService';

/**
 * Custom hook for audio capture
 * @returns {Object} Audio capture state and methods
 */
const useAudioCapture = () => {
  const [isRecording, setIsRecording] = useState(false);
  const [isMuted, setIsMuted] = useState(false);
  const [error, setError] = useState(null);
  const audioCaptureRef = useRef(null);

  /**
   * Start audio capture
   * @param {Function} onAudioData - Callback for audio data
   */
  const startCapture = useCallback(async (onAudioData) => {
    try {
      if (!audioCaptureRef.current) {
        audioCaptureRef.current = new AudioCaptureService();
      }

      const info = await audioCaptureRef.current.start(onAudioData);
      setIsRecording(true);
      setError(null);

      console.log('[useAudioCapture] Capture started:', info);
      return info;
    } catch (err) {
      console.error('[useAudioCapture] Failed to start:', err);
      setError(err.message);
      setIsRecording(false);
      throw err;
    }
  }, []);

  /**
   * Stop audio capture
   */
  const stopCapture = useCallback(() => {
    if (audioCaptureRef.current) {
      audioCaptureRef.current.stop();
      audioCaptureRef.current = null;
      setIsRecording(false);
      console.log('[useAudioCapture] Capture stopped');
    }
  }, []);

  /**
   * Pause audio capture
   */
  const pauseCapture = useCallback(() => {
    if (audioCaptureRef.current) {
      audioCaptureRef.current.pause();
    }
  }, []);

  /**
   * Resume audio capture
   */
  const resumeCapture = useCallback(() => {
    if (audioCaptureRef.current) {
      audioCaptureRef.current.resume();
    }
  }, []);

  /**
   * Toggle mute
   */
  const toggleMute = useCallback(() => {
    if (audioCaptureRef.current) {
      const newMutedState = !isMuted;
      audioCaptureRef.current.setMuted(newMutedState);
      setIsMuted(newMutedState);
    }
  }, [isMuted]);

  return {
    isRecording,
    isMuted,
    error,
    startCapture,
    stopCapture,
    pauseCapture,
    resumeCapture,
    toggleMute,
  };
};

export default useAudioCapture;
