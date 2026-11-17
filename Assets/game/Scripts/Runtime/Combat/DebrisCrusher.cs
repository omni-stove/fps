using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 1 つの破片が同じ相手に 1 度だけダメージを与えるのは、乗ったまま転がる破片で削れ続けないようにするため。
    // OnCollisionEnter から TryCrush を分けているのは、物理の進まない Edit Mode のテストから直接呼ぶため。
    [DisallowMultipleComponent]
    public sealed class DebrisCrusher : MonoBehaviour
    {
        public const string LogTag = DestructibleStructure.LogTag;

        private readonly HashSet<Damageable> _crushed = new HashSet<Damageable>();
        private DebrisCrushDamage _damage;
        private Rigidbody _body;

        public DebrisCrushDamage Damage => _damage;

        public ShotAttribution? Attribution { get; private set; }

        public string Source { get; private set; } = string.Empty;

        public void Initialize(DebrisCrushDamage damage, ShotAttribution? attribution, string source)
        {
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            Attribution = attribution;
            Source = source ?? string.Empty;
            _crushed.Clear();
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryCrush(collision.collider, collision.relativeVelocity.magnitude);
        }

        public int TryCrush(Collider other, float speed)
        {
            if (!(speed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative or NaN");
            }

            if (_damage == null || other == null)
            {
                return 0;
            }

            var target = other.GetComponentInParent<Damageable>();
            if (target == null || target.IsDead || _crushed.Contains(target))
            {
                return 0;
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_body == null)
            {
                return 0;
            }

            var mass = _body.mass;
            var amount = _damage.At(speed, mass);
            if (amount <= 0)
            {
                return 0;
            }

            _crushed.Add(target);
            var position = transform.position;
            using (CombatEvents.Attribute(Attribution))
            {
                target.TakeDamage(amount, position, target.AimPoint - position, DamageKind.Environment);
            }

            Debug.Log(FormatCrushLog(Source, target.Id, amount, speed, mass, Attribution, target.Health.Current), target);
            return amount;
        }

        public static string FormatCrushLog(
            string structureName, string targetId, int damage, float speed, float mass, ShotAttribution? attribution, int health)
        {
            var by = attribution.HasValue ? attribution.Value.KillerId.ToString(CultureInfo.InvariantCulture) : "-";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} debris hit structure={1} id={2} dmg={3} speed={4:0.0} mass={5:0.0} by={6} hp={7}",
                LogTag,
                structureName,
                targetId,
                damage,
                speed,
                mass,
                by,
                health);
        }
    }
}
