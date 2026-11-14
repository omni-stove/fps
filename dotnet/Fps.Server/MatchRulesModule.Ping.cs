using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // 印を付けるか・どこに付けるか・敵を追うかはサーバーが決め、付けた人の陣営の人にだけ送る。
    // クライアントの照準の結果を信じると、見えていない敵の位置を味方に知らせられてしまうため、クライアントからは向きだけを受け取る。
    internal sealed partial class MatchRulesModule
    {
        // 毎 tick 全ての敵の印の視線を引いて位置を送ると、印の数と人数に比例して重くなるので、この間隔でだけ追う (Unity と同じ)
        internal const float PingTrackIntervalSeconds = 0.1f;

        // Unity の Physics.DefaultRaycastLayers と同じく Ignore Raycast (2) だけを外し、同じ壁で印と視線を止める
        internal const int PingRaycastLayers = ~(1 << 2);

        // 頭の高さへの視線を、カプセルの頂からどれだけ下へ引くか (m)。頭だけ出した相手も見えている扱いにするため (Unity と同じ)
        private const float _pingTopProbeInset = 0.1f;

        private const float _minPingDirectionSqrMagnitude = 1e-8f;

        private readonly List<SharedPingChange> _pingChanges = new List<SharedPingChange>();
        private readonly List<SharedPing> _pingScratch = new List<SharedPing>();
        private readonly List<int> _pingCandidateIds = new List<int>();
        private readonly List<PingCandidate> _pingCandidates = new List<PingCandidate>();
        private readonly PingPlacedMessage _pingPlaced = new PingPlacedMessage();
        private readonly PingMovedMessage _pingMoved = new PingMovedMessage();
        private readonly PingLostMessage _pingLost = new PingLostMessage();
        private readonly PingRemovedMessage _pingRemoved = new PingRemovedMessage();

        private TeamPingRelay _pingRelay;
        private float _nextPingTrackTime;
        private float _nextPingRefusalLogTime;

        /// <summary>参加していない人と試合の無いときは、断る理由も出さずに捨てる (射撃と同じ)。そのときは null。</summary>
        internal PingRefusal? ServerPing(int playerId, Vector3 direction)
        {
            if (_session == null || !_session.HasPlayer(playerId) || !_bodies.TryGet(playerId, out var pinger))
            {
                return null;
            }

            if (_pingRelay == null)
            {
                return RefusePing(playerId, PingRefusal.RuleOff);
            }

            if (!_session.IsAlive(playerId))
            {
                return RefusePing(playerId, PingRefusal.Dead);
            }

            if (!IsFinite(direction) || direction.LengthSquared() < _minPingDirectionSqrMagnitude)
            {
                return RefusePing(playerId, PingRefusal.InvalidAim);
            }

            var interval = _pingRelay.CheckInterval(playerId, _now);
            if (interval != PingRefusal.None)
            {
                return RefusePing(playerId, interval);
            }

            direction = Vector3.Normalize(direction);
            var team = _session.GetTeam(playerId);
            var hit = ResolvePingAim(pinger, team, pinger.EyePosition, direction, out var targetId);

            _pingChanges.Clear();
            var refusal = _pingRelay.TryPlace(playerId, team, hit, targetId, _now, _pingChanges, out _);
            DispatchPingChanges();
            return refusal == PingRefusal.None ? PingRefusal.None : RefusePing(playerId, refusal);
        }

        private PingHit ResolvePingAim(ServerBody pinger, Team team, Vector3 origin, Vector3 direction, out int? targetId)
        {
            var spec = _pingRelay.Spec;
            Vector3? surface = null;
            var surfaceDistance = spec.MaxDistance;
            if (_context.World.Raycast(origin, direction, spec.MaxDistance, PingRaycastLayers, out var wall))
            {
                surface = wall.Point;
                surfaceDistance = wall.Distance;
            }

            _pingCandidateIds.Clear();
            _pingCandidates.Clear();
            var surfaceIndex = -1;
            try
            {
                foreach (var other in _bodies.All)
                {
                    var id = other.PlayerId;
                    if (other == pinger || !_session.HasPlayer(id) || _session.GetTeam(id) == team || !_session.IsAlive(id))
                    {
                        continue;
                    }

                    var aimPoint = other.Center;
                    if (Vector3.Distance(origin, aimPoint) > spec.MaxDistance)
                    {
                        continue;
                    }

                    // 体は .fpsmap に無いので、体に当たったかは壁より手前でカプセルに当たるかで決める
                    other.Capsule(out var bottom, out var top);
                    if (RayCapsule(origin, direction, bottom, top, other.Radius, out var distance) && distance < surfaceDistance)
                    {
                        surfaceDistance = distance;
                        surface = origin + (direction * distance);
                        surfaceIndex = _pingCandidates.Count;
                    }

                    _pingCandidateIds.Add(id);
                    _pingCandidates.Add(new PingCandidate(aimPoint, true, HasPingLineOfSight(origin, other, spec.MaxDistance)));
                }

                var hit = PingAim.Resolve(spec, origin, direction, surface, surfaceIndex, _pingCandidates);
                targetId = hit.HasTarget && hit.Kind == PingKind.Enemy ? _pingCandidateIds[hit.CandidateIndex] : (int?)null;
                return hit;
            }
            finally
            {
                _pingCandidateIds.Clear();
                _pingCandidates.Clear();
            }
        }

        // 体の中ほどか頭のどちらかへの線が通っていれば見えているとする。人は視線を遮らない (味方の陰の敵にも付けられるように)
        private bool HasPingLineOfSight(Vector3 eye, ServerBody target, float maxDistance)
        {
            target.Capsule(out _, out var top);
            var bodyCenter = target.Center;
            var head = top + new Vector3(0f, target.Radius - _pingTopProbeInset, 0f);
            if (head.Y < bodyCenter.Y)
            {
                head = bodyCenter;
            }

            if (Vector3.Distance(eye, bodyCenter) > maxDistance)
            {
                return false;
            }

            return !IsPingLineBlocked(eye, bodyCenter) || !IsPingLineBlocked(eye, head);
        }

        private bool IsPingLineBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.Length();
            return length >= 1e-4f && _context.World.Raycast(from, offset / length, length, PingRaycastLayers, out _);
        }

        private void PreparePings(GameRules rules)
        {
            var spec = _data.Ping;
            _pingRelay = rules.Actions.Ping == Ping.On ? new TeamPingRelay(spec) : null;
            _log.WriteLine(PingLog.Rule(rules.Actions.Ping, spec) + " interval="
                + spec.MinIntervalSeconds.ToString("0.00", CultureInfo.InvariantCulture));
        }

        // Session の Tick の後に呼ぶ。倒れた敵の印をその tick のうちに消すため
        private void TickPings()
        {
            if (_pingRelay == null)
            {
                return;
            }

            _pingChanges.Clear();
            _pingRelay.Expire(_now, _pingChanges);
            if (_now >= _nextPingTrackTime)
            {
                _nextPingTrackTime = _now + PingTrackIntervalSeconds;
                TrackPings();
            }

            DispatchPingChanges();
        }

        private void TrackPings()
        {
            _pingRelay.CollectTracking(_pingScratch);
            var maxDistance = _pingRelay.Spec.MaxDistance;
            foreach (var ping in _pingScratch)
            {
                var targetId = ping.Marker.TargetKey ?? PlayerIds.None;
                var hasTarget = _bodies.TryGet(targetId, out var target);
                var alive = hasTarget && _session.HasPlayer(targetId) && _session.IsAlive(targetId);
                var visible = alive && ping.Marker.IsTracking && _bodies.TryGet(ping.PingerId, out var pinger)
                    && HasPingLineOfSight(pinger.EyePosition, target, maxDistance);
                _pingRelay.Track(ping.Id, alive, visible, visible ? target.Center : Vector3.Zero, _pingChanges);
            }

            _pingScratch.Clear();
        }

        // 途中から入った人にも、自分の陣営の今の印を見せる
        private void PingPlayerJoined(ServerPlayer player)
        {
            if (_pingRelay == null || !_session.HasPlayer(player.PlayerId))
            {
                return;
            }

            _pingRelay.CollectTeam(_session.GetTeam(player.PlayerId), _pingScratch);
            foreach (var ping in _pingScratch)
            {
                if (FillPingPlaced(ping))
                {
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _pingPlaced);
                }
            }

            _pingScratch.Clear();
        }

        private void PingPlayerLeft(int playerId)
        {
            if (_pingRelay == null)
            {
                return;
            }

            _pingChanges.Clear();
            _pingRelay.RemovePlayer(playerId, _pingChanges);
            DispatchPingChanges();
        }

        private void PingMatchRestarted()
        {
            if (_pingRelay == null)
            {
                return;
            }

            _pingChanges.Clear();
            _pingRelay.Clear(_pingChanges);
            DispatchPingChanges();
        }

        private void DispatchPingChanges()
        {
            foreach (var change in _pingChanges)
            {
                var ping = change.Ping;
                switch (change.Kind)
                {
                    case SharedPingChangeKind.Placed:
                        var recipients = CountTeam(ping.Team);
                        if (FillPingPlaced(ping))
                        {
                            SendToTeam(ping.Team, TransportChannel.Reliable, _pingPlaced);
                        }

                        _log.WriteLine(PingLog.NetPlaced(ping, change.Refreshed, recipients));
                        _match.WritePingPlaced(ping, change.Refreshed);
                        break;
                    case SharedPingChangeKind.Moved:
                        // 位置は次の知らせで上書きされるので、落ちても送り直さない
                        _pingMoved.PingId = ping.Id;
                        _pingMoved.Position = ping.Marker.Position;
                        SendToTeam(ping.Team, TransportChannel.Unreliable, _pingMoved);
                        break;
                    case SharedPingChangeKind.Lost:
                        _pingLost.PingId = ping.Id;
                        _pingLost.Position = ping.Marker.Position;
                        SendToTeam(ping.Team, TransportChannel.Reliable, _pingLost);
                        _log.WriteLine(PingLog.NetLost(ping));
                        break;
                    case SharedPingChangeKind.Removed:
                        _pingRemoved.PingId = ping.Id;
                        _pingRemoved.Reason = change.Removal;
                        SendToTeam(ping.Team, TransportChannel.Reliable, _pingRemoved);
                        _log.WriteLine(PingLog.NetRemoved(ping, change.Removal));
                        break;
                }
            }

            _pingChanges.Clear();
        }

        private bool FillPingPlaced(SharedPing ping)
        {
            var marker = ping.Marker;
            var remaining = marker.RemainingSeconds(_now);
            if (!(remaining > 0f))
            {
                return false;
            }

            _pingPlaced.PingId = marker.Id;
            _pingPlaced.Kind = marker.Kind;
            _pingPlaced.Position = marker.Position;
            _pingPlaced.Tracking = marker.IsTracking;
            _pingPlaced.RemainingSeconds = remaining;
            return true;
        }

        // 断るのは普段の遊びでも起きる (連打・何も無い所) ので、連打でログを埋めないよう間引く
        private PingRefusal RefusePing(int playerId, PingRefusal reason)
        {
            if (_now >= _nextPingRefusalLogTime)
            {
                _nextPingRefusalLogTime = _now + _rejectWarningInterval;
                _log.WriteLine(PingLog.NetRefused(playerId, reason));
                _match.WritePingRefused(playerId, reason);
            }

            return reason;
        }

        internal static bool RayCapsule(Vector3 origin, Vector3 direction, Vector3 bottom, Vector3 top, float radius, out float distance)
        {
            distance = 0f;
            var axis = top - bottom;
            var axisLength = axis.Length();
            var best = float.PositiveInfinity;

            // 軸に沿った筒の部分
            if (axisLength > 1e-6f)
            {
                var a = axis / axisLength;
                var m = origin - bottom;
                var md = Vector3.Dot(m, a);
                var nd = Vector3.Dot(direction, a);
                var dPerp = direction - (a * nd);
                var mPerp = m - (a * md);
                var qa = Vector3.Dot(dPerp, dPerp);
                var qb = 2f * Vector3.Dot(mPerp, dPerp);
                var qc = Vector3.Dot(mPerp, mPerp) - (radius * radius);
                if (qa > 1e-12f)
                {
                    var disc = (qb * qb) - (4f * qa * qc);
                    if (disc >= 0f)
                    {
                        var t = (-qb - MathF.Sqrt(disc)) / (2f * qa);
                        var along = md + (t * nd);
                        if (t >= 0f && along >= 0f && along <= axisLength)
                        {
                            best = t;
                        }
                    }
                }
            }

            best = MathF.Min(best, RaySphere(origin, direction, bottom, radius));
            best = MathF.Min(best, RaySphere(origin, direction, top, radius));
            if (float.IsPositiveInfinity(best))
            {
                return false;
            }

            distance = best;
            return true;
        }

        private static float RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius)
        {
            var m = origin - center;
            var b = Vector3.Dot(m, direction);
            var c = Vector3.Dot(m, m) - (radius * radius);
            if (c <= 0f)
            {
                return 0f;
            }

            if (b > 0f)
            {
                return float.PositiveInfinity;
            }

            var disc = (b * b) - c;
            return disc < 0f ? float.PositiveInfinity : -b - MathF.Sqrt(disc);
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
