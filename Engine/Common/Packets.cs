namespace Patchwork;

public enum PacketType : uint
{
    Auth = 0,
    Entity = 1,
    Destroy = 2,
    Clear = 3,
    EntityMessage = 4,
    Globals = 5,
}