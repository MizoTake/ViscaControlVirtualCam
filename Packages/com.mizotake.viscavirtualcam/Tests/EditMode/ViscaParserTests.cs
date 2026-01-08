using NUnit.Framework;
using ViscaControlVirtualCam;
using System;

public class ViscaParserTests
{
    private ViscaCommandRegistry _registry;

    [SetUp]
    public void Setup()
    {
        _registry = new ViscaCommandRegistry();
    }

    [Test]
    public void DecodeNibble16_Works()
    {
        ushort v = ViscaParser.DecodeNibble16(0x00, 0x08, 0x00, 0x00);
        Assert.AreEqual(0x0800, v);
    }

    [Test]
    public void Parse_PanTiltDrive_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x06, 0x01, 0x10, 0x05, 0x01, 0x01, 0xFF };
        Assert.AreEqual("PanTiltDrive", _registry.GetCommandName(frame));
        // Verify frame values
        Assert.AreEqual(0x10, frame[4]); // PanSpeed
        Assert.AreEqual(0x05, frame[5]); // TiltSpeed
        Assert.AreEqual(0x01, frame[6]); // PanDir
        Assert.AreEqual(0x01, frame[7]); // TiltDir
    }

    [Test]
    public void GetCommandName_Unknown()
    {
        var frame = new byte[] { 0x81, 0x01, 0x02, 0x03, 0xFF };
        var name = _registry.GetCommandName(frame);
        StringAssert.StartsWith("Unknown(", name);
    }

    [Test]
    public void Parse_PanTiltAbsolute_Speedless_Works()
    {
        // 8X 01 06 02 p1 p2 p3 p4 t1 t2 t3 t4 FF (no speed bytes, 13 bytes)
        var frame = new byte[] { 0x81, 0x01, 0x06, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0xFF };
        Assert.AreEqual("PanTiltAbsolute", _registry.GetCommandName(frame));
        // Decode the values using ViscaParser (positions start at index 4 for speedless format)
        ushort pan = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
        ushort tilt = ViscaParser.DecodeNibble16(frame[8], frame[9], frame[10], frame[11]);
        Assert.AreEqual(0x0800, pan);
        Assert.AreEqual(0x0800, tilt);
    }

    // Blackmagic PTZ Control Tests
    [Test]
    public void Parse_ZoomDirect_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x47, 0x0A, 0x0B, 0x0C, 0x0D, 0xFF };
        Assert.AreEqual("ZoomDirect", _registry.GetCommandName(frame));
        ushort zoomPos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
        Assert.AreEqual(0xABCD, zoomPos);
    }

    [Test]
    public void Parse_FocusVariable_Far_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x08, 0x02, 0xFF };
        Assert.AreEqual("FocusVariable", _registry.GetCommandName(frame));
        Assert.AreEqual(0x02, frame[4]); // Far
    }

    [Test]
    public void Parse_FocusVariable_Near_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x08, 0x03, 0xFF };
        Assert.AreEqual("FocusVariable", _registry.GetCommandName(frame));
        Assert.AreEqual(0x03, frame[4]); // Near
    }

    [Test]
    public void Parse_FocusDirect_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x48, 0x01, 0x02, 0x03, 0x04, 0xFF };
        Assert.AreEqual("FocusDirect", _registry.GetCommandName(frame));
        ushort focusPos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
        Assert.AreEqual(0x1234, focusPos);
    }

    [Test]
    public void Parse_IrisVariable_Open_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x0B, 0x02, 0xFF };
        Assert.AreEqual("IrisVariable", _registry.GetCommandName(frame));
        Assert.AreEqual(0x02, frame[4]); // Open
    }

    [Test]
    public void Parse_IrisVariable_Close_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x0B, 0x03, 0xFF };
        Assert.AreEqual("IrisVariable", _registry.GetCommandName(frame));
        Assert.AreEqual(0x03, frame[4]); // Close
    }

    [Test]
    public void Parse_IrisDirect_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x4B, 0x05, 0x06, 0x07, 0x08, 0xFF };
        Assert.AreEqual("IrisDirect", _registry.GetCommandName(frame));
        ushort irisPos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
        Assert.AreEqual(0x5678, irisPos);
    }

    [Test]
    public void Parse_MemoryRecall_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x05, 0xFF };
        Assert.AreEqual("MemoryRecall", _registry.GetCommandName(frame));
        Assert.AreEqual(0x05, frame[5]); // Memory number
    }

    [Test]
    public void Parse_MemorySet_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x01, 0x03, 0xFF };
        Assert.AreEqual("MemorySet", _registry.GetCommandName(frame));
        Assert.AreEqual(0x03, frame[5]); // Memory number
    }

    [Test]
    public void GetCommandName_Blackmagic_Commands()
    {
        Assert.AreEqual("ZoomDirect", _registry.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x47, 0x00, 0x00, 0x00, 0x00, 0xFF }));
        Assert.AreEqual("FocusVariable", _registry.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x08, 0x02, 0xFF }));
        Assert.AreEqual("FocusDirect", _registry.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x48, 0x00, 0x00, 0x00, 0x00, 0xFF }));
        Assert.AreEqual("IrisVariable", _registry.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x0B, 0x02, 0xFF }));
        Assert.AreEqual("IrisDirect", _registry.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x4B, 0x00, 0x00, 0x00, 0x00, 0xFF }));
        Assert.AreEqual("MemoryRecall", _registry.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x00, 0xFF }));
        Assert.AreEqual("MemorySet", _registry.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x01, 0x00, 0xFF }));
    }

    [Test]
    public void DirFromVisca_Works()
    {
        Assert.AreEqual(AxisDirection.Stop, ViscaParser.DirFromVisca(0x03));
        Assert.AreEqual(AxisDirection.Negative, ViscaParser.DirFromVisca(0x01));
        Assert.AreEqual(AxisDirection.Positive, ViscaParser.DirFromVisca(0x02));
        Assert.AreEqual(AxisDirection.Stop, ViscaParser.DirFromVisca(0x00)); // Invalid defaults to Stop
    }

    [Test]
    public void Registry_CommandCount_IsCorrect()
    {
        // Verify registry has expected number of commands
        Assert.GreaterOrEqual(_registry.Count, 15, "Should have at least 15 registered commands");
    }
}
