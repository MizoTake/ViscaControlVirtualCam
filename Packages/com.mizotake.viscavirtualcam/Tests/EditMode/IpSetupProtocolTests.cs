using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class IpSetupProtocolTests
{
    [Test]
    public void FrameCodec_Parse_ValidFrame_ReturnsUnits()
    {
        var frame = new byte[]
        {
            0x02,
            (byte)'E', (byte)'N', (byte)'Q', (byte)':', (byte)'a', (byte)'l', (byte)'l', (byte)'i', (byte)'n', (byte)'f',
            (byte)'o',
            0xFF,
            (byte)'M', (byte)'A', (byte)'C', (byte)':', (byte)'0', (byte)'2', (byte)':', (byte)'1', (byte)'1', (byte)':',
            (byte)'2', (byte)'2', (byte)':', (byte)'3', (byte)'3', (byte)':', (byte)'4', (byte)'4', (byte)':', (byte)'5',
            (byte)'5',
            0xFF,
            0x03
        };

        var parsed = IpSetupFrameCodec.TryParse(frame, out var units, out var error);

        Assert.IsTrue(parsed, error);
        CollectionAssert.AreEqual(new[] { "ENQ:allinfo", "MAC:02:11:22:33:44:55" }, units);
    }

    [Test]
    public void FrameCodec_Parse_InvalidBoundary_ReturnsFalse()
    {
        var frame = new byte[] { 0x00, 0x03 };

        var parsed = IpSetupFrameCodec.TryParse(frame, out _, out var error);

        Assert.IsFalse(parsed);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void FrameCodec_Parse_NullFrame_ReturnsFalse()
    {
        var parsed = IpSetupFrameCodec.TryParse(null, out _, out var error);

        Assert.IsFalse(parsed);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void FrameCodec_Parse_TooShortFrame_ReturnsFalse()
    {
        var frame = new byte[] { 0x02 };

        var parsed = IpSetupFrameCodec.TryParse(frame, out _, out var error);

        Assert.IsFalse(parsed);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void FrameCodec_Build_SkipsNullOrWhitespaceUnits()
    {
        var frame = IpSetupFrameCodec.Build(new[] { null, "", "   ", "ENQ:network" });
        var parsed = IpSetupFrameCodec.TryParse(frame, out var units, out var error);

        Assert.IsTrue(parsed, error);
        CollectionAssert.AreEqual(new[] { "ENQ:network" }, units);
    }

    [Test]
    public void MessageProcessor_EnqNetwork_ReturnsFixedUnitsInOrder()
    {
        var identity = new VirtualDeviceIdentity
        {
            virtualMac = "88-C9-E8-00-00-03",
            modelName = "IPCA",
            softVersion = "2.10",
            friendlyName = "CAM1"
        };
        var network = new VirtualNetworkConfig
        {
            logicalIp = "192.168.1.50",
            logicalMask = "255.255.255.0",
            logicalGateway = "10.0.0.1"
        };

        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var result = processor.Process(new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380), new[] { "ENQ:network" });

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsEnq);
        CollectionAssert.AreEqual(new[]
        {
            "MAC:88-C9-E8-00-00-03",
            "INFO:network",
            "MODEL:IPCA",
            "SOFTVERSION:2.10",
            "IPADR:192.168.1.50",
            "MASK:255.255.255.0",
            "GATEWAY:10.0.0.1",
            "NAME:CAM1",
            "WRITE:on"
        }, result.ResponseUnits);
    }

    [Test]
    public void MessageProcessor_SetMac_WithMatchingMac_ReturnsSingleAckUnit()
    {
        var identity = new VirtualDeviceIdentity
        {
            virtualMac = "88-C9-E8-00-00-03"
        };
        var network = new VirtualNetworkConfig();

        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var units = new[] { "SETMAC:88-C9-E8-00-00-03" };

        var result = processor.Process(new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380), units);

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsSet);
        CollectionAssert.AreEqual(new[] { "ACK:88-C9-E8-00-00-03" }, result.ResponseUnits);
    }

    [Test]
    public void MessageProcessor_Set_WithMacKeyMatchingMac_ReturnsSingleAckUnit()
    {
        var identity = new VirtualDeviceIdentity
        {
            virtualMac = "88-C9-E8-00-00-03"
        };
        var network = new VirtualNetworkConfig();

        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var units = new[] { "SET:network", "MAC:88-C9-E8-00-00-03" };

        var result = processor.Process(new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380), units);

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsSet);
        CollectionAssert.AreEqual(new[] { "ACK:88-C9-E8-00-00-03" }, result.ResponseUnits);
    }

    [Test]
    public void MessageProcessor_Set_WithMacMismatch_ReturnsNak()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "88-C9-E8-00-00-03" };
        var network = new VirtualNetworkConfig();
        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "SETMAC:88-C9-E8-00-00-04" });

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsSet);
        Assert.AreEqual("NAK:MAC_MISMATCH", result.ResponseUnits.Single());
    }

    [Test]
    public void MessageProcessor_Set_WithoutSetMacOrMac_ReturnsSetMacRequiredNak()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "88-C9-E8-00-00-03" };
        var processor = new IpSetupMessageProcessor(
            identity,
            new VirtualNetworkConfig(),
            _ => "192.168.1.50");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "SET:network" });

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsSet);
        Assert.AreEqual("NAK:SETMAC_REQUIRED", result.ResponseUnits.Single());
    }

    [Test]
    public void MessageProcessor_Set_WithInvalidSetMac_ReturnsInvalidSetMacNak()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "88-C9-E8-00-00-03" };
        var processor = new IpSetupMessageProcessor(
            identity,
            new VirtualNetworkConfig(),
            _ => "192.168.1.50");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "SETMAC:NOT-A-MAC" });

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsSet);
        Assert.AreEqual("NAK:INVALID_SETMAC", result.ResponseUnits.Single());
    }

    [Test]
    public void MessageProcessor_NoUnits_IgnoresRequest()
    {
        var processor = new IpSetupMessageProcessor(
            new VirtualDeviceIdentity(),
            new VirtualNetworkConfig(),
            _ => "192.168.1.50");

        var result = processor.Process(new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380), new string[0]);

        Assert.IsFalse(result.ShouldRespond);
        Assert.IsFalse(result.IsEnq);
        Assert.IsFalse(result.IsSet);
        StringAssert.Contains("no units", result.Summary);
    }

    [Test]
    public void MessageProcessor_UnsupportedUnits_IgnoresRequest()
    {
        var processor = new IpSetupMessageProcessor(
            new VirtualDeviceIdentity(),
            new VirtualNetworkConfig(),
            _ => "192.168.1.50");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "HELLO:world" });

        Assert.IsFalse(result.ShouldRespond);
        Assert.IsFalse(result.IsEnq);
        Assert.IsFalse(result.IsSet);
        StringAssert.Contains("unsupported request", result.Summary);
    }

    [Test]
    public void MessageProcessor_UsesResolverAddressForResponse()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "88-C9-E8-00-00-03" };
        var network = new VirtualNetworkConfig { logicalIp = "10.10.10.10" };
        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "ENQ:network" });

        Assert.IsTrue(result.ShouldRespond);
        Assert.AreEqual("MAC:88-C9-E8-00-00-03", result.ResponseUnits[0]);
        Assert.AreEqual("INFO:network", result.ResponseUnits[1]);
        CollectionAssert.Contains(result.ResponseUnits, "IPADR:192.168.1.50");
        CollectionAssert.Contains(result.ResponseUnits, "WRITE:on");
    }

    [Test]
    public void MessageProcessor_WhenResolverIsLoopback_UsesLogicalIpFallback()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "88-C9-E8-00-00-03" };
        var network = new VirtualNetworkConfig { logicalIp = "10.10.10.10" };
        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "127.0.0.1");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "ENQ:network" });

        CollectionAssert.Contains(result.ResponseUnits, "IPADR:10.10.10.10");
    }

    [Test]
    public void MessageProcessor_WhenResolverAndFallbackInvalid_ReturnsEmptyIpAdr()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "88-C9-E8-00-00-03" };
        var network = new VirtualNetworkConfig { logicalIp = "invalid-ip" };
        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "127.0.0.1");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "ENQ:network" });

        CollectionAssert.Contains(result.ResponseUnits, "IPADR:");
    }

    [Test]
    public void TryNormalizeMac_ColonFormat_IsNormalizedToUpperHyphen()
    {
        var ok = IpSetupMessageProcessor.TryNormalizeMac("88:c9:e8:00:00:03", out var normalized);

        Assert.IsTrue(ok);
        Assert.AreEqual("88-C9-E8-00-00-03", normalized);
    }

    [Test]
    public void FrameCodec_BuildAndParse_RoundTrip()
    {
        var units = new List<string> { "ACK:88-C9-E8-00-00-03" };
        var frame = IpSetupFrameCodec.Build(units);
        var parsed = IpSetupFrameCodec.TryParse(frame, out var roundTrip, out var error);

        Assert.IsTrue(parsed, error);
        CollectionAssert.AreEqual(units, roundTrip.ToList());
    }
}
