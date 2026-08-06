using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RenameRanger.Core.Ai;

public sealed class OpenAiCompatibleRenameClient
{
    private const string ModelsPath = "/v1/models";
    private const string ChatCompletionsPath = "/v1/chat/completions";
    private const int MaxSnippetLength = 800;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SuggestTimeout = TimeSpan.FromSeconds(12);

    private readonly HttpClient _httpClient;

    public OpenAiCompatibleRenameClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<bool> ProbeReachabilityAsync(string endpointUrl, CancellationToken cancellationToken = default)
    {
        var modelsUri = BuildEndpointUri(endpointUrl, ModelsPath);
        if (modelsUri is null)
        {
            return false;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            using var response = await _httpClient.GetAsync(modelsUri, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AiNameSuggestion> SuggestNameOrFallbackAsync(
        string endpointUrl,
        string model,
        AiRenameRequest request,
        string fallbackName,
        CancellationToken cancellationToken = default)
    {
        var normalizedFallback = NormalizeSuggestedName(fallbackName, fallbackName);
        if (string.IsNullOrWhiteSpace(normalizedFallback))
        {
            normalizedFallback = "untitled";
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return AiNameSuggestion.Fallback(normalizedFallback, "Model name not configured.");
        }

        if (!await ProbeReachabilityAsync(endpointUrl, cancellationToken).ConfigureAwait(false))
        {
            return AiNameSuggestion.Fallback(normalizedFallback, "Endpoint is unreachable.");
        }

        var chatUri = BuildEndpointUri(endpointUrl, ChatCompletionsPath);
        if (chatUri is null)
        {
            return AiNameSuggestion.Fallback(normalizedFallback, "Invalid endpoint URL.");
        }

        var requestBody = BuildChatRequest(model, request);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(SuggestTimeout);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(chatUri, requestBody, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return AiNameSuggestion.Fallback(normalizedFallback, $"Endpoint returned HTTP {(int)response.StatusCode}.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(responseStream, cancellationToken: cts.Token).ConfigureAwait(false);
            var content = ExtractMessageContent(json.RootElement);
            if (string.IsNullOrWhiteSpace(content))
            {
                return AiNameSuggestion.Fallback(normalizedFallback, "No message content in completion response.");
            }

            var normalized = NormalizeSuggestedName(content, normalizedFallback);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return AiNameSuggestion.Fallback(normalizedFallback, "Model returned an empty/invalid filename.");
            }

            return new AiNameSuggestion(normalized, UsedFallback: false);
        }
        catch (OperationCanceledException)
        {
            return AiNameSuggestion.Fallback(normalizedFallback, "Request timed out.");
        }
        catch (Exception ex)
        {
            return AiNameSuggestion.Fallback(normalizedFallback, ex.Message);
        }
    }

    private static object BuildChatRequest(string model, AiRenameRequest request)
    {
        var metadataJson = request.Metadata.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(request.Metadata);

        var snippet = string.IsNullOrWhiteSpace(request.TextSnippet)
            ? "(none)"
            : request.TextSnippet.Length <= MaxSnippetLength
                ? request.TextSnippet
                : request.TextSnippet[..MaxSnippetLength];

        var userPrompt = $"""
Provide a concise, human-friendly file name stem (without extension) for this file.
Return only the file name stem text, no quotes, no markdown.

Original file name: {request.OriginalFileName}
Current stem: {request.OriginalName}
Extension: {request.Extension}
Metadata (JSON): {metadataJson}
Text snippet:
{snippet}
""";

        return new
        {
            model,
            temperature = 0.2,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You suggest safe Windows file name stems. Reply with only one filename stem and no extension.",
                },
                new
                {
                    role = "user",
                    content = userPrompt,
                },
            },
        };
    }

    private static Uri? BuildEndpointUri(string endpointUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl) ||
            !Uri.TryCreate(endpointUrl, UriKind.Absolute, out var endpointBaseUri))
        {
            return null;
        }

        var normalizedBase = endpointBaseUri.ToString().TrimEnd('/');
        if (normalizedBase.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            var pathWithoutV1 = path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? path[3..]
                : path;
            return new Uri($"{normalizedBase}{pathWithoutV1}");
        }

        return new Uri($"{normalizedBase}{path}");
    }

    private static string? ExtractMessageContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var element in content.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(text);
                    }
                }
            }

            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }

        return null;
    }

    private static string NormalizeSuggestedName(string? rawSuggestion, string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(rawSuggestion))
        {
            return fallbackName;
        }

        var normalized = rawSuggestion.Trim();

        // Some models return JSON, so try extracting a common key.
        if (normalized.StartsWith('{') && normalized.EndsWith('}'))
        {
            try
            {
                using var json = JsonDocument.Parse(normalized);
                if (json.RootElement.TryGetProperty("suggested_name", out var suggested) && suggested.ValueKind == JsonValueKind.String)
                {
                    normalized = suggested.GetString() ?? fallbackName;
                }
                else if (json.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    normalized = name.GetString() ?? fallbackName;
                }
            }
            catch
            {
                // Keep raw text path below.
            }
        }

        normalized = normalized
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? fallbackName;

        normalized = normalized.Trim('"', '\'', '`').Trim();

        if (normalized.Contains('/') || normalized.Contains('\\'))
        {
            normalized = Path.GetFileName(normalized);
        }

        normalized = Path.GetFileNameWithoutExtension(normalized);

        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        normalized = Regex.Replace(builder.ToString(), "\\s+", " ").Trim();
        normalized = normalized.TrimEnd('.');

        return string.IsNullOrWhiteSpace(normalized)
            ? fallbackName
            : normalized;
    }
}
