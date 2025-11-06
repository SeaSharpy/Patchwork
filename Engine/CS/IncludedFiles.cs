using System.Reflection;
using System.Text;

public static class IncludedFiles
{
    public static Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    static bool Initialized;
    static string[]? AllNames;
    static Assembly? AssemblyRef;

    public static void Init()
    {
        if (Initialized) return;
        Initialized = true;

        AssemblyRef = typeof(IncludedFiles).Assembly;
        AllNames = AssemblyRef.GetManifestResourceNames();

        Dictionary<string, string> aliasMap = ReadAliasMap("data.txt");

        foreach ((string aliasKey, string resourceHint) in aliasMap)
        {
            string fullName = ResolveResourceName(resourceHint);
            string text = ReadResourceText(fullName);
            AddWithAliases(aliasKey, fullName, text);
        }

        HashSet<string> loadedFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string _, string hint) in aliasMap)
            loadedFullNames.Add(ResolveResourceName(hint));

        foreach (string fullName in AllNames!)
        {
            if (fullName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                continue;
            if (loadedFullNames.Contains(fullName))
                continue;

            string text = ReadResourceText(fullName);
            AddWithAliases(null, fullName, text);
        }
    }


    static void AddWithAliases(string? primaryKey, string fullResourceName, string text)
    {
        if (!string.IsNullOrWhiteSpace(primaryKey))
            TryAdd(primaryKey!, text);

        string fileWithExt = GetFileNameWithExtension(fullResourceName);
        TryAdd(fileWithExt, text);

        string fileNoExt = Path.GetFileNameWithoutExtension(fileWithExt);
        if (!string.IsNullOrEmpty(fileNoExt))
            TryAdd(fileNoExt, text);
    }

    static void TryAdd(string key, string value)
    {
        if (!Files.ContainsKey(key))
            Files[key] = value;
    }

    static string ReadResourceText(string fullName)
    {
        using Stream stream = AssemblyRef!.GetManifestResourceStream(fullName)
            ?? throw new Exception($"Could not find embedded resource '{fullName}'.");
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    static Dictionary<string, string> ReadAliasMap(string dataTxtSimpleName)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? fullName = ResolveResourceNameOrNull(dataTxtSimpleName);
        if (fullName == null) return map;

        using Stream stream = AssemblyRef!.GetManifestResourceStream(fullName)!;
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true);
        foreach (string raw in reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.StartsWith('#') || raw.StartsWith("//")) continue;
            string[] parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            string key = parts[0];
            string hint = parts[1];
            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                map[key] = hint;
        }
        return map;
    }

    static string ResolveResourceName(string hint)
    {
        string? full = ResolveResourceNameOrNull(hint);
        if (full != null) return full;

        throw new Exception($"Embedded resource not found for hint '{hint}'. " +
                            $"Known names: {string.Join(", ", AllNames ?? Array.Empty<string>())}");
    }

    static string? ResolveResourceNameOrNull(string hint)
    {
        if (AssemblyRef == null) return null;
        AllNames ??= AssemblyRef.GetManifestResourceNames();

        string? exact = Array.Find(AllNames, n => n.Equals(hint, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        string dotHint = hint.Replace('/', '.').Replace('\\', '.');
        exact = Array.Find(AllNames, n => n.Equals(dotHint, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        string suffix = dotHint.StartsWith('.') ? dotHint : "." + dotHint;
        string? bySuffix = AllNames.LastOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (bySuffix != null) return bySuffix;

        string fileName = Path.GetFileName(hint);
        if (!string.IsNullOrEmpty(fileName))
        {
            string suffix2 = "." + fileName;
            bySuffix = AllNames.LastOrDefault(n => n.EndsWith(suffix2, StringComparison.OrdinalIgnoreCase));
            if (bySuffix != null) return bySuffix;
        }

        return null;
    }

    static string GetFileNameWithExtension(string full)
    {
        string[] parts = full.Split('.');
        return parts.Length >= 2 ? $"{parts[^2]}.{parts[^1]}" : full;
    }
}
