using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace CoreDotAI.Desktop.Services;

/// <summary>
/// Maintains a Gemini Live transcription WebSocket session.
/// Audio supplied to SendAudioAsync must be mono, signed 16-bit PCM, 16 kHz, little-endian.
/// </summary>
public sealed class GeminiLiveTranscriptionService : IAsyncDisposable
{
    private const string Model = "gemini-3.5-transcribe-live";
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveCancellation;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly TaskCompletionSource _setupCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;

    public event EventHandler<string>? InterimTranscriptReceived;
    public event EventHandler<string>? FinalTranscriptReceived;
    public event EventHandler<string>? ConnectionError;

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public async Task ConnectAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Enter your Gemini API key before starting live transcription.");
        if (IsConnected)
            throw new InvalidOperationException("A Gemini Live transcription session is already connected.");

        _socket?.Dispose();
        _socket = new ClientWebSocket();
        _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        _receiveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var url = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=" + Uri.EscapeDataString(apiKey);
        await _socket.ConnectAsync(new Uri(url), cancellationToken);

        _receiveTask = ReceiveLoopAsync(_receiveCancellation.Token);
        var setup = new
        {
            setup = new
            {
                model = $"models/{Model}",
                generationConfig = new { responseModalities = new[] { "TEXT" } },
                inputAudioTranscription = new { languageCodes = Array.Empty<string>() }
            }
        };
        await SendJsonAsync(JsonSerializer.Serialize(setup), cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await _setupCompleted.Task.WaitAsync(timeout.Token);
        }
        catch
        {
            await DisconnectAsync();
            throw new TimeoutException("Gemini Live did not confirm session setup within 20 seconds. Check API access, model availability, and network connectivity.");
        }
    }

    public async Task SendAudioAsync(byte[] pcm16Mono16Khz, CancellationToken cancellationToken = default)
    {
        if (pcm16Mono16Khz is null || pcm16Mono16Khz.Length == 0) return;
        if (!IsConnected)
            throw new InvalidOperationException("Gemini Live transcription is not connected.");

        var message = new
        {
            realtimeInput = new
            {
                audio = new
                {
                    data = Convert.ToBase64String(pcm16Mono16Khz),
                    mimeType = "audio/pcm;rate=16000"
                }
            }
        };
        await SendJsonAsync(JsonSerializer.Serialize(message), cancellationToken);
    }

    public async Task EndAudioStreamAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return;
        await SendJsonAsync("{\"realtimeInput\":{\"audioStreamEnd\":true}}", cancellationToken);
    }

    private async Task SendJsonAsync(string json, CancellationToken cancellationToken)
    {
        var socket = _socket ?? throw new InvalidOperationException("Gemini Live socket is not initialized.");
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (socket.State != WebSocketState.Open)
                throw new WebSocketException("Gemini Live WebSocket is not open.");
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally { _sendLock.Release(); }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var socket = _socket;
        if (socket is null) return;
        var buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    if (result.MessageType == WebSocketMessageType.Text)
                        message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (message.Length == 0) continue;
                using var json = JsonDocument.Parse(message.ToArray());
                var root = json.RootElement;
                if (root.TryGetProperty("setupComplete", out _))
                    _setupCompleted.TrySetResult();

                if (!root.TryGetProperty("serverContent", out var content)) continue;
                if (content.TryGetProperty("interimInputTranscription", out var interim) &&
                    interim.TryGetProperty("text", out var interimText) &&
                    !string.IsNullOrWhiteSpace(interimText.GetString()))
                    InterimTranscriptReceived?.Invoke(this, interimText.GetString()!);

                if (content.TryGetProperty("inputTranscription", out var final) &&
                    final.TryGetProperty("text", out var finalText) &&
                    !string.IsNullOrWhiteSpace(finalText.GetString()))
                    FinalTranscriptReceived?.Invoke(this, finalText.GetString()!);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ConnectionError?.Invoke(this, ex.Message);
            _setupCompleted.TrySetException(ex);
        }
    }

    public async Task DisconnectAsync()
    {
        var socket = _socket;
        if (socket is not null && socket.State == WebSocketState.Open)
        {
            try { await EndAudioStreamAsync(); } catch { }
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User stopped transcription", CancellationToken.None); } catch { }
        }
        if (_receiveCancellation is not null)
        {
            await _receiveCancellation.CancelAsync();
            _receiveCancellation.Dispose();
            _receiveCancellation = null;
        }
        if (_receiveTask is not null)
        {
            try { await _receiveTask.WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
            _receiveTask = null;
        }
        _socket?.Dispose();
        _socket = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync();
        _sendLock.Dispose();
        _disposed = true;
    }
}