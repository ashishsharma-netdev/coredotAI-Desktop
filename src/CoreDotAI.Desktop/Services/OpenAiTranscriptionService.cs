using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CoreDotAI.Desktop.Services;

public sealed class OpenAiTranscriptionService
{
    private const long MaxFileSizeBytes = 25L * 1024 * 1024;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<string> TranscribeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Select an existing audio file to transcribe.", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            throw new InvalidOperationException("The selected audio file is empty.");
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("The selected file is larger than 25 MB. Select a shorter recording.");

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key not found. Set the OPENAI_API_KEY user environment variable, then restart CoreDotAI.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        await using var audioStream = File.OpenRead(filePath);
        using var multipart = new MultipartFormDataContent();
        using var audioContent = new StreamContent(audioStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(audioContent, "file", Path.GetFileName(filePath));
        multipart.Add(new StringContent("gpt-transcribe"), "model");
        request.Content = multipart;

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = responseBody;
            try
            {
                using var errorJson = JsonDocument.Parse(responseBody);
                message = errorJson.RootElement.GetProperty("error").GetProperty("message").GetString() ?? responseBody;
            }
            catch { }
            throw new HttpRequestException($"Transcription request failed ({(int)response.StatusCode}): {message}");
        }

        using var json = JsonDocument.Parse(responseBody);
        if (!json.RootElement.TryGetProperty("text", out var textElement))
            throw new InvalidOperationException("The transcription response did not contain transcript text.");
        return textElement.GetString()?.Trim() ?? string.Empty;
    }
}