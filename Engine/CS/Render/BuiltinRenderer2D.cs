using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;


namespace Patchwork.Render;

public class BuiltinRenderer2D : IRenderSystem
{
    public void Render()
    {

    }
    public void Dispose()
    {

    }
}
public class Sprite : IDataComponent
{
    public Texture Texture;

    public Sprite(Texture texture)
    {
        Texture = texture;
    }
}
