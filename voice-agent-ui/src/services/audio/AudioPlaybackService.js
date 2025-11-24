/**
 * Service for playing back audio received from the server
 */
class AudioPlaybackService {
  constructor() {
    this.audioContext = null;
    this.audioQueue = [];
    this.isPlaying = false;
  }

  /**
   * Initialize audio playback
   */
  initialize() {
    if (!this.audioContext) {
      this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
      console.log('[AudioPlayback] Initialized');
    }
  }

  /**
   * Add audio data to playback queue
   * @param {ArrayBuffer} audioBuffer - Audio data to play
   * @param {Function} onPlaybackStart - Optional callback when playback starts
   * @param {Function} onPlaybackEnd - Optional callback when playback ends
   */
  enqueue(audioBuffer, onPlaybackStart, onPlaybackEnd) {
    this.audioQueue.push({ audioBuffer, onPlaybackStart, onPlaybackEnd });

    if (!this.isPlaying) {
      this.playNext();
    }
  }

  /**
   * Play next audio in queue
   */
  async playNext() {
    if (this.audioQueue.length === 0) {
      this.isPlaying = false;
      return;
    }

    this.isPlaying = true;
    const { audioBuffer, onPlaybackStart, onPlaybackEnd } = this.audioQueue.shift();

    try {
      if (!this.audioContext) {
        this.initialize();
      }

      // Notify playback start
      if (onPlaybackStart) {
        onPlaybackStart();
      }

      const decodedBuffer = await this.audioContext.decodeAudioData(audioBuffer);
      const source = this.audioContext.createBufferSource();
      source.buffer = decodedBuffer;
      source.connect(this.audioContext.destination);

      source.onended = () => {
        console.log('[AudioPlayback] Finished playing audio chunk');

        // Notify playback end
        if (onPlaybackEnd) {
          onPlaybackEnd();
        }

        // Play next in queue
        this.playNext();
      };

      console.log('[AudioPlayback] Playing audio chunk');
      source.start(0);
    } catch (error) {
      console.error('[AudioPlayback] Error playing audio:', error);

      // Notify playback end even on error
      if (onPlaybackEnd) {
        onPlaybackEnd();
      }

      // Continue with next in queue
      this.playNext();
    }
  }

  /**
   * Clear the audio queue
   */
  clearQueue() {
    this.audioQueue = [];
    this.isPlaying = false;
    console.log('[AudioPlayback] Queue cleared');
  }

  /**
   * Cleanup resources
   */
  cleanup() {
    this.clearQueue();

    if (this.audioContext) {
      this.audioContext.close();
      this.audioContext = null;
      console.log('[AudioPlayback] Cleaned up');
    }
  }

  /**
   * Get current playback state
   * @returns {boolean}
   */
  getIsPlaying() {
    return this.isPlaying;
  }

  /**
   * Get queue length
   * @returns {number}
   */
  getQueueLength() {
    return this.audioQueue.length;
  }
}

export default AudioPlaybackService;
