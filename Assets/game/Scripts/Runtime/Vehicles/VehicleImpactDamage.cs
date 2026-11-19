using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 動き方 (IVehicleMotor) と分けるのは、ゲームが足す戦闘機やヘリも同じ部品で人や物にぶつかったダメージを与えられるようにするため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Vehicle))]
    public sealed class VehicleImpactDamage : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("この速さ (m/s) 以下でぶつかってもダメージを与えない")]
        private float _minSpeed = VehicleImpactSpec.DefaultMinSpeed;

        [SerializeField, Min(0f), Tooltip("最低の速さを 1 m/s 超えるごとのダメージ")]
        private float _damagePerSpeed = VehicleImpactSpec.DefaultDamagePerSpeed;

        [SerializeField, Min(0), Tooltip("1 回にぶつかって与えるダメージの上限")]
        private int _maxDamage = VehicleImpactSpec.DefaultMaxDamage;

        [SerializeField, Min(0f), Tooltip("同じ相手にもう一度ダメージを与えるまでの秒数")]
        private float _repeatSeconds = VehicleImpactSpec.DefaultRepeatSeconds;

        private readonly Dictionary<Damageable, float> _lastHit = new Dictionary<Damageable, float>();
        private readonly List<Damageable> _stale = new List<Damageable>();
        private VehicleImpactSpec _spec;
        private Vehicle _vehicle;

        public VehicleImpactSpec Spec => _spec ??= new VehicleImpactSpec(_minSpeed, _damagePerSpeed, _maxDamage, _repeatSeconds);

        public Vehicle Vehicle => _vehicle != null ? _vehicle : (_vehicle = GetComponent<Vehicle>());

        private void OnValidate()
        {
            _spec = null;
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHit(collision.collider, collision.relativeVelocity.magnitude, Time.time);
        }

        public void Configure(VehicleImpactSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _minSpeed = spec.MinSpeed;
            _damagePerSpeed = spec.DamagePerSpeed;
            _maxDamage = spec.MaxDamage;
            _repeatSeconds = spec.RepeatSeconds;
        }

        // 当たり判定は OnCollisionEnter から呼ぶ。物理の進まない Edit Mode のテストからも、時刻を渡して直接呼べるよう public にする。
        public int TryHit(Collider other, float speed, float now)
        {
            if (!(speed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative or NaN");
            }

            if (float.IsNaN(now) || float.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now), now, "now must be finite");
            }

            if (other == null)
            {
                return 0;
            }

            var target = other.GetComponentInParent<Damageable>();
            if (target == null || target.IsDead || target.transform.IsChildOf(transform) || IsAboard(target))
            {
                return 0;
            }

            var spec = Spec;
            if (_lastHit.TryGetValue(target, out var last) && now - last < spec.RepeatSeconds)
            {
                return 0;
            }

            var amount = spec.DamageAt(speed);
            if (amount <= 0)
            {
                return 0;
            }

            ForgetOldHits(now);
            _lastHit[target] = now;
            var driver = Driver();
            var position = transform.position;
            using (CombatEvents.Attribute(driver != null ? driver.gameObject : null, Vehicle.VehicleId))
            {
                target.TakeDamage(amount, position, target.AimPoint - position, DamageKind.Vehicle);
            }

            Debug.Log(VehicleLog.Ram(name, target.Id, amount, speed, driver != null ? driver.name : null, target.Health.Current), target);

            // オフラインで運転するのは手元のプレイヤーだけなので、その人が轢いたときだけ画面にヒットマーカーとダメージ表示を出す。
            var driverBody = driver != null ? driver.GetComponent<Damageable>() : null;
            if (driverBody != null && driverBody.IsLocalPlayer)
            {
                CombatEvents.RaiseHitConfirmed(new HitConfirm(amount, HitZone.Body, target.IsDead, target.IsDowned, target.AimPoint.ToNumerics()));
            }

            return amount;
        }

        private Transform Driver()
        {
            var vehicle = Vehicle;
            var index = vehicle.Spec.DriverSeatIndex;
            var occupant = index >= 0 ? vehicle.OccupantOf(index) : null;
            return occupant != null ? occupant.RiderRoot : null;
        }

        private bool IsAboard(Damageable target)
        {
            foreach (var seat in Vehicle.Seats)
            {
                var root = seat != null && seat.Occupant != null ? seat.Occupant.RiderRoot : null;
                if (root != null && target.transform.IsChildOf(root))
                {
                    return true;
                }
            }

            return false;
        }

        private void ForgetOldHits(float now)
        {
            _stale.Clear();
            foreach (var pair in _lastHit)
            {
                if (pair.Key == null || now - pair.Value >= Spec.RepeatSeconds)
                {
                    _stale.Add(pair.Key);
                }
            }

            foreach (var key in _stale)
            {
                _lastHit.Remove(key);
            }

            _stale.Clear();
        }
    }
}
