using System.Numerics;
public class TestEntity : Entity
{
    public override IEnumerable<string> Inputs => ["test"];
    public override IEnumerable<string> Outputs => ["test"];
    public override float SyncInterval => 0.1f;
    public override void Input(string name)
    {
        
    }
    public override void Server()
    {
        Position = new Vector3(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle());
    }
    public override void Client()
    {
        WriteLine($"{Helper.DeltaTime}");
        WriteLine($"{Position}");
    }
}