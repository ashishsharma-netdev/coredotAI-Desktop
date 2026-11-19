using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CoreDotAI.Desktop.Services;

/// <summary>
/// Captures selected Windows audio sources to separate local WAV files.
/// Audio capture is started only after explicit user action in the UI.
/// </summary>
public sealed class AudioCaptureService : IDisposable
{
    private WasapiCapture? _microphoneCapture;
    private WasapiLoopbackCapture? _systemCapture;
    private WaveFileWriter? _microphoneWriter;
    private WaveFileWriter? _systemWriter;
    private readonly object _microphoneLock = new();
    private readonly object _systemLock = new();
    private TaskCompletionSource? _microphoneStopped;
    private TaskCompletionSource? _systemStopped;
    private bool _disposed;

    public static string RecordingsRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoreDotAI", "Recordings");

    public async Task<string> StartAsync(bool captureMicrophone, bool captureSystemAudio)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!captureMicrophone && !captureSystemAudio)
            throw new InvalidOperationException("Select at least one audio source.");

        if (_microphoneCapture is not null || _systemCapture is not null)
            throw new InvalidOperationException("An audio capture session is already active.");

        var sessionFolder = Path.Combine(RecordingsRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(sessionFolder);

        try
        {
            if (captureMicrophone)
                StartMicrophone(Path.Combine(sessionFolder, "microphone.wav"));

            if (captureSystemAudio)
                StartSystemAudio(Path.Combine(sessionFolder, "system-audio.wav"));

            await Task.CompletedTask;
            return sessionFolder;
        }
        catch
        {
            await StopAsync();
            throw;
        }
    }

    private void StartMicrophone(string outputPath)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        _microphoneCapture = new WasapiCapture(device);
        _microphoneWriter = new WaveFileWriter(outputPath, _microphoneCapture.WaveFormat);
        _microphoneStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _microphoneCapture.DataAvailable += (_, args) =>
        {
            lock (_microphoneLock)
                _microphoneWriter?.Write(args.Buffer, 0, args.BytesRecorded);
        };
        _microphoneCapture.RecordingStopped += (_, _) =>
        {
            lock (_microphoneLock)
            {
                _microphoneWriter?.Dispose();
                _microphoneWriter = null;
            }
            _microphoneStopped?.TrySetResult();
        };
        _microphoneCapture.StartRecording();
    }

    private void StartSystemAudio(string outputPath)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _systemCapture = new WasapiLoopbackCapture(device);
        _systemWriter = new WaveFileWriter(outputPath, _systemCapture.WaveFormat);
        _systemStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _systemCapture.DataAvailable += (_, args) =>
        {
            lock (_systemLock)
                _systemWriter?.Write(args.Buffer, 0, args.BytesRecorded);
        };
        _systemCapture.RecordingStopped += (_, _) =>
        {
            lock (_systemLock)
            {
                _systemWriter?.Dispose();
                _systemWriter = null;
            }
            _systemStopped?.TrySetResult();
        };
        _systemCapture.StartRecording();
    }

    public async Task StopAsync()
    {
        var microphone = _microphoneCapture;
        var system = _systemCapture;
        var microphoneStopped = _microphoneStopped?.Task;
        var systemStopped = _systemStopped?.Task;

        try { microphone?.StopRecording(); } catch (InvalidOperationException) { }
        try { system?.StopRecording(); } catch (InvalidOperationException) { }

        var stopTasks = new[] { microphoneStopped, systemStopped }
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();

        if (stopTasks.Length > 0)
            await Task.WhenAll(stopTasks).WaitAsync(TimeSpan.FromSeconds(10));

        microphone?.Dispose();
        system?.Dispose();
        _microphoneCapture = null;
        _systemCapture = null;
        _microphoneStopped = null;
        _systemStopped = null;

        lock (_microphoneLock)
        {
            _microphoneWriter?.Dispose();
            _microphoneWriter = null;
        }
        lock (_systemLock)
        {
            _systemWriter?.Dispose();
            _systemWriter = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopAsync().GetAwaiter().GetResult();
        _disposed = true;
    }
}
