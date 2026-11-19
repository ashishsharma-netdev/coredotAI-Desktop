using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CoreDotAI.Desktop.Services;

/// <summary>
/// Transcribes a user-selected WAV file using Google's Gemini generateContent API.
/// Audio is uploaded only when the user explicitly starts transcription.
/// </summary>
public sealed class GeminiAudioTranscriptionService
{
    private const string Model = "gemini-2.5-flash";
    private const long MaxFileSizeBytes = 14L * 1024 * 1024;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    public async Task<string> TranscribeAsync(string filePath, string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Gemini API key not found. Enter your Google Gemini API key in the AI Provider panel.");
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Select an existing WAV file to transcribe.", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            throw new InvalidOperationException("The selected WAV file is empty.");
        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("This WAV file is larger than 14 MB. Select a shorter recording for Gemini inline transcription.");

        var audioBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Transcribe the speech in this audio accurately. Return only the transcript, preserving the original language. Do not summarize or answer questions in the recording." },
                        new { inline_data = new { mime_type = "audio/wav", data = Convert.ToBase64String(audioBytes) } }
                    }
                }
            },
            generationConfig = new { temperature = 0.0, maxOutputTokens = 8192 }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini transcription failed (HTTP {(int)response.StatusCode}): {ExtractErrorMessage(responseBody)}");

        using var json = JsonDocument.Parse(responseBody);
        if (json.RootElement.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            {
                var textParts = new List<string>();
                foreach (var part in parts.EnumerateArray())
                    if (part.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(textElement.GetString()))
                        textParts.Add(textElement.GetString()!.Trim());
                if (textParts.Count > 0)
                    return string.Join(Environment.NewLine, textParts);
            }
        }
        throw new InvalidOperationException("Gemini returned a response, but no transcript text was found.");
    }

    private static string ExtractErrorMessage(string responseBody)
    {
        try
        {
            using var errorJson = JsonDocument.Parse(responseBody);
            if (errorJson.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                return message.GetString() ?? responseBody;
        }
        catch { }
        return responseBody;
    }
}