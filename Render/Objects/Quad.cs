using OpenTK.Graphics.OpenGL4;

namespace Patchwork.Render.Objects;

public static class Quad
{
    private static int VAO, VBO;
    public static int VertexCount => 6;
    public static readonly float[] Verts = { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f, 1f };
    private static bool Ready;

    public static void EnsureReady()
    {
        if (Ready) return;
        VAO = GL.GenVertexArray();
        VBO = GL.GenBuffer();
        GL.BindVertexArray(VAO);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
        GL.BufferData(BufferTarget.ArrayBuffer, Verts.Length * sizeof(float), Verts, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 2, 0);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        Ready = true;
    }

    public static void Draw()
    {
        EnsureReady();
        GL.BindVertexArray(VAO);
        GL.DrawArrays(PrimitiveType.Triangles, 0, VertexCount);
        GL.BindVertexArray(0);
    }

    public static void Dispose()
    {
        if (!Ready) return;
        GL.DeleteBuffer(VBO);
        GL.DeleteVertexArray(VAO);
        Ready = false;
    }
}
