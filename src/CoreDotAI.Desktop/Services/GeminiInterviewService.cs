using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CoreDotAI.Desktop.Services;

/// <summary>
/// Calls Google's Gemini Interactions API for interview-answer generation.
/// The API key is supplied by the UI and is never persisted by this service.
/// </summary>
public sealed class GeminiInterviewService
{
    private const string Model = "gemini-3.8-flash";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

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
            model = Model,
            input = prompt,
            store = false
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/interactions");
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
            throw new HttpRequestException($"Gemini request failed ({(int)response.StatusCode}): {message}");
        }

        using var json = JsonDocument.Parse(responseBody);
        if (json.RootElement.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(outputText.GetString()))
        {
            return outputText.GetString()!.Trim();
        }

        // Defensive fallback in case the API response exposes text in output steps.
        if (json.RootElement.TryGetProperty("steps", out var steps) &&
            steps.ValueKind == JsonValueKind.Array)
        {
            var pieces = new List<string>();
            foreach (var step in steps.EnumerateArray())
            {
                if (step.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var text) &&
                            text.ValueKind == JsonValueKind.String)
                        {
                            var value = text.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                                pieces.Add(value.Trim());
                        }
                    }
                }
            }

            if (pieces.Count > 0)
                return string.Join(Environment.NewLine, pieces);
        }

        throw new InvalidOperationException("Gemini returned a response, but no answer text was found.");
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
