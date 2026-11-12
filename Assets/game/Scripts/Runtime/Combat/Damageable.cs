using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ダメージを受けるもの。中で <see cref="Fps.Core.Health"/> を持ち、ダメージと死亡の判断はそちらに任せる。
    /// <see cref="Hitbox"/> からの命中は <see cref="ApplyHit"/> で受け、ダメージ量の計算は <see cref="DamageCalculator"/> に任せる。
    /// Health は Awake か最初に触れたときに <see cref="MaxHealth"/> で作るので、Awake が走らない Edit Mode でも使える。
    /// <see cref="ResetHealth"/> で Health を作り直しても、<see cref="Died"/> の購読者はそのまま通知を受け取れる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Damageable : MonoBehaviour
    {
        public const string LogTag = "[Combat]";
        public const int DefaultMaxHealth = 100;

        [SerializeField, Min(1), Tooltip("最大体力。開始時と ResetHealth 時は満タン")]
        private int _maxHealth = DefaultMaxHealth;

        private Health _health;

        /// <summary>死亡した瞬間に、死んだ Damageable を渡して 1 度だけ呼ばれる (ResetHealth 後は再び呼ばれ得る)。</summary>
        public event Action<Damageable> Died;

        /// <summary>今の体力。初めて触れたときに <see cref="MaxHealth"/> で作る。</summary>
        public Health Health
        {
            get
            {
                if (_health == null)
                {
                    ResetHealth();
                }

                return _health;
            }
        }

        /// <summary>
        /// 最大体力。Health を作った後に変えても今の Health はそのままで、次の <see cref="ResetHealth"/> から反映する。
        /// </summary>
        public int MaxHealth
        {
            get => _maxHealth;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxHealth must be positive");
                }

                _maxHealth = value;
            }
        }

        /// <summary>死んでいれば true。Health をまだ作っていなければ false (ここでは作らない)。</summary>
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

        /// <summary>体力を amount 減らす。死亡後は何もしない。</summary>
        public void TakeDamage(int amount)
        {
            Health.TakeDamage(amount);
        }

        /// <summary>残りの体力ぶんのダメージを与えて倒す。Play 中に Inspector の右クリックメニューからも呼べる。</summary>
        [ContextMenu("Kill")]
        public void Kill()
        {
            TakeDamage(Health.Current);
        }

        /// <summary><see cref="MaxHealth"/> で満タンの Health を作り直す。</summary>
        public void ResetHealth()
        {
            if (_health != null)
            {
                _health.Died -= OnHealthDied;
            }

            // Inspector で 0 以下にされていても Health の例外で落ちないよう 1 に揃える。
            _health = new Health(Math.Max(1, _maxHealth));
            _health.Died += OnHealthDied;
        }

        private void OnHealthDied()
        {
            Debug.Log($"{LogTag} target died id={name}", this);
            Died?.Invoke(this);
        }
    }
}
