using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CoreDotAI.Desktop.Services;

/// <summary>
/// Generates interview-preparation answers using the Gemini Developer API.
/// The API key is supplied by the UI and is never persisted by this service.
/// </summary>
public sealed class GeminiInterviewService
{
    private const string Model = "gemini-2.5-flash";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public async Task<string> GenerateAnswerAsync(
        string apiKey,
        string question,
        string? jobRole = null,
        string? candidateContext = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Enter and configure your Gemini API key in the AI Provider section.");
        if (string.IsNullOrWhiteSpace(question))
            throw new InvalidOperationException("Enter an interview question first.");

        var prompt = BuildPrompt(question, jobRole, candidateContext);
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.4,
                maxOutputTokens = 700
            }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = ExtractErrorMessage(responseBody);
            throw new HttpRequestException($"Gemini API returned HTTP {(int)response.StatusCode}: {message}");
        }

        using var json = JsonDocument.Parse(responseBody);
        if (json.RootElement.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (candidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                var textParts = new List<string>();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        var value = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            textParts.Add(value.Trim());
                    }
                }

                if (textParts.Count > 0)
                    return string.Join(Environment.NewLine, textParts);
            }

            if (candidate.TryGetProperty("finishReason", out var finishReason))
                throw new InvalidOperationException($"Gemini did not return answer text. Finish reason: {finishReason.GetString()}.");
        }

        throw new InvalidOperationException("Gemini returned a successful response, but no answer text was found.");
    }

    private static string BuildPrompt(string question, string? jobRole, string? candidateContext)
    {
        var role = string.IsNullOrWhiteSpace(jobRole) ? "Not specified" : jobRole.Trim();
        var context = string.IsNullOrWhiteSpace(candidateContext)
            ? "No candidate background supplied. Do not invent personal experience, employers, qualifications, or achievements."
            : candidateContext.Trim();

        return $"""
            You are CoreDotAI, a professional interview preparation coach.
            Help the candidate prepare an honest, clear, natural interview response.

            Job role: {role}
            Candidate-provided background: {context}
            Interview question: {question.Trim()}

            Instructions:
            - Answer the exact interview question.
            - Use first-person wording as a sample answer the candidate can adapt.
            - Keep the language professional, natural, and easy to speak aloud.
            - Aim for approximately 100 to 160 words unless the question needs less.
            - Use only facts supplied in the candidate background. Never fabricate experience or credentials.
            - If important personal details are missing, use clearly marked placeholders or explain what the candidate should personalize.
            - After the sample answer, add a short "Delivery tip" with one practical speaking suggestion.
            """;
    }

    private static string ExtractErrorMessage(string responseBody)
    {
        try
        {
            using var errorJson = JsonDocument.Parse(responseBody);
            if (errorJson.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message))
                    return message.GetString() ?? responseBody;
                return error.ToString();
            }
        }
        catch { }

        return responseBody;
    }
}
