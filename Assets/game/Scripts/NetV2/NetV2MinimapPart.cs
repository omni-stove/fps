using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // 敵はスナップショットに載っていても地図には載せない。載せるとオンラインに無い UAV と同じく敵が常に見えてしまう。
    public sealed class NetV2MinimapPart : INetV2ClientPart
    {
        private const float _degreesPerRadian = 180f / MathF.PI;

        private readonly Func<int> _localPlayerId;
        private readonly SharedPingMarkers _pings;
        private readonly MinimapSpec _spec;
        private readonly Func<int, Vector3?> _controlPointPosition;
        private readonly List<MinimapContact> _allies = new List<MinimapContact>();
        private readonly List<ControlPointSnapshot> _controlPoints = new List<ControlPointSnapshot>();
        private readonly List<MinimapContact> _contacts = new List<MinimapContact>();

        private bool _hasSelf;
        private Vector3 _selfPosition;
        private float _selfHeading;
        private Team _selfTeam;
        private bool _hasSnapshot;
        private float _sincePublish;
        private MinimapStatus? _published;
        private bool _disposed;

        // 部品はつなぐときに作り、自分の Id は承認の後に、拠点の場所はマップのシーンを読んだ後に決まるので、値ではなく引く口を受け取る。
        public NetV2MinimapPart(Func<int> localPlayerId, SharedPingMarkers pings, MinimapSpec spec, Func<int, Vector3?> controlPointPosition)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _pings = pings ?? throw new ArgumentNullException(nameof(pings));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _controlPointPosition = controlPointPosition ?? throw new ArgumentNullException(nameof(controlPointPosition));
            _sincePublish = spec.UpdateIntervalSeconds;
        }

        // 印のメッセージは ClientPings が写して印の表示も読むので、ここで受け持って横取りしない。
        public bool Handle(IMessage message)
        {
            return false;
        }

        // 届いたスナップショットは後で使い回されることがあるので、要る値だけを写しておく。
        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (_disposed)
            {
                return;
            }

            _hasSnapshot = true;
            _hasSelf = false;
            _allies.Clear();
            var localId = _localPlayerId();
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId != localId)
                {
                    continue;
                }

                if (TryPose(players[i], out _selfPosition, out _selfHeading))
                {
                    _hasSelf = true;
                    _selfTeam = players[i].Team;
                }

                break;
            }

            if (_hasSelf)
            {
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player.PlayerId == localId || player.Team != _selfTeam || player.Health <= 0)
                    {
                        continue;
                    }

                    if (TryPose(player, out var position, out var heading))
                    {
                        _allies.Add(new MinimapContact(MinimapContactKind.Ally, position, heading, player.Team));
                    }
                }
            }

            _controlPoints.Clear();
            var points = snapshot.ControlPoints;
            for (var i = 0; i < points.Count; i++)
            {
                _controlPoints.Add(points[i]);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || !_hasSnapshot)
            {
                return;
            }

            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                _sincePublish += deltaSeconds;
            }

            if (_sincePublish < _spec.UpdateIntervalSeconds)
            {
                return;
            }

            _sincePublish = 0f;
            if (!_hasSelf)
            {
                Withdraw();
                return;
            }

            var status = Build();

            // Publish は同じ値でも購読者を呼ぶので、止まっている間に地図を描き直させないよう変わったときだけ出す。
            if (_published.HasValue && _published.Value.Equals(status))
            {
                return;
            }

            _published = status;
            GameSignals.Minimap.Publish(status);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Withdraw();
        }

        private MinimapStatus Build()
        {
            _contacts.Clear();
            _contacts.AddRange(_allies);
            var markers = _pings.Markers;
            for (var i = 0; i < markers.Count; i++)
            {
                var position = markers[i].Position;
                if (IsFinite(position))
                {
                    _contacts.Add(new MinimapContact(MinimapContactKind.Ping, position, 0f, _selfTeam));
                }
            }

            foreach (var point in _controlPoints)
            {
                var position = _controlPointPosition(point.Index);
                if (position.HasValue && IsFinite(position.Value))
                {
                    _contacts.Add(new MinimapContact(
                        MinimapContactKind.Objective, position.Value, 0f, point.HasOwner ? point.Owner : (Team?)null));
                }
            }

            return new MinimapStatus(_selfPosition, _selfHeading, _contacts);
        }

        // 他の側が後から出した値まで消さないよう、自分が出した値のときだけ消す。
        private void Withdraw()
        {
            if (_published.HasValue)
            {
                GameSignals.Minimap.ClearIfCurrent(_published);
                _published = null;
            }
        }

        // サーバーから届いた値が壊れていても、1 人を載せないだけで地図全体は止めない。
        private static bool TryPose(in PlayerSnapshot player, out Vector3 position, out float headingDegrees)
        {
            position = player.Position;
            var yaw = player.Yaw;
            if (!IsFinite(position) || float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                headingDegrees = 0f;
                return false;
            }

            headingDegrees = yaw * _degreesPerRadian;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}
