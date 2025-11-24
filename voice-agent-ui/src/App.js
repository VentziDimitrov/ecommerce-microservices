import React, { useState } from 'react';
import VoiceSession from './components/VoiceSession';
import './App.css';

const API_BASE_URL = 'http://localhost:5000/api';

function App() {
  const [sessionActive, setSessionActive] = useState(false);

  // Use environment variable or default to localhost
  const backendWsUrl = process.env.REACT_APP_WS_URL ||
    (window.location.protocol === 'https:'
      ? 'wss://localhost:5000/voice/ws'
      : 'ws://localhost:5000/voice/ws');

  return (
    <div className="App">
      <header className="App-header">
        <h1>Voice AI Agent</h1>
        <p>Powered by Azure Cognitive Services</p>
      </header>
      <main className="App-main">
        { !sessionActive ? (
          <div className="start-container">
            <button className="start-button" onClick={() => setSessionActive(true)}>
              Start Voice Session
            </button>
            <p className="info-text">
              Click the button above to start a voice conversation with the AI agent
            </p>
          </div>
        ) : (
          <VoiceSession
            backendWsUrl={backendWsUrl}
            onSessionEnd={() => setSessionActive(false)}
          />
        )}
      </main>
    </div>
  );
}

export default App;

