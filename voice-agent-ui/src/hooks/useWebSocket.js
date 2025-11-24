import { useState, useEffect, useRef, useCallback } from 'react';
import WebSocketService from '../services/websocket/WebSocketService';
import { SESSION_STATUS, MESSAGE_TYPES } from '../utils/constants';

/**
 * Custom hook for managing WebSocket connection
 * @param {string} url - WebSocket URL
 * @returns {Object} WebSocket state and methods
 */
const useWebSocket = (url) => {
  const [status, setStatus] = useState(SESSION_STATUS.CONNECTING);
  const [error, setError] = useState(null);
  const [messages, setMessages] = useState([]);
  const wsServiceRef = useRef(null);

  // Initialize WebSocket service
  useEffect(() => {
    if (!url) return;

    const wsService = new WebSocketService(url);
    wsServiceRef.current = wsService;

    // Set up callbacks
    wsService.setCallbacks({
      onOpen: () => {
        console.log('[useWebSocket] Connection opened');
        setStatus(SESSION_STATUS.CONNECTED);
        setError(null);
      },
      onClose: () => {
        console.log('[useWebSocket] Connection closed');
        setStatus(SESSION_STATUS.DISCONNECTED);
      },
      onError: (errorMsg) => {
        console.error('[useWebSocket] Error:', errorMsg);
        setError(errorMsg);
        setStatus(SESSION_STATUS.ERROR);
      },
      onTextMessage: (message) => {
        handleTextMessage(message);
      },
    });

    // Connect
    wsService.connect().catch((err) => {
      console.error('[useWebSocket] Failed to connect:', err);
      setError('Failed to connect to server');
      setStatus(SESSION_STATUS.ERROR);
    });

    // Cleanup on unmount
    return () => {
      wsService.close();
    };
  }, [url]);

  // Handle text messages from server
  const handleTextMessage = useCallback((message) => {
    if (message.type === MESSAGE_TYPES.STATUS) {
      console.log('[useWebSocket] Status update:', message.status);
      setStatus(message.status);
    } else if (message.type === MESSAGE_TYPES.ERROR) {
      setError(message.error);
      console.error('[useWebSocket] Server error:', message.error);
    } else if (message.type === MESSAGE_TYPES.TRANSCRIPT) {
      console.log('[useWebSocket] Transcript:', message.text, 'Role:', message.role);
      setMessages((prev) => [...prev, { text: message.text, role: message.role, timestamp: Date.now() }]);
    }
  }, []);

  // Send audio data
  const sendAudio = useCallback((audioData) => {
    if (wsServiceRef.current) {
      wsServiceRef.current.sendAudio(audioData);
    }
  }, []);

  // Send message
  const sendMessage = useCallback((message) => {
    if (wsServiceRef.current) {
      wsServiceRef.current.sendMessage(message);
    }
  }, []);

  // End session
  const endSession = useCallback(() => {
    if (wsServiceRef.current) {
      wsServiceRef.current.endSession();
    }
  }, []);

  // Check if connected
  const isConnected = useCallback(() => {
    return wsServiceRef.current ? wsServiceRef.current.isConnected() : false;
  }, []);

  // Register audio data callback
  const onAudioData = useCallback((callback) => {
    if (wsServiceRef.current) {
      wsServiceRef.current.callbacks.onAudioData = callback;
    }
  }, []);

  return {
    status,
    error,
    messages,
    sendAudio,
    sendMessage,
    endSession,
    isConnected,
    onAudioData,
  };
};

export default useWebSocket;
