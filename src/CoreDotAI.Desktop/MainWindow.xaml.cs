using CoreDotAI.Desktop.Services;
using System.Windows;

namespace CoreDotAI.Desktop;

public partial class MainWindow : Window
{
    private readonly AudioCaptureService _audioCaptureService = new();
    private string? _sessionFolder;

    public MainWindow()
    {
        InitializeComponent();
        OutputFolderText.Text = $"Local storage: {AudioCaptureService.RecordingsRoot}";
        UpdateStartAvailability();
    }

    private void ConsentCheckBox_Changed(object sender, RoutedEventArgs e) =>
        UpdateStartAvailability();

    private void UpdateStartAvailability()
    {
        if (StartButton is null) return;
        StartButton.IsEnabled = ConsentCheckBox.IsChecked == true
            && (MicrophoneCheckBox.IsChecked == true || SystemAudioCheckBox.IsChecked == true);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (ConsentCheckBox.IsChecked != true)
        {
            MessageBox.Show("Please confirm that you have permission before capturing audio.",
                "Permission required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StartButton.IsEnabled = false;
        SessionStatusText.Text = "Starting audio devices…";
        SessionDetailsText.Text = "Preparing local WAV recordings.";

        try
        {
            _sessionFolder = await _audioCaptureService.StartAsync(
                MicrophoneCheckBox.IsChecked == true,
                SystemAudioCheckBox.IsChecked == true);

            SessionStatusText.Text = "● RECORDING";
            SessionStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            HeaderStatusText.Text = "● CAPTURING";
            HeaderStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            SessionDetailsText.Text = "Audio is being captured locally. Select Stop capture to end the session.";
            OutputFolderText.Text = $"Saving to: {_sessionFolder}";
            StopButton.IsEnabled = true;
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
        await _audioCaptureService.StopAsync();

        SessionStatusText.Text = "Capture saved";
        SessionStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
        SessionDetailsText.Text = "The audio capture session has ended. Files are stored locally.";
        HeaderStatusText.Text = "● READY";
        HeaderStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
        MicrophoneCheckBox.IsEnabled = true;
        SystemAudioCheckBox.IsEnabled = true;
        ConsentCheckBox.IsEnabled = true;
        UpdateStartAvailability();
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _audioCaptureService.StopAsync();
        _audioCaptureService.Dispose();
        base.OnClosed(e);
    }
}
