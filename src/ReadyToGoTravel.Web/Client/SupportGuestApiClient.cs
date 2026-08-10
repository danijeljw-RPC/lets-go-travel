using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using ReadyToGoTravel.Web.Authentication;

namespace ReadyToGoTravel.Web.Client;

// Sets the forwarded-browser-address headers directly on each request rather than via a
// DelegatingHandler: IHttpClientFactory pools and can reuse a message handler chain built from a
// different logical request's DI scope, so a Scoped dependency read inside a DelegatingHandler is
// not reliably the current circuit's instance. The typed client itself, unlike its handler chain,
// is always resolved from the calling (circuit) scope, so reading GuestClientAddressAccessor here
// is safe.
public class SupportGuestApiClient(HttpClient client, GuestClientAddressAccessor clientAddress, IConfiguration configuration)
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    private readonly string? internalCallerSecret = configuration["Support:InternalCallerSecret"];

    public virtual async Task<SupportTicketDetail?> GetTicketAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/guest/ticket");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            AddForwardingHeaders(request);
            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<SupportTicketDetail>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public virtual async Task<SupportTicketDetail?> ReplyAsync(string token, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/support/guest/ticket/messages")
            {
                Content = JsonContent.Create(new { body }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            AddForwardingHeaders(request);
            using var response = await client.SendAsync(request, cancellationToken);
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
        string token,
        Guid messageId,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream(MaxAttachmentBytes, cancellationToken);
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "file", file.Name);
            content.Add(new StringContent(messageId.ToString()), "messageId");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/support/guest/ticket/attachments")
            {
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            AddForwardingHeaders(request);
            using var response = await client.SendAsync(request, cancellationToken);
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
        string token,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/api/v1/support/guest/ticket/attachments/{attachmentId}/download");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            AddForwardingHeaders(request);
            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<SupportDownloadUrl>(cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private void AddForwardingHeaders(HttpRequestMessage request)
    {
        var address = clientAddress.ClientAddress;
        if (address is null || string.IsNullOrEmpty(internalCallerSecret))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation("X-Rtgt-Forwarded-Client-Ip", address.ToString());
        request.Headers.TryAddWithoutValidation("X-Rtgt-Internal-Caller-Secret", internalCallerSecret);
    }
}
