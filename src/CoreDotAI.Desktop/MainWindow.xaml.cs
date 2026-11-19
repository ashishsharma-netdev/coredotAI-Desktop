using CoreDotAI.Desktop.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace CoreDotAI.Desktop;

public partial class MainWindow : Window
{
    private readonly AudioCaptureService _audioCaptureService = new();
    private readonly OpenAiTranscriptionService _transcriptionService = new();
    private string? _selectedAudioPath;
    private string? _sessionApiKey;

    public MainWindow()
    {
        InitializeComponent();
        OutputFolderText.Text = $"Local storage: {AudioCaptureService.RecordingsRoot}";
        UpdateStartAvailability();
    }

    private void UseApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyPasswordBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ApiKeyStatusText.Text = "Enter your OpenAI API key first.";
            ApiKeyStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            return;
        }

        _sessionApiKey = key;
        ApiKeyPasswordBox.Clear();
        ApiKeyStatusText.Text = "API key configured for this app session.";
        ApiKeyStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
    }

    private void ConsentCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateStartAvailability();

    private void UpdateStartAvailability()
    {
        if (StartButton is null) return;
        StartButton.IsEnabled = ConsentCheckBox.IsChecked == true &&
            (MicrophoneCheckBox.IsChecked == true || SystemAudioCheckBox.IsChecked == true);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (ConsentCheckBox.IsChecked != true)
        {
            MessageBox.Show("Please confirm that you have permission before capturing audio.", "Permission required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        StartButton.IsEnabled = false;
        SessionStatusText.Text = "Starting audio devices…";
        SessionDetailsText.Text = "Preparing local WAV recordings.";
        try
        {
            var folder = await _audioCaptureService.StartAsync(MicrophoneCheckBox.IsChecked == true, SystemAudioCheckBox.IsChecked == true);
            SessionStatusText.Text = "● RECORDING";
            SessionStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            HeaderStatusText.Text = "● CAPTURING";
            HeaderStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            SessionDetailsText.Text = "Audio is being captured locally. Select Stop capture to end the session.";
            OutputFolderText.Text = $"Saving to: {folder}";
            StopButton.IsEnabled = true;
            SelectAudioButton.IsEnabled = false;
            MicrophoneCheckBox.IsEnabled = false;
            SystemAudioCheckBox.IsEnabled = false;
            ConsentCheckBox.IsEnabled = false;
        }
        catch (Exception ex)
        {
            await _audioCaptureService.StopAsync();
            SessionStatusText.Text = "Capture could not start";
            SessionStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            SessionDetailsText.Text = ex.Message;
            HeaderStatusText.Text = "● READY";
            HeaderStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            UpdateStartAvailability();
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        SessionStatusText.Text = "Stopping…";
        try
        {
            await _audioCaptureService.StopAsync();
            SessionStatusText.Text = "Capture saved";
            SessionStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            SessionDetailsText.Text = "The audio capture session has ended. WAV files are stored locally.";
            HeaderStatusText.Text = "● READY";
            HeaderStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            MicrophoneCheckBox.IsEnabled = true;
            SystemAudioCheckBox.IsEnabled = true;
            ConsentCheckBox.IsEnabled = true;
            SelectAudioButton.IsEnabled = true;
            UpdateStartAvailability();
        }
        catch (Exception ex)
        {
            SessionStatusText.Text = "Error stopping capture";
            SessionDetailsText.Text = ex.Message;
            SelectAudioButton.IsEnabled = true;
        }
    }

    private void SelectAudioButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Select a WAV recording", Filter = "WAV audio (*.wav)|*.wav|All files (*.*)|*.*", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        _selectedAudioPath = dialog.FileName;
        TranscribeButton.IsEnabled = true;
        TranscriptTextBox.Text = $"Selected file: {_selectedAudioPath}\n\nClick Transcribe to upload this file for speech-to-text.";
    }

    private async void TranscribeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedAudioPath)) return;
        var confirm = MessageBox.Show("The selected audio file will be uploaded to OpenAI for transcription. Continue?", "Confirm audio upload", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        TranscribeButton.IsEnabled = false;
        SelectAudioButton.IsEnabled = false;
        TranscriptTextBox.Text = "Transcribing audio… Please wait.";
        try
        {
            var transcript = await _transcriptionService.TranscribeAsync(_selectedAudioPath, _sessionApiKey);
            TranscriptTextBox.Text = transcript.Length == 0 ? "No speech was detected in this recording." : transcript;
            if (transcript.Length > 0)
            {
                var transcriptPath = Path.ChangeExtension(_selectedAudioPath, ".transcript.txt");
                await File.WriteAllTextAsync(transcriptPath, transcript);
                SessionDetailsText.Text = $"Transcription complete. Transcript saved to: {transcriptPath}";
            }
        }
        catch (Exception ex) { TranscriptTextBox.Text = $"Transcription failed:\n{ex.Message}"; }
        finally
        {
            TranscribeButton.IsEnabled = !string.IsNullOrWhiteSpace(_selectedAudioPath);
            SelectAudioButton.IsEnabled = true;
        }
    }

    protected override async void OnClosed(EventArgs e)
    {
        _sessionApiKey = null;
        await _audioCaptureService.StopAsync();
        _audioCaptureService.Dispose();
        base.OnClosed(e);
    }
}