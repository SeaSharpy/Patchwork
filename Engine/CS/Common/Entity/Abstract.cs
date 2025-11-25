namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public abstract IEnumerable<string> Inputs { get; }
    public abstract IEnumerable<string> Outputs { get; }
    public abstract void Input(string name);
    public abstract void Server();
    public abstract void Client();
}