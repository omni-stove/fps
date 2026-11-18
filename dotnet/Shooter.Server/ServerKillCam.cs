using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // クライアントが見た相手の動きではなくサーバーの体の姿勢を残すのは、倒された人の手元には倒した人の動きが補間の遅れ分しか届いておらず、
    // 倒される直前まで映せないため。
    internal sealed class ServerKillCam : IServerModule
    {
        private readonly MatchRulesModule _rules;
        private readonly ServerCombat _combat;
        private readonly Dictionary<int, KillCamBuffer> _buffers = new Dictionary<int, KillCamBuffer>();
        private readonly KillCamClipMessage _message = new KillCamClipMessage();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private double _now;

        public ServerKillCam(MatchRulesModule rules, ServerCombat combat, float seconds = KillCamBuffer.DefaultSeconds)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be positive and finite");
            }

            Seconds = seconds;
        }

        public string Name => "killcam";

        public float Seconds { get; }

        internal int RecordingCount => _buffers.Count;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            _match.PlayerLeft += HandlePlayerLeft;
            _match.MapChanged += HandleMapChanged;
            _bodies.Teleported += HandleTeleported;
            _rules.MatchStarted += HandleMatchStarted;
            _combat.PlayerKilled += HandlePlayerKilled;
            _log.WriteLine(ServerKillCamLog.Settings(Seconds, DeathCameraNow));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            var session = _rules.Session;
            if (DeathCameraNow != DeathCamera.KillCam || session == null)
            {
                _buffers.Clear();
                return;
            }

            foreach (var body in _bodies.All)
            {
                var id = body.PlayerId;
                if (!session.HasPlayer(id) || !session.IsAlive(id) || !body.HasSpawned || !IsFinite(body.EyePosition) || !IsFinite(body.Yaw))
                {
                    // 倒れている間や出現前の位置を、次に動き出した後の記録と補間でつながないよう捨てる
                    if (_buffers.TryGetValue(id, out var stale))
                    {
                        stale.Clear();
                    }

                    continue;
                }

                if (!_buffers.TryGetValue(id, out var buffer))
                {
                    buffer = new KillCamBuffer(Seconds);
                    _buffers.Add(id, buffer);
                }

                buffer.Record(_now, new KillCamPose(body.EyePosition, body.Yaw));
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerLeft -= HandlePlayerLeft;
            _match.MapChanged -= HandleMapChanged;
            _bodies.Teleported -= HandleTeleported;
            _rules.MatchStarted -= HandleMatchStarted;
            _combat.PlayerKilled -= HandlePlayerKilled;
            _buffers.Clear();
        }

        internal bool SendClip(KillFeedEntry entry)
        {
            if (entry.IsSuicide || DeathCameraNow != DeathCamera.KillCam)
            {
                return false;
            }

            if (!_match.TryGetPlayer(entry.VictimId, out var victim) || victim.IsBot || entry.KillerId < 0 || entry.VictimId < 0)
            {
                return false;
            }

            var clip = _buffers.TryGetValue(entry.KillerId, out var buffer) ? buffer.Capture(_now, Seconds) : KillCamClip.Empty;
            if (clip.IsEmpty)
            {
                _log.WriteLine(ServerKillCamLog.NoRecord(entry.KillerId, entry.VictimId));
                return false;
            }

            Fill(_message, entry.KillerId, entry.VictimId, clip);
            _context.Messenger.Send(victim.Connection, TransportChannel.Reliable, _message);
            _log.WriteLine(ServerKillCamLog.Sent(entry.KillerId, entry.VictimId, _message.Count, _message.OffsetAt(_message.Count - 1)));
            return true;
        }

        // 1 通に載る数を超える長さを決めても送らずに終わらないよう、等しい間隔で間引く。最後 (倒した瞬間) は間引きの間隔に関わらず残す
        internal static void Fill(KillCamClipMessage message, int killerId, int victimId, KillCamClip clip)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(clip);
            message.KillerId = killerId;
            message.VictimId = victimId;
            message.Clear();
            var count = clip.Count;
            if (count == 0)
            {
                return;
            }

            var last = count - 1;
            var stride = count <= KillCamClipMessage.MaxSamples
                ? 1
                : (last + KillCamClipMessage.MaxSamples - 2) / (KillCamClipMessage.MaxSamples - 1);
            for (var i = 0; i < last; i += stride)
            {
                message.Add(clip.OffsetAt(i), clip.PoseAt(i));
            }

            message.Add(clip.OffsetAt(last), clip.PoseAt(last));
        }

        private DeathCamera DeathCameraNow => _rules.Rules?.DeathCamera ?? DeathCamera.None;

        private void HandlePlayerKilled(KillFeedEntry entry)
        {
            SendClip(entry);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _buffers.Remove(player.PlayerId);
        }

        // 置き直した前後の位置を補間でつながないよう、それまでの記録を捨てる
        private void HandleTeleported(ServerBody body)
        {
            if (_buffers.TryGetValue(body.PlayerId, out var buffer))
            {
                buffer.Clear();
            }
        }

        private void HandleMapChanged(ServerMapData map)
        {
            _buffers.Clear();
        }

        private void HandleMatchStarted()
        {
            _buffers.Clear();
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
