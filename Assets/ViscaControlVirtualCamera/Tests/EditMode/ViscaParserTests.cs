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
        var frame = new byte[] { 0x81, 0x01, 0x06, 0x02, 0x08, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0xFF };
        Assert.IsTrue(ViscaParser.TryParsePanTiltAbsolute(frame, out var vv, out var ww, out var pan, out var tilt));
        Assert.AreEqual(0, vv);
        Assert.AreEqual(0, ww);
        Assert.AreEqual(0x0800, pan);
        Assert.AreEqual(0x0800, tilt);
    }
}

