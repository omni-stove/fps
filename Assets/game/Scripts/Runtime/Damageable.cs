using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ダメージを受けられるもの (プレイヤー・敵・標的)。中に <see cref="Fps.Core.Health"/> を持ち、
    /// 命中は <see cref="Hitbox"/> 経由でここに届く。体力の判断は Health が行い、ここはログと通知の中継だけをする。
    /// Health は最初に使われたときに作るので、Awake が走らない Edit Mode でもそのまま使える。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Damageable : MonoBehaviour
    {
        public const string LogTag = "[Combat]";
        public const int DefaultMaxHealth = 100;

        /// <summary>死亡した瞬間に 1 度だけ呼ばれる。</summary>
        public event Action<Damageable> Died;

        [SerializeField, Tooltip("ログに出す識別子。空なら GameObject の名前")]
        private string _id = string.Empty;

        [SerializeField, Min(1), Tooltip("最大体力")]
        private int _maxHealth = DefaultMaxHealth;

        private Health _health;

        /// <summary>ログに出す識別子。未設定なら GameObject の名前。</summary>
        public string Id => string.IsNullOrEmpty(_id) ? name : _id;

        public int MaxHealth => _maxHealth;

        public Health Health
        {
            get
            {
                if (_health == null)
                {
                    Initialize(_id, _maxHealth);
                }

                return _health;
            }
        }

        public bool IsDead => Health.IsDead;

        /// <summary>
        /// 識別子と最大体力を設定し、体力を満タンで作り直す。シーン生成やコードでの組み立てから呼ぶ。
        /// </summary>
        /// <param name="id">ログに出す識別子。null か空なら GameObject の名前を使う。</param>
        /// <param name="maxHealth">最大体力。1 以上。</param>
        public void Initialize(string id, int maxHealth)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (_health != null)
            {
                _health.Died -= OnHealthDied;
            }

            _id = id ?? string.Empty;
            _maxHealth = maxHealth;
            _health = new Health(maxHealth);
            _health.Died += OnHealthDied;
        }

        /// <summary>
        /// 計算済みのダメージを体力に渡す。死亡後は何もしない。<see cref="Hitbox.ApplyHit"/> から呼ばれる。
        /// </summary>
        /// <param name="amount">ダメージ。0 以上。</param>
        public void ApplyDamage(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var health = Health;
            if (health.IsDead)
            {
                return;
            }

            health.TakeDamage(amount);
        }

        /// <summary>
        /// 狙う点 (コライダーを合わせた範囲の中心)。コライダーが無ければ Transform の位置。
        /// </summary>
        public Vector3 AimPoint => TryGetBounds(out var bounds) ? bounds.center : transform.position;

        /// <summary>
        /// 足元の点 (コライダーを合わせた範囲の底面の中心)。コライダーが無ければ Transform の位置。
        /// </summary>
        public Vector3 GroundPoint
        {
            get
            {
                if (!TryGetBounds(out var bounds))
                {
                    return transform.position;
                }

                var center = bounds.center;
                return new Vector3(center.x, bounds.min.y, center.z);
            }
        }

        /// <summary>自分と子孫の有効でトリガーでないコライダーを合わせた範囲。1 つも無ければ false。</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    found = true;
                }
            }

            return found;
        }

        private void Awake()
        {
            // Inspector の値を検査し、ここで作っておく (不正なら既定値で作る)。
            if (_health != null)
            {
                return;
            }

            if (_maxHealth <= 0)
            {
                Debug.LogError($"{LogTag} '{name}' の最大体力 {_maxHealth} は不正です。{DefaultMaxHealth} を使います。", this);
                _maxHealth = DefaultMaxHealth;
            }

            Initialize(_id, _maxHealth);
        }

        private void OnHealthDied()
        {
            Debug.Log($"{LogTag} died id={Id}", this);
            Died?.Invoke(this);
        }
    }
}
