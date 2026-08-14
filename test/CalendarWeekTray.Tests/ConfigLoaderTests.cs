using Xunit;

namespace CalendarWeekTray.Tests;

/// <summary>
/// Spec §11.2 item 6's config assertions: an unknown key is rejected, an unknown value is rejected,
/// and an absent file yields <c>(auto, auto)</c>. Resolution always runs over an explicit path list
/// (never the real profile) so the suite cannot see, or collide with, a config.json a developer
/// actually has on disk.
/// </summary>
public class ConfigLoaderTests
{
    [Fact]
    public void UnknownKeyIsRejected()
    {
        string path = WriteTempConfig("""{ "language": "auto", "extra": "nope" }""");
        try
        {
            ConfigLoadResult result = ConfigLoader.Load([path, MissingPath()]);

            Assert.NotNull(result.Fault);
            Assert.Equal(new AppConfig(), result.Config);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnknownValueIsRejected()
    {
        string path = WriteTempConfig("""{ "theme": "drak" }""");
        try
        {
            ConfigLoadResult result = ConfigLoader.Load([path, MissingPath()]);

            Assert.NotNull(result.Fault);
            Assert.Equal(new AppConfig(), result.Config);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidConfigParsesCaseInsensitively()
    {
        string path = WriteTempConfig("""{ "language": "DE", "theme": "Dark" }""");
        try
        {
            ConfigLoadResult result = ConfigLoader.Load([path, MissingPath()]);

            Assert.Null(result.Fault);
            Assert.Equal(Language.De, result.Config.Language);
            Assert.Equal(Theme.Dark, result.Config.Theme);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AbsentFileYieldsAutoAuto()
    {
        ConfigLoadResult result = ConfigLoader.Load([MissingPath(), MissingPath()]);

        Assert.Null(result.Fault);
        Assert.Equal(Language.Auto, result.Config.Language);
        Assert.Equal(Theme.Auto, result.Config.Theme);
    }

    private static string WriteTempConfig(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string MissingPath() =>
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-missing.json");
}
