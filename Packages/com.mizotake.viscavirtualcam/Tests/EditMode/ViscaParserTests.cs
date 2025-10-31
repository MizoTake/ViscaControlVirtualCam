using NUnit.Framework;
using ViscaControlVirtualCam;

public class ViscaParserTests
{
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
        Assert.IsTrue(ViscaParser.TryParsePanTiltDrive(frame, out var vv, out var ww, out var pp, out var tt));
        Assert.AreEqual(0x10, vv);
        Assert.AreEqual(0x05, ww);
        Assert.AreEqual(0x01, pp);
        Assert.AreEqual(0x01, tt);
    }

    [Test]
    public void GetCommandName_Unknown()
    {
        var frame = new byte[] { 0x81, 0x01, 0x02, 0x03, 0xFF };
        var name = ViscaParser.GetCommandName(frame);
        StringAssert.StartsWith("Unknown(", name);
    }

    [Test]
    public void Parse_PanTiltAbsolute_Speedless_Works()
    {
        // 8X 01 06 02 p1 p2 p3 p4 t1 t2 t3 t4 FF (no speed bytes)
        var frame = new byte[] { 0x81, 0x01, 0x06, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0xFF };
        Assert.IsTrue(ViscaParser.TryParsePanTiltAbsolute(frame, out var vv, out var ww, out var pan, out var tilt));
        Assert.AreEqual(0, vv);
        Assert.AreEqual(0, ww);
        Assert.AreEqual(0x0800, pan);
        Assert.AreEqual(0x0800, tilt);
    }

    // Blackmagic PTZ Control Tests
    [Test]
    public void Parse_ZoomDirect_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x47, 0x0A, 0x0B, 0x0C, 0x0D, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseZoomDirect(frame, out var zoomPos));
        Assert.AreEqual(0xABCD, zoomPos);
    }

    [Test]
    public void Parse_FocusVariable_Far_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x08, 0x02, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseFocusVariable(frame, out var focusSpeed));
        Assert.AreEqual(0x02, focusSpeed); // Far
    }

    [Test]
    public void Parse_FocusVariable_Near_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x08, 0x03, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseFocusVariable(frame, out var focusSpeed));
        Assert.AreEqual(0x03, focusSpeed); // Near
    }

    [Test]
    public void Parse_FocusDirect_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x48, 0x01, 0x02, 0x03, 0x04, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseFocusDirect(frame, out var focusPos));
        Assert.AreEqual(0x1234, focusPos);
    }

    [Test]
    public void Parse_IrisVariable_Open_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x0B, 0x02, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseIrisVariable(frame, out var irisDir));
        Assert.AreEqual(0x02, irisDir); // Open
    }

    [Test]
    public void Parse_IrisVariable_Close_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x0B, 0x03, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseIrisVariable(frame, out var irisDir));
        Assert.AreEqual(0x03, irisDir); // Close
    }

    [Test]
    public void Parse_IrisDirect_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x4B, 0x05, 0x06, 0x07, 0x08, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseIrisDirect(frame, out var irisPos));
        Assert.AreEqual(0x5678, irisPos);
    }

    [Test]
    public void Parse_MemoryRecall_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x05, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseMemoryRecall(frame, out var memoryNumber));
        Assert.AreEqual(0x05, memoryNumber);
    }

    [Test]
    public void Parse_MemorySet_Works()
    {
        var frame = new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x01, 0x03, 0xFF };
        Assert.IsTrue(ViscaParser.TryParseMemorySet(frame, out var memoryNumber));
        Assert.AreEqual(0x03, memoryNumber);
    }

    [Test]
    public void GetCommandName_Blackmagic_Commands()
    {
        Assert.AreEqual("ZoomDirect", ViscaParser.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x47, 0x00, 0x00, 0x00, 0x00, 0xFF }));
        Assert.AreEqual("FocusVariable", ViscaParser.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x08, 0x02, 0xFF }));
        Assert.AreEqual("FocusDirect", ViscaParser.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x48, 0x00, 0x00, 0x00, 0x00, 0xFF }));
        Assert.AreEqual("IrisVariable", ViscaParser.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x0B, 0x02, 0xFF }));
        Assert.AreEqual("IrisDirect", ViscaParser.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x4B, 0x00, 0x00, 0x00, 0x00, 0xFF }));
        Assert.AreEqual("MemoryRecall", ViscaParser.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x00, 0xFF }));
        Assert.AreEqual("MemorySet", ViscaParser.GetCommandName(new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x01, 0x00, 0xFF }));
    }
}
