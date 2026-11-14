using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 破片 (<see cref="DebrisPool"/> が出したもの) に付け、キャラクター (<see cref="Damageable"/>) に当たったら
    /// 速さと Rigidbody の質量から <see cref="DebrisCrushDamage"/> でダメージを与える。種類は <see cref="DamageKind.Environment"/>。
    /// 倒したときのキルフィードは、建物を壊したときの帰属 (<see cref="Attribution"/>) で出す。
    /// 1 つの破片は同じ相手に 1 度だけダメージを与える (乗ったまま転がる破片で削れ続けないように)。
    /// 当たり判定は <see cref="OnCollisionEnter"/> から <see cref="TryCrush"/> を呼ぶので、物理の進まない Edit Mode のテストでは直接呼べる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebrisCrusher : MonoBehaviour
    {
        public const string LogTag = DestructibleStructure.LogTag;

        private readonly HashSet<Damageable> _crushed = new HashSet<Damageable>();
        private DebrisCrushDamage _damage;
        private Rigidbody _body;

        /// <summary>ダメージの出し方。<see cref="Initialize"/> の前は null で、その間は何もしない。</summary>
        public DebrisCrushDamage Damage => _damage;

        /// <summary>建物を壊した者と武器。分からなければ null (倒れた本人の自滅として出す)。</summary>
        public ShotAttribution? Attribution { get; private set; }

        /// <summary>ログに出す、破片を出した建物の名前。</summary>
        public string Source { get; private set; } = string.Empty;

        /// <summary>ダメージの出し方・帰属・出した建物の名前を設定し、これまでに当たった相手を忘れる。</summary>
        /// <param name="damage">ダメージの出し方。null 不可。</param>
        /// <param name="attribution">建物を壊した者と武器。分からなければ null。</param>
        /// <param name="source">ログに出す建物の名前。null は空文字として扱う。</param>
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

        /// <summary>
        /// other の親をたどって見つけた生きている <see cref="Damageable"/> に、速さ speed で当たったときのダメージを与え、与えた量を返す。
        /// 相手がいない・死んでいる・この破片で既にダメージを受けた・計算したダメージが 0・Rigidbody が無いときは何もせず 0 を返す。
        /// </summary>
        /// <param name="other">当たったコライダー。null なら何もしない。</param>
        /// <param name="speed">当たったときの相対的な速さ (m/s)。0 以上。</param>
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

        /// <summary>
        /// 破片がキャラクターに当たったときのログ。
        /// 例: <c>[Destruction] debris hit structure=Pillar id=Enemy dmg=141 speed=7.7 mass=100.0 by=1 hp=0</c>。帰属が無ければ <c>by=-</c>。
        /// </summary>
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
