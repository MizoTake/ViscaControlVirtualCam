using NUnit.Framework;
using ViscaControlVirtualCam;

public class ViscaFrameFramerTests
{
    [Test]
    public void SplitsBy0xFFAcrossReads()
    {
        var framer = new ViscaFrameFramer(256);
        int count = 0;
        void OnFrame(byte[] f)
        {
            count++;
            Assert.AreEqual(0xFF, f[^1]);
        }
        var part1 = new byte[] { 0x81, 0x01, 0x04 };
        var part2 = new byte[] { 0x07, 0x00, 0xFF, 0x81, 0x01 };
        var part3 = new byte[] { 0x06, 0x01, 0x10, 0x05, 0x01, 0x01, 0xFF };
        framer.Append(part1, OnFrame);
        framer.Append(part2, OnFrame);
        framer.Append(part3, OnFrame);
        Assert.AreEqual(2, count);
    }
}

