namespace Testurio.Infrastructure;

/// <summary>
/// Loads a .env file from the solution root into environment variables.
/// Only sets variables that are not already present — real environment variables
/// (container, CI) always take precedence. No-ops when the file does not exist.
/// </summary>
public static class DotEnv
{
    public static void Load(string fileName = ".env")
    {
        var dir = Directory.GetCurrentDirectory();
        string? path = null;

        while (dir is not null)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) { path = candidate; break; }
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (path is null) return;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.Length == 0) continue;

            var idx = trimmed.IndexOf('=');
            if (idx < 0) continue;

            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();

            if (!string.IsNullOrEmpty(key) && Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
