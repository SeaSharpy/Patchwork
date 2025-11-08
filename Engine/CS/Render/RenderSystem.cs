namespace Patchwork.Render;

public struct Box
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public Box(Vector2 pos, Vector2 size)
    {
        X = pos.X;
        Y = pos.Y;
        Width = size.X;
        Height = size.Y;
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
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    public Vector4 ToVector4() => new(X, Y, Width, Height);
    public Vector2 XY => new(X, Y);
    public Vector2 Size { get => new(Width, Height); set { Width = value.X; Height = value.Y; } }
    public bool Contains(Vector2 point) => point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
    public bool Contains(Box box) =>
        box.X >= X &&
        box.Y >= Y &&
        box.X + box.Width <= X + Width &&
        box.Y + box.Height <= Y + Height;
    public bool Intersects(Box box) =>
        !(box.X > X + Width ||
          box.X + box.Width < X ||
          box.Y > Y + Height ||
          box.Y + box.Height < Y);
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

    public override string ToString() => $"{X}, {Y}, {Width}, {Height}";
}
public interface IRenderSystem : ISystem
{
    public void Render();
}
