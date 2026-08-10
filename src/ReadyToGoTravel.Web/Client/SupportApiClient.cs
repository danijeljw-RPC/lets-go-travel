using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;

namespace ReadyToGoTravel.Web.Client;

public sealed record CreateSupportTicketRequest(
    string? ContactName,
    string? ContactEmail,
    string Category,
    string? BookingReference,
    string Message);

public sealed record SupportMessage(
    Guid Id,
    int SequenceNumber,
    string AuthorType,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record SupportAttachment(
    Guid Id,
    Guid MessageId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string ScanStatus,
    DateTimeOffset CreatedAt);

public sealed record SupportTicketDetail(
    Guid Id,
    string ContactName,
    string ContactEmail,
    string Category,
    bool IsUrgent,
    string? BookingReference,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<SupportMessage> Messages,
    IReadOnlyList<SupportAttachment> Attachments);

public sealed record SupportTicketSummary(
    Guid Id,
    string Category,
    bool IsUrgent,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record SupportDownloadUrl(string Url, DateTimeOffset ExpiresAt);

public class SupportApiClient(HttpClient client)
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    public async Task<SupportTicketDetail?> CreateTicketAsync(
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("/api/v1/support/tickets", request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<SupportTicketDetail>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SupportTicketSummary>> GetTicketsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<SupportTicketSummary[]>("/api/v1/support/tickets", cancellationToken)
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
            return await client.GetFromJsonAsync<SupportTicketDetail>($"/api/v1/support/tickets/{ticketId}", cancellationToken);
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
                $"/api/v1/support/tickets/{ticketId}/messages", new { body }, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<SupportTicketDetail>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public virtual async Task<SupportAttachment?> UploadAttachmentAsync(
        Guid ticketId,
        Guid messageId,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream(MaxAttachmentBytes, cancellationToken);
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "file", file.Name);
            content.Add(new StringContent(messageId.ToString()), "messageId");

            using var response = await client.PostAsync($"/api/v1/support/tickets/{ticketId}/attachments", content, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<SupportAttachment>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            // IOException: IBrowserFile.OpenReadStream throws when the selected file exceeds
            // MaxAttachmentBytes, before any request is sent - must fail the same way an HTTP
            // failure would, not escape and tear down the interactive circuit.
            return null;
        }
    }

    public virtual async Task<SupportDownloadUrl?> GetDownloadUrlAsync(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.GetFromJsonAsync<SupportDownloadUrl>(
                $"/api/v1/support/tickets/{ticketId}/attachments/{attachmentId}/download", cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
