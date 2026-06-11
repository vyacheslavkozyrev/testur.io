using Testurio.Infrastructure;

namespace Testurio.UnitTests.Infrastructure;

public sealed class DotEnvTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _envFilePath;
    private readonly List<string> _keysToClean = [];

    public DotEnvTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _envFilePath = Path.Combine(_tempDir, ".env");
    }

    public void Dispose()
    {
        foreach (var key in _keysToClean)
            Environment.SetEnvironmentVariable(key, null);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string UniqueKey(string prefix)
    {
        var key = $"{prefix}_{Guid.NewGuid():N}";
        _keysToClean.Add(key);
        return key;
    }

    [Fact]
    public void Load_NoFileFound_NoOp()
    {
        var isolated = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(isolated);
        try
        {
            DotEnv.Load(".env.nonexistent", isolated);
        }
        finally
        {
            Directory.Delete(isolated);
        }
    }

    [Fact]
    public void Load_SetsVariableFromFile()
    {
        var key = UniqueKey("DOTENV_SET");
        File.WriteAllText(_envFilePath, $"{key}=hello");

        DotEnv.Load(".env", _tempDir);

        Assert.Equal("hello", Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public void Load_SkipsCommentLines()
    {
        var key = UniqueKey("DOTENV_COMMENT");
        File.WriteAllText(_envFilePath, $"# {key}=should-not-set\n");

        DotEnv.Load(".env", _tempDir);

        Assert.Null(Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public void Load_SkipsBlankLines()
    {
        var key = UniqueKey("DOTENV_BLANK");
        File.WriteAllText(_envFilePath, $"\n\n{key}=value\n\n");

        DotEnv.Load(".env", _tempDir);

        Assert.Equal("value", Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public void Load_DoesNotOverrideExistingEnvVar()
    {
        var key = UniqueKey("DOTENV_EXISTING");
        Environment.SetEnvironmentVariable(key, "original");
        File.WriteAllText(_envFilePath, $"{key}=from-file");

        DotEnv.Load(".env", _tempDir);

        Assert.Equal("original", Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public void Load_FindsFileInParentDirectory()
    {
        var subDir = Path.Combine(_tempDir, "sub", "nested");
        Directory.CreateDirectory(subDir);
        var key = UniqueKey("DOTENV_PARENT");
        File.WriteAllText(_envFilePath, $"{key}=from-parent");

        DotEnv.Load(".env", subDir);

        Assert.Equal("from-parent", Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public void Load_HandlesValueContainingEqualsSign()
    {
        var key = UniqueKey("DOTENV_EQUALS");
        File.WriteAllText(_envFilePath, $"{key}=value=with=equals");

        DotEnv.Load(".env", _tempDir);

        Assert.Equal("value=with=equals", Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public void Load_SetsMultipleVariables()
    {
        var key1 = UniqueKey("DOTENV_MULTI1");
        var key2 = UniqueKey("DOTENV_MULTI2");
        File.WriteAllText(_envFilePath, $"{key1}=first\n{key2}=second\n");

        DotEnv.Load(".env", _tempDir);

        Assert.Equal("first", Environment.GetEnvironmentVariable(key1));
        Assert.Equal("second", Environment.GetEnvironmentVariable(key2));
    }
}
