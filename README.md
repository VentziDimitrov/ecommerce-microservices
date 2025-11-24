# Voice Conversational AI Agent

A real-time voice conversational AI agent built with Azure Cognitive Services, featuring speech-to-text, text-to-speech, and LLM-powered responses.

## Architecture

This application consists of:

- **Backend (VoiceAgentApi)**: .NET 8 WebSocket API handling voice communication
  - Azure Speech Services for STT and TTS
  - Conversation service with LLM integration
  - Real-time WebSocket communication

- **Frontend (voice-agent-ui)**: React application
  - Real-time voice capture and playback
  - WebSocket-based bidirectional communication
  - Modern, responsive UI

## Features

- Real-time speech recognition (Speech-to-Text)
- AI-powered conversational responses
- Natural text-to-speech output
- WebSocket-based low-latency communication
- Session management and conversation history
- Extensible RAG (Retrieval Augmented Generation) support

## Prerequisites

### Backend Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Azure Speech Services subscription
- (Optional) Azure OpenAI or other LLM endpoint
- (Optional) Pinecone or other vector database for RAG

### Frontend Requirements
- [Node.js](https://nodejs.org/) (v16 or higher)
- npm or yarn

## Setup Instructions

### 1. Backend Setup

#### a. Navigate to the backend directory
```bash
cd VoiceAgentApi
```

#### b. Configure Azure credentials

Edit `appsettings.json` or create `appsettings.Development.json`:

```json
{
  "AzureSpeech": {
    "Key": "YOUR_AZURE_SPEECH_KEY",
    "Region": "YOUR_AZURE_REGION"
  },
  "LLM": {
    "Endpoint": "YOUR_AZURE_OPENAI_ENDPOINT",
    "ApiKey": "YOUR_AZURE_OPENAI_KEY"
  },
  "Pinecone": {
    "ApiKey": "YOUR_PINECONE_API_KEY",
    "Environment": "YOUR_PINECONE_ENVIRONMENT"
  }
}
```

**How to get Azure Speech credentials:**
1. Go to [Azure Portal](https://portal.azure.com/)
2. Create a "Speech Services" resource
3. Copy the Key and Region from the resource's "Keys and Endpoint" page

**Note:** The LLM service currently expects the endpoint to be in the format:
`https://YOUR_RESOURCE.openai.azure.com/openai/deployments/YOUR_DEPLOYMENT/chat/completions?api-version=2023-05-15`

#### c. Install dependencies and run
```bash
dotnet restore
dotnet run
```

The backend will start on `http://localhost:5000`

### 2. Frontend Setup

#### a. Navigate to the frontend directory
```bash
cd voice-agent-ui
```

#### b. Install dependencies
```bash
npm install
```

#### c. Configure environment variables

Create a `.env` file (use `.env.example` as template):

```bash
cp .env.example .env
```

Edit `.env`:
```
REACT_APP_WS_URL=ws://localhost:5000/voice/ws
```

#### d. Start the development server
```bash
npm start
```

The frontend will open at `http://localhost:3000`

## Usage

1. Open the application in your browser at `http://localhost:3000`
2. Click "Start Voice Session" to begin
3. Grant microphone permissions when prompted
4. Start speaking - the AI will listen and respond
5. Use the Mute button to temporarily disable your microphone
6. Click "End Session" when finished

## Important Notes

### Audio Streaming Architecture

**✅ OPTIMIZED:** Direct PCM streaming using AudioWorklet!

The application now uses a highly efficient audio pipeline:
- **Frontend**: Uses AudioWorklet API to capture raw PCM audio (16kHz, 16-bit, mono)
- **WebSocket**: Streams PCM data directly with minimal latency
- **Backend**: Receives PCM and feeds it directly to Azure Speech Services
- **No conversion needed**: Zero overhead, maximum performance

**Why AudioWorklet?**
- **Lower latency**: No encoding/decoding overhead
- **Better quality**: No compression artifacts
- **Reduced server load**: No CPU spent on audio conversion
- **Scalable**: Backend can handle more concurrent users
- **Native PCM**: Exactly what Azure Speech expects

**Browser Requirements:**
- Chrome 66+, Firefox 76+, Safari 14.1+, Edge 79+
- Requires HTTPS in production (localhost works without HTTPS)

### LLM Configuration

The `LlmService` has a fallback mode - if not configured, it will echo back what you said. To enable full AI responses:

1. Configure Azure OpenAI or another compatible LLM endpoint
2. Update the endpoint format in `LlmService.cs` if using a non-Azure provider

### RAG (Retrieval) Setup

The `RetrievalService` is a placeholder. To implement RAG:

1. Set up a vector database (Pinecone, Qdrant, etc.)
2. Implement embedding generation (Azure OpenAI embeddings)
3. Update `RetrievalService.QueryAsync()` with actual query logic

## API Endpoints

### Backend Endpoints

- `GET /health` - Health check endpoint
- `WS /voice/ws` - WebSocket endpoint for voice sessions

### WebSocket Protocol

**Client → Server:**
- Binary messages: Audio chunks (WebM/Opus)
- Text messages: `{"type": "endSession"}` to end the session

**Server → Client:**
- Binary messages: Audio chunks (MP3 from TTS)
- Text messages:
  - `{"type": "status", "status": "listening|speaking"}`
  - `{"type": "error", "error": "error message"}`
  - `{"type": "transcript", "text": "recognized text"}`

## Project Structure

```
speech_service/
├── VoiceAgentApi/               # Backend .NET API
│   ├── Program.cs               # Application entry point with WebSocket setup
│   ├── VoiceSessionHandler.cs   # Handles WebSocket sessions and audio processing
│   ├── ConversationService.cs   # Manages conversation logic, LLM, and RAG
│   ├── appsettings.json         # Configuration (don't commit secrets!)
│   └── VoiceAgentApi.csproj     # Project file
│
└── voice-agent-ui/              # Frontend React app
    ├── src/
    │   ├── App.js               # Main application component
    │   ├── App.css              # Application styles
    │   └── components/
    │       └── VoiceSession.jsx # Voice session component
    ├── public/
    ├── package.json
    └── .env.example             # Environment variable template
```

## Security Considerations

**⚠️ IMPORTANT:**
- Never commit `appsettings.json` with real API keys to version control
- Use environment variables or Azure Key Vault for production
- The current CORS policy allows all origins - restrict this in production
- Consider implementing authentication for WebSocket connections

## Troubleshooting

### Backend won't start
- Verify .NET 8 SDK is installed: `dotnet --version`
- Check if port 5000 is available
- Verify Azure credentials are correct

### Frontend can't connect
- Ensure backend is running on port 5000
- Check browser console for WebSocket errors
- Verify `.env` file has correct WebSocket URL
- Check CORS configuration if using different domains

### No audio playback
- Check browser console for audio decoding errors
- Verify Azure Speech credentials are valid
- Check that TTS is configured correctly
- Ensure browser has permission to play audio

### Microphone not working
- Grant microphone permissions in browser
- Check browser console for getUserMedia errors
- Verify microphone is not being used by another application

### Speech not recognized
- Speak clearly and at normal volume
- Check Azure Speech Service region matches your subscription
- Verify internet connection is stable
- Note: Audio format conversion may be needed (see above)

## Next Steps

To enhance this application:

1. **Audio Format Fix**: Implement server-side audio conversion from WebM/Opus to PCM
2. **Implement RAG**: Add vector database integration for knowledge-based responses
3. **Add Authentication**: Secure the WebSocket endpoint
4. **Enhance UI**: Add conversation history display, settings panel
5. **Voice Activity Detection**: Implement better end-of-utterance detection
6. **Multi-language Support**: Configure different languages for STT/TTS
7. **Error Handling**: Add reconnection logic and better error messages
8. **Production Deployment**: Deploy to Azure App Service or similar

## Technologies Used

- **Backend**: .NET 8, Azure Speech SDK, WebSockets
- **Frontend**: React, Web Audio API, MediaRecorder API
- **Cloud Services**: Azure Cognitive Services (Speech), Azure OpenAI (optional)
- **Vector DB**: Pinecone (optional)

## License

This project is provided as-is for educational and development purposes.

## Support

For Azure Speech Services issues, visit [Azure Speech Documentation](https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/)

For React and frontend issues, visit [React Documentation](https://react.dev/)
