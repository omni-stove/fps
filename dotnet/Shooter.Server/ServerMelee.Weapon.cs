using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 持っている近接武器 (WeaponSpec.Melee) の溜めた振りと投げを決める。
    // 溜めは攻撃のボタンでなく近接のボタン (InputButtons.Melee) を押している長さで測る。攻撃のボタンは射撃の要求 (FireRequestMessage) でしか届かず、
    // 落ちてもよい届け方で何度も届く入力と違い、離した知らせが落ちると溜めたまま戻らなくなるため。
    // 投げは照準のボタン (InputButtons.Aim) を押した瞬間にする (オフラインの MeleeController.ThrowActionPath と同じ)
    internal sealed partial class ServerMelee
    {
        private readonly List<Thrown> _thrown = new List<Thrown>();
        private readonly List<int> _throwOrder = new List<int>();

        internal int ThrownCount => _thrown.Count;

        internal bool IsCharging(int playerId)
        {
            return _swingers.TryGetValue(playerId, out var swinger) && swinger.ChargeWeaponId != null;
        }

        // 入力は上下の向きを送らないので、照準の上下ではなく水平の向きから UpwardDegrees だけ起こす
        internal static Vector3 LaunchDirection(float yaw, float upwardDegrees)
        {
            var up = upwardDegrees * MathF.PI / 180f;
            var flat = MathF.Cos(up);
            return new Vector3(MathF.Sin(yaw) * flat, MathF.Sin(up), MathF.Cos(yaw) * flat);
        }

        // 溜められない近接武器は、オフラインの近接のボタンと同じく押した瞬間にその武器のふつうの振りで振る
        private bool PressWeapon(Swinger swinger, int tick)
        {
            var weapon = HeldMeleeWeapon(swinger.PlayerId);
            if (weapon == null)
            {
                return false;
            }

            var melee = weapon.Melee;
            if (!melee.CanCharge)
            {
                StartWeaponSwing(swinger, melee.Swing, 0f, weapon.Id);
                return true;
            }

            swinger.ChargeWeaponId = weapon.Id;
            swinger.ChargeTick = tick;
            swinger.ChargeTime = _now;
            return true;
        }

        // 押していた長さは入力の tick の差で測り、届いた時刻の差 (と届き方の揺れの分) を超えさせない。
        // 入力の tick だけで測ると、tick を飛ばした入力で押した瞬間に溜め切れてしまうため
        private void ReleaseWeapon(Swinger swinger, int tick)
        {
            var weaponId = swinger.ChargeWeaponId;
            if (weaponId == null)
            {
                return;
            }

            var byTicks = Math.Max(0, tick - swinger.ChargeTick) * _movement.TickSeconds;
            var byArrival = (float)(_now - swinger.ChargeTime + ServerPlayerWeapons.ChargeArrivalToleranceSeconds);
            swinger.DropCharge();

            // 持ち替えたら溜めを捨てる。持ち替えて戻ったときに、前に押していた分で振らないように (WeaponController.FollowChargingWeapon)
            var weapon = HeldMeleeWeapon(swinger.PlayerId);
            if (weapon == null || !string.Equals(weapon.Id, weaponId, StringComparison.Ordinal))
            {
                return;
            }

            var fraction = weapon.Melee.ChargeFraction(Math.Min(byTicks, byArrival));
            StartWeaponSwing(swinger, weapon.Melee.SwingFor(fraction), fraction, weapon.Id);
        }

        // 振りは始めたときの値で終えるので (MeleeSwing.Spec)、武器の振りの値は振り始めにだけ差し替えて戻す。調整値の Spec は変えない
        private void StartWeaponSwing(Swinger swinger, MeleeSpec spec, float chargeFraction, string weaponId)
        {
            var playerId = swinger.PlayerId;
            var refusal = Check(playerId);
            if (refusal != MeleeRefusal.None)
            {
                LogRefused(playerId, refusal);
                return;
            }

            var swing = swinger.Swing;
            var normal = swing.Spec;
            swing.Spec = spec;
            swing.TryStart();
            swing.Spec = normal;
            swinger.SwingWeaponId = weaponId;
            _log.WriteLine(MeleeLog.WeaponSwing(Text(playerId), weaponId, chargeFraction));
            _combat.InterruptReload(playerId, ReloadInterrupt.Melee);
        }

        // 振り終えて待っているだけなら投げられる。振っている最中は投げない (MeleeController.TryThrowWeapon と同じ)
        private void TryThrow(Swinger swinger)
        {
            var playerId = swinger.PlayerId;
            var weapon = HeldMeleeWeapon(playerId);
            if (weapon == null || !weapon.Melee.CanThrow)
            {
                return;
            }

            var refusal = Check(playerId);
            if (refusal == MeleeRefusal.Cooldown && !swinger.Swing.IsSwinging)
            {
                refusal = MeleeRefusal.None;
            }

            if (refusal != MeleeRefusal.None)
            {
                LogRefused(playerId, refusal);
                return;
            }

            if (!_context.Bodies.TryGet(playerId, out var body) || !_combat.WeaponsOf(playerId).Drop(weapon.Id, out _))
            {
                return;
            }

            WeaponsDropped?.Invoke(playerId);
            swinger.DropCharge();
            var spec = weapon.Melee.Throw;
            var velocity = LaunchDirection(body.Yaw, spec.UpwardDegrees) * spec.Speed;
            _thrown.Add(new Thrown(playerId, weapon.Id, spec, body.EyePosition, velocity));
            _log.WriteLine(MeleeLog.Thrown(Text(playerId), weapon.Id));
        }

        // 持ち替えの最中は振らない・投げない。構えていない武器で振れてしまう (WeaponController.IsMeleeWeaponBlocked)
        private WeaponSpec HeldMeleeWeapon(int playerId)
        {
            return _combat.TryGetWeapon(playerId, out var spec, out var state) && spec.IsMelee && !state.IsSwitching ? spec : null;
        }

        private void TickThrown(float deltaSeconds)
        {
            if (_thrown.Count == 0)
            {
                return;
            }

            // 当てたキルで試合が終わって始まり直すと、飛んでいる物を捨てて一覧が空になる。foreach だとそこで例外になるので番号で回す
            for (var i = 0; i < _thrown.Count; i++)
            {
                Fly(_thrown[i], deltaSeconds);
            }

            _thrown.RemoveAll(thrown => thrown.HasLanded);
        }

        // 最初に触れた物にだけ当たりを決め、跳ね返らずにそこで落ちたとみなす。跳ね返った先でもう一度当たると、1 回の投げで何人も倒せてしまう (ThrownMeleeWeapon.Strike)
        private void Fly(Thrown thrown, float deltaSeconds)
        {
            thrown.Velocity += new Vector3(0f, ServerGrenadeFlight.Gravity * deltaSeconds, 0f);
            var speed = thrown.Velocity.Length();
            var distance = speed * deltaSeconds;
            if (distance > 1e-6f)
            {
                var direction = thrown.Velocity / speed;
                var radius = thrown.Spec.Radius;
                var limit = distance;
                var hitWorld = _context.World.CapsuleCast(thrown.Position, thrown.Position, radius, direction, distance, ServerHitboxes.RaycastLayers, out var world)
                    && world.Distance < limit;
                if (hitWorld)
                {
                    limit = world.Distance;
                }

                var target = FindThrowTarget(thrown, direction, limit);
                if (target >= 0)
                {
                    HitWithThrown(thrown, target);
                    Land(thrown);
                    return;
                }

                if (hitWorld)
                {
                    _log.WriteLine(MeleeLog.ThrowWorldHit(Text(thrown.ThrowerId), thrown.WeaponId, ServerCombat.DefaultSurfaceMaterialId));
                    thrown.Position += direction * limit;
                    Land(thrown);
                    return;
                }

                thrown.Position += direction * distance;
            }

            thrown.FlightSeconds += deltaSeconds;
            if (thrown.FlightSeconds >= thrown.Spec.MaxFlightSeconds)
            {
                Land(thrown);
            }
        }

        // 同じ距離の相手を毎回同じに選ぶよう、Id の小さい順に見る。投げた本人には当てない (ThrownMeleeWeapon.Throw が本人との衝突を切るのと同じ)
        private int FindThrowTarget(Thrown thrown, Vector3 direction, float limit)
        {
            _throwOrder.Clear();
            foreach (var body in _context.Bodies.All)
            {
                if (body.PlayerId != thrown.ThrowerId && body.HasSpawned && _combat.IsHittable(body.PlayerId))
                {
                    _throwOrder.Add(body.PlayerId);
                }
            }

            _throwOrder.Sort();
            var best = -1;
            var bestDistance = float.PositiveInfinity;
            foreach (var playerId in _throwOrder)
            {
                _context.Bodies.TryGet(playerId, out var body);
                body.Capsule(out var bottom, out var top);
                if (ServerHitboxes.RayCapsule(thrown.Position, direction, bottom, top, body.Radius + thrown.Spec.Radius, limit, out var distance, out _)
                    && distance < bestDistance)
                {
                    best = playerId;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // 当たった所で頭か体かを分けないのは、オフラインの投げも部位で倍率をかけずに与える (Damageable.TakeDamage) ため
        private void HitWithThrown(Thrown thrown, int targetId)
        {
            var source = _context.Bodies.TryGet(thrown.ThrowerId, out var thrower) ? thrower.Position : thrown.Origin;
            _log.WriteLine(MeleeLog.ThrowHit(Text(thrown.ThrowerId), thrown.WeaponId, Text(targetId), thrown.Spec.Damage));
            _combat.DealDamage(thrown.ThrowerId, targetId, thrown.Spec.Damage, thrown.WeaponId, HitZone.Body, source);
        }

        private void Land(Thrown thrown)
        {
            thrown.HasLanded = true;
            _log.WriteLine(MeleeLog.ThrowLanded(Text(thrown.ThrowerId), thrown.WeaponId));
        }

        private sealed class Thrown
        {
            public Thrown(int throwerId, string weaponId, MeleeThrowSpec spec, Vector3 origin, Vector3 velocity)
            {
                ThrowerId = throwerId;
                WeaponId = weaponId;
                Spec = spec;
                Origin = origin;
                Position = origin;
                Velocity = velocity;
            }

            public int ThrowerId { get; }

            public string WeaponId { get; }

            public MeleeThrowSpec Spec { get; }

            public Vector3 Origin { get; }

            public Vector3 Position { get; set; }

            public Vector3 Velocity { get; set; }

            public float FlightSeconds { get; set; }

            public bool HasLanded { get; set; }
        }

        private sealed partial class Swinger
        {
            // null ならふつうの近接の振り。持っている近接武器で振ったなら、その武器の Id で数える
            public string SwingWeaponId { get; set; }

            public bool AimHeld { get; set; }

            // null なら溜めていない
            public string ChargeWeaponId { get; set; }

            public int ChargeTick { get; set; }

            public double ChargeTime { get; set; }

            public void DropCharge()
            {
                ChargeWeaponId = null;
                ChargeTick = 0;
                ChargeTime = 0d;
            }
        }
    }
}
