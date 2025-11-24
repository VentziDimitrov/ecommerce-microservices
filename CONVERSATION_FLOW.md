# Voice Agent Conversation Flow

## Complete Architecture

This document explains the complete end-to-end flow of the voice conversational AI agent.

## System Overview

```
┌─────────────┐         ┌─────────────┐         ┌──────────────────┐
│   Browser   │ ◄─────► │   Backend   │ ◄─────► │  Azure Speech   │
│  (Frontend) │ WebSocket  (.NET API) │           Services       │
└─────────────┘         └─────────────┘         └──────────────────┘
                               │
                               ▼
                        ┌──────────────┐
                        │  Azure OpenAI │
                        │  (LLM + RAG)  │
                        └──────────────┘
```

## Conversation Flow

### 1. Session Initialization

**Frontend:**
1. User clicks "Start Voice Session"
2. WebSocket connection established to `ws://localhost:5000/voice/ws`
3. Backend receives connection

**Backend:**
```csharp
// VoiceSessionHandler.HandleSessionAsync()
await _recognizer.StartContinuousRecognitionAsync();
```

**Status:** Frontend shows "Connected" → "Listening"

---

### 2. User Speaks (Speech-to-Text)

**Frontend:**
```javascript
// AudioWorklet captures PCM audio
Microphone → AudioContext (16kHz) → AudioWorklet
  → Float32 samples → Int16 PCM conversion
  → WebSocket.send(PCM data)
```

**Backend:**
```csharp
// VoiceSessionHandler.HandleAudioChunkAsync()
_pushStream.Write(audioChunk);  // Feed to Azure Speech Recognizer
```

**Azure Speech Services:**
- Continuous recognition processes audio stream
- Fires `Recognized` event when speech detected

---

### 3. Recognition Complete

**Backend:**
```csharp
// VoiceSessionHandler Recognition Event Handler
_recognizer.Recognized += async (s, e) => {
    if (e.Result.Reason == ResultReason.RecognizedSpeech) {
        await ProcessRecognizedTextAsync(e.Result.Text);
    }
}
```

**Message sent to Frontend:**
```json
{
  "type": "transcript",
  "text": "What is the weather today?",
  "role": "user"
}
```

**Frontend:**
- Displays user message in conversation UI (blue gradient bubble, right-aligned)

---

### 4. AI Processing

**Backend:**
```csharp
// VoiceSessionHandler.ProcessRecognizedTextAsync()

// 1. Correct grammar/spelling
var correctedText = await _conversationService.CorrectUserTextAsync(userText);

// 2. Stop listening (prevent echo during response)
await _recognizer.StopContinuousRecognitionAsync();

// 3. Get AI response (RAG + LLM)
string answer = await _conversationService.ProcessUserTextAsync(correctedText);
```

**ConversationService Flow:**
```csharp
// 1. RAG: Query vector database for relevant context
var relevantDocs = await _retrievalService.QueryAsync(userText);

// 2. LLM: Generate response with context
var response = await _llmService.GenerateResponseAsync(
    conversationHistory,
    relevantDocs,
    userText
);
```

---

### 5. AI Response Text

**Backend:**
```csharp
// Send text response to frontend for display
await SendTranscriptAsync(answer, "assistant");
```

**Message sent to Frontend:**
```json
{
  "type": "transcript",
  "text": "The weather is sunny with a high of 75°F.",
  "role": "assistant"
}
```

**Frontend:**
- Displays AI message in conversation UI (gray bubble, left-aligned)

---

### 6. Text-to-Speech

**Backend:**
```csharp
// VoiceSessionHandler.PerformTextToSpeechAndSendAsync()

// Update status
await SendStatusAsync("speaking");

// Generate speech audio
using var synthesizer = new SpeechSynthesizer(_speechConfig, null);
using var result = await synthesizer.SpeakTextAsync(answerText);

// Send audio to frontend (MP3 format)
await _socket.SendAsync(
    new ArraySegment<byte>(result.AudioData),
    WebSocketMessageType.Binary,
    true,
    CancellationToken.None
);
```

**Frontend:**
```javascript
// useAudioPlayback hook
playAudio(audioBuffer,
  onStart: () => pauseCapture(),  // Pause mic during playback
  onEnd: () => resumeCapture()     // Resume mic after playback
);
```

**Status:** Frontend shows "AI Speaking..."

---

### 7. Resume Listening

**Backend:**
```csharp
// Resume speech recognition after TTS completes
await _recognizer.StartContinuousRecognitionAsync();
await SendStatusAsync("listening");
```

**Frontend:**
- Status returns to "Listening..."
- User can speak again (repeat from step 2)

---

## WebSocket Message Protocol

### Client → Server

| Type | Format | Purpose |
|------|--------|---------|
| **Audio** | `ArrayBuffer` (Binary) | PCM audio data (16kHz, 16-bit, mono) |
| **Control** | `{ "type": "endSession" }` | End voice session |

### Server → Client

| Type | Format | Purpose |
|------|--------|---------|
| **Transcript** | `{ "type": "transcript", "text": "...", "role": "user\|assistant" }` | Display conversation text |
| **Status** | `{ "type": "status", "status": "listening\|speaking" }` | Update UI status |
| **Error** | `{ "type": "error", "error": "..." }` | Display error message |
| **Audio** | `ArrayBuffer` (Binary) | TTS audio response (MP3) |

---

## Frontend State Management

### Hooks Architecture

```javascript
useWebSocket()       // Manages WebSocket connection & messages
  ├─ messages        // Conversation history [{ text, role, timestamp }]
  ├─ status          // Session status (listening/speaking/etc)
  └─ error           // Error messages

useAudioCapture()    // Manages microphone capture
  ├─ isRecording     // Recording state
  ├─ isMuted         // Mute state
  └─ methods         // start/stop/pause/resume/toggleMute

useAudioPlayback()   // Manages TTS audio playback
  ├─ isPlaying       // Playback state
  └─ playAudio()     // Play audio with callbacks
```

---

## Key Design Decisions

### Why Send Transcripts?

**Original:** Backend only sent audio, no text feedback to user

**Updated:** Backend sends both text and audio

**Benefits:**
- ✅ User can see what was recognized (transparency)
- ✅ Shows conversation history
- ✅ Better debugging and user experience
- ✅ Accessibility (can read if audio fails)

### Why Pause Recording During Playback?

**Problem:** Microphone picks up speaker audio → echo/feedback loop

**Solution:**
```javascript
playAudio(audioBuffer,
  onStart: () => pauseCapture(),   // Pause mic
  onEnd: () => resumeCapture()      // Resume after
);
```

### Why Stop/Start Recognition?

**Backend pauses Azure Speech Recognizer during TTS:**
```csharp
await _recognizer.StopContinuousRecognitionAsync();  // Before TTS
// ... TTS playback ...
await _recognizer.StartContinuousRecognitionAsync(); // After TTS
```

**Reason:** Prevents recognizer from processing AI's own voice

---

## Message Flow Diagram

```
User speaks → Frontend AudioWorklet
                 ↓
              PCM Audio (Binary)
                 ↓
           Backend WebSocket
                 ↓
        Azure Speech Recognizer
                 ↓
          Recognized Text
                 ↓
    ┌────────────┴────────────┐
    ↓                         ↓
[Transcript]              [LLM Processing]
role: "user"                    ↓
    ↓                      AI Response Text
Frontend UI                     ↓
                         ┌──────┴──────┐
                         ↓             ↓
                  [Transcript]    [Azure TTS]
                  role: "assistant"     ↓
                         ↓         MP3 Audio
                    Frontend UI        ↓
                                  Frontend Playback
                                       ↓
                                  Resume Listening
```

---

## Error Handling

### Azure Authentication Error (401)

**Symptom:**
```
[STT] Error Code: AuthenticationFailure
[STT] Error Details: WebSocket upgrade failed: Authentication error (401)
```

**Solution:**
- Update Azure Speech credentials in `appsettings.json`
- Verify Key and Region are correct
- Ensure subscription is active

### No Speech Recognized

**Symptom:**
```
[STT] ✗ No speech could be recognized (NoMatch)
```

**Possible causes:**
- Audio too quiet
- Wrong sample rate
- Network issues
- Background noise

---

## Testing Checklist

- [ ] WebSocket connects successfully
- [ ] Audio chunks sent from frontend (`[Audio] Received X chunks`)
- [ ] Speech recognized by Azure (`[STT] ✓ Recognized: ...`)
- [ ] User transcript appears in UI (blue bubble, right side)
- [ ] AI response text appears in UI (gray bubble, left side)
- [ ] TTS audio plays back
- [ ] Microphone pauses during playback
- [ ] Microphone resumes after playback
- [ ] Status updates correctly (listening → speaking → listening)
- [ ] Conversation history persists during session

---

## Next Steps

- [ ] Add conversation history persistence (database)
- [ ] Implement proper RAG with vector embeddings
- [ ] Add voice activity detection (VAD) for better turn-taking
- [ ] Support multiple languages
- [ ] Add conversation export
- [ ] Implement session recovery on disconnect
