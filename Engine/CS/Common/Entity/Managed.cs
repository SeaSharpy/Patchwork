namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    [SerializedMember(save: false)] public uint ID { get; private set; }
    [SerializedMember(save: false)] public bool Dead { get; private set; } = false;
    public bool Disposed { get; private set; }
    public bool Disposing { get; private set; }
    public float DisposeTimer { get; private set; }
    public void Kill() => Dead = true;
    public void Dispose()
    {
        Kill();
        if (Disposed) return;
        Disposed = true;
        Disposing = false;

        lock (Entities)
            if (Entities.Remove(ID))
                FreeIds.Push(ID);
    }
    public void DisposeIn(float time)
    {
        Kill();
        if (Disposed || (Disposing && time > DisposeTimer)) return;
        Disposing = true;
        DisposeTimer = time;
    }
    protected Entity()
    {
        ID = GetID();
        lock (Entities)
            Entities.Add(ID, this);
    }
    public struct Connection
    {
        public string InputObjectName;
        public string InputName;
        public string Output;
    }
    public List<(Connection connection, float wait)> QueuedOutputs = new();
    public void Output(string name, float wait = 0)
    {
        Connection connection = Connections.First(c => c.Output == name);
        if (string.IsNullOrWhiteSpace(connection.InputObjectName))
            throw new InvalidDataException("Connection has no input object.");
        QueuedOutputs.Add((connection, wait));
    }
}