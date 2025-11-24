/**
 * Audio configuration constants for Azure Speech Services
 */
export const AUDIO_CONFIG = {
  SAMPLE_RATE: 16000,
  BIT_DEPTH: 16,
  CHANNELS: 1,
  BUFFER_SIZE: 4096, // samples per chunk (~256ms at 16kHz)
};

/**
 * WebSocket configuration
 */
export const WEBSOCKET_CONFIG = {
  RECONNECT_DELAY: 3000,
  MAX_RECONNECT_ATTEMPTS: 5,
};

/**
 * Session status constants
 */
export const SESSION_STATUS = {
  CONNECTING: 'connecting',
  CONNECTED: 'connected',
  LISTENING: 'listening',
  SPEAKING: 'speaking',
  ERROR: 'error',
  DISCONNECTED: 'disconnected',
};

/**
 * Message types for WebSocket communication
 */
export const MESSAGE_TYPES = {
  STATUS: 'status',
  ERROR: 'error',
  TRANSCRIPT: 'transcript',
  END_SESSION: 'endSession',
};
