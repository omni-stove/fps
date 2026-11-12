using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ダメージを受けるもの。中に <see cref="Fps.Core.Health"/> を 1 つ持ち、<see cref="Hitbox"/> から命中を受け取る。
    /// ダメージ量の計算は <see cref="DamageCalculator"/> に任せ、ここでは結果を Health に渡すだけ。
    /// Health は最初に触れたときに <see cref="MaxHealth"/> で作るので、Awake が走らない Edit Mode でも使える。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Damageable : MonoBehaviour
    {
        public const int DefaultMaxHealth = 100;

        [SerializeField, Min(1), Tooltip("最大体力。開始時は満タン")]
        private int _maxHealth = DefaultMaxHealth;

        private Health _health;

        /// <summary>体力が 0 になった瞬間に 1 度だけ呼ばれる。</summary>
        public event Action<Damageable> Died;

        /// <summary>最大体力。Health を作った後は変えられない。</summary>
        public int MaxHealth
        {
            get => _health?.Max ?? _maxHealth;
            set
            {
                if (_health != null)
                {
                    throw new InvalidOperationException($"'{name}' already created its Health; set MaxHealth before the first hit");
                }

                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxHealth must be positive");
                }

                _maxHealth = value;
            }
        }

        /// <summary>この Damageable の体力。初回アクセス時に <see cref="MaxHealth"/> で作る。</summary>
        public Health Health
        {
            get
            {
                if (_health == null)
                {
                    // Inspector で 0 以下にされていても Health の例外で落ちないよう 1 に揃える。
                    _health = new Health(Math.Max(1, _maxHealth));
                    _health.Died += OnHealthDied;
                }

                return _health;
            }
        }

        public bool IsDead => _health != null && _health.IsDead;

        private void Awake()
        {
            _ = Health;
        }

        /// <summary>
        /// baseDamage に部位倍率と距離減衰を掛けた値を <see cref="DamageCalculator.Calculate"/> で求め、Health から引く。
        /// 計算したダメージ量を返す (既に死んでいれば Health は変わらないが、計算値はそのまま返す)。
        /// </summary>
        /// <param name="baseDamage">武器の基本ダメージ。0 以上。</param>
        /// <param name="zone">命中した部位。</param>
        /// <param name="distance">射手から命中点までの距離。0 以上。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        public int ApplyHit(int baseDamage, HitZone zone, float distance, DamageFalloff falloff)
        {
            var damage = DamageCalculator.Calculate(baseDamage, zone, distance, falloff);
            Health.TakeDamage(damage);
            return damage;
        }

        private void OnHealthDied()
        {
            Debug.Log($"[Combat] killed target={name}", this);
            Died?.Invoke(this);
        }
    }
}
