namespace Patchwork;
public static partial class Globals
{
    [SerializedMember] public static Vector3 Gravity { get; private set; } = new Vector3(0, -10, 0);
    [SerializedMember] public static ITexture? Skybox { get; private set; } = new PathTexture
    {
        Path = "A:/Textures/a_skybox.pwtex",
    };
}