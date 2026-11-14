using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    // 印を付けるか・どこに付けるか・敵を追うかはサーバーが決め、付けた人の陣営の人にだけ RPC で配る。
    // クライアントの照準の結果を信じると、見えていない敵の位置を味方に知らせられてしまうため、クライアントからは向きだけを受け取る。
    public sealed partial class NetTeamDeathmatch
    {
        // 毎フレーム全ての敵の印の視線を引いて位置を送ると、印の数と人数に比例して重くなるので、この間隔でだけ追う。
        internal const float PingTrackIntervalSeconds = 0.1f;

        // 頭の高さへの視線を、カプセルの頂からどれだけ下へ引くか (m)。爆発の判定と同じく、頭だけ出した相手も見えている扱いにするため
        private const float _pingTopProbeInset = 0.1f;

        private const float _minPingDirectionSqrMagnitude = 1e-8f;

        private readonly List<SharedPingChange> _pingChanges = new List<SharedPingChange>();
        private readonly List<SharedPing> _pingScratch = new List<SharedPing>();
        private readonly List<ulong> _pingRecipients = new List<ulong>();
        private readonly List<KeyValuePair<ulong, Team>> _pingMembers = new List<KeyValuePair<ulong, Team>>();
        private readonly List<NetPlayer> _pingCandidatePlayers = new List<NetPlayer>();
        private readonly List<PingCandidate> _pingCandidates = new List<PingCandidate>();
        private readonly SharedPingMarkers _sharedPings = new SharedPingMarkers();

        private PingSpec _pingSpec = PingSpec.Default;
        private TeamPingRelay _pingRelay;
        private float _nextPingTrackTime;
        private float _nextPingRefusalLogTime;

        // 試合の開始時に読むので、試合中に差し替えても次の試合 (Spawn) まで効かない。
        public PingSpec PingSpec
        {
            get => _pingSpec;
            set => _pingSpec = value ?? throw new ArgumentNullException(nameof(value));
        }

        public SharedPingMarkers SharedPings => _sharedPings;

        // Edit Mode のテストでは時間が進まないので、差し込めるようにしてある。
        internal Func<float> PingClock { get; set; }

        private float PingNow => PingClock != null ? PingClock() : Time.time;

        internal static void CollectPingRecipients(
            IEnumerable<KeyValuePair<ulong, Team>> humans,
            Team team,
            IEnumerable<ulong> connectedClientIds,
            ulong serverClientId,
            bool serverIsClient,
            List<ulong> into)
        {
            if (humans == null)
            {
                throw new ArgumentNullException(nameof(humans));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            foreach (var member in humans)
            {
                if (member.Value != team || into.Contains(member.Key)
                    || !NetCombatFeed.CanReceive(member.Key, connectedClientIds, serverClientId, serverIsClient))
                {
                    continue;
                }

                into.Add(member.Key);
            }
        }

        internal static bool CanRequestPing(bool pingEnabled, bool alive, bool watchingDeathCamera)
        {
            return pingEnabled && alive && !watchingDeathCamera;
        }

        internal void ServerPing(NetPlayer pinger, Vector3 direction)
        {
            ServerPing(pinger, direction, PingNow);
        }

        // 参加していない人と試合の無いときは、断る理由も出さずに捨てる (射撃と同じ)。そのときは null。
        internal PingRefusal? ServerPing(NetPlayer pinger, Vector3 direction, float now)
        {
            if (!IsServer || _session == null || pinger == null || !IsJoined(pinger))
            {
                return null;
            }

            var playerId = pinger.ServerPlayerId;
            if (_pingRelay == null)
            {
                return RefusePing(playerId, PingRefusal.RuleOff);
            }

            if (!_session.IsAlive(playerId))
            {
                return RefusePing(playerId, PingRefusal.Dead);
            }

            if (!NetMath.IsFinite(direction) || direction.sqrMagnitude < _minPingDirectionSqrMagnitude)
            {
                return RefusePing(playerId, PingRefusal.InvalidAim);
            }

            var interval = _pingRelay.CheckInterval(playerId, now);
            if (interval != PingRefusal.None)
            {
                return RefusePing(playerId, interval);
            }

            direction.Normalize();
            var origin = pinger.EyePosition();
            var team = _session.GetTeam(playerId);
            var hit = ResolvePingAim(pinger, team, origin, direction, out var target);

            _pingChanges.Clear();
            var refusal = _pingRelay.TryPlace(
                playerId, team, hit, target != null ? target.ServerPlayerId : (int?)null, now, _pingChanges, out _);
            DispatchPingChanges(_pingChanges, now);
            _pingChanges.Clear();
            return refusal == PingRefusal.None ? PingRefusal.None : RefusePing(playerId, refusal);
        }

        private PingHit ResolvePingAim(NetPlayer pinger, Team team, Vector3 origin, Vector3 direction, out NetPlayer target)
        {
            var spec = _pingRelay.Spec;
            System.Numerics.Vector3? surface = null;
            NetPlayer surfaceOwner = null;
            if (Hitscan.TryFirstHit(origin, direction, spec.MaxDistance, pinger.transform, out var surfaceHit))
            {
                surface = NetMath.ToNumerics(surfaceHit.point);
                surfaceOwner = surfaceHit.collider.GetComponentInParent<NetPlayer>();
            }

            _pingCandidatePlayers.Clear();
            _pingCandidates.Clear();
            try
            {
                foreach (var pair in _playerById)
                {
                    var other = pair.Value;
                    if (other == null || other == pinger || !_session.HasPlayer(pair.Key)
                        || _session.GetTeam(pair.Key) == team || !_session.IsAlive(pair.Key))
                    {
                        continue;
                    }

                    var aimPoint = PingAimPoint(other);
                    if (Vector3.Distance(origin, aimPoint) > spec.MaxDistance)
                    {
                        continue;
                    }

                    _pingCandidatePlayers.Add(other);
                    _pingCandidates.Add(new PingCandidate(NetMath.ToNumerics(aimPoint), true, HasPingLineOfSight(origin, other, spec.MaxDistance)));
                }

                var surfaceIndex = surfaceOwner != null ? _pingCandidatePlayers.IndexOf(surfaceOwner) : -1;
                var hit = PingAim.Resolve(
                    spec, NetMath.ToNumerics(origin), NetMath.ToNumerics(direction), surface, surfaceIndex, _pingCandidates);
                target = hit.HasTarget && hit.Kind == PingKind.Enemy ? _pingCandidatePlayers[hit.CandidateIndex] : null;
                return hit;
            }
            finally
            {
                _pingCandidatePlayers.Clear();
                _pingCandidates.Clear();
            }
        }

        internal static Vector3 PingAimPoint(NetPlayer player)
        {
            TryGetBodyCapsule(player, out var bottom, out var top, out _);
            return Vector3.Lerp(bottom, top, 0.5f);
        }

        // 体の中ほどか頭のどちらかへの線が通っていれば見えているとする。人とグレネードは視線を遮らない (味方の陰の敵にも付けられるように)。
        internal static bool HasPingLineOfSight(Vector3 eye, NetPlayer target, float maxDistance)
        {
            if (target == null)
            {
                return false;
            }

            TryGetBodyCapsule(target, out var bottom, out var top, out _);
            var bodyCenter = Vector3.Lerp(bottom, top, 0.5f);
            var head = top - (Vector3.up * _pingTopProbeInset);
            if (head.y < bodyCenter.y)
            {
                head = bodyCenter;
            }

            if (Vector3.Distance(eye, bodyCenter) > maxDistance)
            {
                return false;
            }

            return !IsPingLineBlocked(eye, bodyCenter) || !IsPingLineBlocked(eye, head);
        }

        private static bool IsPingLineBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.magnitude;
            if (length < 1e-4f)
            {
                return false;
            }

            foreach (var hit in Physics.RaycastAll(from, offset / length, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<NetPlayer>() != null || hit.collider.GetComponentInParent<NetGrenade>() != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void PreparePings(GameRules rules)
        {
            _sharedPings.Clear();
            _pingChanges.Clear();
            _pingRelay = rules.Actions.Ping == Fps.Core.Ping.On ? new TeamPingRelay(_pingSpec) : null;
            _nextPingTrackTime = 0f;
            _nextPingRefusalLogTime = 0f;
            Debug.Log(PingLog.Rule(rules.Actions.Ping, _pingSpec) + " interval="
                + _pingSpec.MinIntervalSeconds.ToString("0.00", CultureInfo.InvariantCulture), this);
        }

        // Session の Tick の後に呼ぶ。倒れた敵の印をその tick のうちに消すため。
        private void PingServerTick()
        {
            if (_pingRelay == null)
            {
                return;
            }

            var now = PingNow;
            _pingChanges.Clear();
            _pingRelay.Expire(now, _pingChanges);
            if (now >= _nextPingTrackTime)
            {
                _nextPingTrackTime = now + PingTrackIntervalSeconds;
                TrackPings();
            }

            DispatchPingChanges(_pingChanges, now);
            _pingChanges.Clear();
        }

        private void TrackPings()
        {
            _pingRelay.CollectTracking(_pingScratch);
            var maxDistance = _pingRelay.Spec.MaxDistance;
            foreach (var ping in _pingScratch)
            {
                var targetId = ping.Marker.TargetKey ?? NetPlayer.NotJoined;
                _playerById.TryGetValue(targetId, out var target);
                var alive = target != null && _session.HasPlayer(targetId) && _session.IsAlive(targetId);
                _playerById.TryGetValue(ping.PingerId, out var pinger);
                var visible = alive && pinger != null && HasPingLineOfSight(pinger.EyePosition(), target, maxDistance);
                var position = visible ? NetMath.ToNumerics(PingAimPoint(target)) : System.Numerics.Vector3.Zero;
                _pingRelay.Track(ping.Id, alive, visible, position, _pingChanges);
            }

            _pingScratch.Clear();
        }

        private void PingPlayerJoined(int playerId, NetPlayer player)
        {
            if (_pingRelay == null || player == null || player.IsBot || !CanSendPings() || !_session.HasPlayer(playerId))
            {
                return;
            }

            var clientId = player.OwnerClientId;
            if (!NetCombatFeed.CanReceive(clientId, NetworkManager.ConnectedClientsIds, NetworkManager.ServerClientId, IsHost))
            {
                return;
            }

            var now = PingNow;
            _pingRelay.CollectTeam(_session.GetTeam(playerId), _pingScratch);
            foreach (var ping in _pingScratch)
            {
                SendPingPlaced(ping, now, RpcTarget.Single(clientId, RpcTargetUse.Temp));
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
            DispatchPingChanges(_pingChanges, PingNow);
            _pingChanges.Clear();
        }

        private void PingMatchRestarted()
        {
            if (_pingRelay == null)
            {
                return;
            }

            _pingChanges.Clear();
            _pingRelay.Clear(_pingChanges);
            DispatchPingChanges(_pingChanges, PingNow);
            _pingChanges.Clear();
        }

        private void PingServerStopped()
        {
            _pingRelay = null;
            _pingChanges.Clear();
            _pingScratch.Clear();
            _sharedPings.Clear();
        }

        // サーバーの知らせが遅れても古い印を出し続けないよう、クライアントでも寿命で消す。
        private void PingClientTick()
        {
            if (IsSpawned && IsClient)
            {
                _sharedPings.Expire(Time.time);
            }
        }

        private void DispatchPingChanges(List<SharedPingChange> changes, float now)
        {
            foreach (var change in changes)
            {
                var ping = change.Ping;
                switch (change.Kind)
                {
                    case SharedPingChangeKind.Placed:
                        var recipients = GatherPingRecipients(ping.Team);
                        if (recipients > 0)
                        {
                            SendPingPlaced(ping, now, RpcTarget.Group(_pingRecipients, RpcTargetUse.Temp));
                        }

                        Debug.Log(PingLog.NetPlaced(ping, change.Refreshed, recipients), this);
                        ServerEventsPingPlaced(ping, change.Refreshed);
                        break;
                    case SharedPingChangeKind.Moved:
                        if (GatherPingRecipients(ping.Team) > 0)
                        {
                            PingMovedRpc(ping.Id, NetMath.ToUnity(ping.Marker.Position), RpcTarget.Group(_pingRecipients, RpcTargetUse.Temp));
                        }

                        break;
                    case SharedPingChangeKind.Lost:
                        if (GatherPingRecipients(ping.Team) > 0)
                        {
                            PingLostRpc(ping.Id, NetMath.ToUnity(ping.Marker.Position), RpcTarget.Group(_pingRecipients, RpcTargetUse.Temp));
                        }

                        Debug.Log(PingLog.NetLost(ping), this);
                        break;
                    case SharedPingChangeKind.Removed:
                        if (GatherPingRecipients(ping.Team) > 0)
                        {
                            PingRemovedRpc(ping.Id, (byte)change.Removal, RpcTarget.Group(_pingRecipients, RpcTargetUse.Temp));
                        }

                        Debug.Log(PingLog.NetRemoved(ping, change.Removal), this);
                        break;
                }
            }

            _pingRecipients.Clear();
        }

        private void SendPingPlaced(SharedPing ping, float now, RpcParams rpcParams)
        {
            var marker = ping.Marker;
            var remaining = marker.RemainingSeconds(now);
            if (!(remaining > 0f))
            {
                return;
            }

            PingPlacedRpc(marker.Id, (byte)marker.Kind, NetMath.ToUnity(marker.Position), marker.IsTracking, remaining, rpcParams);
        }

        private int GatherPingRecipients(Team team)
        {
            _pingRecipients.Clear();
            if (!CanSendPings())
            {
                return 0;
            }

            _pingMembers.Clear();
            foreach (var pair in _playerById)
            {
                var player = pair.Value;
                if (player != null && !player.IsBot && _session != null && _session.HasPlayer(pair.Key))
                {
                    _pingMembers.Add(new KeyValuePair<ulong, Team>(player.OwnerClientId, _session.GetTeam(pair.Key)));
                }
            }

            CollectPingRecipients(_pingMembers, team, NetworkManager.ConnectedClientsIds, NetworkManager.ServerClientId, IsHost, _pingRecipients);
            _pingMembers.Clear();
            return _pingRecipients.Count;
        }

        private bool CanSendPings()
        {
            return IsSpawned && IsServer && NetworkManager != null && !NetworkManager.ShutdownInProgress;
        }

        // 断るのは普段の遊びでも起きる (連打・何も無い所) ので警告にはせず、連打でログを埋めないよう間引く。
        private PingRefusal RefusePing(int playerId, PingRefusal reason)
        {
            if (Time.unscaledTime >= _nextPingRefusalLogTime)
            {
                _nextPingRefusalLogTime = Time.unscaledTime + _rejectWarningInterval;
                Debug.Log(PingLog.NetRefused(playerId, reason), this);
                ServerEventsPingRefused(playerId, reason);
            }

            return reason;
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void PingPlacedRpc(int id, byte kind, Vector3 position, bool tracking, float remainingSeconds, RpcParams rpcParams)
        {
            if (!IsPingFromServer(rpcParams))
            {
                return;
            }

            var pingKind = (PingKind)kind;
            if (!_sharedPings.Place(id, pingKind, NetMath.ToNumerics(position), tracking, remainingSeconds, Time.time))
            {
                Debug.LogWarning(PingLog.Tag + " ignored an invalid ping from the server id=" + id + " kind=" + kind
                    + " remaining=" + remainingSeconds, this);
                return;
            }

            Debug.Log(PingLog.ReceivedPlaced(id, pingKind, NetMath.ToNumerics(position), remainingSeconds, tracking), this);
        }

        // 位置は次の知らせで上書きされるので、落ちても送り直さない。
        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void PingMovedRpc(int id, Vector3 position, RpcParams rpcParams)
        {
            if (IsPingFromServer(rpcParams))
            {
                _sharedPings.Move(id, NetMath.ToNumerics(position));
            }
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void PingLostRpc(int id, Vector3 position, RpcParams rpcParams)
        {
            if (IsPingFromServer(rpcParams) && _sharedPings.Lose(id, NetMath.ToNumerics(position)))
            {
                Debug.Log(PingLog.ReceivedLost(id, NetMath.ToNumerics(position)), this);
            }
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void PingRemovedRpc(int id, byte reason, RpcParams rpcParams)
        {
            if (IsPingFromServer(rpcParams) && _sharedPings.Remove(id))
            {
                Debug.Log(PingLog.ReceivedRemoved(id, (PingRemoval)reason), this);
            }
        }

        private bool IsPingFromServer(RpcParams rpcParams)
        {
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId)
            {
                return true;
            }

            Debug.LogWarning(PingLog.Tag + " ignored a ping from client " + rpcParams.Receive.SenderClientId, this);
            return false;
        }
    }
}
