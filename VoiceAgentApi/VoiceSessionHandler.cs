using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class VoiceSessionHandler
{
    private readonly WebSocket _socket;
    private readonly SpeechConfig _speechConfig;
    private readonly ConversationService _conversationService;
    private readonly PushAudioInputStream _pushStream;
    private readonly SpeechRecognizer _recognizer;
    //private readonly TranslationRecognizer _recognizer2;
    private string _lastRecognizedText = string.Empty;
    private readonly SemaphoreSlim _recognitionLock = new SemaphoreSlim(1, 1);
    private bool _isRecognitionActive = false;

    public VoiceSessionHandler(WebSocket socket, IConfiguration configuration)
    {
        _socket = socket;
        var key = configuration["AzureSpeech:Key"] ?? throw new ArgumentNullException("AzureSpeech:Key");
        var region = configuration["AzureSpeech:Region"] ?? throw new ArgumentNullException("AzureSpeech:Region");
        _speechConfig = SpeechConfig.FromSubscription(key, region);        
        _speechConfig.SpeechRecognitionLanguage = "bg-BG";
        _speechConfig.SpeechSynthesisLanguage = "bg-BG";
        //_speechConfig.SetProperty(PropertyId.SpeechServiceConnection_TranslationToLanguages, "bg-BG");
        _speechConfig.SetProperty(PropertyId.Speech_SegmentationStrategy, "Sentence");
        //_speechConfig.SetProperty(PropertyId.AudioConfig_PlaybackBufferLengthInMs, "2000");


        // Set output format for TTS
        _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);

        _conversationService = new ConversationService(configuration);

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
                Console.WriteLine($"[STT] Recognizing: {e.Result.Text}");
            }
        };

        _recognizer.Recognized += async (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
            {
                Console.WriteLine($"[STT] ✓ Recognized: {e.Result.Text}");
                _lastRecognizedText = e.Result.Text;

                // Process the recognized text
                await ProcessRecognizedTextAsync(e.Result.Text);
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine("[STT] ✗ No speech could be recognized (NoMatch)");
                var noMatch = NoMatchDetails.FromResult(e.Result);
                Console.WriteLine($"[STT] NoMatch Reason: {noMatch.Reason}");
            }
        };

        _recognizer.Canceled += (s, e) =>
        {
            Console.WriteLine($"[STT] ⚠ Recognition canceled: {e.Reason}");
            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"[STT] Error Code: {e.ErrorCode}");
                Console.WriteLine($"[STT] Error Details: {e.ErrorDetails}");
            }
        };

        _recognizer.SessionStarted += (s, e) =>
        {
            Console.WriteLine("[STT] Recognition session STARTED");
        };

        _recognizer.SessionStopped += (s, e) =>
        {
            Console.WriteLine("[STT] Recognition session STOPPED");
        };
    }

    public async Task HandleSessionAsync()
    {
        try
        {
            Console.WriteLine("[Session] Starting new voice session...");

            // Start continuous recognition
            await _recognizer.StartContinuousRecognitionAsync();
            Console.WriteLine("[Session] Continuous recognition started, waiting for audio...");

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
            Console.WriteLine($"Error in HandleSessionAsync: {ex.Message}");
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
            // Not a JSON message, ignore
            Console.WriteLine($"Received non-JSON text message: {text}");
        }
    }

    private int _audioChunksReceived = 0;
    private int _totalBytesReceived = 0;

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
                Console.WriteLine($"[Audio] Received {_audioChunksReceived} chunks, {_totalBytesReceived} bytes total (latest: {audioChunk.Length} bytes)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling audio chunk: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task ProcessRecognizedTextAsync(string userText)
    {
        //await _recognitionLock.WaitAsync();
        try
        {
            Console.WriteLine($"Processing user text: {userText}");
            var correctedText = await _conversationService.CorrectUserTextAsync(userText);
            Console.WriteLine($"Corrected user text: {correctedText}");

            // Stop recognition to prevent echo while agent is speaking
            Console.WriteLine("Pausing speech recognition...");
            await _recognizer.StopContinuousRecognitionAsync();
            _isRecognitionActive = false;

            // Get response from conversation service
            string answer = await _conversationService.ProcessUserTextAsync(correctedText);

            // Send status update to client
            await SendStatusAsync("speaking");

            // Convert answer to speech and send to client
            await PerformTextToSpeechAndSendAsync(answer);

            // Resume recognition after speaking
            Console.WriteLine("Resuming speech recognition...");
            await _recognizer.StartContinuousRecognitionAsync();
            _isRecognitionActive = true;

            // Send status update to client
            await SendStatusAsync("listening");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing recognized text: {ex.Message}");
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
                    Console.WriteLine($"Error resuming recognition: {resumeEx.Message}");
                }
            }
        }
        finally
        {
            //_recognitionLock.Release();
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
                Console.WriteLine($"TTS completed, sending {result.AudioData.Length} bytes");
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
                Console.WriteLine($"TTS canceled: {cancellation.Reason}, {cancellation.ErrorDetails}");
                await SendErrorAsync($"TTS error: {cancellation.ErrorDetails}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in TTS: {ex.Message}");
            await SendErrorAsync($"TTS error: {ex.Message}");
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
            Console.WriteLine($"Error sending status: {ex.Message}");
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
            Console.WriteLine($"Error sending error message: {ex.Message}");
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
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }
}
