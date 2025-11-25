using OpenTK.Graphics.OpenGL4;
using System.Runtime.InteropServices;

namespace Patchwork.Render.Objects;

public record Font(Texture Texture, float Spacing, float Descent);

[StructLayout(LayoutKind.Sequential)]
public struct QuadInstanceData
{
    public Vector2 TopLeft;
    public Vector2 Size;
    public Vector4 Color;
    public Vector4 Color2;
    public int Mode;
    public float CornerRadius;
    public long TextureHandle;
}

public interface INonBatched
{
    void Execute();
}

public sealed class TextDraw : INonBatched
{
    public string Text;
    public Vector2 TopLeft;
    public int CharSize;
    public Vector4 Color;
    public int Vao;
    public int Vbo;
    public int Stride;

    public TextDraw(string text, Vector2 topLeft, int charSize, Vector4 color, int vao, int vbo, int stride)
    {
        Text = text;
        TopLeft = topLeft;
        CharSize = charSize;
        Color = color;
        Vao = vao;
        Vbo = vbo;
        Stride = stride;
    }

    public void Execute()
    {
        GL.BindVertexArray(Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);

        int numChars = Text.Length;
        float[] verts = new float[numChars * 6 * 4];
        int s = 0;
        float invAtlas = 1.0f / 16.0f;
        float cursorX = TopLeft.X;
        float offsetCursor = UIRenderer.Spacing * CharSize;
        float cursorY = TopLeft.Y;

        for (int i = 0; i < Text.Length; i++)
        {
            char c = Text[i];
            int cx = c % 16;
            int cy = c / 16;

            float u0 = cx * invAtlas;
            float v1 = cy * invAtlas;
            float u1 = u0 + invAtlas;
            float v0 = v1 + invAtlas;
            v0 = 1.0f - v0;
            v1 = 1.0f - v1;

            float x0 = cursorX;
            float y0 = cursorY;
            float x1 = cursorX + CharSize;
            float y1 = cursorY + CharSize;

            bool isDesc = UIRenderer.Descenders.Contains(c);
            bool isDesc2 = UIRenderer.SecondaryDescenders.Contains(c);

            if (isDesc)
            {
                y0 -= CharSize * UIRenderer.Descent;
                y1 -= CharSize * UIRenderer.Descent;
            }
            else if (isDesc2)
            {
                y0 -= CharSize * UIRenderer.Descent * 0.5f;
                y1 -= CharSize * UIRenderer.Descent * 0.5f;
            }

            verts[s++] = x0; verts[s++] = y0; verts[s++] = u0; verts[s++] = v0;
            verts[s++] = x1; verts[s++] = y0; verts[s++] = u1; verts[s++] = v0;
            verts[s++] = x0; verts[s++] = y1; verts[s++] = u0; verts[s++] = v1;
            verts[s++] = x0; verts[s++] = y1; verts[s++] = u0; verts[s++] = v1;
            verts[s++] = x1; verts[s++] = y0; verts[s++] = u1; verts[s++] = v0;
            verts[s++] = x1; verts[s++] = y1; verts[s++] = u1; verts[s++] = v1;

            cursorX += offsetCursor;
        }

        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.StreamDraw);

        UIRenderer.TextShader.Use();
        UIRenderer.TextShader.Set("Viewport", Viewport);
        UIRenderer.TextShader.Set("Color", Color);
        UIRenderer.Font.Texture.Bind(0);
        UIRenderer.TextShader.Set("Texture", 0);

        GL.DrawArrays(PrimitiveType.Triangles, 0, numChars * 6);

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }
}

public sealed class ImmediateQuadDraw : INonBatched
{
    public Vector2 TopLeft;
    public Vector2 Size;
    public float CornerRadius;
    public Texture Texture;
    public Vector4 Color;
    public bool UseColor;

    public ImmediateQuadDraw(Vector2 topLeft, Vector2 size, float cornerRadius, Texture texture)
    {
        TopLeft = topLeft;
        Size = size;
        CornerRadius = cornerRadius;
        Texture = texture;
        Color = new Vector4(1f);
        UseColor = false;
    }

    public ImmediateQuadDraw(Vector2 topLeft, Vector2 size, float cornerRadius, Texture texture, Vector4 color)
    {
        TopLeft = topLeft;
        Size = size;
        CornerRadius = cornerRadius;
        Texture = texture;
        Color = color;
        UseColor = true;
    }

    public void Execute()
    {
        UIRenderer.QuadImmediate.Use();
        UIRenderer.QuadImmediate.Set("Viewport", Viewport);
        UIRenderer.QuadImmediate.Set("Size", Size);
        UIRenderer.QuadImmediate.Set("TopLeft", TopLeft);
        UIRenderer.QuadImmediate.Set("CornerRadius", CornerRadius);
        Vector4 c = UseColor ? Color : new Vector4(1f);
        UIRenderer.QuadImmediate.Set("Color", c);
        UIRenderer.QuadImmediate.Set("Mode", 1);
        Texture.Bind(0);
        UIRenderer.QuadImmediate.Set("Texture", 0);
        GL.BindVertexArray(UIRenderer.QuadVAOHandle);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);
    }
}

public sealed class LineDraw : INonBatched
{
    public Vector2 Start;
    public Vector2 End;
    public float Thickness;
    public Vector4 Color;

    public LineDraw(Vector2 start, Vector2 end, float thickness, Vector4 color)
    {
        Start = start;
        End = end;
        Thickness = thickness;
        Color = color;
    }

    public void Execute()
    {
        Vector2 delta = End - Start;
        float thickness = MathF.Max(Thickness, 1f);
        if (delta.LengthSquared <= float.Epsilon)
        {
            float half = thickness * 0.5f;
            float[] pointVertices = new float[]
            {
                Start.X - half, Start.Y - half,
                Start.X + half, Start.Y - half,
                Start.X + half, Start.Y + half,
                Start.X - half, Start.Y - half,
                Start.X + half, Start.Y + half,
                Start.X - half, Start.Y + half,
            };

            UploadAndDraw(pointVertices);
            return;
        }

        float length = delta.Length;
        Vector2 direction = delta / length;
        Vector2 normal = new Vector2(-direction.Y, direction.X) * (thickness * 0.5f);

        float[] vertices = new float[]
        {
            Start.X - normal.X, Start.Y - normal.Y,
            Start.X + normal.X, Start.Y + normal.Y,
            End.X + normal.X,   End.Y + normal.Y,
            Start.X - normal.X, Start.Y - normal.Y,
            End.X + normal.X,   End.Y + normal.Y,
            End.X - normal.X,   End.Y - normal.Y,
        };

        UploadAndDraw(vertices);
    }

    void UploadAndDraw(float[] vertices)
    {
        GL.BindVertexArray(UIRenderer.LineVAOHandle);
        GL.BindBuffer(BufferTarget.ArrayBuffer, UIRenderer.LineVBOHandle);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StreamDraw);

        UIRenderer.LineShader.Use();
        UIRenderer.LineShader.Set("Viewport", Viewport);
        UIRenderer.LineShader.Set("Color", Color);

        GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Length / 2);

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }
}

public static class UIRenderer
{
    public static Font Font = null!;

    private static int TextVAO;
    private static int TextVBO;

    private static int QuadVAO;
    private static int QuadVBO;

    private static int LineVAO;
    private static int LineVBO;

    private static int InstanceSSBO;

    private static readonly List<QuadInstanceData> Instances = [];
    private static readonly List<int> SplitIndices = [];
    private static readonly List<List<INonBatched>> NonBatchedGroups = [];

    public static Shader QuadImmediate = Shader.Text(
        @"
layout(location=0) in vec2 Position;
out vec2 UV;
uniform vec2 TopLeft;
uniform vec2 Size;
uniform ivec4 Viewport;
void main() {
    vec2 tl = TopLeft - vec2(Viewport.xy);
    vec2 tlN = tl / vec2(Viewport.zw);
    UV = Position;
    vec2 sN = Size / vec2(Viewport.zw);
    vec2 wp = tlN + UV * sN;
    vec2 ndc = wp * 2.0 - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);
}
        ",
        @"
in vec2 UV;
out vec4 FragColor;
uniform int Mode;
uniform sampler2D Texture;
uniform vec4 Color;
uniform vec4 Color2;
uniform ivec4 Viewport;
uniform float CornerRadius;
uniform vec2 Size;

float sdRoundedBox(vec2 p, vec2 halfSize, float r)
{
    vec2 q = abs(p) - (halfSize - vec2(r));
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float hash21(vec2 p)
{
    float n1 = fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
    float n2 = fract(sin(dot(p, vec2(39.3467, 11.135)))  * 96321.9127);
    return clamp(n1 + n2 - 1.0, -1.0, 1.0);
}

void main()
{
    vec2 ppx = UV * Size;
    vec2 hs = 0.5 * Size;
    vec2 c = ppx - hs;
    float mr = min(hs.x, hs.y);
    float r = clamp(CornerRadius, 0.0, mr);
    float sd = sdRoundedBox(c, hs, r);
    float aa = fwidth(sd);
    float mask = 1.0 - smoothstep(0.0, aa, sd);

    vec4 baseColor = (Mode == 1) ? texture(Texture, UV) * Color : Color;
    if (Mode == 2) baseColor = mix(baseColor, Color2, UV.x);
    else if (Mode == 3) baseColor = mix(baseColor, Color2, UV.y);

    if (Mode == 2 || Mode == 3)
    {
        float n = hash21(gl_FragCoord.xy);
        baseColor.rgb = clamp(baseColor.rgb + n * (1.0 / 255.0), 0.0, 1.0);
    }

    FragColor = vec4(baseColor.rgb, mask * baseColor.a);
}
",
        "QuadImmediate"
        );

    public static Shader QuadBatch = Shader.Text(
        @"
layout(location=0) in vec2 Position;
layout(location=1) in vec2 InUV;
out vec2 UV;
flat out int InstanceIndex;
uniform ivec4 Viewport;

struct QuadData
{
    vec2 TopLeft;
    vec2 Size;
    vec4 Color;
    vec4 Color2;
    int Mode;
    float CornerRadius;
    uint64_t TextureHandle;
};

layout(std430, binding=0) buffer QuadBuffer
{
    QuadData Quads[];
};

void main()
{
    int idx = gl_InstanceID;
    QuadData q = Quads[idx];
    vec2 tl = q.TopLeft - vec2(Viewport.xy);
    vec2 tlN = tl / vec2(Viewport.zw);
    vec2 sN = q.Size / vec2(Viewport.zw);
    vec2 wp = tlN + Position * sN;
    vec2 ndc = wp * 2.0 - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);
    UV = InUV;
    InstanceIndex = idx;
}
        ",
        @"
in vec2 UV;
flat in int InstanceIndex;
out vec4 FragColor;
uniform ivec4 Viewport;

struct QuadData
{
    vec2 TopLeft;
    vec2 Size;
    vec4 Color;
    vec4 Color2;
    int Mode;
    float CornerRadius;
    uint64_t TextureHandle;
};

layout(std430, binding=0) buffer QuadBuffer
{
    QuadData Quads[];
};

float sdRoundedBox(vec2 p, vec2 halfSize, float r)
{
    vec2 q = abs(p) - (halfSize - vec2(r));
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

float hash21(vec2 p)
{
    float n1 = fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
    float n2 = fract(sin(dot(p, vec2(39.3467, 11.135)))  * 96321.9127);
    return clamp(n1 + n2 - 1.0, -1.0, 1.0);
}

void main()
{
    QuadData q = Quads[InstanceIndex];
    vec2 ppx = UV * q.Size;
    vec2 hs = 0.5 * q.Size;
    vec2 c = ppx - hs;
    float mr = min(hs.x, hs.y);
    float r = clamp(q.CornerRadius, 0.0, mr);
    float sd = sdRoundedBox(c, hs, r);
    float aa = fwidth(sd);
    float mask = 1.0 - smoothstep(0.0, aa, sd);

    vec4 baseColor = q.Color;

    if (q.Mode == 1)
    {
        sampler2D tex = sampler2D(q.TextureHandle);
        baseColor = texture(tex, UV) * q.Color;
    }
    else if (q.Mode == 2)
    {
        baseColor = mix(q.Color, q.Color2, UV.x);
    }
    else if (q.Mode == 3)
    {
        baseColor = mix(q.Color, q.Color2, UV.y);
    }

    if (q.Mode == 2 || q.Mode == 3)
    {
        float n = hash21(gl_FragCoord.xy);
        baseColor.rgb = clamp(baseColor.rgb + n * (1.0 / 255.0), 0.0, 1.0);
    }

    FragColor = vec4(baseColor.rgb, mask * baseColor.a);
}
",
        "QuadBatch"
        );

    public static Shader TextShader = Shader.Text(
        @"
layout(location=0) in vec2 Position;
layout(location=1) in vec2 vUV;
out vec2 UV;
uniform ivec4 Viewport;
void main() {
    vec2 tl = Position - vec2(Viewport.xy);
    vec2 wp = tl / vec2(Viewport.zw);
    UV = vUV;
    vec2 ndc = wp * 2.0 - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);
}
        ",
        @"
in vec2 UV;
out vec4 FragColor;
uniform sampler2D Texture;
uniform vec4 Color;
void main()
{
    FragColor = texture(Texture, UV) * Color;
}
",
        "Text"
        );

    private static int TextStride = sizeof(float) * 4;

    public static Shader LineShader = Shader.Text(
        @"
layout(location=0) in vec2 Position;
uniform ivec4 Viewport;
void main() {
    vec2 tl = Position - vec2(Viewport.xy);
    vec2 wp = tl / vec2(Viewport.zw);
    vec2 ndc = wp * 2.0 - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);
}
        ",
        @"
out vec4 FragColor;
uniform vec4 Color;
void main()
{
    FragColor = Color;
}
",
        "Line"
        );

    public static float Spacing => Font.Spacing;
    public static float Descent => Font.Descent;

    public static string Descenders = "gjpqyQ";
    public static string SecondaryDescenders = ",_;|()[]{}\\";

    public static int QuadVAOHandle => QuadVAO;
    public static int LineVAOHandle => LineVAO;
    public static int LineVBOHandle => LineVBO;

    public static void Init()
    {
        TextVAO = GL.GenVertexArray();
        TextVBO = GL.GenBuffer();
        GL.BindVertexArray(TextVAO);
        GL.BindBuffer(BufferTarget.ArrayBuffer, TextVBO);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, TextStride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, TextStride, sizeof(float) * 2);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        QuadVAO = GL.GenVertexArray();
        QuadVBO = GL.GenBuffer();
        GL.BindVertexArray(QuadVAO);
        GL.BindBuffer(BufferTarget.ArrayBuffer, QuadVBO);
        float[] quadVerts = new float[]
        {
            0f, 0f, 0f, 0f,
            1f, 0f, 1f, 0f,
            0f, 1f, 0f, 1f,
            0f, 1f, 0f, 1f,
            1f, 0f, 1f, 0f,
            1f, 1f, 1f, 1f
        };
        GL.BufferData(BufferTarget.ArrayBuffer, quadVerts.Length * sizeof(float), quadVerts, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, sizeof(float) * 2);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        LineVAO = GL.GenVertexArray();
        LineVBO = GL.GenBuffer();
        GL.BindVertexArray(LineVAO);
        GL.BindBuffer(BufferTarget.ArrayBuffer, LineVBO);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 2, 0);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        InstanceSSBO = GL.GenBuffer();
    }

    public static void Dispose()
    {
        QuadImmediate.Dispose();
        QuadBatch.Dispose();
        TextShader.Dispose();
        LineShader.Dispose();

        GL.DeleteBuffer(TextVBO);
        GL.DeleteVertexArray(TextVAO);

        GL.DeleteBuffer(QuadVBO);
        GL.DeleteVertexArray(QuadVAO);

        GL.DeleteBuffer(LineVBO);
        GL.DeleteVertexArray(LineVAO);

        GL.DeleteBuffer(InstanceSSBO);
    }

    private static void AddSplitIfNeeded()
    {
        int count = Instances.Count;
        int groups = NonBatchedGroups.Count;
        if (groups == 0)
        {
            SplitIndices.Add(count);
            NonBatchedGroups.Add([]);
            return;
        }
        int lastSplit = SplitIndices[groups - 1];
        if (lastSplit != count)
        {
            SplitIndices.Add(count);
            NonBatchedGroups.Add([]);
        }
    }

    private static void QueueNonBatched(INonBatched item)
    {
        AddSplitIfNeeded();
        NonBatchedGroups[NonBatchedGroups.Count - 1].Add(item);
    }

    private static void DrawBatchRange(int start, int count)
    {
        if (count <= 0) return;
        QuadInstanceData[] slice = Instances.GetRange(start, count).ToArray();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, InstanceSSBO);
        int size = slice.Length * Marshal.SizeOf<QuadInstanceData>();
        GL.BufferData(BufferTarget.ShaderStorageBuffer, size, slice, BufferUsageHint.StreamDraw);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, InstanceSSBO);

        GL.BindVertexArray(QuadVAO);
        QuadBatch.Use();
        QuadBatch.Set("Viewport", Viewport);
        GL.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, count);
        GL.BindVertexArray(0);
    }

    public static void Flush()
    {
        int prev = 0;
        for (int i = 0; i < NonBatchedGroups.Count; i++)
        {
            int split = SplitIndices[i];
            int count = split - prev;
            DrawBatchRange(prev, count);

            List<INonBatched> group = NonBatchedGroups[i];
            for (int j = 0; j < group.Count; j++)
            {
                group[j].Execute();
            }

            prev = split;
        }

        int remaining = Instances.Count - prev;
        DrawBatchRange(prev, remaining);

        Instances.Clear();
        SplitIndices.Clear();
        NonBatchedGroups.Clear();
    }

    private static void Enqueue(QuadInstanceData data)
    {
        Instances.Add(data);
    }

    public static float MeasureLength(float textSize, string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        int size = (int)textSize;
        if (size <= 0)
            return 0f;

        float advance = Spacing * size;
        return text.Length * advance;
    }

    public static void DrawText(string text, Vector2 topLeft, int charSize, Vector4 color)
    {
        TextDraw item = new TextDraw(text, topLeft, charSize, color, TextVAO, TextVBO, TextStride);
        QueueNonBatched(item);
    }

    public static void DrawText(string text, Vector2 topLeft, float charSize, Vector4 color)
    {
        int size = (int)charSize;
        DrawText(text, topLeft, size, color);
    }

    public static void DrawQuad(Vector2 topLeft, Vector2 size, float cornerRadius, Vector4 color)
    {
        QuadInstanceData d = new QuadInstanceData
        {
            TopLeft = topLeft,
            Size = size,
            Color = color,
            Color2 = new Vector4(0f),
            Mode = 0,
            CornerRadius = cornerRadius,
            TextureHandle = 0L,
        };
        Enqueue(d);
    }

    public static void DrawQuadGH(Vector2 topLeft, Vector2 size, float cornerRadius, Vector4 color, Vector4 color2)
    {
        QuadInstanceData d = new QuadInstanceData
        {
            TopLeft = topLeft,
            Size = size,
            Color = color,
            Color2 = color2,
            Mode = 2,
            CornerRadius = cornerRadius,
            TextureHandle = 0L,
        };
        Enqueue(d);
    }

    public static void DrawQuadGV(Vector2 topLeft, Vector2 size, float cornerRadius, Vector4 color, Vector4 color2)
    {
        QuadInstanceData d = new QuadInstanceData
        {
            TopLeft = topLeft,
            Size = size,
            Color = color,
            Color2 = color2,
            Mode = 3,
            CornerRadius = cornerRadius,
            TextureHandle = 0L,
        };
        Enqueue(d);
    }

    public static void DrawQuad(Vector2 topLeft, Vector2 size, float cornerRadius, Texture texture)
    {
        if (texture is TextureBindless bindless)
        {
            QuadInstanceData d = new QuadInstanceData
            {
                TopLeft = topLeft,
                Size = size,
                Color = new Vector4(1f),
                Color2 = new Vector4(0f),
                Mode = 1,
                CornerRadius = cornerRadius,
                TextureHandle = bindless.BindlessHandle,
            };
            Enqueue(d);
        }
        else
        {
            ImmediateQuadDraw item = new ImmediateQuadDraw(topLeft, size, cornerRadius, texture);
            QueueNonBatched(item);
        }
    }

    public static void DrawQuad(Vector2 topLeft, Vector2 size, float cornerRadius, Texture texture, Vector4 color)
    {
        if (texture is TextureBindless bindless)
        {
            QuadInstanceData d = new QuadInstanceData
            {
                TopLeft = topLeft,
                Size = size,
                Color = color,
                Color2 = new Vector4(0f),
                Mode = 1,
                CornerRadius = cornerRadius,
                TextureHandle = bindless.BindlessHandle,
            };
            Enqueue(d);
        }
        else
        {
            ImmediateQuadDraw item = new ImmediateQuadDraw(topLeft, size, cornerRadius, texture, color);
            QueueNonBatched(item);
        }
    }

    public static void DrawLine(Vector2 start, Vector2 end, float thickness, Vector4 color)
    {
        LineDraw item = new LineDraw(start, end, thickness, color);
        QueueNonBatched(item);
    }
}
