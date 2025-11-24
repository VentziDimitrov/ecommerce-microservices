import { useState, useRef, useCallback, useEffect } from 'react';
import AudioPlaybackService from '../services/audio/AudioPlaybackService';

/**
 * Custom hook for audio playback
 * @returns {Object} Audio playback state and methods
 */
const useAudioPlayback = () => {
  const [isPlaying, setIsPlaying] = useState(false);
  const playbackServiceRef = useRef(null);

  // Initialize playback service
  useEffect(() => {
    playbackServiceRef.current = new AudioPlaybackService();
    playbackServiceRef.current.initialize();

    return () => {
      if (playbackServiceRef.current) {
        playbackServiceRef.current.cleanup();
      }
    };
  }, []);

  /**
   * Play audio data
   * @param {ArrayBuffer} audioBuffer - Audio data to play
   * @param {Function} onStart - Callback when playback starts
   * @param {Function} onEnd - Callback when playback ends
   */
  const playAudio = useCallback((audioBuffer, onStart, onEnd) => {
    if (playbackServiceRef.current) {
      playbackServiceRef.current.enqueue(
        audioBuffer,
        () => {
          setIsPlaying(true);
          if (onStart) onStart();
        },
        () => {
          setIsPlaying(playbackServiceRef.current.getIsPlaying());
          if (onEnd) onEnd();
        }
      );
    }
  }, []);

  /**
   * Clear playback queue
   */
  const clearQueue = useCallback(() => {
    if (playbackServiceRef.current) {
      playbackServiceRef.current.clearQueue();
      setIsPlaying(false);
    }
  }, []);

  /**
   * Get queue length
   */
  const getQueueLength = useCallback(() => {
    return playbackServiceRef.current ? playbackServiceRef.current.getQueueLength() : 0;
  }, []);

  return {
    isPlaying,
    playAudio,
    clearQueue,
    getQueueLength,
  };
};

export default useAudioPlayback;
