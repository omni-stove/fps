using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 本人宛ての噴射中の知らせは往復ぶん遅れた視線なので使わず、撃った本人は知らせを待たずに押してから離すまで毎フレーム (Update) 今の視線で世界に当てて火炎を出す。
    // 止まった知らせ (熱・倒れた) だけは本人宛てでも使って火炎を消し、次に押すまで出さない。
    public sealed class ClientSpray
    {
        private readonly Func<string, WeaponSpec> _findWeapon;
        private readonly HashSet<int> _shown = new HashSet<int>();
        private IWorldCollision _world;
        private int _layerMask;
        private int _localPlayerId = -1;
        private string _localWeaponId;
        private RoundSpec _localRound;

        public ClientSpray(Func<string, WeaponSpec> findWeapon)
        {
            _findWeapon = findWeapon ?? throw new ArgumentNullException(nameof(findWeapon));
        }

        public event Action<SprayView> Drawn;

        public event Action<int> Hidden;

        public bool IsAttached => _world != null;

        public bool IsLocalFiring => _localRound != null;

        public void Attach(IWorldCollision world, int layerMask, int localPlayerId)
        {
            Detach();
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _layerMask = layerMask;
            _localPlayerId = localPlayerId;
        }

        public void Detach()
        {
            StopLocal();
            if (_shown.Count > 0)
            {
                var shown = new List<int>(_shown);
                _shown.Clear();
                foreach (var shooterId in shown)
                {
                    Hidden?.Invoke(shooterId);
                }
            }

            _world = null;
            _localPlayerId = -1;
        }

        public void Receive(BeamStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!IsAttached)
            {
                return;
            }

            var shooterId = message.ShooterId;
            if (shooterId == _localPlayerId)
            {
                if (!message.Active)
                {
                    StopLocal();
                }

                return;
            }

            if (!message.Active)
            {
                if (_shown.Remove(shooterId))
                {
                    Hidden?.Invoke(shooterId);
                }

                return;
            }

            // 始点と終点が重なった知らせは向きが分からず火炎を向けられないので、出さずに次の噴射を待つ。
            if ((message.End - message.Start).LengthSquared() < 1e-12f)
            {
                return;
            }

            _shown.Add(shooterId);
            Drawn?.Invoke(new SprayView(shooterId, message.WeaponId ?? "", message.Start, message.End, _findWeapon(message.WeaponId)?.Round.Spray));
        }

        public void LocalTrigger(FireRequestMessage request, WeaponSpec spec)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!IsAttached)
            {
                return;
            }

            switch (request.Trigger)
            {
                case TriggerEdge.Pressed:
                    var round = RoundFor(spec, request.AmmoTypeIndex);
                    if (round.Delivery != RoundDelivery.Spray)
                    {
                        return;
                    }

                    _localWeaponId = spec.Id;
                    _localRound = round;
                    Update(request.Origin, request.Direction);
                    break;
                case TriggerEdge.Released:
                    StopLocal();
                    break;
            }
        }

        public void Update(Vector3 eye, Vector3 direction)
        {
            var round = _localRound;
            if (!IsAttached || round == null || !IsFinite(eye) || !IsFinite(direction) || direction.LengthSquared() < 1e-12f)
            {
                return;
            }

            direction = Vector3.Normalize(direction);

            // 他の人の体は火炎を止めないので、世界で止まる所まで出す
            var end = _world.Raycast(eye, direction, round.Range, _layerMask, out var hit)
                ? hit.Point
                : eye + (direction * round.Range);
            Drawn?.Invoke(new SprayView(_localPlayerId, _localWeaponId, eye, end, round.Spray));
        }

        private void StopLocal()
        {
            if (_localRound == null)
            {
                return;
            }

            _localRound = null;
            _localWeaponId = null;
            Hidden?.Invoke(_localPlayerId);
        }

        // ClientProjectiles と同じ引き方にして、弾薬の種類で届け方が変わる武器の扱いを揃える
        private static RoundSpec RoundFor(WeaponSpec spec, int ammoTypeIndex)
        {
            var types = spec.AmmoTypes;
            return ammoTypeIndex < types.Count ? types[ammoTypeIndex].RoundOr(spec.Round) : spec.Round;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }
    }

    public readonly struct SprayView
    {
        public const float DefaultHoldSeconds = 0.1f;

        public const float DefaultConeDegrees = 10f;

        public SprayView(int shooterId, string weaponId, Vector3 start, Vector3 end, SpraySpec spray)
        {
            ShooterId = shooterId;
            WeaponId = weaponId;
            Start = start;
            End = end;

            // 武器が分からない火炎も出せるよう、間合いと広がりが無ければ既定で出す
            TickSeconds = spray != null ? spray.TickSeconds : DefaultHoldSeconds;
            ConeDegrees = spray != null ? spray.ConeDegrees : DefaultConeDegrees;
        }

        public int ShooterId { get; }

        public string WeaponId { get; }

        // 銃口から出すかは見た目の側が決めるので、撃った人の目の位置のまま持つ。
        public Vector3 Start { get; }

        public Vector3 End { get; }

        public float TickSeconds { get; }

        public float ConeDegrees { get; }
    }
}
