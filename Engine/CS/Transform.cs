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
        Matrix4 t = Matrix4.CreateTranslation(Position);
        Matrix4 r = Matrix4.CreateFromQuaternion(Rotation);
        Matrix4 s = Matrix4.CreateScale(Scale);
        return s * r * t;
    }
}
