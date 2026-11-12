using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Agender.Shared.Contracts;

namespace Agender.Infrastructure.Services;

public class WhatsAppSender(HttpClient http)
{
    private const int MaxErrorBody = 160;

    public async Task<List<WhatsAppGroupDto>> FetchGroupsAsync(string apiUrl, string instance, string apiKey, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"{apiUrl.TrimEnd('/')}/group/fetchAllGroups/{Uri.EscapeDataString(instance)}?getParticipants=false");
        request.Headers.Add("apikey", apiKey);
        using HttpResponseMessage response = await SendCoreAsync(apiUrl, request, cancellationToken);
        await EnsureSuccessAsync(response, "recusou o pedido dos grupos", cancellationToken);

        List<GroupResponse> groups = await response.Content.ReadFromJsonAsync<List<GroupResponse>>(cancellationToken) ?? [];

        return [.. groups.Where(g => !string.IsNullOrWhiteSpace(g.Id))
            .Select(g => new WhatsAppGroupDto(g.Id!, string.IsNullOrWhiteSpace(g.Subject) ? g.Id! : g.Subject))
            .OrderBy(g => g.Name == g.Id)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task SendAsync(string apiUrl, string instance, string apiKey, string destination, string text, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"{apiUrl.TrimEnd('/')}/message/sendText/{Uri.EscapeDataString(instance)}");
        request.Headers.Add("apikey", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new { number = destination, text }), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await SendCoreAsync(apiUrl, request, cancellationToken);
        await EnsureSuccessAsync(response, "recusou a mensagem", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(string apiUrl, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ArgumentException($"Não foi possível ligar à Evolution API em {apiUrl} ({ex.Message}).", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ArgumentException("A Evolution API não respondeu a tempo.", ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string failure, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        body = body.Length > MaxErrorBody ? body[..MaxErrorBody] + "…" : body;
        throw new ArgumentException($"A Evolution API {failure} ({(int)response.StatusCode}). {body}".Trim());
    }

    private sealed record GroupResponse(string? Id, string? Subject);
}
