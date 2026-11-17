using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>
    /// ビーム (<see cref="RoundDelivery.Beam"/>) の線。当たりはサーバーが決めるので、ここでは見せるだけ。
    /// 他の人の線は照射の知らせ (<see cref="BeamStateMessage"/>) の始点と終点で出し、見た目を出す側 (NetV2Client) に <see cref="Drawn"/> と <see cref="Hidden"/> で知らせる。
    /// </summary>
    /// <remarks>
    /// 撃った本人は知らせを待たずに、押してから離すまで毎フレーム (<see cref="Update"/>) 今の視線で世界に当てて線を出す。
    /// 本人宛ての照射中の知らせは往復ぶん遅れた視線なので使わず、止まった知らせ (熱・弾切れ・倒れた) だけで線を消し、次に押すまで出さない。
    /// </remarks>
    public sealed class ClientBeams
    {
        private readonly Func<string, WeaponSpec> _findWeapon;
        private readonly HashSet<int> _shown = new HashSet<int>();
        private IWorldCollision _world;
        private int _layerMask;
        private int _localPlayerId = -1;
        private string _localWeaponId;
        private RoundSpec _localRound;

        public ClientBeams(Func<string, WeaponSpec> findWeapon)
        {
            _findWeapon = findWeapon ?? throw new ArgumentNullException(nameof(findWeapon));
        }

        public event Action<BeamView> Drawn;

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

        /// <summary>つなぎ直しや試合の切り替えで、出していた線をすべて消す。</summary>
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

            _shown.Add(shooterId);
            Drawn?.Invoke(new BeamView(shooterId, message.WeaponId ?? "", message.Start, message.End, TickSecondsOf(_findWeapon(message.WeaponId)?.Round)));
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
                    if (round.Delivery != RoundDelivery.Beam)
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

        /// <summary>撃った本人の照射の線を、今の目の位置 eye と視線 direction で出し直す。押していなければ何もしない。</summary>
        public void Update(Vector3 eye, Vector3 direction)
        {
            var round = _localRound;
            if (!IsAttached || round == null || !IsFinite(eye) || !IsFinite(direction) || direction.LengthSquared() < 1e-12f)
            {
                return;
            }

            direction = Vector3.Normalize(direction);

            // 他の人の体は手元の当たりに無いので、世界で止まる所まで出し、人に当たったかはサーバーの照射の知らせに任せる
            var end = _world.Raycast(eye, direction, round.Range, _layerMask, out var hit)
                ? hit.Point
                : eye + (direction * round.Range);
            Drawn?.Invoke(new BeamView(_localPlayerId, _localWeaponId, eye, end, TickSecondsOf(round)));
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

        // 武器が分からない線も出せるよう、照射の間合いが無ければ既定の間合いで残す
        private static float TickSecondsOf(RoundSpec round)
        {
            return round?.Beam != null ? round.Beam.TickSeconds : BeamView.DefaultHoldSeconds;
        }

        // ClientProjectiles と同じ引き方にして、弾薬の種類で届け方が変わる武器の扱いを揃える
        private static RoundSpec RoundFor(WeaponSpec spec, int ammoTypeIndex)
        {
            var types = spec.AmmoTypes;
            return ammoTypeIndex < types.Count ? types[ammoTypeIndex].RoundOr(spec.Round) : spec.Round;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }

    public readonly struct BeamView
    {
        /// <summary>武器が分からない照射の線を、次の知らせを待って残す秒数。</summary>
        public const float DefaultHoldSeconds = 0.1f;

        public BeamView(int shooterId, string weaponId, Vector3 start, Vector3 end, float tickSeconds)
        {
            ShooterId = shooterId;
            WeaponId = weaponId;
            Start = start;
            End = end;
            TickSeconds = tickSeconds;
        }

        public int ShooterId { get; }

        public string WeaponId { get; }

        /// <summary>撃った人の目の位置。銃口から出すかは見た目の側が決める。</summary>
        public Vector3 Start { get; }

        public Vector3 End { get; }

        /// <summary>照射の間合い。次の照射が来なければ、見た目の側はこの間合いを基に線を消す。</summary>
        public float TickSeconds { get; }
    }
}
