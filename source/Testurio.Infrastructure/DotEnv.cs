namespace Testurio.Infrastructure;

/// <summary>
/// Loads a .env file from the solution root into environment variables.
/// Only sets variables that are not already present — real environment variables
/// (container, CI) always take precedence. No-ops when the file does not exist.
/// <para>
/// NOTE: This method walks up from <paramref name="startDirectory"/> (defaulting to
/// <see cref="Directory.GetCurrentDirectory"/>) until it finds the file or reaches the
/// filesystem root. When running <c>dotnet test</c> from below the repository root the walk
/// will locate any repo-root <c>.env</c> and inject its values into the test process.
/// This is intentional for local developer convenience; CI and container environments are
/// unaffected because their real environment variables take precedence.
/// </para>
/// </summary>
public static class DotEnv
{
    public static void Load(string fileName = ".env", string? startDirectory = null)
    {
        var dir = startDirectory ?? Directory.GetCurrentDirectory();
        string? path = null;

        while (dir is not null)
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) { path = candidate; break; }
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (path is null) return;

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.Length == 0) continue;

            var idx = trimmed.IndexOf('=');
            if (idx < 0) continue;

            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();

            // Strip surrounding matching quote pairs (single or double).
            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
                value = value[1..^1];

            if (!string.IsNullOrEmpty(key) && Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
