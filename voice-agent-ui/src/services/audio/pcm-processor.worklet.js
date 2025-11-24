/**
 * AudioWorklet Processor for capturing raw PCM audio
 * This runs in a separate audio worklet thread
 */
class PCMProcessor extends AudioWorkletProcessor {
  constructor() {
    super();
    this.bufferSize = 4096; // Send chunks of 4096 samples (~256ms at 16kHz)
    this.buffer = new Float32Array(this.bufferSize);
    this.bufferIndex = 0;
    this.isPaused = false;

    // Listen for pause/resume messages
    this.port.onmessage = (event) => {
      if (event.data.type === 'pause') {
        this.isPaused = true;
        console.log('[PCMProcessor] Paused');
      } else if (event.data.type === 'resume') {
        this.isPaused = false;
        console.log('[PCMProcessor] Resumed');
      }
    };
  }

  /**
   * Convert Float32 audio samples to Int16 PCM
   */
  float32ToInt16(float32Array) {
    const int16 = new Int16Array(float32Array.length);
    for (let i = 0; i < float32Array.length; i++) {
      // Clamp to [-1, 1] range
      const s = Math.max(-1, Math.min(1, float32Array[i]));
      // Convert to 16-bit integer
      int16[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
    }
    return int16;
  }

  /**
   * Process audio in 128-sample quantum (standard Web Audio block size)
   */
  process(inputs) {
    const input = inputs[0];

    // If paused, skip processing but keep processor alive
    if (this.isPaused) {
      return true;
    }

    // If no input, keep processor alive
    if (!input || input.length === 0) {
      return true;
    }

    // Get first channel (mono)
    const samples = input[0];

    if (!samples || samples.length === 0) {
      return true;
    }

    // Accumulate samples in buffer
    for (let i = 0; i < samples.length; i++) {
      this.buffer[this.bufferIndex++] = samples[i];

      // When buffer is full, convert to PCM and send
      if (this.bufferIndex >= this.bufferSize) {
        const pcmData = this.float32ToInt16(this.buffer);

        // Send PCM data to main thread
        this.port.postMessage({
          type: 'audio',
          data: pcmData.buffer
        }, [pcmData.buffer]);

        // Reset buffer
        this.buffer = new Float32Array(this.bufferSize);
        this.bufferIndex = 0;
      }
    }

    return true; // Keep processor alive
  }
}

registerProcessor('pcm-processor', PCMProcessor);
