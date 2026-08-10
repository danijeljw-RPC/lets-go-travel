using System.Text;

namespace ReadyToGoTravel.Support.Storage;

public abstract record AttachmentTypeValidationResult
{
    public sealed record Accepted(string Extension) : AttachmentTypeValidationResult;

    public sealed record Rejected(string Reason) : AttachmentTypeValidationResult;
}

public static class AttachmentTypeValidator
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly Dictionary<string, string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["text/plain"] = ".txt",
    };

    public static AttachmentTypeValidationResult Validate(string declaredContentType, ReadOnlySpan<byte> leadingBytes)
    {
        if (!AllowedExtensions.TryGetValue(declaredContentType, out var extension))
        {
            return new AttachmentTypeValidationResult.Rejected("attachment_content_type_not_allowed");
        }

        if (leadingBytes.IsEmpty)
        {
            return new AttachmentTypeValidationResult.Rejected("attachment_empty");
        }

        var matches = declaredContentType switch
        {
            "application/pdf" => StartsWith(leadingBytes, PdfSignature),
            "image/jpeg" => StartsWith(leadingBytes, JpegSignature),
            "image/png" => StartsWith(leadingBytes, PngSignature),
            "text/plain" => IsStrictUtf8TextWithoutNul(leadingBytes),
            _ => false,
        };

        return matches
            ? new AttachmentTypeValidationResult.Accepted(extension)
            : new AttachmentTypeValidationResult.Rejected("attachment_content_signature_mismatch");
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> signature) =>
        bytes.Length >= signature.Length && bytes[..signature.Length].SequenceEqual(signature);

    private static bool IsStrictUtf8TextWithoutNul(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IndexOf((byte)0) >= 0)
        {
            return false;
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
