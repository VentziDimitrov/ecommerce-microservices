import { AUDIO_CONFIG } from '../../utils/constants';

/**
 * Service for capturing audio from microphone using AudioWorklet
 */
class AudioCaptureService {
  constructor() {
    this.audioContext = null;
    this.stream = null;
    this.sourceNode = null;
    this.workletNode = null;
    this.isRecording = false;
    this.audioChunksSent = 0;
  }

  /**
   * Initialize audio capture with microphone
   * @param {Function} onAudioData - Callback for audio data chunks
   * @returns {Promise<void>}
   */
  async start(onAudioData) {
    try {
      // Request microphone access
      this.stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
          sampleRate: AUDIO_CONFIG.SAMPLE_RATE,
          channelCount: AUDIO_CONFIG.CHANNELS,
        }
      });

      // Create AudioContext
      this.audioContext = new (window.AudioContext || window.webkitAudioContext)({
        sampleRate: AUDIO_CONFIG.SAMPLE_RATE,
      });

      console.log('[AudioCapture] AudioContext created with sample rate:', this.audioContext.sampleRate);

      if (this.audioContext.sampleRate !== AUDIO_CONFIG.SAMPLE_RATE) {
        console.warn(
          `[AudioCapture] Browser using ${this.audioContext.sampleRate}Hz instead of ${AUDIO_CONFIG.SAMPLE_RATE}Hz`
        );
      }

      // Load AudioWorklet processor
      await this.audioContext.audioWorklet.addModule('/pcm-processor.js');

      // Create audio source from microphone
      this.sourceNode = this.audioContext.createMediaStreamSource(this.stream);

      // Create AudioWorklet node
      this.workletNode = new AudioWorkletNode(this.audioContext, 'pcm-processor');

      // Handle PCM data from worklet
      this.workletNode.port.onmessage = (event) => {
        if (event.data.type === 'audio') {
          this.audioChunksSent++;

          if (this.audioChunksSent % 10 === 0) {
            console.log(
              `[AudioCapture] Sent ${this.audioChunksSent} chunks (latest: ${event.data.data.byteLength} bytes)`
            );
          }

          onAudioData(event.data.data);
        }
      };

      // Connect: microphone -> worklet
      this.sourceNode.connect(this.workletNode);

      this.isRecording = true;
      console.log('[AudioCapture] Recording started');
      console.log('[AudioCapture] Microphone settings:', this.stream.getAudioTracks()[0].getSettings());

      return {
        sampleRate: this.audioContext.sampleRate,
        settings: this.stream.getAudioTracks()[0].getSettings(),
      };
    } catch (error) {
      console.error('[AudioCapture] Failed to start:', error);
      throw new Error(`Could not access microphone: ${error.message}`);
    }
  }

  /**
   * Pause audio capture
   */
  pause() {
    if (this.workletNode) {
      this.workletNode.port.postMessage({ type: 'pause' });
      console.log('[AudioCapture] Paused');
    }
  }

  /**
   * Resume audio capture
   */
  resume() {
    if (this.workletNode) {
      this.workletNode.port.postMessage({ type: 'resume' });
      console.log('[AudioCapture] Resumed');
    }
  }

  /**
   * Stop audio capture and cleanup resources
   */
  stop() {
    this.isRecording = false;

    if (this.sourceNode) {
      this.sourceNode.disconnect();
      this.sourceNode = null;
    }

    if (this.workletNode) {
      this.workletNode.disconnect();
      this.workletNode.port.close();
      this.workletNode = null;
    }

    if (this.audioContext) {
      this.audioContext.close();
      this.audioContext = null;
    }

    if (this.stream) {
      this.stream.getTracks().forEach((track) => track.stop());
      this.stream = null;
    }

    console.log('[AudioCapture] Stopped and cleaned up');
  }

  /**
   * Toggle mute state
   * @param {boolean} muted - Whether to mute
   */
  setMuted(muted) {
    if (this.stream) {
      this.stream.getAudioTracks().forEach((track) => {
        track.enabled = !muted;
      });
      console.log(`[AudioCapture] ${muted ? 'Muted' : 'Unmuted'}`);
    }
  }

  /**
   * Get current recording state
   * @returns {boolean}
   */
  getIsRecording() {
    return this.isRecording;
  }
}

export default AudioCaptureService;
