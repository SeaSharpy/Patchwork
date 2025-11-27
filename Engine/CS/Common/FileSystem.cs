using System.IO.Compression;
using System.Net;

namespace Patchwork.FileSystem;

public interface IEngineFileSystem : IDisposable
{
    FileStream FileStream(string path);
    string FileText(string path);
    bool FileExists(string path);
    bool FolderExists(string path);
    IEnumerable<string> FolderEnumerate(string path, bool recursive);
}

public sealed class DriveMountHttpHost : IDisposable
{
    private readonly string DriveLetter;
    private readonly string Prefix;
    private readonly HttpListener Listener;

    private volatile bool Running;

    public DriveMountHttpHost(string driveLetter, string prefix)
    {
        if (string.IsNullOrWhiteSpace(driveLetter))
            throw new ArgumentException("Drive letter must not be null or empty.", nameof(driveLetter));

        if (driveLetter.Length != 1)
            throw new ArgumentException("Drive letter must be a single character.", nameof(driveLetter));

        DriveLetter = driveLetter.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix must not be null or empty.", nameof(prefix));

        if (!prefix.EndsWith("/", StringComparison.Ordinal))
            prefix += "/";

        Prefix = prefix;

        Listener = new HttpListener();
        Listener.Prefixes.Add(Prefix);
    }

    public void Start()
    {
        if (Running)
            return;

        Running = true;
        Listener.Start();
        Listener.BeginGetContext(OnContext, null);
    }

    public void Stop()
    {
        if (!Running)
            return;

        Running = false;

        try
        {
            Listener.Stop();
        }
        catch
        {
        }
    }

    private void OnContext(IAsyncResult ar)
    {
        if (!Running || !Listener.IsListening)
            return;

        HttpListenerContext? context = null;

        try
        {
            context = Listener.EndGetContext(ar);
        }
        catch
        {
            if (Running)
            {
                try
                {
                    Listener.BeginGetContext(OnContext, null);
                }
                catch
                {
                }
            }

            return;
        }

        // Queue the next request as soon as possible.
        try
        {
            if (Running && Listener.IsListening)
                Listener.BeginGetContext(OnContext, null);
        }
        catch
        {
        }

        try
        {
            HandleRequest(context);
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        string method = context.Request.HttpMethod.ToUpperInvariant();

        if (method != "GET" && method != "HEAD")
        {
            context.Response.StatusCode = 405;
            context.Response.Close();
            return;
        }

        // AbsolutePath is something like "/dll/image.png"
        string rawPath = context.Request.Url?.AbsolutePath ?? "/";
        string path = rawPath.TrimStart('/');

        if (string.IsNullOrEmpty(path))
        {
            // No directory listing, just treat as not found.
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        // Decode URL and map to virtual path.
        string decodedPath = Uri.UnescapeDataString(path);

        // The client HttpFileSystem will send paths like "dll/image.png".
        // We convert '/' to '|' so it matches your NormalizePath convention.
        string virtualPathInDrive = decodedPath.Replace('/', '|');

        string fullPath = DriveLetter + ":" + virtualPathInDrive;

        try
        {
            using FileStream fileStream = DriveMounts.FileStream(fullPath);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength64 = fileStream.Length;

            // For HEAD, we only send headers.
            if (method == "HEAD")
            {
                context.Response.OutputStream.Close();
                return;
            }

            fileStream.CopyTo(context.Response.OutputStream);
            context.Response.OutputStream.Flush();
            context.Response.OutputStream.Close();
        }
        catch (DriveNotFoundException)
        {
            context.Response.StatusCode = 404;
            context.Response.OutputStream.Close();
        }
        catch (FileNotFoundException)
        {
            context.Response.StatusCode = 404;
            context.Response.OutputStream.Close();
        }
    }

    public void Dispose()
    {
        Stop();
        Listener.Close();
    }
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

public sealed class HttpFileSystem : IEngineFileSystem
{
    private readonly string BaseUrl;
    private readonly HttpClient Client;

    public HttpFileSystem(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL must not be null or empty.", nameof(baseUrl));

        // Normalize base URL so we can simply append paths.
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            baseUrl += "/";

        BaseUrl = baseUrl;
        Client = new HttpClient();
    }

    private string BuildUrl(string path)
    {
        string normalized = PathShenanigans.NormalizePath(path);
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        string urlPath = normalized.Replace('|', '/');
        return BaseUrl + urlPath;
    }

    public FileStream FileStream(string path)
    {
        string url = BuildUrl(path);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        HttpResponseMessage response = Client.Send(request);

        if (!response.IsSuccessStatusCode)
            throw new FileNotFoundException($"File not found in HTTP filesystem: {path}", url);

        string tempPath = Path.GetTempFileName();

        FileStream tempStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read,
            4096,
            FileOptions.DeleteOnClose
        );

        using Stream responseStream = response.Content.ReadAsStream();
        responseStream.CopyTo(tempStream);

        tempStream.Position = 0;
        return tempStream;
    }

    public string FileText(string path)
    {
        string url = BuildUrl(path);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        HttpResponseMessage response = Client.Send(request);

        if (!response.IsSuccessStatusCode)
            throw new FileNotFoundException($"File not found in HTTP filesystem: {path}", url);

        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    public bool FileExists(string path)
    {
        string url;
        try
        {
            url = BuildUrl(path);
        }
        catch
        {
            return false;
        }

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);
        HttpResponseMessage response;

        try
        {
            response = Client.Send(request);
        }
        catch
        {
            return false;
        }

        // If HEAD is not allowed, some servers return 405 etc.
        if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
        {
            using HttpRequestMessage getRequest = new HttpRequestMessage(HttpMethod.Get, url);
            try
            {
                HttpResponseMessage getResponse = Client.Send(getRequest);
                return getResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        return response.IsSuccessStatusCode;
    }

    public bool FolderExists(string path)
    {
        // There is no general concept of folders over HTTP here.
        // Treat the "root" (empty path) as always existing and everything else as unknown.
        string normalized = PathShenanigans.NormalizePath(path);
        return string.IsNullOrEmpty(normalized);
    }

    public IEnumerable<string> FolderEnumerate(string path, bool recursive)
    {
        // HTTP listing is server specific, so we provide no enumeration here.
        yield break;
    }

    public void Dispose()
    {
        Client.Dispose();
    }
}

public sealed class MultiZipFileSystem : IEngineFileSystem
{
    private sealed class Entry
    {
        public string VirtualPath { get; }
        public string? PhysicalPath { get; }
        public ZipArchiveEntry? ZipEntry { get; }

        public bool IsPhysical => PhysicalPath != null;

        public Entry(string virtualPath, string physicalPath)
        {
            VirtualPath = virtualPath;
            PhysicalPath = physicalPath;
        }

        public Entry(string virtualPath, ZipArchiveEntry zipEntry)
        {
            VirtualPath = virtualPath;
            ZipEntry = zipEntry;
        }
    }

    private readonly string RootPath;
    private readonly Dictionary<string, Entry> Files;
    private readonly HashSet<string> Folders;

    private readonly List<FileStream> ZipStreams;
    private readonly List<ZipArchive> Archives;

    public MultiZipFileSystem(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path must not be null or empty.", nameof(rootPath));

        RootPath = Path.GetFullPath(rootPath);

        if (!Directory.Exists(RootPath))
            throw new DirectoryNotFoundException($"Root folder not found: {RootPath}");

        Files = new Dictionary<string, Entry>(StringComparer.Ordinal);
        Folders = new HashSet<string>(StringComparer.Ordinal);
        ZipStreams = new List<FileStream>();
        Archives = new List<ZipArchive>();

        IndexFolderAndZips();
    }

    private void IndexFolderAndZips()
    {
        Folders.Add(string.Empty);

        // 1. Index physical files (recursive), excluding top level zip files
        foreach (string filePath in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(RootPath, filePath);

            // Exclude top level zip files
            string? directoryName = Path.GetDirectoryName(filePath);
            if (string.Equals(directoryName?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                              RootPath,
                              StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            string virtualPath = PathShenanigans.NormalizePath(
                relative.Replace(Path.DirectorySeparatorChar, '|')
                        .Replace(Path.AltDirectorySeparatorChar, '|')
            );

            if (string.IsNullOrEmpty(virtualPath))
                continue;

            AddFileEntry(new Entry(virtualPath, filePath));
            AddFolderHierarchyForFile(virtualPath);
        }

        // 2. Index zip files (top level only)
        foreach (string zipFilePath in Directory.EnumerateFiles(RootPath, "*.zip", SearchOption.TopDirectoryOnly))
        {
            FileStream zipStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);

            ZipStreams.Add(zipStream);
            Archives.Add(archive);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string normalized = PathShenanigans.NormalizePath(entry.FullName);
                if (string.IsNullOrEmpty(normalized))
                    continue;

                // Treat entries ending with slash as folder only
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                    entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                {
                    AddFolderHierarchy(normalized);
                    continue;
                }

                AddFileEntry(new Entry(normalized, entry));
                AddFolderHierarchyForFile(normalized);
            }
        }
    }

    private void AddFileEntry(Entry entry)
    {
        if (Files.ContainsKey(entry.VirtualPath))
        {
            throw new InvalidOperationException(
                $"Duplicate file path in MultiZipFileSystem: '{entry.VirtualPath}'"
            );
        }

        Files[entry.VirtualPath] = entry;
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
        while (folderPath.EndsWith("|", StringComparison.Ordinal))
        {
            folderPath = folderPath.Substring(0, folderPath.Length - 1);
        }

        if (string.IsNullOrEmpty(folderPath))
            return;

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

    public FileStream FileStream(string path)
    {
        Entry entry = GetEntryOrThrow(path);

        if (entry.IsPhysical)
        {
            return new FileStream(entry.PhysicalPath!, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        // Zip entry: same pattern as ZipFileSystem
        string tempPath = Path.GetTempFileName();

        FileStream tempStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read,
            4096,
            FileOptions.DeleteOnClose
        );

        using (Stream entryStream = entry.ZipEntry!.Open())
        {
            entryStream.CopyTo(tempStream);
        }

        tempStream.Position = 0;
        return tempStream;
    }

    public string FileText(string path)
    {
        Entry entry = GetEntryOrThrow(path);

        if (entry.IsPhysical)
        {
            return File.ReadAllText(entry.PhysicalPath!);
        }

        using (Stream entryStream = entry.ZipEntry!.Open())
        using (StreamReader reader = new StreamReader(entryStream))
        {
            return reader.ReadToEnd();
        }
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

        string prefix = string.IsNullOrEmpty(normalized) ? string.Empty : normalized + "|";

        foreach (string filePath in Files.Keys)
        {
            if (prefix.Length > 0)
            {
                if (!filePath.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (!recursive)
                {
                    if (filePath.Substring(prefix.Length).Contains('|'))
                        continue;
                }
            }
            else if (!recursive)
            {
                if (filePath.Contains('|'))
                    continue;
            }

            yield return filePath;
        }
    }

    private Entry GetEntryOrThrow(string path)
    {
        string normalized = PathShenanigans.NormalizePath(path);
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        if (!Files.TryGetValue(normalized, out Entry? entry))
            throw new FileNotFoundException($"File not found in MultiZipFileSystem: {path}");

        return entry;
    }

    public void Dispose()
    {
        foreach (ZipArchive archive in Archives)
        {
            archive.Dispose();
        }

        foreach (FileStream stream in ZipStreams)
        {
            stream.Dispose();
        }

        Archives.Clear();
        ZipStreams.Clear();
        Files.Clear();
        Folders.Clear();
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