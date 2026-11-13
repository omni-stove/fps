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
    /// 敵の視線判定や射撃の狙いには <see cref="AimPoint"/> / <see cref="GroundPoint"/> を使う。
    /// 生きている間にダメージを受けると <see cref="Damaged"/> で量と撃った者の位置を知らせる (敵が撃たれた方向へ向かうのに使う)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Damageable : MonoBehaviour
    {
        public const string LogTag = "[Combat]";
        public const int DefaultMaxHealth = 100;

        [SerializeField, Tooltip("ログに出す識別子。空なら GameObject の名前")]
        private string _id = string.Empty;

        [SerializeField, Min(1), Tooltip("最大体力。開始時と ResetHealth 時は満タン")]
        private int _maxHealth = DefaultMaxHealth;

        private Health _health;

        /// <summary>死亡した瞬間に、死んだ Damageable を渡して 1 度だけ呼ばれる (ResetHealth 後は再び呼ばれ得る)。</summary>
        public event Action<Damageable> Died;

        /// <summary>
        /// 生きている状態でダメージを受けたときに、受けた Damageable と <see cref="DamageReport"/> を渡して呼ばれる。
        /// Health を減らした後に呼ぶので、とどめの 1 発では <see cref="Died"/> の後に呼ばれ、<see cref="IsDead"/> は true になっている。
        /// 既に死んでいるときのダメージでは呼ばない。
        /// </summary>
        public event Action<Damageable, DamageReport> Damaged;

        /// <summary>
        /// <see cref="ResetHealth"/> で <see cref="Health"/> を作り直したときに、新しい Health を渡して呼ばれる。
        /// Health のイベントを直接購読している側 (HUD など) はここで購読し直す。
        /// </summary>
        public event Action<Health> HealthReset;

        /// <summary>ログに出す識別子。未設定なら GameObject の名前。</summary>
        public string Id => string.IsNullOrEmpty(_id) ? name : _id;

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

        /// <summary>
        /// この画面で操作しているプレイヤーか。true のときだけ被弾を <see cref="CombatEvents.DamageTaken"/> に流す。
        /// 実行時にプレイヤーを組み立てる <see cref="GameBootstrap"/> が設定する (シーンには保存しない)。
        /// </summary>
        public bool IsLocalPlayer { get; set; }

        private void Awake()
        {
            _ = Health;
        }

        /// <summary>
        /// 識別子と最大体力を設定し、満タンの Health を作り直す。シーン生成やコードでの組み立てから呼ぶ。
        /// </summary>
        /// <param name="id">ログに出す識別子。null か空なら GameObject の名前を使う。</param>
        /// <param name="maxHealth">最大体力。1 以上。</param>
        public void Initialize(string id, int maxHealth)
        {
            MaxHealth = maxHealth;
            _id = id ?? string.Empty;
            ResetHealth();
        }

        /// <summary>
        /// baseDamage に部位倍率と距離減衰を掛けた値を <see cref="DamageCalculator.Calculate"/> で求め、Health から引く。
        /// 計算したダメージ量を返す (既に死んでいれば Health は変わらないが、計算値はそのまま返す)。
        /// </summary>
        /// <param name="baseDamage">武器の基本ダメージ。0 以上。</param>
        /// <param name="zone">命中した部位。</param>
        /// <param name="distance">射手から命中点までの距離。0 以上。</param>
        /// <param name="falloff">距離減衰の設定。</param>
        /// <param name="sourcePosition">撃った者の位置。分からなければ null。<see cref="Damaged"/> にそのまま渡す。</param>
        public int ApplyHit(int baseDamage, HitZone zone, float distance, DamageFalloff falloff, Vector3? sourcePosition = null)
        {
            var damage = DamageCalculator.Calculate(baseDamage, zone, distance, falloff);
            TakeDamageCore(damage, sourcePosition, zone);
            return damage;
        }

        /// <summary>
        /// 体力を amount 減らし、生きていたなら <see cref="Damaged"/> を呼ぶ。死亡後は何もしない。
        /// 生きていたなら、<see cref="IsLocalPlayer"/> のときは <see cref="CombatEvents.DamageTaken"/> を、
        /// この減りで倒れたときは <see cref="CombatEvents.Killed"/> を出す (<see cref="Damaged"/> の後)。
        /// </summary>
        /// <param name="amount">ダメージ量。0 以上。</param>
        /// <param name="sourcePosition">ダメージを与えた者の位置。分からなければ null。</param>
        public void TakeDamage(int amount, Vector3? sourcePosition = null)
        {
            TakeDamageCore(amount, sourcePosition, null);
        }

        private void TakeDamageCore(int amount, Vector3? sourcePosition, HitZone? zone)
        {
            var health = Health;
            var wasAlive = !health.IsDead;
            health.TakeDamage(amount);
            if (!wasAlive)
            {
                return;
            }

            Damaged?.Invoke(this, new DamageReport(amount, sourcePosition));

            if (IsLocalPlayer)
            {
                CombatEvents.RaiseDamageTaken(sourcePosition, amount);
            }

            if (health.IsDead)
            {
                CombatEvents.RaiseKilled(KillFeedEntryFor(zone));
            }
        }

        // 帰属が無い死 (Inspector の Kill・落下など) は、KillFeedEntry の決まりに合わせて自滅として出す。
        private KillFeedEntry KillFeedEntryFor(HitZone? zone)
        {
            var victimId = CombatEvents.OfflineId(gameObject);
            var attribution = CombatEvents.CurrentAttribution;
            var killerId = attribution.HasValue ? attribution.Value.KillerId : victimId;
            var weaponId = attribution.HasValue ? attribution.Value.WeaponId : string.Empty;
            return new KillFeedEntry(killerId, victimId, weaponId, zone == HitZone.Head, friendly: false);
        }

        /// <summary>残りの体力ぶんのダメージを与えて倒す。Play 中に Inspector の右クリックメニューからも呼べる。</summary>
        [ContextMenu("Kill")]
        public void Kill()
        {
            TakeDamage(Health.Current);
        }

        /// <summary>狙う点 (自分と子孫のコライダーを合わせた範囲の中心)。コライダーが無ければ Transform の位置。</summary>
        public Vector3 AimPoint => TryGetBounds(out var bounds) ? bounds.center : transform.position;

        /// <summary>足元の点 (コライダーを合わせた範囲の底面の中心)。コライダーが無ければ Transform の位置。</summary>
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
            HealthReset?.Invoke(_health);
        }

        /// <summary>
        /// Health を作り直して体力を current にする。セーブデータから戻すときに使う。<see cref="MaxHealth"/> を超える値は Max に揃える。
        /// <see cref="ResetHealth"/> と同じく <see cref="HealthReset"/> を呼ぶ。
        /// </summary>
        /// <param name="current">戻す体力。1 以上 (0 では生きた状態に戻せない)。</param>
        public void RestoreHealth(int current)
        {
            if (current <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must be positive");
            }

            ResetHealth();
            var missing = _health.Max - Math.Min(current, _health.Max);
            if (missing > 0)
            {
                _health.TakeDamage(missing);
            }
        }

        private void OnHealthDied()
        {
            Debug.Log($"{LogTag} target died id={Id}", this);
            Died?.Invoke(this);
        }
    }

    /// <summary><see cref="Damageable.Damaged"/> で渡す、1 回の被弾の情報。</summary>
    public readonly struct DamageReport
    {
        /// <summary>受けたダメージ量 (残りの体力で切り詰める前の値)。</summary>
        public int Amount { get; }

        /// <summary>ダメージを与えた者の位置。分からなければ null。</summary>
        public Vector3? SourcePosition { get; }

        public DamageReport(int amount, Vector3? sourcePosition)
        {
            Amount = amount;
            SourcePosition = sourcePosition;
        }
    }
}
