namespace Patchwork;

public partial class Entity : IDisposable
{
    [SerializedMember(flags: ["runtime"])] public uint ID { get; private set; }
    [SerializedMember(flags: ["runtime"])] public bool Dead { get; private set; } = false;
    public bool Disposed { get; private set; }
    public bool Disposing { get; private set; }
    public float DisposeTimer { get; private set; }
    public void Kill() => Dead = true;
    private bool Initialized = false;
    protected Entity()
    {
        ID = GetID();
    }
    public void Dispose()
    {
        Kill();
        if (Disposed) return;
        Disposed = true;
        Disposing = false;
        if (Physics && Handle.HasValue)
            PhysicsManager.DisposeBody(Handle.Value);
        DisposeExtras();
        lock (Entities)
            Entities.Remove(ID);
    }
    private void Initialize()
    {
        if (Initialized) return;
        Initialized = true;
        if (Physics)
            PhysicsManager.Add(this);
    }
    public void DisposeIn(float time)
    {
        Kill();
        if (Disposed || (Disposing && time > DisposeTimer)) return;
        Disposing = true;
        DisposeTimer = time;
    }
    private void AddToEntities()
    {
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