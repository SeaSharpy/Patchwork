using OpenTK.Graphics.OpenGL4;
namespace Patchwork;

public partial class Engine
{
    public void WindowLoad()
    {
        GameClient.Connect("127.0.0.1", 4000, "Walt");
        Entity.SetupPackets();
    }
    public void WindowUnload()
    {
    }
    public void Render()
    {
        GL.ClearColor(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle(), 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);
    }

}