using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// EAC の P2P セッションでの相手 (transport の ID) ごとの状態を持ち、何をすべきかを決める。
    /// 接続したら互いに Hello (<see cref="AntiCheatPacket"/>) で EAC の有効・無効を伝え、両方有効なら
    /// EAC にピアとして登録するハンドルを割り当て、片方だけ有効なら切断を求める。
    /// Hello が来るまでは <see cref="CollectDue"/> が一定間隔で送り直しを求め、期限を過ぎたら切断を求める。
    /// SDK も時計も持たず、呼び出し側 (transport) が結果に従って送信・登録・切断をする。
    /// </summary>
    public sealed class AntiCheatPeers
    {
        /// <summary>Hello が来る前に届いた EAC のメッセージを相手ごとに溜めておく上限。</summary>
        public const int MaxEarlyMessagesPerPeer = 16;

        private readonly Dictionary<ulong, Peer> _peers = new Dictionary<ulong, Peer>();
        private readonly Dictionary<long, ulong> _peersByHandle = new Dictionary<long, ulong>();
        private readonly double _helloInterval;
        private readonly double _helloTimeout;
        private long _nextHandle = 1;

        /// <param name="localEnabled">こちらの EAC が有効か。</param>
        /// <param name="helloIntervalSeconds">相手の Hello が来るまで、こちらの Hello を送り直す間隔 (秒)。</param>
        /// <param name="helloTimeoutSeconds">接続から相手の Hello を待つ秒数。過ぎたら切断を求める。</param>
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

        /// <summary>こちらの EAC が有効か。Hello にこの値を載せる。</summary>
        public bool LocalEnabled { get; }

        /// <summary>接続中の相手の数。</summary>
        public int Count => _peers.Count;

        /// <summary>
        /// 相手が接続した。呼び出し側はすぐにこちらの Hello を送ること (送った時刻としてここで記録する)。
        /// </summary>
        /// <returns>新しい相手なら true。接続中の相手なら何もせず false。</returns>
        public bool Connected(ulong peerId, double now)
        {
            if (_peers.ContainsKey(peerId))
            {
                return false;
            }

            _peers.Add(peerId, new Peer(now));
            return true;
        }

        /// <summary>相手の Hello を受け取った。</summary>
        /// <param name="peerId">送り手。</param>
        /// <param name="remoteEnabled">相手の EAC が有効か。</param>
        /// <param name="handle"><see cref="AntiCheatHelloVerdict.Registered"/> のとき、EAC に登録するハンドル (0 ではない)。それ以外は 0。</param>
        /// <param name="reason"><see cref="AntiCheatHelloVerdict.Rejected"/> のときの理由。それ以外は null。</param>
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

        /// <summary>相手との接続が切れた (こちらから切った場合も含む) ので忘れる。</summary>
        /// <param name="peerId">相手。</param>
        /// <param name="handle">EAC に登録済みだったときのハンドル。呼び出し側は登録を外すこと。それ以外は 0。</param>
        /// <returns>接続中の相手だったら true。</returns>
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

        /// <summary>EAC に登録済みの相手のハンドル。受け取ったメッセージを EAC へ渡すときに使う。</summary>
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

        /// <summary>ハンドルの相手。EAC が送信・切断を求めてきたときに使う。</summary>
        public bool TryGetPeer(long handle, out ulong peerId)
        {
            return _peersByHandle.TryGetValue(handle, out peerId);
        }

        /// <summary>接続中で、まだ相手の Hello を受け取っていないか。</summary>
        public bool IsAwaitingHello(ulong peerId)
        {
            return _peers.TryGetValue(peerId, out var peer) && !peer.HelloReceived;
        }

        /// <summary>
        /// Hello より先に届いた EAC のメッセージを溜める (コピーする)。Hello で登録されたら <see cref="TakeEarlyMessages"/> で取り出す。
        /// </summary>
        /// <returns>溜めたら true。こちらの EAC が無効・相手の Hello を受け取り済み・上限に達したなら false。</returns>
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

        /// <summary>溜めていたメッセージを届いた順に取り出し、空にする。無ければ空の一覧。</summary>
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

        /// <summary>
        /// 時間で起きることを集める。相手の Hello を待っている相手のうち、前に送ってから間隔が過ぎたものを
        /// <paramref name="helloDue"/> に (送った時刻として記録する)、接続から期限を過ぎたものを
        /// <paramref name="timedOut"/> に入れる。期限切れは 1 相手につき 1 回だけ入れる。
        /// </summary>
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

        /// <summary>Hello が来なかったときの切断理由。</summary>
        public string TimeoutReason()
        {
            return "no anti-cheat hello within " + _helloTimeout.ToString("0.#", CultureInfo.InvariantCulture)
                + "s (an old build, or the anti-cheat channel is blocked)";
        }

        /// <summary>すべて忘れる。次に割り当てるハンドルは戻さない。</summary>
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

            /// <summary>EAC に登録したハンドル。未登録なら 0。</summary>
            public long Handle { get; set; }

            public List<byte[]> EarlyMessages { get; set; }
        }
    }
}
