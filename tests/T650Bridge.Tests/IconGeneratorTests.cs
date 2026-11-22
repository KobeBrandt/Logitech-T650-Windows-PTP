using T650Bridge.UI;

namespace T650Bridge.Tests;

public sealed class IconGeneratorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "T650BridgeTests-" + Guid.NewGuid().ToString("N"));

    public IconGeneratorTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Theory]
    [InlineData(16, true)]
    [InlineData(32, false)]
    public void CreateTouchpadBitmap_ReturnsRequestedSize(int size, bool active)
    {
        using var bmp = IconGenerator.CreateTouchpadBitmap(size, Color.Green, active);

        Assert.Equal(size, bmp.Width);
        Assert.Equal(size, bmp.Height);
    }

    [Fact]
    public void EnsureIcoFileExists_WritesIconWhenMissing()
    {
        string path = Path.Combine(_tempDir, "T650.ico");

        IconGenerator.EnsureIcoFileExists(path);

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void EnsureIcoFileExists_KeepsExistingFile()
    {
        string path = Path.Combine(_tempDir, "T650.ico");
        File.WriteAllText(path, "keep");

        IconGenerator.EnsureIcoFileExists(path);

        Assert.Equal("keep", File.ReadAllText(path));
    }
}
