using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
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
        private ZoneDamageMultipliers _ruleZoneMultipliers = ZoneDamageMultipliers.Default;

        // ルールの倍率と分けて持つのは、ボスの段階のような上書きを、ルールを効かせる順番 (敵の Start など) に関係なく残すため。
        private ZoneDamageMultipliers _zoneMultipliersOverride;
        private StatusEffectReceiver _statusEffects;

        public event Action<Damageable> Died;

        public event Action<Damageable, DamageReport> Damaged;

        public event Action<Health> HealthReset;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;

        // Awake が走らない Edit Mode でも使えるよう、初めて触れたときにも作る。
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
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxHealth must be positive");
                }

                _maxHealth = value;
            }
        }

        public bool IsDead => _health != null && _health.IsDead;

        // ネット対戦のダウンはサーバーが持つので、ネット対戦ではここは false のまま。
        public bool IsDowned { get; private set; }

        // 実行時にプレイヤーを組み立てる GameBootstrap が決めるので、シーンには保存しない。
        public bool IsLocalPlayer { get; set; }

        // 置いた盾 (Deployable) のように、壊れてもキルフィードや得点に載せない物があるため。組み立てる側が決めるので、シーンには保存しない。
        public bool ReportsKills { get; set; } = true;

        // シーンに保存しない。ボスの段階のように実行時に組み立てる側が持つ値で、Damageable ごとに Inspector で同じ値を書かせないため。
        public ZoneDamageMultipliers ZoneMultipliers
        {
            get => _zoneMultipliersOverride ?? _ruleZoneMultipliers;
            set => _zoneMultipliersOverride = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void ApplyRules(GameRules rules)
        {
            _ruleZoneMultipliers = (rules ?? throw new ArgumentNullException(nameof(rules))).ZoneDamage;
        }

        public void ApplySceneRules()
        {
            var rules = GameBootstrap.FindSceneRules();
            if (rules != null)
            {
                ApplyRules(rules);
            }
        }

        private void Awake()
        {
            _ = Health;
        }

        public void Initialize(string id, int maxHealth)
        {
            MaxHealth = maxHealth;
            _id = id ?? string.Empty;
            ResetHealth();
        }

        // 部位に当たるのは今は銃弾だけなので、kind の既定は Bullet にする。
        public int ApplyHit(
            int baseDamage,
            HitZone zone,
            float distance,
            DamageFalloff falloff,
            Vector3? sourcePosition = null,
            Vector3? direction = null,
            DamageKind kind = DamageKind.Bullet)
        {
            var damage = DamageCalculator.Calculate(baseDamage, zone, distance, falloff, ZoneMultipliers);
            TakeDamageCore(damage, sourcePosition, zone, direction, kind);
            return damage;
        }

        // 部位倍率と距離減衰を ImpactResolver が済ませた量を受けるため。ApplyHit に渡すと倍率が二重に掛かる。
        public int ApplyResolvedHit(int damage, HitZone zone, Vector3? sourcePosition, Vector3? direction, DamageKind kind)
        {
            TakeDamageCore(damage, sourcePosition, zone, direction, kind);
            return damage;
        }

        // 状態異常を受けない物 (壁の的など) に空の受け口を付けて回らないよう、付けるのは状態異常を当てる側が要るときだけにする。
        public StatusEffectReceiver StatusEffects => _statusEffects != null ? _statusEffects : (_statusEffects = GetComponent<StatusEffectReceiver>());

        public StatusEffectReceiver EnsureStatusEffects()
        {
            var receiver = StatusEffects;
            if (receiver == null)
            {
                receiver = gameObject.AddComponent<StatusEffectReceiver>();
                _statusEffects = receiver;
            }

            return receiver;
        }

        public void TakeDamage(int amount, Vector3? sourcePosition = null)
        {
            TakeDamageCore(amount, sourcePosition, null, null, DamageKind.Other);
        }

        public void TakeDamage(int amount, Vector3? sourcePosition, Vector3? direction, DamageKind kind)
        {
            TakeDamageCore(amount, sourcePosition, null, direction, kind);
        }

        private void TakeDamageCore(int amount, Vector3? sourcePosition, HitZone? zone, Vector3? direction, DamageKind kind)
        {
            if (!Enum.IsDefined(typeof(DamageKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind is not a defined DamageKind");
            }

            var health = Health;
            var wasAlive = !health.IsDead;
            var before = health.Current;
            health.TakeDamage(amount);
            if (!wasAlive)
            {
                return;
            }

            if (ReportsKills)
            {
                RecordForAssists(before - health.Current);
            }

            Damaged?.Invoke(this, new DamageReport(amount, sourcePosition, zone, direction, kind));

            if (IsLocalPlayer)
            {
                CombatEvents.RaiseDamageTaken(sourcePosition, amount);
            }

            if (health.IsDead && ReportsKills)
            {
                var entry = KillFeedEntryFor(zone);
                Debug.Log(KillLog.Killed(entry), this);
                CombatEvents.RaiseKilled(entry);
            }
        }

        // 渡された量ではなく減った体力を記録するのは、残り体力を超えた分までアシストの量に数えないため。
        private void RecordForAssists(int dealt)
        {
            var attribution = CombatEvents.CurrentAttribution;
            if (!attribution.HasValue || dealt <= 0)
            {
                return;
            }

            // オフラインの Damageable は陣営を持たないので、味方撃ちは KillFeedEntry と同じく false として扱う。
            KillFeedAssists.Ledger.Record(CombatEvents.OfflineId(gameObject), attribution.Value.KillerId, dealt, friendly: false, KillFeedAssists.Now);
        }

        // 帰属が無い死 (Inspector の Kill・落下など) は、KillFeedEntry の決まりに合わせて自滅として出す。
        // 自滅でも台帳から引くのは、倒れた相手の記録を消すためと、自滅の前に削った人をアシストに載せるため。
        private KillFeedEntry KillFeedEntryFor(HitZone? zone)
        {
            var victimId = CombatEvents.OfflineId(gameObject);
            var attribution = CombatEvents.CurrentAttribution;
            var killerId = attribution.HasValue ? attribution.Value.KillerId : victimId;
            var weaponId = attribution.HasValue ? attribution.Value.WeaponId : string.Empty;
            var assisterIds = KillFeedAssists.Ledger.TakeAssisters(victimId, killerId, KillFeedAssists.Now);
            return new KillFeedEntry(killerId, victimId, weaponId, zone == HitZone.Head, friendly: false, assisterIds);
        }

        [ContextMenu("Kill")]
        public void Kill()
        {
            TakeDamage(Health.Current);
        }

        public Vector3 AimPoint => TryGetBounds(out var bounds) ? bounds.center : transform.position;

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

        // 体力を変えないのは、ダウンは体力 0 で倒れた後の状態で、起き上がるときはモードが RestoreHealth で戻すため。
        public void SetDowned(bool downed)
        {
            IsDowned = downed;
        }

        public void ResetHealth()
        {
            if (_health != null)
            {
                _health.Died -= OnHealthDied;

                // 倒れずに満たし直したとき (ラウンドの切り替えなど) に、前の撃ち合いの分を次の死のアシストへ持ち越さないため。
                KillFeedAssists.Forget(gameObject);
            }

            // Inspector で 0 以下にされていても Health の例外で落ちないよう 1 に揃える。
            _health = new Health(Math.Max(1, _maxHealth));
            _health.Died += OnHealthDied;
            HealthReset?.Invoke(_health);
        }

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

    public readonly struct DamageReport
    {
        public int Amount { get; }

        public Vector3? SourcePosition { get; }

        // 部位を持たないダメージ (爆発・落下など) を Body と見分けられるよう null にする。
        public HitZone? Zone { get; }

        public Vector3? Direction { get; }

        public DamageKind Kind { get; }

        public DamageReport(int amount, Vector3? sourcePosition)
            : this(amount, sourcePosition, null, null, DamageKind.Other)
        {
        }

        // 向きは長さを揃えて持つ。受け取る側が毎回正規化しなくて済むように。長さが 0 か有限でなければ向きが無いものとして null。
        public DamageReport(int amount, Vector3? sourcePosition, HitZone? zone, Vector3? direction, DamageKind kind)
        {
            Amount = amount;
            SourcePosition = sourcePosition;
            Zone = zone;
            Direction = Normalize(direction);
            Kind = kind;
        }

        private static Vector3? Normalize(Vector3? direction)
        {
            if (!direction.HasValue)
            {
                return null;
            }

            var value = direction.Value;
            var sqr = value.sqrMagnitude;
            if (!(sqr > 1e-12f) || float.IsInfinity(sqr))
            {
                return null;
            }

            return value / Mathf.Sqrt(sqr);
        }
    }
}
