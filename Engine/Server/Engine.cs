namespace Patchwork;

public partial class Engine
{
    public void ServerLoad()
    {
        DriveMounts.Mount("A", new MultiZipFileSystem("./Assets"));
        DriveMountHttpHost host = new DriveMountHttpHost("A", "http://localhost:4001/");
        host.Start();
        _ = GameServer.StartAsync(4000);
        Entity.SetupPackets();
        Entity entity = Entity.Create(typeof(Entity));
        entity.Model = new Model
        {
            DataPath = "A:/Models/gun.pwmdl",
            Albedo = new PathTexture
            {
                Path = "A:/Textures/a_backrooms.pwtex"
            }
        };
    }
    public void UpdateExtrasPre()
    {
        Globals.Sync();
    }
    public void UpdateExtrasPost()
    {

    }
    public void ServerUnload()
    {
        GameServer.Dispose();
    }
}