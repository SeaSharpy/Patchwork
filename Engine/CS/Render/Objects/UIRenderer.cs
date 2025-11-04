using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using static Patchwork.Engine;
namespace Patchwork.Render.Objects;

public record Font(Texture Texture, float Spacing, float Descent);
public static class UIRenderer
{
    public static Font Font = null!;
    static int VAO, VBO;
    public static Shader QuadS = Shader.Text(
        @"
layout(location=0) in vec2 Position;
out vec2 UV;
uniform vec2 TopLeft;
uniform vec2 Size;
uniform ivec4 Viewport;
void main() {
    vec2 TopLeftOffset = TopLeft - vec2(Viewport.xy);
    vec2 ScaledTopLeftOffset = TopLeftOffset / vec2(Viewport.zw);
    UV = Position;
    vec2 ScaledSize = Size / Viewport.zw;
    vec2 WorldPosition = ScaledTopLeftOffset + UV * ScaledSize;
    vec2 NDC = WorldPosition * 2.0 - 1.0;
    gl_Position = vec4(NDC, 0.0, 1.0);
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
    return clamp(n1 + n2 - 1.0, -1.0, 1.0); // [-1, 1]
}

void main()
{
    vec2 p_px = UV * Size;
    vec2 halfSize = 0.5 * Size;
    vec2 centered = p_px - halfSize;

    float maxR = min(halfSize.x, halfSize.y);
    float r = clamp(CornerRadius, 0.0, maxR);

    float sd = sdRoundedBox(centered, halfSize, r);

    float aa = fwidth(sd);
    float mask = 1.0 - smoothstep(0.0, aa, sd);

    // Base color selection
    vec4 baseColor = (Mode == 1) ? texture(Texture, UV) * Color : Color;
    if (Mode == 2) {
        baseColor = mix(baseColor, Color2, UV.x);
    } else if (Mode == 3) {
        baseColor = mix(baseColor, Color2, UV.y);
    }

    if (Mode == 2 || Mode == 3)
    {
        float n = hash21(gl_FragCoord.xy);
        baseColor.rgb = clamp(baseColor.rgb + n * 1/255.0, 0.0, 1.0);
    }

    FragColor = vec4(baseColor.rgb, mask * baseColor.a);
}

",
        "Quad"
        );

    public static Shader Text = Shader.Text(
        @"
layout(location=0) in vec2 Position;
layout(location=1) in vec2 vUV;
out vec2 UV;
uniform ivec4 Viewport;
void main() {
    vec2 TopLeftOffset = Position - vec2(Viewport.xy);
    vec2 WorldPosition = TopLeftOffset / vec2(Viewport.zw);
    UV = vUV;
    vec2 NDC = WorldPosition * 2.0 - 1.0;
    gl_Position = vec4(NDC, 0.0, 1.0);
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

    static int Stride = sizeof(float) * 4;
    public static void Init()
    {
        VAO = GL.GenVertexArray();
        VBO = GL.GenBuffer();
        GL.BindVertexArray(VAO);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, Stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Stride, sizeof(float) * 2);

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }
    public static float Spacing => Font.Spacing;
    public static float Descent => Font.Descent;
    static string Descenders =
    "gjpqyQ";
    static string SecondaryDescenders =
    ",_;|()[]{}\\";
    public static void DrawText(string text, Vector2 topLeft, int charSize, Vector4 color)
    {
        GL.BindVertexArray(VAO);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);

        int numChars = text.Length;
        float[] verts = new float[numChars * 6 * 4];
        int s = 0;

        const float invAtlas = 1.0f / 16.0f;

        float cursorX = topLeft.X;
        float offsetCursor = Spacing * charSize;
        float cursorY = topLeft.Y;

        foreach (char c in text)
        {
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
            float x1 = cursorX + charSize;
            float y1 = cursorY + charSize;
            if (Descenders.Contains(c))
            {
                y0 -= charSize * Descent;
                y1 -= charSize * Descent;
            }
            else if (SecondaryDescenders.Contains(c))
            {
                y0 -= charSize * Descent * 0.5f;
                y1 -= charSize * Descent * 0.5f;
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

        Text.Use();
        Text.Set("Viewport", Viewport);
        Text.Set("Color", color);
        Font.Texture.Bind(0);
        Text.Set("Texture", 0);

        GL.DrawArrays(PrimitiveType.Triangles, 0, numChars * 6);

        GL.BindVertexArray(0);
    }
    public static void DrawText(string text, Vector2 topLeft, float charSize, Vector4 color) => DrawText(text, topLeft, (int)charSize, color);

    public static void DrawQuad(Vector2 topLeft, Vector2 size, float cornerRadius, Texture texture)
    {
        QuadS.Use();
        QuadS.Set("Viewport", Viewport);
        QuadS.Set("Size", size);
        QuadS.Set("TopLeft", topLeft);
        QuadS.Set("CornerRadius", cornerRadius);
        QuadS.Set("Color", new Vector4(1f));
        QuadS.Set("Mode", 1);
        texture.Bind(0);
        QuadS.Set("Texture", 0);
        Quad.Draw();
    }
    public static void DrawQuad(Vector2 topLeft, Vector2 size, float cornerRadius, Texture texture, Vector4 color)
    {
        QuadS.Use();
        QuadS.Set("Viewport", Viewport);
        QuadS.Set("Size", size);
        QuadS.Set("TopLeft", topLeft);
        QuadS.Set("CornerRadius", cornerRadius);
        QuadS.Set("Color", color);
        QuadS.Set("Mode", 1);
        texture.Bind(0);
        QuadS.Set("Texture", 0);
        Quad.Draw();
    }
    public static void DrawQuad(Vector2 topLeft, Vector2 size, float cornerRadius, Vector4 color)
    {
        QuadS.Use();
        QuadS.Set("Viewport", Viewport);
        QuadS.Set("Size", size);
        QuadS.Set("TopLeft", topLeft);
        QuadS.Set("CornerRadius", cornerRadius);
        QuadS.Set("Mode", 0);
        QuadS.Set("Color", color);
        Quad.Draw();
    }
    public static void DrawQuadGV(Vector2 topLeft, Vector2 size, float cornerRadius, Vector4 color, Vector4 color2)
    {
        QuadS.Use();
        QuadS.Set("Viewport", Viewport);
        QuadS.Set("Size", size);
        QuadS.Set("TopLeft", topLeft);
        QuadS.Set("CornerRadius", cornerRadius);
        QuadS.Set("Mode", 3);
        QuadS.Set("Color", color);
        QuadS.Set("Color2", color2);
        Quad.Draw();
    }
    public static void DrawQuadGH(Vector2 topLeft, Vector2 size, float cornerRadius, Vector4 color, Vector4 color2)
    {
        QuadS.Use();
        QuadS.Set("Viewport", Viewport);
        QuadS.Set("Size", size);
        QuadS.Set("TopLeft", topLeft);
        QuadS.Set("CornerRadius", cornerRadius);
        QuadS.Set("Mode", 2);
        QuadS.Set("Color", color);
        QuadS.Set("Color2", color2);
        Quad.Draw();
    }
    public static void Dispose()
    {
        QuadS.Dispose();
        Text.Dispose();
        GL.DeleteBuffer(VBO);
        GL.DeleteVertexArray(VAO);
    }
}