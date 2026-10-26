using System.Net;
using System.Net.Http.Headers;
using Agender.Shared.Abstractions;
using Agender.Shared.State;

namespace Agender.Auth;

public sealed class AuthHeaderHandler(AuthSession session, AgendaScope scope) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = await session.GetAccessTokenAsync();
        request.Headers.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Remove(IAgendaScope.HeaderName);

        if (scope.TargetId is string target)
        {
            request.Headers.TryAddWithoutValidation(IAgendaScope.HeaderName, target);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || token is null)
        {
            return response;
        }

        if (!await session.RefreshAfterUnauthorizedAsync(token))
        {
            return response;
        }

        response.Dispose();
        string? refreshed = await session.GetAccessTokenAsync();
        request.Headers.Authorization = refreshed is null ? null : new AuthenticationHeaderValue("Bearer", refreshed);

        return await base.SendAsync(await CloneAsync(request, cancellationToken), cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(request.Method, request.RequestUri) { Version = request.Version };
        clone.Headers.Authorization = request.Headers.Authorization;

        if (request.Headers.TryGetValues(IAgendaScope.HeaderName, out IEnumerable<string>? scopeValues))
        {
            clone.Headers.TryAddWithoutValidation(IAgendaScope.HeaderName, scopeValues);
        }

        if (request.Content is not null)
        {
            byte[] body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(body);

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
