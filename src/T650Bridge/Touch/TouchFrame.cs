namespace T650Bridge.Touch;

public enum ContactStatus : byte
{
    None = 0,
    Touch = 1,
    Hover = 2
}

public struct TouchContact
{
    public byte Id { get; init; }
    public ContactStatus Status { get; init; }
    public byte Type { get; init; }
    public ushort X { get; init; }
    public ushort Y { get; init; }
    public byte Area { get; init; }
    public bool IsTouching => Id != 0;
}

public class TouchFrame
{
    public ushort HardwareTimestamp { get; set; }
    public long SystemTimestampMicroseconds { get; set; }
    public byte DeclaredFingerCount { get; set; }
    public bool PhysicalButtonPressed { get; set; }
    public List<TouchContact> Contacts { get; } = new();
}
