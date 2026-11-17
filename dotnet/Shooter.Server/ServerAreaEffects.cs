using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed partial class ServerAreaEffects
    {
        public const string LogTag = "[AreaEffect]";

        private readonly ServerCombat _combat;
        private readonly List<Zone> _zones = new List<Zone>();
        private readonly AreaEffectMessage _message = new AreaEffectMessage();
        private int _nextId;

        public ServerAreaEffects(ServerCombat combat)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            AttachSmoke();
        }

        public int Count => _zones.Count;

        public IReadOnlyList<Zone> Zones => _zones;

        public Zone Spread(AreaEffectSpec spec, Vector3 center, int? ownerId, string weaponId)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(weaponId);
            var zone = new Zone(_nextId++, spec, center, ownerId, weaponId);
            _zones.Add(zone);
            _combat.SendToAll(Fill(zone));
            _combat.Log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net spread id={1} kind={2} at=({3:0.0}, {4:0.0}, {5:0.0}) radius={6:0.0} seconds={7:0.0}",
                LogTag,
                zone.Id,
                KindOf(spec),
                center.X,
                center.Y,
                center.Z,
                spec.Radius,
                spec.DurationSeconds));
            return zone;
        }

        public void Tick(float deltaSeconds)
        {
            for (var i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];
                var step = Math.Min(deltaSeconds, zone.Remaining);
                StepSmoke(zone, step);
                StepFire(zone, step);
                zone.Remaining -= deltaSeconds;
                if (zone.Remaining > 0f)
                {
                    continue;
                }

                _zones.RemoveAt(i);
                i--;
                End(zone);
            }
        }

        public void SendState(ServerMessenger messenger, ConnectionId connection)
        {
            ArgumentNullException.ThrowIfNull(messenger);
            foreach (var zone in _zones)
            {
                messenger.Send(connection, TransportChannel.Reliable, Fill(zone));
            }
        }

        public void Clear()
        {
            while (_zones.Count > 0)
            {
                var zone = _zones[_zones.Count - 1];
                _zones.RemoveAt(_zones.Count - 1);
                End(zone);
            }
        }

        partial void AttachSmoke();

        partial void StepSmoke(Zone zone, float deltaSeconds);

        partial void StepFire(Zone zone, float deltaSeconds);

        private void End(Zone zone)
        {
            zone.Remaining = 0f;
            _combat.SendToAll(Fill(zone));
            _combat.Log.WriteLine(LogTag + " net ended id=" + zone.Id.ToString(CultureInfo.InvariantCulture));
        }

        private AreaEffectMessage Fill(Zone zone)
        {
            _message.AreaId = zone.Id;
            _message.KindId = KindOf(zone.Spec);
            _message.Center = zone.Center;
            _message.Radius = zone.Spec.Radius;
            _message.RemainingSeconds = Math.Max(0f, zone.Remaining);
            return _message;
        }

        // クライアントは見た目だけを出すので、差し替えられる見た目の Id を種類として送る
        private static string KindOf(AreaEffectSpec spec)
        {
            return spec.VisualId;
        }

        internal sealed class Zone
        {
            public Zone(int id, AreaEffectSpec spec, Vector3 center, int? ownerId, string weaponId)
            {
                Id = id;
                Spec = spec;
                Center = center;
                OwnerId = ownerId;
                WeaponId = weaponId;
                Remaining = spec.DurationSeconds;
            }

            public int Id { get; }

            public AreaEffectSpec Spec { get; }

            public Vector3 Center { get; }

            public int? OwnerId { get; }

            public string WeaponId { get; }

            public float Remaining { get; set; }
        }
    }
}
