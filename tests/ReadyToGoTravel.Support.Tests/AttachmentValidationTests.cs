using ReadyToGoTravel.Support.Storage;

namespace ReadyToGoTravel.Support.Tests;

public sealed class AttachmentValidationTests
{
    [Fact]
    public void ValidPdfSignatureIsAccepted()
    {
        var bytes = "%PDF-1.7 rest of file"u8.ToArray();

        var result = AttachmentTypeValidator.Validate("application/pdf", bytes);

        var accepted = Assert.IsType<AttachmentTypeValidationResult.Accepted>(result);
        Assert.Equal(".pdf", accepted.Extension);
    }

    [Fact]
    public void ValidJpegSignatureIsAccepted()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        var result = AttachmentTypeValidator.Validate("image/jpeg", bytes);

        Assert.IsType<AttachmentTypeValidationResult.Accepted>(result);
    }

    [Fact]
    public void ValidPngSignatureIsAccepted()
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

        var result = AttachmentTypeValidator.Validate("image/png", bytes);

        Assert.IsType<AttachmentTypeValidationResult.Accepted>(result);
    }

    [Fact]
    public void ValidUtf8TextIsAccepted()
    {
        var bytes = "Hello, this is a plain text support message."u8.ToArray();

        var result = AttachmentTypeValidator.Validate("text/plain", bytes);

        Assert.IsType<AttachmentTypeValidationResult.Accepted>(result);
    }

    [Fact]
    public void APortableExecutableDeclaredAsPdfIsRejected()
    {
        byte[] bytes = [(byte)'M', (byte)'Z', 0x90, 0x00];

        var result = AttachmentTypeValidator.Validate("application/pdf", bytes);

        var rejected = Assert.IsType<AttachmentTypeValidationResult.Rejected>(result);
        Assert.Equal("attachment_content_signature_mismatch", rejected.Reason);
    }

    [Fact]
    public void PngBytesDeclaredAsJpegAreRejected()
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        var result = AttachmentTypeValidator.Validate("image/jpeg", bytes);

        Assert.IsType<AttachmentTypeValidationResult.Rejected>(result);
    }

    [Fact]
    public void TextContainingANulByteIsRejected()
    {
        byte[] bytes = [(byte)'a', (byte)'b', 0x00, (byte)'c'];

        var result = AttachmentTypeValidator.Validate("text/plain", bytes);

        Assert.IsType<AttachmentTypeValidationResult.Rejected>(result);
    }

    [Fact]
    public void ADisallowedDeclaredContentTypeIsRejectedRegardlessOfBytes()
    {
        var bytes = "%PDF-1.7"u8.ToArray();

        var result = AttachmentTypeValidator.Validate("application/x-msdownload", bytes);

        var rejected = Assert.IsType<AttachmentTypeValidationResult.Rejected>(result);
        Assert.Equal("attachment_content_type_not_allowed", rejected.Reason);
    }

    [Fact]
    public void AnEmptyByteSpanIsRejected()
    {
        var result = AttachmentTypeValidator.Validate("application/pdf", ReadOnlySpan<byte>.Empty);

        Assert.IsType<AttachmentTypeValidationResult.Rejected>(result);
    }
}
