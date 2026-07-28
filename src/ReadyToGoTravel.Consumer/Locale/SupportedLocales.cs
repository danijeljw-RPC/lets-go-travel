namespace ReadyToGoTravel.Consumer.Locale;

internal static class SupportedLocales
{
    public const string Default = "en-AU";

    public static IReadOnlyList<string> All { get; } = [Default];

    public static bool Contains(string locale) =>
        All.Contains(locale, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string locale) =>
        All.Single(value => string.Equals(value, locale, StringComparison.OrdinalIgnoreCase));
}
