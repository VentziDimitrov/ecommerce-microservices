import { MESSAGE_TYPES } from '../../utils/constants';

/**
 * Service for managing WebSocket connection to backend
 */
class WebSocketService {
  constructor(url) {
    this.url = url;
    this.ws = null;
    this.callbacks = {
      onOpen: null,
      onClose: null,
      onError: null,
      onTextMessage: null,
      onAudioData: null,
    };
  }

  /**
   * Set event callbacks
   * @param {Object} callbacks - Event callbacks
   */
  setCallbacks(callbacks) {
    this.callbacks = { ...this.callbacks, ...callbacks };
  }

  /**
   * Connect to WebSocket server
   * @returns {Promise<void>}
   */
  connect() {
    return new Promise((resolve, reject) => {
      try {
        this.ws = new WebSocket(this.url);
        this.ws.binaryType = 'arraybuffer';

        this.ws.onopen = () => {
          console.log('[WebSocket] Connected to:', this.url);
          if (this.callbacks.onOpen) {
            this.callbacks.onOpen();
          }
          resolve();
        };

        this.ws.onmessage = (evt) => {
          if (typeof evt.data === 'string') {
            console.log('[WebSocket] Received text message:', evt.data.substring(0, 100));
            this.handleTextMessage(evt.data);
          } else {
            console.log('[WebSocket] Received audio data:', evt.data.byteLength, 'bytes');
            if (this.callbacks.onAudioData) {
              this.callbacks.onAudioData(evt.data);
            }
          }
        };

        this.ws.onerror = (err) => {
          console.error('[WebSocket] Error:', err);
          if (this.callbacks.onError) {
            this.callbacks.onError('WebSocket connection error');
          }
          reject(err);
        };

        this.ws.onclose = () => {
          console.log('[WebSocket] Closed');
          if (this.callbacks.onClose) {
            this.callbacks.onClose();
          }
        };
      } catch (err) {
        console.error('[WebSocket] Failed to connect:', err);
        if (this.callbacks.onError) {
          this.callbacks.onError('Failed to connect to server');
        }
        reject(err);
      }
    });
  }

  /**
   * Handle text messages from server
   * @param {string} data - JSON string message
   */
  handleTextMessage(data) {
    try {
      const message = JSON.parse(data);
      console.log('[WebSocket] Parsed message:', message);

      if (this.callbacks.onTextMessage) {
        this.callbacks.onTextMessage(message);
      }
    } catch (err) {
      console.error('[WebSocket] Error parsing message:', err);
    }
  }

  /**
   * Send audio data to server
   * @param {ArrayBuffer} audioData - PCM audio data
   */
  sendAudio(audioData) {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(audioData);
    } else {
      console.warn('[WebSocket] Cannot send audio, WebSocket not open:', this.ws?.readyState);
    }
  }

  /**
   * Send text message to server
   * @param {Object} message - Message object
   */
  sendMessage(message) {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(message));
      console.log('[WebSocket] Sent message:', message);
    } else {
      console.warn('[WebSocket] Cannot send message, WebSocket not open');
    }
  }

  /**
   * End session
   */
  endSession() {
    this.sendMessage({ type: MESSAGE_TYPES.END_SESSION });
    this.close();
  }

  /**
   * Close WebSocket connection
   */
  close() {
    if (this.ws) {
      this.ws.close();
      this.ws = null;
      console.log('[WebSocket] Connection closed');
    }
  }

  /**
   * Check if WebSocket is connected
   * @returns {boolean}
   */
  isConnected() {
    return this.ws && this.ws.readyState === WebSocket.OPEN;
  }

  /**
   * Get WebSocket ready state
   * @returns {number|null}
   */
  getReadyState() {
    return this.ws ? this.ws.readyState : null;
  }
}

export default WebSocketService;
