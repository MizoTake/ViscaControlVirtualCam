using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace ViscaControlVirtualCam
{
    public sealed class IpSetupResponder : IDisposable
    {
        private readonly object _debounceLock = new();
        private readonly Dictionary<string, DateTime> _enqDebounceCache = new();
        private readonly Action<string> _logger;
        private readonly IpSetupResponderOptions _options;
        private readonly IpSetupMessageProcessor _processor;
        private UdpClient _udp;

        public IpSetupResponder(
            IpSetupMessageProcessor processor,
            IpSetupResponderOptions options,
            Action<string> logger = null)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public void Start()
        {
            Stop();

            var bindAddress = _options.BindAddress ?? IPAddress.Any;
            _udp = new UdpClient(new IPEndPoint(bindAddress, _options.Port))
            {
                EnableBroadcast = true
            };
            _udp.Client.ReceiveBufferSize = 64 * 1024;
            _udp.BeginReceive(ReceiveCallback, null);

            Log($"IPSETUP responder started on {bindAddress}:{_options.Port} (mode={_options.ResponderMode})");
        }

        public void Stop()
        {
            try
            {
                _udp?.Close();
            }
            catch (Exception e)
            {
                Log($"IPSETUP UDP close error: {e.Message}");
            }
            finally
            {
                _udp = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var udp = _udp;
            if (udp == null)
                return;

            byte[] data;
            var remote = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                data = udp.EndReceive(ar, ref remote);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                if (_udp != null) Log($"IPSETUP receive error: {e.Message}");
                ScheduleNextReceive();
                return;
            }

            ScheduleNextReceive();
            HandlePacket(data, remote);
        }

        private void HandlePacket(byte[] data, IPEndPoint remote)
        {
            if (!IpSetupFrameCodec.TryParse(data, out var units, out var parseError))
            {
                Log($"IPSETUP ignored invalid frame from {remote.Address}:{remote.Port} ({parseError})");
                return;
            }

            Log($"IPSETUP RX {remote.Address}:{remote.Port} units=[{string.Join(", ", units)}]");

            if (ShouldDebounceEnq(remote, units))
            {
                Log($"IPSETUP ENQ debounced for {remote.Address}:{remote.Port}");
                return;
            }

            var result = _processor.Process(remote, units);
            Log(result.Summary);
            if (!result.ShouldRespond || result.ResponseUnits == null || result.ResponseUnits.Length == 0)
                return;

            var responsePacket = IpSetupFrameCodec.Build(result.ResponseUnits);
            SendResponse(responsePacket, remote, result.ResponseUnits);
        }

        private bool ShouldDebounceEnq(IPEndPoint remote, IReadOnlyList<string> units)
        {
            if (_options.EnqDebounceMilliseconds <= 0)
                return false;

            var selector = GetEnqSelector(units);
            if (selector == null)
                return false;

            var now = DateTime.UtcNow;
            var key = $"{remote.Address}|{selector}";

            lock (_debounceLock)
            {
                if (_enqDebounceCache.TryGetValue(key, out var lastSeen) &&
                    (now - lastSeen).TotalMilliseconds < _options.EnqDebounceMilliseconds)
                    return true;

                _enqDebounceCache[key] = now;

                if (_enqDebounceCache.Count > 64)
                {
                    var expiredBefore = now.AddSeconds(-5);
                    var removeKeys = new List<string>();
                    foreach (var pair in _enqDebounceCache)
                        if (pair.Value < expiredBefore)
                            removeKeys.Add(pair.Key);
                    for (var i = 0; i < removeKeys.Count; i++)
                        _enqDebounceCache.Remove(removeKeys[i]);
                }
            }

            return false;
        }

        private static string GetEnqSelector(IReadOnlyList<string> units)
        {
            if (units == null)
                return null;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (string.IsNullOrWhiteSpace(unit))
                    continue;

                if (unit.StartsWith("ENQ", StringComparison.OrdinalIgnoreCase))
                {
                    var separator = unit.IndexOf(':');
                    if (separator < 0 || separator + 1 >= unit.Length)
                        return "allinfo";
                    return unit.Substring(separator + 1).Trim();
                }
            }

            return null;
        }

        private void SendResponse(byte[] packet, IPEndPoint remote, IReadOnlyList<string> units)
        {
            var udp = _udp;
            if (udp == null)
                return;

            var destination = _options.ResponderMode == IpSetupResponderMode.Broadcast
                ? new IPEndPoint(IPAddress.Broadcast, remote.Port)
                : remote;

            try
            {
                udp.Send(packet, packet.Length, destination);
                Log(
                    $"IPSETUP TX {destination.Address}:{destination.Port} units=[{string.Join(", ", units)}]");
            }
            catch (Exception e)
            {
                Log($"IPSETUP send error: {e.Message}");
            }
        }

        private void ScheduleNextReceive()
        {
            try
            {
                _udp?.BeginReceive(ReceiveCallback, null);
            }
            catch (Exception e)
            {
                Log($"IPSETUP begin receive error: {e.Message}");
            }
        }

        private void Log(string message)
        {
            _logger?.Invoke(message);
        }
    }
}
