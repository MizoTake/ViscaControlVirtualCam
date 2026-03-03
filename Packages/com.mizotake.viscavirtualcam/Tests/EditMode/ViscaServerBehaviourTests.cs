using System.Net;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ViscaControlVirtualCam;

public class ViscaServerBehaviourTests
{
    private bool _previousLoggerEnabled;

    [SetUp]
    public void SetUp()
    {
        _previousLoggerEnabled = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;
    }

    [TearDown]
    public void TearDown()
    {
        Debug.unityLogger.logEnabled = _previousLoggerEnabled;
    }

    [Test]
    public void TryParseBindAddress_InvalidValue_ReturnsFalse()
    {
        var behaviour = CreateBehaviour(out var go);
        try
        {
            behaviour.bindAddress = "not-an-ip";

            var ok = InvokeTryParseBindAddress(behaviour, out var _);

            Assert.IsFalse(ok);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryResolveAdvertisedAddress_BindAddressAny_ReturnsFalse()
    {
        var behaviour = CreateBehaviour(out var go);
        try
        {
            behaviour.ipSetupAdvertisedAddressSource = IpSetupAdvertisedAddressSource.BindAddress;
            behaviour.bindAddress = "0.0.0.0";

            var ok = InvokeTryResolveAdvertisedAddress(behaviour, IPAddress.Any, out var advertised);

            Assert.IsFalse(ok);
            Assert.IsNull(advertised);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryResolveAdvertisedAddress_BindAddressLoopback_ReturnsFalse()
    {
        var behaviour = CreateBehaviour(out var go);
        try
        {
            behaviour.ipSetupAdvertisedAddressSource = IpSetupAdvertisedAddressSource.BindAddress;
            behaviour.bindAddress = "127.0.0.1";

            var ok = InvokeTryResolveAdvertisedAddress(behaviour, IPAddress.Loopback, out var advertised);

            Assert.IsFalse(ok);
            Assert.IsNull(advertised);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryResolveAdvertisedAddress_CustomInvalid_ReturnsFalse()
    {
        var behaviour = CreateBehaviour(out var go);
        try
        {
            behaviour.ipSetupAdvertisedAddressSource = IpSetupAdvertisedAddressSource.CustomAddress;
            behaviour.ipSetupCustomAdvertisedAddress = "invalid-ip";

            var ok = InvokeTryResolveAdvertisedAddress(behaviour, IPAddress.Parse("192.168.1.10"), out var advertised);

            Assert.IsFalse(ok);
            Assert.IsNull(advertised);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryResolveAdvertisedAddress_CustomLoopback_ReturnsFalse()
    {
        var behaviour = CreateBehaviour(out var go);
        try
        {
            behaviour.ipSetupAdvertisedAddressSource = IpSetupAdvertisedAddressSource.CustomAddress;
            behaviour.ipSetupCustomAdvertisedAddress = "127.0.0.1";

            var ok = InvokeTryResolveAdvertisedAddress(behaviour, IPAddress.Parse("192.168.1.10"), out var advertised);

            Assert.IsFalse(ok);
            Assert.IsNull(advertised);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void EnsureIpSetupDefaults_FillsIdentityAndNetworkFallbacks()
    {
        var behaviour = CreateBehaviour(out var go);
        try
        {
            behaviour.ipSetupIdentity = new VirtualDeviceIdentity
            {
                virtualMac = "invalid-mac",
                modelName = "",
                serial = "",
                softVersion = "",
                webPort = 0,
                friendlyName = ""
            };
            behaviour.ipSetupNetwork = new VirtualNetworkConfig
            {
                logicalIp = "invalid-ip",
                logicalMask = "invalid-mask",
                logicalGateway = "invalid-gateway"
            };

            InvokeEnsureIpSetupDefaults(behaviour, "192.168.10.20");

            Assert.AreEqual("88-C9-E8-00-00-03", behaviour.ipSetupIdentity.virtualMac);
            Assert.AreEqual("IPCA", behaviour.ipSetupIdentity.modelName);
            Assert.AreEqual("VC000001", behaviour.ipSetupIdentity.serial);
            Assert.AreEqual("2.10", behaviour.ipSetupIdentity.softVersion);
            Assert.AreEqual(80, behaviour.ipSetupIdentity.webPort);
            Assert.AreEqual("CAM1", behaviour.ipSetupIdentity.friendlyName);

            Assert.AreEqual("192.168.10.20", behaviour.ipSetupNetwork.logicalIp);
            Assert.AreEqual("255.255.255.0", behaviour.ipSetupNetwork.logicalMask);
            Assert.AreEqual("0.0.0.0", behaviour.ipSetupNetwork.logicalGateway);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static ViscaServerBehaviour CreateBehaviour(out GameObject go)
    {
        go = new GameObject("ViscaServerBehaviourTests");
        return go.AddComponent<ViscaServerBehaviour>();
    }

    private static bool InvokeTryParseBindAddress(ViscaServerBehaviour behaviour, out IPAddress address)
    {
        var method = typeof(ViscaServerBehaviour).GetMethod("TryParseBindAddress",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "TryParseBindAddress method not found.");
        var args = new object[] { null };
        var ok = (bool)method.Invoke(behaviour, args);
        address = args[0] as IPAddress;
        return ok;
    }

    private static bool InvokeTryResolveAdvertisedAddress(ViscaServerBehaviour behaviour, IPAddress bindAddress,
        out string advertised)
    {
        var method = typeof(ViscaServerBehaviour).GetMethod("TryResolveIpSetupAdvertisedAddress",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "TryResolveIpSetupAdvertisedAddress method not found.");
        var args = new object[] { bindAddress, null };
        var ok = (bool)method.Invoke(behaviour, args);
        advertised = args[1] as string;
        return ok;
    }

    private static void InvokeEnsureIpSetupDefaults(ViscaServerBehaviour behaviour, string advertisedAddress)
    {
        var method = typeof(ViscaServerBehaviour).GetMethod("EnsureIpSetupDefaults",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "EnsureIpSetupDefaults method not found.");
        method.Invoke(behaviour, new object[] { advertisedAddress });
    }
}
