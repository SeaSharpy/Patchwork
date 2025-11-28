namespace Patchwork;

public partial class Engine
{
    public void ServerLoad()
    {
        DriveMounts.Mount("A", new MultiZipFileSystem("."));
        DriveMountHttpHost host = new DriveMountHttpHost("A", "http://localhost:4001/");
        _ = GameServer.StartAsync(4000);
        Entity.SetupPackets();
    }
    public void ServerUnload()
    {
    }
}