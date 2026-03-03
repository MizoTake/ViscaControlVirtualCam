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
    public void MessageProcessor_EnqNetwork_ReturnsFixedUnitsInOrder()
    {
        var identity = new VirtualDeviceIdentity
        {
            virtualMac = "88-C9-E8-00-00-03",
            modelName = "BRC-X400",
            softVersion = "1.23",
            friendlyName = "VCam"
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
            "INFO:network",
            "MODEL:BRC-X400",
            "VERSION:1.23",
            "IPADR:192.168.1.50",
            "MASK:255.255.255.0",
            "GATEWAY:10.0.0.1",
            "NAME:VCam"
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
        Assert.AreEqual("INFO:network", result.ResponseUnits[0]);
        CollectionAssert.Contains(result.ResponseUnits, "IPADR:192.168.1.50");
        Assert.IsFalse(result.ResponseUnits.Any(x => x.StartsWith("MAC:", System.StringComparison.Ordinal)));
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
