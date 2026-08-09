using System.Net.Http.Json;

namespace ReadyToGoTravel.Web.Client;

public class SupportStaffApiClient(HttpClient client)
{
    public virtual async Task<IReadOnlyList<SupportTicketSummary>> GetTicketsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<SupportTicketSummary[]>("/api/v1/support/staff/tickets", cancellationToken)
                ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    public virtual async Task<SupportTicketDetail?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<SupportTicketDetail>($"/api/v1/support/staff/tickets/{ticketId}", cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public virtual async Task<SupportTicketDetail?> ReplyAsync(Guid ticketId, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/v1/support/staff/tickets/{ticketId}/messages", new { body }, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<SupportTicketDetail>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public virtual async Task<bool> CloseAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsync($"/api/v1/support/staff/tickets/{ticketId}/close", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public virtual async Task<bool> RotateGuestLinkAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsync(
                $"/api/v1/support/staff/tickets/{ticketId}/guest-link/rotate", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public virtual async Task<bool> RevokeGuestLinkAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsync(
                $"/api/v1/support/staff/tickets/{ticketId}/guest-link/revoke", null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
