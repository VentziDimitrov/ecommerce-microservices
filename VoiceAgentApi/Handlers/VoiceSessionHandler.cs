using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VoiceAgentApi.Services.Interfaces;

namespace VoiceAgentApi.Handlers;

/// <summary>
/// Handles WebSocket voice session lifecycle
/// </summary>
public class VoiceSessionHandler
{
    private readonly WebSocket _socket;
    private readonly SpeechConfig _speechConfig;
    private readonly IConversationService _conversationService;
    private readonly PushAudioInputStream _pushStream;
    private readonly SpeechRecognizer _recognizer;
    private readonly ILogger<VoiceSessionHandler> _logger;
    private string _lastRecognizedText = string.Empty;
    private readonly SemaphoreSlim _recognitionLock = new SemaphoreSlim(1, 1);
    private bool _isRecognitionActive = false;
    private int _audioChunksReceived = 0;
    private int _totalBytesReceived = 0;

    public VoiceSessionHandler(
        WebSocket socket,
        IConfiguration configuration,
        IConversationService conversationService,
        ILogger<VoiceSessionHandler> logger)
    {
        _socket = socket;
        _conversationService = conversationService;
        _logger = logger;

        var key = configuration["AzureSpeech:Key"] ?? throw new ArgumentNullException("AzureSpeech:Key");
        var region = configuration["AzureSpeech:Region"] ?? throw new ArgumentNullException("AzureSpeech:Region");
        _speechConfig = SpeechConfig.FromSubscription(key, region);
        _speechConfig.SpeechRecognitionLanguage = "bg-BG";
        _speechConfig.SpeechSynthesisLanguage = "bg-BG";
        _speechConfig.SetProperty(PropertyId.Speech_SegmentationStrategy, "Sentence");

        // Set output format for TTS
        _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);

        // Initialize push stream and recognizer for streaming STT
        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
        _pushStream = AudioInputStream.CreatePushStream(audioFormat);
        var audioConfig = AudioConfig.FromStreamInput(_pushStream);

        _recognizer = new SpeechRecognizer(_speechConfig, audioConfig);

        SetupRecognitionEvents();
    }

    private void SetupRecognitionEvents()
    {
        _recognizer.Recognizing += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizingSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                _logger.LogDebug("[STT] Recognizing: {Text}", e.Result.Text);
            }
        };

        _recognizer.Recognized += async (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                _logger.LogInformation("[STT] ✓ Recognized: {Text}", e.Result.Text);
                _lastRecognizedText = e.Result.Text;

                // Process the recognized text
                await ProcessRecognizedTextAsync(e.Result.Text);
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                _logger.LogWarning("[STT] ✗ No speech could be recognized (NoMatch)");
                var noMatch = NoMatchDetails.FromResult(e.Result);
                _logger.LogWarning("[STT] NoMatch Reason: {Reason}", noMatch.Reason);
            }
        };

        _recognizer.Canceled += (s, e) =>
        {
            _logger.LogWarning("[STT] ⚠ Recognition canceled: {Reason}", e.Reason);
            if (e.Reason == CancellationReason.Error)
            {
                _logger.LogError("[STT] Error Code: {ErrorCode}", e.ErrorCode);
                _logger.LogError("[STT] Error Details: {ErrorDetails}", e.ErrorDetails);
            }
        };

        _recognizer.SessionStarted += (s, e) =>
        {
            _logger.LogInformation("[STT] Recognition session STARTED");
        };

        _recognizer.SessionStopped += (s, e) =>
        {
            _logger.LogInformation("[STT] Recognition session STOPPED");
        };
    }

    public async Task HandleSessionAsync()
    {
        try
        {
            _logger.LogInformation("[Session] Starting new voice session...");

            // Start continuous recognition
            await _recognizer.StartContinuousRecognitionAsync();
            _logger.LogInformation("[Session] Continuous recognition started, waiting for audio...");

            var buffer = new byte[8192];
            while (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var audioChunk = buffer.Take(result.Count).ToArray();
                    await HandleAudioChunkAsync(audioChunk);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleTextMessageAsync(text);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleSessionAsync");
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private async Task HandleTextMessageAsync(string text)
    {
        try
        {
            var message = JsonSerializer.Deserialize<JsonElement>(text);
            if (message.TryGetProperty("type", out var type) && type.GetString() == "endSession")
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning("Received non-JSON text message: {Text}", text);
        }
    }

    private Task HandleAudioChunkAsync(byte[] audioChunk)
    {
        try
        {
            // Frontend now sends PCM directly (16kHz, 16-bit, mono) via AudioWorklet
            // No conversion needed - push directly to the recognizer
            _pushStream.Write(audioChunk);

            _audioChunksReceived++;
            _totalBytesReceived += audioChunk.Length;

            // Log every 50 chunks for diagnostics
            if (_audioChunksReceived % 50 == 0)
            {
                _logger.LogDebug("[Audio] Received {ChunkCount} chunks, {TotalBytes} bytes total (latest: {LatestSize} bytes)",
                    _audioChunksReceived, _totalBytesReceived, audioChunk.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling audio chunk");
        }

        return Task.CompletedTask;
    }

    private async Task ProcessRecognizedTextAsync(string userText)
    {
        try
        {
            _logger.LogInformation("Processing user text: {UserText}", userText);

            // Send recognized text to frontend for display
            await SendTranscriptAsync(userText, "user");

            var correctedText = await _conversationService.CorrectUserTextAsync(userText);
            _logger.LogInformation("Corrected user text: {CorrectedText}", correctedText);

            // Stop recognition to prevent echo while agent is speaking
            _logger.LogInformation("Pausing speech recognition...");
            await _recognizer.StopContinuousRecognitionAsync();
            _isRecognitionActive = false;

            // Get response from conversation service
            string answer = await _conversationService.ProcessUserTextAsync(correctedText);

            // Send AI response text to frontend for display
            await SendTranscriptAsync(answer, "assistant");

            // Send status update to client
            await SendStatusAsync("speaking");

            // Convert answer to speech and send to client
            await PerformTextToSpeechAndSendAsync(answer);

            // Resume recognition after speaking
            _logger.LogInformation("Resuming speech recognition...");
            await _recognizer.StartContinuousRecognitionAsync();
            _isRecognitionActive = true;

            // Send status update to client
            await SendStatusAsync("listening");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing recognized text");
            await SendErrorAsync($"Processing error: {ex.Message}");

            // Make sure to resume recognition even if there was an error
            if (!_isRecognitionActive)
            {
                try
                {
                    await _recognizer.StartContinuousRecognitionAsync();
                    _isRecognitionActive = true;
                }
                catch (Exception resumeEx)
                {
                    _logger.LogError(resumeEx, "Error resuming recognition");
                }
            }
        }
    }

    private async Task PerformTextToSpeechAndSendAsync(string answerText)
    {
        try
        {
            using var synthesizer = new SpeechSynthesizer(_speechConfig, null);
            using var result = await synthesizer.SpeakTextAsync(answerText);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                _logger.LogInformation("TTS completed, sending {Size} bytes", result.AudioData.Length);
                await _socket.SendAsync(
                    new ArraySegment<byte>(result.AudioData),
                    WebSocketMessageType.Binary,
                    true,
                    CancellationToken.None
                );
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                _logger.LogError("TTS canceled: {Reason}, {ErrorDetails}", cancellation.Reason, cancellation.ErrorDetails);
                await SendErrorAsync($"TTS error: {cancellation.ErrorDetails}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TTS");
            await SendErrorAsync($"TTS error: {ex.Message}");
        }
    }

    private async Task SendTranscriptAsync(string text, string role)
    {
        try
        {
            var message = JsonSerializer.Serialize(new { type = "transcript", text, role });
            var bytes = Encoding.UTF8.GetBytes(message);
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
                _logger.LogInformation("[WebSocket] Sent transcript ({Role}): {Text}", role, text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transcript");
        }
    }

    private async Task SendStatusAsync(string status)
    {
        try
        {
            var message = JsonSerializer.Serialize(new { type = "status", status });
            var bytes = Encoding.UTF8.GetBytes(message);
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending status");
        }
    }

    private async Task SendErrorAsync(string error)
    {
        try
        {
            var message = JsonSerializer.Serialize(new { type = "error", error });
            var bytes = Encoding.UTF8.GetBytes(message);
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending error message");
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            await _recognizer.StopContinuousRecognitionAsync();
            _recognizer.Dispose();
            _pushStream.Close();
            _recognitionLock.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }
}
