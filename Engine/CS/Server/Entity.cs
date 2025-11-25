namespace Patchwork;

public abstract partial class Entity : IDisposable
{
    public void Tick()
    {
        if (Disposed) return;
        if (Disposing)
        {
            DisposeTimer -= Helper.DeltaTime;
            if (DisposeTimer <= 0)
                Dispose();
        }
        lock (QueuedOutputs)
            if (QueuedOutputs.Count > 0)
                for (int i = 0; i < QueuedOutputs.Count; i++)
                    if (QueuedOutputs[i].wait <= 0)
                    {
                        Connection connection = QueuedOutputs[i].connection;
                        Entity entity = GetEntity(connection.InputObjectName);
                        entity.Input(connection.InputName);
                        QueuedOutputs.RemoveAt(i);
                        i--;
                    }
                    else
                        QueuedOutputs[i] = QueuedOutputs[i] with { wait = QueuedOutputs[i].wait - Helper.DeltaTime };
        Server();
    }
}