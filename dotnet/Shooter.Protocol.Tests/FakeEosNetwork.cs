using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Protocol.Tests
{
    /// <summary>
    /// EOS の P2P を真似るメモリ上のネットワーク。受け入れの手順 (両方が AcceptConnection して開く)・閉じた相手への通知・
    /// 接続が無い相手へは送れないことだけを真似る。パケットは送ったその場で相手の受け取りの列に入る。
    /// サーバーを別のスレッドで動かすテストでも使うので、どの呼び出しもネットワーク全体を 1 つの鍵で守る。
    /// </summary>
    internal sealed class FakeEosNetwork
    {
        private readonly Dictionary<string, FakeEosP2P> _users = new Dictionary<string, FakeEosP2P>(StringComparer.Ordinal);

        public long Now { get; set; }

        /// <summary>true の間、落ちてよいパケットを捨てる。</summary>
        public bool DropUnreliable { get; set; }

        /// <summary>true の間、どのパケットも捨てる (相手が黙ったように見せる)。</summary>
        public bool DropAll { get; set; }

        public FakeEosP2P Join(string userId)
        {
            lock (this)
            {
                var p2p = new FakeEosP2P(this, userId);
                _users[userId] = p2p;
                return p2p;
            }
        }

        public bool TryGet(string userId, out FakeEosP2P p2p)
        {
            return _users.TryGetValue(userId, out p2p) && p2p.Running;
        }

        /// <summary>知らせの無いまま、2 人の間の接続を閉じる (経路が切れた)。</summary>
        public void Sever(string a, string b)
        {
            lock (this)
            {
                SeverLocked(a, b);
            }
        }

        private void SeverLocked(string a, string b)
        {
            if (TryGet(a, out var first))
            {
                first.ClosedByNetwork(b);
            }

            if (TryGet(b, out var second))
            {
                second.ClosedByNetwork(a);
            }
        }
    }

    internal sealed class FakeEosP2P : IEosP2P
    {
        private readonly FakeEosNetwork _network;
        private readonly Queue<EosP2PNotice> _notices = new Queue<EosP2PNotice>();
        private readonly Queue<(string From, byte Channel, byte[] Data)> _inbox = new Queue<(string From, byte Channel, byte[] Data)>();
        private readonly HashSet<string> _accepted = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _open = new HashSet<string>(StringComparer.Ordinal);
        private bool _acceptsIncoming;

        public FakeEosP2P(FakeEosNetwork network, string userId)
        {
            _network = network;
            UserId = userId;
        }

        public string UserId { get; }

        /// <summary>null でなければ TryBegin がこの理由で失敗する。</summary>
        public string BeginProblem { get; set; }

        /// <summary>null でなければログインがこの理由で失敗する。</summary>
        public string SignInProblem { get; set; }

        /// <summary>false ならログインの結果をいつまでも返さない。</summary>
        public bool SignsIn { get; set; } = true;

        public bool Running { get; private set; }

        public int CloseAllCount { get; private set; }

        public List<string> Closed { get; } = new List<string>();

        public bool IsOpenTo(string remoteUserId)
        {
            return _open.Contains(remoteUserId);
        }

        private bool TryBeginLocked(bool acceptsIncoming, out string problem)
        {
            problem = BeginProblem;
            if (problem != null)
            {
                return false;
            }

            Running = true;
            _acceptsIncoming = acceptsIncoming;
            if (SignInProblem != null)
            {
                _notices.Enqueue(EosP2PNotice.SignInFailed(SignInProblem));
            }
            else if (SignsIn)
            {
                _notices.Enqueue(EosP2PNotice.SignedIn(UserId));
            }

            return true;
        }

        private bool TryTakeNoticeLocked(out EosP2PNotice notice)
        {
            if (_notices.Count > 0)
            {
                notice = _notices.Dequeue();
                return true;
            }

            notice = default;
            return false;
        }

        private bool TryReceivePacketLocked(byte[] buffer, out string remoteUserId, out byte channel, out int length)
        {
            if (_inbox.Count == 0)
            {
                remoteUserId = null;
                channel = 0;
                length = 0;
                return false;
            }

            var packet = _inbox.Dequeue();
            Buffer.BlockCopy(packet.Data, 0, buffer, 0, packet.Data.Length);
            remoteUserId = packet.From;
            channel = packet.Channel;
            length = packet.Data.Length;
            return true;
        }

        private bool SendPacketLocked(string remoteUserId, byte channel, bool reliable, ArraySegment<byte> packet, out string problem)
        {
            Assert.That(packet.Count, Is.LessThanOrEqualTo(EosMatchTransport.MaxPacketSize));
            if (!_open.Contains(remoteUserId) || !_network.TryGet(remoteUserId, out var remote))
            {
                problem = "NoConnection";
                return false;
            }

            problem = null;
            if (_network.DropAll || (!reliable && _network.DropUnreliable))
            {
                return true;
            }

            remote._inbox.Enqueue((UserId, channel, packet.AsSpan().ToArray()));
            return true;
        }

        private void AcceptConnectionLocked(string remoteUserId)
        {
            _accepted.Add(remoteUserId);
            if (!_network.TryGet(remoteUserId, out var remote))
            {
                return;
            }

            if (remote._accepted.Contains(UserId))
            {
                Open(remote);
                return;
            }

            if (remote._acceptsIncoming)
            {
                remote._notices.Enqueue(EosP2PNotice.ConnectionRequested(UserId));
            }
        }

        private void CloseConnectionLocked(string remoteUserId)
        {
            Closed.Add(remoteUserId);
            _accepted.Remove(remoteUserId);
            if (!_open.Remove(remoteUserId))
            {
                return;
            }

            if (_network.TryGet(remoteUserId, out var remote))
            {
                remote.ClosedByNetwork(UserId);
            }
        }

        private void CloseAllLocked()
        {
            CloseAllCount++;
            foreach (var remoteUserId in new List<string>(_open))
            {
                CloseConnection(remoteUserId);
            }

            Running = false;
            _notices.Clear();
            _inbox.Clear();
        }

        private void ClosedByNetworkLocked(string remoteUserId)
        {
            _accepted.Remove(remoteUserId);
            if (_open.Remove(remoteUserId))
            {
                _notices.Enqueue(EosP2PNotice.ConnectionClosed(remoteUserId, "ClosedByPeer"));
            }
        }

        /// <summary>知らない相手からのパケットを入れる。</summary>
        private void InjectLocked(string from, byte channel, byte[] data)
        {
            _inbox.Enqueue((from, channel, data));
        }

        public bool TryBegin(bool acceptsIncoming, out string problem)
        {
            lock (_network)
            {
                return TryBeginLocked(acceptsIncoming, out problem);
            }
        }

        public void Tick()
        {
        }

        public bool TryTakeNotice(out EosP2PNotice notice)
        {
            lock (_network)
            {
                return TryTakeNoticeLocked(out notice);
            }
        }

        public bool TryReceivePacket(byte[] buffer, out string remoteUserId, out byte channel, out int length)
        {
            lock (_network)
            {
                return TryReceivePacketLocked(buffer, out remoteUserId, out channel, out length);
            }
        }

        public bool SendPacket(string remoteUserId, byte channel, bool reliable, ArraySegment<byte> packet, out string problem)
        {
            lock (_network)
            {
                return SendPacketLocked(remoteUserId, channel, reliable, packet, out problem);
            }
        }

        public void AcceptConnection(string remoteUserId)
        {
            lock (_network)
            {
                AcceptConnectionLocked(remoteUserId);
            }
        }

        public void CloseConnection(string remoteUserId)
        {
            lock (_network)
            {
                CloseConnectionLocked(remoteUserId);
            }
        }

        public void CloseAll()
        {
            lock (_network)
            {
                CloseAllLocked();
            }
        }

        public void ClosedByNetwork(string remoteUserId)
        {
            lock (_network)
            {
                ClosedByNetworkLocked(remoteUserId);
            }
        }

        public void Inject(string from, byte channel, byte[] data)
        {
            lock (_network)
            {
                InjectLocked(from, channel, data);
            }
        }

        private void Open(FakeEosP2P remote)
        {
            _open.Add(remote.UserId);
            remote._open.Add(UserId);
            _notices.Enqueue(EosP2PNotice.ConnectionEstablished(remote.UserId));
            remote._notices.Enqueue(EosP2PNotice.ConnectionEstablished(UserId));
        }
    }
}
