namespace Patchwork.Render;

public struct Box
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Box(Vector2 pos, Vector2 size)
    {
        X = (int)pos.X;
        Y = (int)pos.Y;
        Width = (int)size.X;
        Height = (int)size.Y;
    }
    public Box(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    public Box(float x, float y, float width, float height)
    {
        X = (int)x;
        Y = (int)y;
        Width = (int)width;
        Height = (int)height;
    }
    public Vector4 ToVector4() => new(X, Y, Width, Height);
    public Vector2 XY => new(X, Y);
    public Vector2 Size { get => new(Width, Height); set { Width = (int)value.X; Height = (int)value.Y; } }
    public bool Contains(Vector2 point) => point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
    public bool Contains(Box box) =>
        box.X >= X &&
        box.Y >= Y &&
        box.X + box.Width <= X + Width &&
        box.Y + box.Height <= Y + Height;
    public Matrix4 ToOrthoMatrix(float zNear = -1f, float zFar = 1f, bool yDown = false)
    {
        float left = X;
        float right = X + Width;
        float top = Y;
        float bottom = Y + Height;

        if (!yDown)
        {
            bottom = Y;
            top = Y + Height;
        }

        return Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, zNear, zFar);
    }
}
public interface IRenderSystem : ISystem
{
    public void Render();
}