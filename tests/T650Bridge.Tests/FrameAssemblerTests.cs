using T650Bridge.Touch;

namespace T650Bridge.Tests;

public class FrameAssemblerTests
{
    private const int ReportLength = 20;

    /// <summary>Builds a raw HID++ TOUCHPAD_RAW_XY report with up to two contacts.</summary>
    private static byte[] Report(
        (byte Id, int X, int Y)? a = null,
        (byte Id, int X, int Y)? b = null,
        bool button = false,
        bool endOfFrame = true,
        byte fingerCount = 0)
    {
        var r = new byte[ReportLength];
        r[0] = 0x11;
        if (a is var (idA, xA, yA))
            WriteContact(r, 6, xA, yA);
        if (b is var (idB, xB, yB))
            WriteContact(r, 13, xB, yB);

        r[12] = (byte)(((a?.Id ?? 0) << 4) | (button ? 0x04 : 0) | (endOfFrame ? 0x01 : 0));
        r[19] = (byte)(((b?.Id ?? 0) << 4) | fingerCount);
        return r;
    }

    private static void WriteContact(byte[] r, int offset, int x, int y)
    {
        r[offset] = (byte)((x >> 8) & 0x3F);
        r[offset + 1] = (byte)(x & 0xFF);
        r[offset + 2] = (byte)(0x40 | ((y >> 8) & 0x3F)); // status = Touch
        r[offset + 3] = (byte)(y & 0xFF);
        r[offset + 5] = 0x20;
    }

    private static List<TouchFrame> Collect(FrameAssembler assembler)
    {
        var frames = new List<TouchFrame>();
        assembler.FrameReady += frames.Add;
        return frames;
    }

    [Fact]
    public void SingleReport_WithEndOfFrame_EmitsFrame()
    {
        var assembler = new FrameAssembler();
        var frames = Collect(assembler);

        assembler.ProcessRawReport(Report(a: (1, 100, 200), fingerCount: 1));

        var frame = Assert.Single(frames);
        var contact = Assert.Single(frame.Contacts);
        Assert.Equal(1, contact.Id);
        Assert.Equal(100, contact.X);
        Assert.Equal(ContactStatus.Touch, contact.Status);
        Assert.Equal(1, frame.DeclaredFingerCount);
    }

    [Fact]
    public void Y_IsInvertedToTopLeftOrigin()
    {
        var assembler = new FrameAssembler();
        var frames = Collect(assembler);

        assembler.ProcessRawReport(Report(a: (1, 0, 364)));

        Assert.Equal(FrameAssembler.MaxY - 364, frames[0].Contacts[0].Y);
    }

    [Fact]
    public void OutOfRangeCoordinates_AreClamped()
    {
        var assembler = new FrameAssembler();
        var frames = Collect(assembler);

        assembler.ProcessRawReport(Report(a: (1, 0x3FFF, 0x3FFF)));

        Assert.Equal(FrameAssembler.MaxX, frames[0].Contacts[0].X);
        Assert.Equal(0, frames[0].Contacts[0].Y);
    }

    [Fact]
    public void ContactsAcrossReports_AreCombinedUntilEndOfFrame()
    {
        var assembler = new FrameAssembler();
        var frames = Collect(assembler);

        assembler.ProcessRawReport(Report(a: (1, 100, 100), b: (2, 200, 200), endOfFrame: false, fingerCount: 3));
        Assert.Empty(frames);

        assembler.ProcessRawReport(Report(a: (3, 300, 300)));

        Assert.Equal([1, 2, 3], frames.Single().Contacts.Select(c => (int)c.Id));
        Assert.Equal(3, frames[0].DeclaredFingerCount);
    }

    [Fact]
    public void ButtonBit_SetsPhysicalButtonForThatFrameOnly()
    {
        var assembler = new FrameAssembler();
        var frames = Collect(assembler);

        assembler.ProcessRawReport(Report(a: (1, 100, 100), button: true));
        assembler.ProcessRawReport(Report(a: (1, 100, 100)));

        Assert.True(frames[0].PhysicalButtonPressed);
        Assert.False(frames[1].PhysicalButtonPressed);
    }

    [Theory]
    [InlineData(19, 0x11)]
    [InlineData(20, 0x10)]
    public void InvalidReports_AreIgnored(int length, byte reportId)
    {
        var assembler = new FrameAssembler();
        var frames = Collect(assembler);
        var report = new byte[length];
        report[0] = reportId;
        if (length > 12) report[12] = 0x11;

        assembler.ProcessRawReport(report);

        Assert.Empty(frames);
    }

    [Fact]
    public void EmptyFrame_HasNoContacts()
    {
        var assembler = new FrameAssembler();
        var frames = Collect(assembler);

        assembler.ProcessRawReport(Report());

        Assert.Empty(frames.Single().Contacts);
    }
}
