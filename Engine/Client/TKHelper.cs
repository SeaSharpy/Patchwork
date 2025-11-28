global using TKVector4 = OpenTK.Mathematics.Vector4;
global using TKVector3 = OpenTK.Mathematics.Vector3;
global using TKVector2 = OpenTK.Mathematics.Vector2;
global using TKQuaternion = OpenTK.Mathematics.Quaternion;
global using static Patchwork.TKHelper;
namespace Patchwork;

public static class TKHelper
{
    public static TKVector3 TK(Vector3 v) => new(v.X, v.Y, v.Z);
    public static TKVector2 TK(Vector2 v) => new(v.X, v.Y);
    public static TKVector4 TK(Vector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static TKQuaternion TK(Quaternion v) => new(v.X, v.Y, v.Z, v.W);
    public static Vector3 NU(TKVector3 v) => new(v.X, v.Y, v.Z);
    public static Vector2 NU(TKVector2 v) => new(v.X, v.Y);
    public static Vector4 NU(TKVector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static Quaternion NU(TKQuaternion v) => new(v.X, v.Y, v.Z, v.W);
}