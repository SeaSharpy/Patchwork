namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public virtual IEnumerable<string> Inputs { get; } = [];
    public virtual IEnumerable<string> Outputs { get; } = [];
    public virtual void Input(string name) { }
    public virtual void Server() { }
    public virtual void Client() { }
    public virtual void MessageRecieved(string name, BinaryReader reader) { }
    public virtual void MessageRecievedServer(string player, string name, BinaryReader reader) { }
    public virtual void MessageRecievedClient(string name, BinaryReader reader) { }
    public virtual float SyncInterval { get; } = 0.02f;
    public virtual bool PhysicsObject { get; } = false;
}