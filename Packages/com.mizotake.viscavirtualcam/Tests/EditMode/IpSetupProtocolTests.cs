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
    public void MessageProcessor_Enq_ReturnsInfoUnits()
    {
        var identity = new VirtualDeviceIdentity
        {
            virtualMac = "02:11:22:33:44:55",
            modelName = "BRC-X400",
            serial = "VC123456",
            softVersion = "1.23",
            webPort = 80,
            friendlyName = "VCam"
        };
        var network = new VirtualNetworkConfig
        {
            logicalIp = "10.0.0.20",
            logicalMask = "255.255.255.0",
            logicalGateway = "10.0.0.1"
        };

        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var result = processor.Process(new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380), new[] { "ENQ:allinfo" });

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsEnq);
        CollectionAssert.Contains(result.ResponseUnits, "ACK:ENQ");
        CollectionAssert.Contains(result.ResponseUnits, "MAC:02:11:22:33:44:55");
        CollectionAssert.Contains(result.ResponseUnits, "IPADR:192.168.1.50");
    }

    [Test]
    public void MessageProcessor_Set_WithMatchingMac_UpdatesNetworkAndIdentity()
    {
        var identity = new VirtualDeviceIdentity
        {
            virtualMac = "02:11:22:33:44:55",
            friendlyName = "Before",
            webPort = 80
        };
        var network = new VirtualNetworkConfig
        {
            logicalIp = "10.0.0.20",
            logicalMask = "255.255.255.0",
            logicalGateway = "10.0.0.1"
        };

        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var units = new[]
        {
            "SET:network",
            "SETMAC:02:11:22:33:44:55",
            "IPADR:10.0.0.99",
            "MASK:255.255.255.0",
            "GATEWAY:10.0.0.1",
            "WEBPORT:8080",
            "NAME:After"
        };

        var result = processor.Process(new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380), units);

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsSet);
        CollectionAssert.Contains(result.ResponseUnits, "ACK:SET");
        Assert.AreEqual("10.0.0.99", network.logicalIp);
        Assert.AreEqual("After", identity.friendlyName);
        Assert.AreEqual(8080, identity.webPort);
    }

    [Test]
    public void MessageProcessor_Set_WithMacMismatch_ReturnsNak()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "02:11:22:33:44:55" };
        var network = new VirtualNetworkConfig();
        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "SET:network", "SETMAC:02:AA:BB:CC:DD:EE", "IPADR:10.0.0.99" });

        Assert.IsTrue(result.ShouldRespond);
        Assert.IsTrue(result.IsSet);
        Assert.AreEqual("NAK:MAC_MISMATCH", result.ResponseUnits.Single());
    }

    [Test]
    public void MessageProcessor_UsesResolverAddressForResponse()
    {
        var identity = new VirtualDeviceIdentity { virtualMac = "02:11:22:33:44:55" };
        var network = new VirtualNetworkConfig { logicalIp = "10.10.10.10" };
        var processor = new IpSetupMessageProcessor(
            identity,
            network,
            _ => "192.168.1.50");

        var result = processor.Process(
            new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
            new[] { "ENQ:network" });

        Assert.IsTrue(result.ShouldRespond);
        CollectionAssert.Contains(result.ResponseUnits, "IPADR:192.168.1.50");
    }

    [Test]
    public void FrameCodec_BuildAndParse_RoundTrip()
    {
        var units = new List<string> { "ACK:SET", "MAC:02:11:22:33:44:55", "IPADR:10.0.0.10" };
        var frame = IpSetupFrameCodec.Build(units);
        var parsed = IpSetupFrameCodec.TryParse(frame, out var roundTrip, out var error);

        Assert.IsTrue(parsed, error);
        CollectionAssert.AreEqual(units, roundTrip.ToList());
    }
}
