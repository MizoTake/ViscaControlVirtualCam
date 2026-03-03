using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class IpSetupResponderTests
{
    [Test]
    public void StartStop_CanBeCalledRepeatedly()
    {
        var port = GetFreeUdpPort();
        var responder = CreateResponder(
            new IpSetupResponderOptions
            {
                BindAddress = IPAddress.Loopback,
                Port = port,
                ResponderMode = IpSetupResponderMode.Unicast
            });

        try
        {
            Assert.DoesNotThrow(() => responder.Start());
            Assert.DoesNotThrow(() => responder.Start());
            Assert.DoesNotThrow(() => responder.Stop());
            Assert.DoesNotThrow(() => responder.Stop());
        }
        finally
        {
            responder.Dispose();
        }
    }

    [Test]
    public void ShouldDebounceEnq_SameRemoteAndSelector_DebouncesSecondCall()
    {
        var responder = CreateResponder(new IpSetupResponderOptions
        {
            EnqDebounceMilliseconds = 500
        });

        try
        {
            var remote = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380);
            var first = InvokeShouldDebounceEnq(responder, remote, new[] { "ENQ:network" });
            var second = InvokeShouldDebounceEnq(responder, remote, new[] { "ENQ:network" });

            Assert.IsFalse(first);
            Assert.IsTrue(second);
        }
        finally
        {
            responder.Dispose();
        }
    }

    [Test]
    public void ShouldDebounceEnq_DifferentSelectorOrRemote_IsNotDebounced()
    {
        var responder = CreateResponder(new IpSetupResponderOptions
        {
            EnqDebounceMilliseconds = 500
        });

        try
        {
            var remote1 = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380);
            var remote2 = new IPEndPoint(IPAddress.Parse("192.168.1.11"), 52380);

            Assert.IsFalse(InvokeShouldDebounceEnq(responder, remote1, new[] { "ENQ:network" }));
            Assert.IsFalse(InvokeShouldDebounceEnq(responder, remote1, new[] { "ENQ:allinfo" }));
            Assert.IsFalse(InvokeShouldDebounceEnq(responder, remote2, new[] { "ENQ:network" }));
        }
        finally
        {
            responder.Dispose();
        }
    }

    [Test]
    [Timeout(5000)]
    public void InvalidFrame_DoesNotRespond()
    {
        var port = GetFreeUdpPort();
        using (var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            var responder = CreateResponder(
                new IpSetupResponderOptions
                {
                    BindAddress = IPAddress.Loopback,
                    Port = port,
                    ResponderMode = IpSetupResponderMode.Unicast,
                    EnqDebounceMilliseconds = 0
                });

            try
            {
                responder.Start();
                Thread.Sleep(50);

                sender.Client.ReceiveTimeout = 500;
                var invalid = new byte[] { 0x00, 0x03 };
                sender.Send(invalid, invalid.Length, new IPEndPoint(IPAddress.Loopback, port));

                var remote = new IPEndPoint(IPAddress.Any, 0);
                Assert.Throws<SocketException>(() => sender.Receive(ref remote));
            }
            finally
            {
                responder.Dispose();
            }
        }
    }

    [Test]
    [Timeout(5000)]
    public void EnqNetwork_Unicast_RespondsToSender()
    {
        var port = GetFreeUdpPort();
        using (var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            var responder = CreateResponder(
                new IpSetupResponderOptions
                {
                    BindAddress = IPAddress.Loopback,
                    Port = port,
                    ResponderMode = IpSetupResponderMode.Unicast,
                    EnqDebounceMilliseconds = 0
                });

            try
            {
                responder.Start();
                Thread.Sleep(50);

                sender.Client.ReceiveTimeout = 2000;
                var request = IpSetupFrameCodec.Build(new[] { "ENQ:network" });
                sender.Send(request, request.Length, new IPEndPoint(IPAddress.Loopback, port));

                var remote = new IPEndPoint(IPAddress.Any, 0);
                var response = sender.Receive(ref remote);

                var parsed = IpSetupFrameCodec.TryParse(response, out var units, out var error);
                Assert.IsTrue(parsed, error);
                CollectionAssert.Contains(units, "INFO:network");
                CollectionAssert.Contains(units, "WRITE:on");
            }
            finally
            {
                responder.Dispose();
            }
        }
    }

    [Test]
    public void SendResponse_BroadcastMode_UsesBroadcastAddress()
    {
        var port = GetFreeUdpPort();
        var logs = new List<string>();
        var responder = CreateResponder(
            new IpSetupResponderOptions
            {
                BindAddress = IPAddress.Loopback,
                Port = port,
                ResponderMode = IpSetupResponderMode.Broadcast,
                EnqDebounceMilliseconds = 0
            },
            msg => logs.Add(msg));

        try
        {
            responder.Start();
            var packet = IpSetupFrameCodec.Build(new[] { "ACK:88-C9-E8-00-00-03" });
            InvokeSendResponse(
                responder,
                packet,
                new IPEndPoint(IPAddress.Parse("192.168.1.10"), 52380),
                new[] { "ACK:88-C9-E8-00-00-03" });

            Assert.IsTrue(logs.Exists(msg => msg.Contains("255.255.255.255:52380")));
        }
        finally
        {
            responder.Dispose();
        }
    }

    private static IpSetupResponder CreateResponder(IpSetupResponderOptions options, Action<string> logger = null)
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
            logicalGateway = "0.0.0.0"
        };
        var processor = new IpSetupMessageProcessor(identity, network, _ => "192.168.1.50");
        return new IpSetupResponder(processor, options, logger);
    }

    private static bool InvokeShouldDebounceEnq(IpSetupResponder responder, IPEndPoint remote, IReadOnlyList<string> units)
    {
        var method = typeof(IpSetupResponder).GetMethod("ShouldDebounceEnq",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "ShouldDebounceEnq method not found.");
        return (bool)method.Invoke(responder, new object[] { remote, units });
    }

    private static void InvokeSendResponse(IpSetupResponder responder, byte[] packet, IPEndPoint remote,
        IReadOnlyList<string> units)
    {
        var method = typeof(IpSetupResponder).GetMethod("SendResponse",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "SendResponse method not found.");
        method.Invoke(responder, new object[] { packet, remote, units });
    }

    private static int GetFreeUdpPort()
    {
        using (var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            return ((IPEndPoint)udp.Client.LocalEndPoint).Port;
    }
}
