using OpenTK.Graphics.OpenGL4;
namespace Patchwork;

public partial class Engine
{
    public void ClientLoad()
    {
        GameClient.Connect("127.0.0.1", 4000, "Walt");
        DriveMounts.Mount("A", new HttpFileSystem("http://localhost:4001/"));
        Entity.SetupPackets();
    }
    public void ClientUnload()
    {
    }
    public int Loading = 0;
    public void Render()
    {
        GL.ClearColor(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle(), 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        if (FrameGraph.Build())
        {
            Loading = 0;
        }
        else
        {
            float value = Loading++;
            float color = 1f - (1f / (1f + value));
            GL.ClearColor(color, color, color, 1);
        }
    }

}