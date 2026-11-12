using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ダメージを受けるもの。中で <see cref="Fps.Core.Health"/> を持ち、ダメージと死亡の判断はそちらに任せる。
    /// <see cref="ResetHealth"/> で Health を作り直しても、<see cref="Died"/> の購読者はそのまま通知を受け取れる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Damageable : MonoBehaviour
    {
        public const string LogTag = "[Combat]";
        public const int DefaultMaxHealth = 100;

        [SerializeField, Min(1), Tooltip("最大体力")]
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

        public int MaxHealth
        {
            get => _maxHealth;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "max health must be positive");
                }

                _maxHealth = value;
            }
        }

        public bool IsDead => Health.IsDead;

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

            _health = new Health(Mathf.Max(1, _maxHealth));
            _health.Died += OnHealthDied;
        }

        private void OnHealthDied()
        {
            Debug.Log($"{LogTag} target died id={name}", this);
            Died?.Invoke(this);
        }
    }
}
