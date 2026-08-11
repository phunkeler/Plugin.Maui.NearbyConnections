namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// A real temp directory, removed on dispose. Inbound file naming genuinely touches the file
/// system, so these tests use a real directory rather than an abstraction over one.
/// </summary>
/// <remarks>
/// The name is GUID-based, so tests remain isolated under method-level parallelism. Cleanup
/// swallows its own failures: a directory that cannot be deleted must not replace the real
/// assertion failure with an <see cref="IOException"/>.
/// </remarks>
sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>The absolute path of the directory.</summary>
    public string Path { get; }

    /// <summary>Creates an empty file in this directory and returns its full path.</summary>
    public string Touch(string fileName)
    {
        var path = System.IO.Path.Combine(Path, fileName);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: the OS reclaims temp. Never mask the test's own failure.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
