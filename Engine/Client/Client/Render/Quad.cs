using OpenTK.Graphics.OpenGL4;

public static class FullscreenQuad
{
    public static int Vao { get; private set; }
    public static int Vbo { get; private set; }
    private static bool Initialized = false;

    public static void Init()
    {
        if (Initialized) return;
        Initialized = true;
        float[] vertices =
        {
            -1f, -1f,           0f, 0f,
             1f, -1f,           1f, 0f,
            -1f,  1f,           0f, 1f,
             1f,  1f,           1f, 1f
        };

        Vao = GL.GenVertexArray();
        Vbo = GL.GenBuffer();

        GL.BindVertexArray(Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public static void Draw()
    {
        Init();
        GL.BindVertexArray(Vao);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }

    public static void Dispose()
    {
        GL.DeleteBuffer(Vbo);
        GL.DeleteVertexArray(Vao);
    }
}
