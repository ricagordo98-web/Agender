using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agender.Auth;
using Agender.Shared.Services;

namespace Agender.Api;

public abstract class ApiClientBase(IHttpClientFactory httpClientFactory, AuthSession session)
{
    public const string HttpClientName = "agender-api";

    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    protected Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        ExecuteAsync(method, path, body, async response =>
            await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new PersistenceException("The server returned an empty response."), cancellationToken, timeout);

    protected Task SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken) =>
        ExecuteAsync<object?>(method, path, body, _ => Task.FromResult<object?>(null), cancellationToken, null);

    private async Task<T> ExecuteAsync<T>(HttpMethod method, string path, object? body, Func<HttpResponseMessage, Task<T>> read, CancellationToken cancellationToken, TimeSpan? timeout)
    {
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        if (timeout is not null)
        {
            client.Timeout = timeout.Value;
        }

        using HttpRequestMessage request = new(method, $"{session.ServerUrl}{path}");

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        HttpResponseMessage response;

        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ServerUnavailableException("The server could not be reached.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await ToExceptionAsync(response, cancellationToken);
            }

            return await read(response);
        }
    }

    private static async Task<Exception> ToExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string detail = await ReadDetailAsync(response, cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new ArgumentException(detail),
            HttpStatusCode.NotFound => new NotFoundException(detail),
            HttpStatusCode.Unauthorized => new UnauthorizedAccessException(detail),
            _ => new PersistenceException($"The server responded with {(int)response.StatusCode}.")
        };
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            ProblemBody? problem = await response.Content.ReadFromJsonAsync<ProblemBody>(JsonOptions, cancellationToken);
            return problem?.Detail ?? problem?.Title ?? response.ReasonPhrase ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return response.ReasonPhrase ?? string.Empty;
        }
    }

    private sealed record ProblemBody(string? Title, string? Detail);
}
