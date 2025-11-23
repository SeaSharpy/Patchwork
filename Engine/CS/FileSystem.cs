using System.IO.Compression;

namespace Patchwork.FileSystem;

public interface IEngineFileSystem : IDisposable
{
    FileStream FileStream(string path);
    string FileText(string path);
    bool FileExists(string path);
    bool FolderExists(string path);
    IEnumerable<string> FolderEnumerate(string path, bool recursive);
}

file static class PathShenanigans
{
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string s = path.Trim();

        s = s.Replace('\\', '|')
             .Replace('/', '|');

        while (s.Length > 0 && s[0] == '|')
        {
            s = s.Substring(1);
        }

        Span<char> buffer = s.ToCharArray();
        int writeIndex = 0;
        bool lastWasSep = false;

        for (int i = 0; i < buffer.Length; i++)
        {
            char c = buffer[i];
            if (c == '|')
            {
                if (lastWasSep)
                {
                    continue;
                }

                lastWasSep = true;
                buffer[writeIndex++] = c;
            }
            else
            {
                lastWasSep = false;
                buffer[writeIndex++] = char.ToLowerInvariant(c);
            }
        }

        if (writeIndex == 0)
        {
            return string.Empty;
        }

        return new string(buffer.Slice(0, writeIndex));
    }
}
public sealed class PhysicalFileSystem : IEngineFileSystem
{
    private readonly string RootPath;

    public PhysicalFileSystem(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path must not be null or empty.", nameof(rootPath));

        RootPath = Path.GetFullPath(rootPath);
    }

    public FileStream FileStream(string path)
    {
        string physicalPath = GetPhysicalPath(path);

        if (!File.Exists(physicalPath))
            throw new FileNotFoundException($"File not found in physical filesystem: {path}", physicalPath);

        return new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public string FileText(string path) => File.ReadAllText(GetPhysicalPath(path));

    public bool FileExists(string path)
    {
        string physicalPath = GetPhysicalPath(path);
        return File.Exists(physicalPath);
    }

    public bool FolderExists(string path)
    {
        string physicalPath = GetPhysicalPath(path, treatEmptyAsRoot: true);
        return Directory.Exists(physicalPath);
    }

    public IEnumerable<string> FolderEnumerate(string path, bool recursive)
    {
        string physicalPath = GetPhysicalPath(path, treatEmptyAsRoot: true);
        if (!Directory.Exists(physicalPath))
            yield break;

        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (string filePath in Directory.EnumerateFiles(physicalPath, "*", searchOption))
        {
            string relative = Path.GetRelativePath(RootPath, filePath);
            string virtualPath = PathShenanigans.NormalizePath(relative.Replace(Path.DirectorySeparatorChar, '|')
                                                      .Replace(Path.AltDirectorySeparatorChar, '|'));
            if (!string.IsNullOrEmpty(virtualPath))
            {
                yield return virtualPath;
            }
        }
    }

    private string GetPhysicalPath(string path, bool treatEmptyAsRoot = false)
    {
        string normalized = PathShenanigans.NormalizePath(path);

        if (string.IsNullOrEmpty(normalized))
            if (!treatEmptyAsRoot)
                throw new ArgumentException("Path must not be empty for files.", nameof(path));
            else
                return RootPath;
        

        string relative = normalized.Replace('|', Path.DirectorySeparatorChar);
        return Path.Combine(RootPath, relative);
    }
    public void Dispose()
    {
        
    }
}

public sealed class ZipFileSystem : IEngineFileSystem, IDisposable
{
    private readonly string ZipPath;
    private readonly FileStream ZipStream;
    private readonly ZipArchive Archive;
    private readonly Dictionary<string, ZipArchiveEntry> Files;
    private readonly HashSet<string> Folders;

    public ZipFileSystem(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
            throw new ArgumentException("Zip path must not be null or empty.", nameof(zipPath));

        ZipPath = Path.GetFullPath(zipPath);

        if (!File.Exists(ZipPath))
            throw new FileNotFoundException("Zip file not found.", ZipPath);

        ZipStream = new FileStream(ZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Archive = new ZipArchive(ZipStream, ZipArchiveMode.Read, leaveOpen: false);

        Files = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        Folders = new HashSet<string>(StringComparer.Ordinal);

        IndexArchive();
    }

    public FileStream FileStream(string path)
    {
        string normalized = PathShenanigans.NormalizePath(path);
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        if (!Files.TryGetValue(normalized, out ZipArchiveEntry? entry))
            throw new FileNotFoundException($"File not found in zip filesystem: {path}");

        string tempPath = Path.GetTempFileName();

        FileStream tempStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read,
            4096,
            FileOptions.DeleteOnClose
        );

        using (Stream entryStream = entry.Open())
            entryStream.CopyTo(tempStream);

        tempStream.Position = 0;
        return tempStream;
    }
    
    public string FileText(string path)
    {
        string normalized = PathShenanigans.NormalizePath(path);
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Path must not be empty.", nameof(path));
        if (!Files.TryGetValue(normalized, out ZipArchiveEntry? entry))
            throw new FileNotFoundException($"File not found in zip filesystem: {path}");
        using (Stream entryStream = entry.Open())
            return new StreamReader(entryStream).ReadToEnd();
    }

    public bool FileExists(string path)
    {
        string normalized = PathShenanigans.NormalizePath(path);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return Files.ContainsKey(normalized);
    }

    public bool FolderExists(string path)
    {
        string normalized = PathShenanigans.NormalizePath(path);

        if (string.IsNullOrEmpty(normalized))
            return Files.Count > 0;

        return Folders.Contains(normalized);
    }

    public IEnumerable<string> FolderEnumerate(string path, bool recursive)
    {
        string normalized = PathShenanigans.NormalizePath(path);

        if (!FolderExists(normalized))
            yield break;

        string prefix;
        if (string.IsNullOrEmpty(normalized))
            prefix = string.Empty;
        else
            prefix = normalized + "|";

        foreach (string filePath in Files.Keys)
        {
            if (prefix.Length > 0)
            {
                if (!filePath.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (!recursive)
                    if (filePath.Substring(prefix.Length).Contains('|'))
                        continue;
            }
            else if (!recursive)
                if (filePath.Contains('|'))
                    continue;

            yield return filePath;
        }
    }

    private void IndexArchive()
    {
        Folders.Add(string.Empty);

        foreach (ZipArchiveEntry entry in Archive.Entries)
        {
            string normalized = PathShenanigans.NormalizePath(entry.FullName);
            if (string.IsNullOrEmpty(normalized))
                continue;

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                entry.FullName.EndsWith("\\", StringComparison.Ordinal))
            {
                AddFolderHierarchy(normalized);
                continue;
            }

            Files[normalized] = entry;
            AddFolderHierarchyForFile(normalized);
        }
    }

    private void AddFolderHierarchyForFile(string filePath)
    {
        int index = filePath.IndexOf('|');
        while (index > 0)
        {
            string folder = filePath.Substring(0, index);
            Folders.Add(folder);

            index = filePath.IndexOf('|', index + 1);
        }
    }

    private void AddFolderHierarchy(string folderPath)
    {
        // Make sure no trailing separator
        while (folderPath.EndsWith("|", StringComparison.Ordinal))
        {
            folderPath = folderPath.Substring(0, folderPath.Length - 1);
        }

        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        int index = folderPath.IndexOf('|');
        if (index < 0)
        {
            Folders.Add(folderPath);
            return;
        }

        while (index > 0)
        {
            string part = folderPath.Substring(0, index);
            Folders.Add(part);

            index = folderPath.IndexOf('|', index + 1);
        }

        Folders.Add(folderPath);
    }

    public void Dispose()
    {
        Archive.Dispose();
        ZipStream.Dispose();
    }
}

public static class DriveMounts
{
    private static Dictionary<char, IEngineFileSystem> Mounts = new();
    private static char GetDriveChar(string driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
        {
            throw new ArgumentException("Drive letter must not be null or empty.", nameof(driveLetter));
        }
        if (driveLetter.Length != 1)
        {
            throw new ArgumentException("Drive letter must be a single character.", nameof(driveLetter));
        }
        driveLetter = driveLetter.ToUpperInvariant();
        char driveChar = driveLetter[0];
        if (!char.IsLetter(driveChar))
        {
            throw new ArgumentException("Drive letter must be a single letter.", nameof(driveLetter));
        }
        return driveChar;
    }
    public static void Mount(string driveLetter, IEngineFileSystem fileSystem) => Mounts[GetDriveChar(driveLetter)] = fileSystem;
    public static IEngineFileSystem? Get(string driveLetter) => Mounts.TryGetValue(GetDriveChar(driveLetter), out IEngineFileSystem? fs) ? fs : null;
    public static bool TryGet(string driveLetter, out IEngineFileSystem? fileSystem)
    {
        IEngineFileSystem? fs = Get(driveLetter);
        fileSystem = fs;
        return fs != null;
    }
    public static IEngineFileSystem GetOrThrow(string driveLetter)
    {
        if (!TryGet(driveLetter, out IEngineFileSystem? fs))
            throw new DriveNotFoundException($"Drive '{driveLetter}' not found.");
        return fs!;
    }
    public static (string, string) SplitPath(string path)
    {
        int index = path.IndexOf(':');
        if (index < 0) throw new ArgumentException("Path must be a drive letter followed by a colon.", nameof(path));
        string driveLetter = path.Substring(0, index);
        string pathInDrive = path.Substring(index + 1);
        return (driveLetter, pathInDrive);
    }
    public static FileStream FileStream(string path)
    {
        (string driveLetter, string pathInDrive) = SplitPath(path);
        IEngineFileSystem fs = GetOrThrow(driveLetter);
        return fs.FileStream(pathInDrive);
    }
    public static string FileText(string path)
    {
        (string driveLetter, string pathInDrive) = SplitPath(path);
        IEngineFileSystem fs = GetOrThrow(driveLetter);
        return fs.FileText(pathInDrive);
    }

    public static bool FileExists(string path)
    {
        (string driveLetter, string pathInDrive) = SplitPath(path);
        IEngineFileSystem fs = GetOrThrow(driveLetter);
        return fs.FileExists(pathInDrive);
    }

    public static bool FolderExists(string path)
    {
        (string driveLetter, string pathInDrive) = SplitPath(path);
        IEngineFileSystem fs = GetOrThrow(driveLetter);
        return fs.FolderExists(pathInDrive);
    }

    public static IEnumerable<string> FolderEnumerate(string path, bool recursive)
    {
        (string driveLetter, string pathInDrive) = SplitPath(path);
        IEngineFileSystem fs = GetOrThrow(driveLetter);
        return fs.FolderEnumerate(pathInDrive, recursive);
    }

    public static void Dispose()
    {
        foreach (IEngineFileSystem fs in Mounts.Values)
        {
            fs.Dispose();
        }
        Mounts.Clear();
    }
}