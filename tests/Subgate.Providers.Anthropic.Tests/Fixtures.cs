namespace Subgate.Providers.Anthropic.Tests;

internal static class Fixtures
{
    public static string Load(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "anthropic", name));
}
