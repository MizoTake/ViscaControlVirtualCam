using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class ViscaForwarderTests
{
    [Test]
    [Timeout(5000)]
    public void Forward_SendsPacketToRealCamera_AndRelaysResponse()
    {
        using (var realCamera = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        using (var forwarder = new ViscaForwarder("127.0.0.1", ((IPEndPoint)realCamera.Client.LocalEndPoint).Port))
        {
            realCamera.Client.ReceiveTimeout = 2000;
            forwarder.Start();

            byte[] relayed = null;
            using (var relayReceived = new ManualResetEventSlim(false))
            {
                byte[] command = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x01, 0xFF };
                forwarder.Forward(command, bytes =>
                {
                    relayed = bytes;
                    relayReceived.Set();
                });

                var sender = new IPEndPoint(IPAddress.Any, 0);
                var forwarded = realCamera.Receive(ref sender);
                CollectionAssert.AreEqual(command, forwarded, "Command should be forwarded as-is");

                byte[] response = { 0x90, 0x51, 0xFF };
                realCamera.Send(response, response.Length, sender);

                Assert.IsTrue(relayReceived.Wait(2000), "Relayed response should be received");
                CollectionAssert.AreEqual(response, relayed, "Response should be relayed as-is");
            }
        }
    }
}
