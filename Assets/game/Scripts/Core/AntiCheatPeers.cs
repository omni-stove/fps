using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class AntiCheatPeers
    {
        public const int MaxEarlyMessagesPerPeer = 16;

        private readonly Dictionary<ulong, Peer> _peers = new Dictionary<ulong, Peer>();
        private readonly Dictionary<long, ulong> _peersByHandle = new Dictionary<long, ulong>();
        private readonly double _helloInterval;
        private readonly double _helloTimeout;
        private long _nextHandle = 1;

        public AntiCheatPeers(bool localEnabled, double helloIntervalSeconds, double helloTimeoutSeconds)
        {
            if (!(helloIntervalSeconds > 0))
            {
                throw new ArgumentOutOfRangeException(nameof(helloIntervalSeconds), helloIntervalSeconds, "must be positive");
            }

            if (!(helloTimeoutSeconds >= helloIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(helloTimeoutSeconds), helloTimeoutSeconds, "must be at least the hello interval");
            }

            LocalEnabled = localEnabled;
            _helloInterval = helloIntervalSeconds;
            _helloTimeout = helloTimeoutSeconds;
        }

        public bool LocalEnabled { get; }

        public int Count => _peers.Count;

        // 呼び出し側がすぐに Hello を送る前提で、接続の時刻を最後に Hello を送った時刻として記録する。
        public bool Connected(ulong peerId, double now)
        {
            if (_peers.ContainsKey(peerId))
            {
                return false;
            }

            _peers.Add(peerId, new Peer(now));
            return true;
        }

        public AntiCheatHelloVerdict ReceiveHello(ulong peerId, bool remoteEnabled, out long handle, out string reason)
        {
            handle = 0;
            reason = null;
            if (!_peers.TryGetValue(peerId, out var peer))
            {
                return AntiCheatHelloVerdict.Unknown;
            }

            if (peer.HelloReceived)
            {
                return AntiCheatHelloVerdict.Duplicate;
            }

            peer.HelloReceived = true;
            if (remoteEnabled != LocalEnabled)
            {
                peer.EarlyMessages = null;
                reason = LocalEnabled
                    ? "the peer runs without anti-cheat (" + AntiCheatSettings.NoEacFlag + " or an unprotected build)"
                    : "the peer requires anti-cheat but this side runs without it";
                return AntiCheatHelloVerdict.Rejected;
            }

            if (!LocalEnabled)
            {
                return AntiCheatHelloVerdict.Accepted;
            }

            if (_nextHandle == long.MaxValue)
            {
                throw new InvalidOperationException("anti-cheat peer handles are exhausted");
            }

            handle = _nextHandle++;
            peer.Handle = handle;
            _peersByHandle.Add(handle, peerId);
            return AntiCheatHelloVerdict.Registered;
        }

        public bool Disconnected(ulong peerId, out long handle)
        {
            handle = 0;
            if (!_peers.TryGetValue(peerId, out var peer))
            {
                return false;
            }

            _peers.Remove(peerId);
            if (peer.Handle != 0)
            {
                handle = peer.Handle;
                _peersByHandle.Remove(handle);
            }

            return true;
        }

        public bool TryGetHandle(ulong peerId, out long handle)
        {
            if (_peers.TryGetValue(peerId, out var peer) && peer.Handle != 0)
            {
                handle = peer.Handle;
                return true;
            }

            handle = 0;
            return false;
        }

        public bool TryGetPeer(long handle, out ulong peerId)
        {
            return _peersByHandle.TryGetValue(handle, out peerId);
        }

        public bool IsAwaitingHello(ulong peerId)
        {
            return _peers.TryGetValue(peerId, out var peer) && !peer.HelloReceived;
        }

        public bool TryQueueEarlyMessage(ulong peerId, ArraySegment<byte> payload)
        {
            if (payload.Array == null)
            {
                throw new ArgumentException("payload must have an array", nameof(payload));
            }

            if (!LocalEnabled || !_peers.TryGetValue(peerId, out var peer) || peer.HelloReceived)
            {
                return false;
            }

            if (peer.EarlyMessages == null)
            {
                peer.EarlyMessages = new List<byte[]>();
            }

            if (peer.EarlyMessages.Count >= MaxEarlyMessagesPerPeer)
            {
                return false;
            }

            var copy = new byte[payload.Count];
            Buffer.BlockCopy(payload.Array, payload.Offset, copy, 0, payload.Count);
            peer.EarlyMessages.Add(copy);
            return true;
        }

        public IReadOnlyList<byte[]> TakeEarlyMessages(ulong peerId)
        {
            if (!_peers.TryGetValue(peerId, out var peer) || peer.EarlyMessages == null)
            {
                return Array.Empty<byte[]>();
            }

            var messages = peer.EarlyMessages;
            peer.EarlyMessages = null;
            return messages;
        }

        public void CollectDue(double now, List<ulong> helloDue, List<ulong> timedOut)
        {
            if (helloDue == null)
            {
                throw new ArgumentNullException(nameof(helloDue));
            }

            if (timedOut == null)
            {
                throw new ArgumentNullException(nameof(timedOut));
            }

            foreach (var pair in _peers)
            {
                var peer = pair.Value;
                if (peer.HelloReceived || peer.TimedOut)
                {
                    continue;
                }

                if (now - peer.ConnectedAt >= _helloTimeout)
                {
                    peer.TimedOut = true;
                    peer.EarlyMessages = null;
                    timedOut.Add(pair.Key);
                    continue;
                }

                if (now - peer.LastHelloSentAt >= _helloInterval)
                {
                    peer.LastHelloSentAt = now;
                    helloDue.Add(pair.Key);
                }
            }
        }

        public string TimeoutReason()
        {
            return "no anti-cheat hello within " + _helloTimeout.ToString("0.#", CultureInfo.InvariantCulture)
                + "s (an old build, or the anti-cheat channel is blocked)";
        }

        // _nextHandle を戻さないのは、Clear の前に配ったハンドルを別の相手に使い回さないため。
        public void Clear()
        {
            _peers.Clear();
            _peersByHandle.Clear();
        }

        private sealed class Peer
        {
            public Peer(double connectedAt)
            {
                ConnectedAt = connectedAt;
                LastHelloSentAt = connectedAt;
            }

            public double ConnectedAt { get; }

            public double LastHelloSentAt { get; set; }

            public bool HelloReceived { get; set; }

            public bool TimedOut { get; set; }

            public long Handle { get; set; }

            public List<byte[]> EarlyMessages { get; set; }
        }
    }
}
