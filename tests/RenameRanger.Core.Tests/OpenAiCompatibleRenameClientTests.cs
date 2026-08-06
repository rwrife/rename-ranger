using System.Net;
using System.Net.Http;
using System.Text;
using RenameRanger.Core.Ai;

namespace RenameRanger.Core.Tests;

public class OpenAiCompatibleRenameClientTests
{
    [Fact]
    public async Task SuggestNameOrFallbackAsync_ReturnsFallback_WhenProbeEndpointIsUnreachable()
    {
        using var httpClient = new HttpClient(new ThrowingHttpMessageHandler());
        var client = new OpenAiCompatibleRenameClient(httpClient);

        var result = await client.SuggestNameOrFallbackAsync(
            endpointUrl: "http://127.0.0.1:11434",
            model: "qwen2.5:1.5b",
            request: BuildRequest(),
            fallbackName: "rule_based_name");

        Assert.True(result.UsedFallback);
        Assert.Equal("rule_based_name", result.SuggestedName);
        Assert.Contains("unreachable", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestNameOrFallbackAsync_ReturnsFallback_WhenChatCompletionFails()
    {
        var handler = new RoutingHttpMessageHandler(request =>
        {
            if (request.RequestUri is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleRenameClient(httpClient);

        var result = await client.SuggestNameOrFallbackAsync(
            endpointUrl: "http://127.0.0.1:11434",
            model: "qwen2.5:1.5b",
            request: BuildRequest(),
            fallbackName: "fallback_from_rules");

        Assert.True(result.UsedFallback);
        Assert.Equal("fallback_from_rules", result.SuggestedName);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SuggestNameOrFallbackAsync_ReturnsModelSuggestion_WhenEndpointIsReachable()
    {
        var handler = new RoutingHttpMessageHandler(request =>
        {
            if (request.RequestUri is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "content": "Quarterly_Report_2026.pdf"
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleRenameClient(httpClient);

        var result = await client.SuggestNameOrFallbackAsync(
            endpointUrl: "http://127.0.0.1:11434",
            model: "qwen2.5:1.5b",
            request: BuildRequest(),
            fallbackName: "fallback_name");

        Assert.False(result.UsedFallback);
        Assert.Equal("Quarterly_Report_2026", result.SuggestedName);
        Assert.Null(result.FailureReason);
    }

    private static AiRenameRequest BuildRequest()
    {
        return new AiRenameRequest(
            OriginalFileName: "scan0042.txt",
            OriginalName: "scan0042",
            Extension: ".txt",
            Metadata: new Dictionary<string, string?>
            {
                ["file:size"] = "1024",
                ["file:modified"] = "2026-08-06T12:00:00Z",
            },
            TextSnippet: "Invoice for ACME Corp, Q2 2026.");
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("connection refused");
        }
    }

    private sealed class RoutingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _route;

        public RoutingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> route)
        {
            _route = route;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_route(request));
        }
    }
}
