namespace ReadyToGoTravel.Support.Storage;

public sealed class SupportStorageOptions
{
    public const string SectionName = "Support:Storage";

    public string ServiceUrl { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "ap-southeast-2";

    public bool ForcePathStyle { get; set; } = true;
}
