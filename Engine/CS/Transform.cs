using OpenTK.Mathematics;
public struct Transform
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }

    public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    public Transform()
    {
        Position = Vector3.Zero;
        Rotation = Quaternion.Identity;
        Scale = Vector3.One;
    }

    public Matrix4 Matrix()
    {
        var t = Matrix4.CreateTranslation(Position);
        var r = Matrix4.CreateFromQuaternion(Rotation);
        var s = Matrix4.CreateScale(Scale);
        return s * r * t;
    }
}
