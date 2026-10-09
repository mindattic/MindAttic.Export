namespace MindAttic.Export.Tests.Support;

/// <summary>A unique folder under the system temp directory, deleted on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    public static readonly string Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MindAttic.Export.Tests");

    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string Sub(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
                return;
            }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }
}

/// <summary>Base fixture: a fresh <see cref="Temp"/> folder per test, removed in TearDown.</summary>
public abstract class TempDirTestBase
{
    private TempDirectory? temp;

    protected TempDirectory Temp => temp ?? throw new InvalidOperationException("SetUp has not run.");

    [SetUp]
    public void CreateTempDirectory() => temp = new TempDirectory();

    [TearDown]
    public void DeleteTempDirectory()
    {
        temp?.Dispose();
        temp = null;
    }
}
