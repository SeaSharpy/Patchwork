using OpenTK.Graphics.OpenGL4;
using System.Text;
using System.Text.RegularExpressions;

namespace Patchwork.Render.Objects;

public sealed class Shader : IDisposable
{
    int Vertex = 0;
    int Fragment = 0;
    public int Id { get; private set; }
    public string Name { get; private set; }

    public Shader(string vertexPath, string fragmentPath, string name)
    {
        Build(File.ReadAllText(vertexPath), File.ReadAllText(fragmentPath), vertexPath, fragmentPath);
        Name = name;
    }
    public static Shader Text(string vertex, string fragment, string name)
    {
        Shader shader = new(name);
        shader.Build(vertex, fragment, null, null);
        return shader;
    }

    internal Shader(string name)
    {
        Name = name;
    }

    public void Use() => GL.UseProgram(Id);

    public void Dispose()
    {
        if (Id != 0) GL.DeleteProgram(Id);
        Id = 0;
    }

    public static Shader Template(string template, string path, string name)
    {
        Shader shader = new(name);

        string user = StripComments(File.ReadAllText(path));
        string userExpanded = Preprocess(user, path);

        Dictionary<string, string> sections = ParseSections(userExpanded);

        string vTemplateRaw = StripComments(IncludedFiles.Files["shadertemplate_" + template + "_vertex"]);
        string fTemplateRaw = StripComments(IncludedFiles.Files["shadertemplate_" + template + "_fragment"]);

        HashSet<string> disallowsV = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> disallowsF = new(StringComparer.OrdinalIgnoreCase);

        string vTemplateExpanded = PreprocessTemplateIncludes(vTemplateRaw, $"shadertemplate_{template}_vertex", disallowsV);
        string fTemplateExpanded = PreprocessTemplateIncludes(fTemplateRaw, $"shadertemplate_{template}_fragment", disallowsF);

        string vComposed = ApplyTemplate(
            vTemplateExpanded, sections, disallowsV);

        string fComposed = ApplyTemplate(
            fTemplateExpanded, sections, disallowsF);
        shader.Build(vComposed, fComposed, path, path);
        return shader;
    }

    private void Build(string vertexSource, string fragmentSource, string? vPath = null, string? fPath = null)
    {
        Header ??= IncludedFiles.Files["lib.glsl"];
        vertexSource = Header + "#define VERTEX 1\n" + Preprocess(vertexSource, vPath);
        fragmentSource = Header + "#define FRAGMENT 1\n" + Preprocess(fragmentSource, fPath);

        Vertex = GL.CreateShader(ShaderType.VertexShader);
        Fragment = GL.CreateShader(ShaderType.FragmentShader);

        GL.ShaderSource(Vertex, vertexSource);
        GL.CompileShader(Vertex);
        CheckShader(Vertex, "Vertex", vPath);

        GL.ShaderSource(Fragment, fragmentSource);
        GL.CompileShader(Fragment);
        CheckShader(Fragment, "Fragment", fPath);

        Id = GL.CreateProgram();
        GL.AttachShader(Id, Vertex);
        GL.AttachShader(Id, Fragment);
        GL.LinkProgram(Id);
        CheckProgram(Id);

        GL.DetachShader(Id, Vertex);
        GL.DetachShader(Id, Fragment);
        GL.DeleteShader(Vertex);
        GL.DeleteShader(Fragment);
    }

    private static void CheckShader(int shader, string label, string? path)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 1) return;

        string log = GL.GetShaderInfoLog(shader);

        GL.GetShader(shader, ShaderParameter.ShaderSourceLength, out int length);
        string src = string.Empty;
        if (length > 0)
        {
            GL.GetShaderSource(shader, length, out _, out src);
        }

        string numberedSource = AddLineNumbers(src);

        throw new Exception($@"
{label} shader compile failed{(path is not null ? $" ({path})" : "")}:
--- GLSL COMPILE LOG ---
{log}
--- SOURCE WITH LINE NUMBERS ---
{numberedSource}
");
    }

    private static void CheckProgram(int program)
    {
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 1) return;

        string log = GL.GetProgramInfoLog(program);
        throw new Exception($"Program link failed:\n{log}");
    }

    private static string AddLineNumbers(string source)
    {
        if (string.IsNullOrEmpty(source))
            return "<no source>";

        StringBuilder sb = new StringBuilder(source.Length + 128);
        string[] lines = source.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            sb.AppendFormat("{0,4}: {1}\n", i + 1, lines[i]);
        }

        return sb.ToString();
    }


    private static string Header = null!;
    private static string Preprocess(string source, string? path = null, HashSet<string>? includes = null, int depth = 0)
    {
        includes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (depth > 64)
            throw new Exception("Exceeded include recursion depth (possible cycle).");

        source = StripComments(source);

        StringBuilder sb = new StringBuilder(source.Length + 1024);
        string[] lines = source.Replace("\r\n", "\n").Split('\n');

        string baseDir = Path.GetDirectoryName(path ?? "") ?? Environment.CurrentDirectory;

        for (int i = 0; i < lines.Length; ++i)
        {
            string raw = lines[i];
            string line = raw.TrimStart();

            if (line.StartsWith("#include"))
            {
                string[] parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    throw new Exception($"Malformed include at line {i + 1} of {path ?? "<memory>"}: '{raw}'");

                string kind = parts[1];
                string incPath = parts[2].Trim('"');

                string resolved = kind switch
                {
                    "rel" => Path.GetFullPath(Path.Combine(baseDir, incPath)),
                    "abs" => Path.GetFullPath(incPath),
                    _ => throw new Exception($"Invalid include kind '{kind}' at line {i + 1} of {path ?? "<memory>"}")
                };

                if (!File.Exists(resolved))
                    throw new FileNotFoundException($"#include file not found: {resolved}");

                if (!includes.Add(resolved))
                    continue;

                string includedSrc = File.ReadAllText(resolved);
                includedSrc = StripComments(includedSrc);

                sb.Append(Preprocess(includedSrc, resolved, includes, depth + 1));
            }
            else
            {
                sb.AppendLine(raw);
            }
        }

        return sb.ToString();
    }

    private static string PreprocessTemplateIncludes(string templateSource,
                                                     string templateName,
                                                     HashSet<string> disallows,
                                                     HashSet<string>? includes = null,
                                                     int depth = 0)
    {
        includes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (depth > 64)
            throw new Exception("Exceeded include recursion depth in template (possible cycle).");

        templateSource = StripComments(templateSource);

        StringBuilder sb = new StringBuilder(templateSource.Length + 256);
        string[] lines = templateSource.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string line = raw.TrimStart();

            if (line.StartsWith("#include"))
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    throw new Exception($"Malformed template include at line {i + 1} of {templateName}: '{raw}'");

                string key = parts[1].Trim('"');

                if (!IncludedFiles.Files.TryGetValue(key, out string? incSource))
                    throw new FileNotFoundException($"Template #include not found in IncludedFiles: '{key}'");

                if (!includes.Add($"template::{key}"))
                {
                    continue;
                }

                sb.Append(PreprocessTemplateIncludes(incSource, key, disallows, includes, depth + 1));
            }
            else if (line.StartsWith("#disallow"))
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    throw new Exception($"Malformed template disallow at line {i + 1} of {templateName}: '{raw}'");

                string key = parts[1].Trim('"');
                disallows.Add(key);

                sb.AppendLine($"//disallow {key}");
            }
            else
            {
                sb.AppendLine(raw);
            }
        }

        return sb.ToString();
    }

    private static string StripComments(string src)
    {
        if (string.IsNullOrWhiteSpace(src))
            return src;

        string pattern = @"
            (""([^""\\]|\\.)*"")    # strings
          | (//[^\n]*)              # // comments
          | (/\*[\s\S]*?\*/)        # /* */ comments
        ";

        string result = Regex.Replace(
            src,
            pattern,
            me =>
            {
                if (me.Value.StartsWith("\"")) return me.Value;
                return "";
            },
            RegexOptions.IgnorePatternWhitespace);

        return result;
    }

    private static readonly Regex TemplateUseRegex =
        new(@"#template\s+(?<name>[A-Za-z_]\w*)\b", RegexOptions.CultureInvariant);

    private static Dictionary<string, string> ParseSections(string src)
    {
        Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(src))
            return dict;

        string[] lines = src.Replace("\r\n", "\n").Split('\n');
        string? current = null;
        StringBuilder sb = new StringBuilder();

        void Flush()
        {
            if (current != null)
            {
                dict[current] = sb.ToString().Trim();
                sb.Clear();
            }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            Match m = Regex.Match(line, @"^\s*([A-Za-z_]\w*)\s*:\s*$");
            if (m.Success)
            {
                Flush();
                current = m.Groups[1].Value;
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        Flush();

        return dict;
    }

    private static string ApplyTemplate(string templateSource,
                                       Dictionary<string, string> sections,
                                       HashSet<string>? disallows = null)
    {
        return TemplateUseRegex.Replace(templateSource, me =>
        {
            string name = me.Groups["name"].Value;

            sections.TryGetValue(name, out string? body);
            body ??= string.Empty;

            if (disallows != null && body.Length != 0)
            {
                foreach (string disallow in disallows)
                {
                    string pat = $@"\A\s*{Regex.Escape(disallow)}\s*\z";
                    if (Regex.IsMatch(body, pat, RegexOptions.CultureInvariant))
                        throw new Exception($"Disallowed item '{disallow}' found in shader template");
                }
            }

            if (sections.ContainsKey(name))
            {
                body = "#define " + name + " 1\n" + body;
            }

            return body;
        });
    }
}
