using System.Numerics;
public class TestEntity : Entity
{
    public override IEnumerable<string> Inputs => new[] { "test" };
    public override IEnumerable<string> Outputs => new[] { "test" };
    public override float SyncInterval => 0.02f;
    public override void Input(string name)
    {
        if (name == "test")
            WriteLine("Test input");
    }
    public override void Server()
    {
        Position = new Vector3(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle());
    }
    public override void Client()
    {
    }
}