namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public void Tick()
    {
        Client();
    }
}