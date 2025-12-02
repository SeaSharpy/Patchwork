using OpenTK.Graphics.OpenGL4;
using System.Text.RegularExpressions;
namespace Patchwork.Client.Render;

public static partial class Renderer
{
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
                string v = me.Value;

                // Keep strings exactly as they are
                if (v.StartsWith("\""))
                    return v;

                // Line comments: keep the newline if present, and leave characters before it unchanged
                if (v.StartsWith("//"))
                {
                    int newlineIndex = v.IndexOf('\n');
                    if (newlineIndex >= 0)
                        return new string(' ', newlineIndex) + "\n";
                    return "";
                }

                // Block comments: must preserve the number of lines
                {
                    int lines = v.Count(c => c == '\n');

                    if (lines == 0)
                    {
                        return "";
                    }

                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    for (int i = 0; i < lines; i++)
                        sb.Append('\n');

                    return sb.ToString();
                }
            },
            RegexOptions.IgnorePatternWhitespace);

        return result;
    }
    private static int CreateShaderProgram(string vertexSource, string fragmentSource)
    {
        string ShaderHeader = @"
#version 460 core
// HEADER START
#extension GL_ARB_gpu_shader_int64 : enable
#extension GL_ARB_bindless_texture : enable
#extension GL_ARB_shader_draw_parameters : enable
#ifdef GL_NV_gpu_shader5
#extension GL_NV_gpu_shader5 : enable
#endif
#ifdef GL_EXT_nonuniform_qualifier
#extension GL_EXT_nonuniform_qualifier : enable
#endif
const float PI = 3.14159265358979323846264338327950288419716939937510;

#define Buffer(Name, Type, Binding) \
layout(std430, binding = Binding) readonly buffer Name##Buffer { \
    Type Name[]; \
};

#define BufferWriteable(Name, Type, Binding, Modifier) \
layout(std430, binding = Binding) Modifier buffer Name##Buffer { \
    Type Name[]; \
};
sampler2D HandleToSampler(uint64_t h) {
    return sampler2D(h);
}
vec2 DirToEquirectUV(vec3 dir)
{
    dir = normalize(dir);

    // Longitude (theta) around Y axis, range [-PI, PI]
    float theta = atan(dir.z, dir.x);

    // Latitude (phi), range [-PI/2, PI/2]
    float phi = asin(clamp(dir.y, -1.0, 1.0));

    // Map to [0, 1]
    float u = theta / (2.0 * PI) + 0.5;
    float v = 0.5 - phi / PI;

    return vec2(u, v);
}
// HEADER END
#line 1
        ";
        int vert = GL.CreateShader(ShaderType.VertexShader);
        string sanitizedVertexSource = ShaderHeader + StripComments(vertexSource);
        GL.ShaderSource(vert, sanitizedVertexSource);
        GL.CompileShader(vert);
        CheckShaderCompile(vert);

        int frag = GL.CreateShader(ShaderType.FragmentShader);
        string sanitizedFragmentSource = ShaderHeader + StripComments(fragmentSource);
        GL.ShaderSource(frag, sanitizedFragmentSource);
        GL.CompileShader(frag);
        CheckShaderCompile(frag);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vert);
        GL.AttachShader(program, frag);
        GL.LinkProgram(program);
        CheckProgramLink(program);

        GL.DetachShader(program, vert);
        GL.DetachShader(program, frag);
        GL.DeleteShader(vert);
        GL.DeleteShader(frag);

        return program;
    }

    private static void CheckShaderCompile(int shader)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            throw new Exception($"Shader compile error: {log}");
        }
    }

    private static void CheckProgramLink(int program)
    {
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetProgramInfoLog(program);
            throw new Exception($"Program link error: {log}");
        }
    }
}

public static class ShaderExtensions
{
    public static void Use(this int shader)
    {
        GL.UseProgram(shader);
    }
    public static Dictionary<int, Dictionary<string, int>> UniformCache = new();
    public static int Uniform(this int shader, string name)
    {
        if (!UniformCache.TryGetValue(shader, out Dictionary<string, int>? cache))
        {
            cache = new();
            UniformCache[shader] = cache;
        }
        if (cache.TryGetValue(name, out int location))
            return location;
        location = GL.GetUniformLocation(shader, name);
        cache[name] = location;
        return location;
    }
}