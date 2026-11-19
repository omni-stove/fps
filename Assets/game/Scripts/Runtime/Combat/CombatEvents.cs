using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 撃つ側・撃たれる側は購読者を知らずに Raise 系を呼ぶだけにし、演出の有無で戦闘の結果が変わらないようにする。
    // 購読者が例外を投げても戦闘の処理を止めないよう、例外はログに出して残りの購読者を呼ぶ。
    // 静的なので、ドメインの再読み込みを切った Play でも前回の購読が残らないよう、Play の開始時に購読をすべて外す。
    public static class CombatEvents
    {
        // 入れ子の Attribute を戻せるよう、今の帰属を 1 つだけ持ち、スコープが前の値を覚える。
        private static ShotAttribution? _attribution;

        // EntityId は int に変換できない (Unity 6.6 で変換が error) ので、初めて見た EntityId に連番を振る。
        private static readonly Dictionary<EntityId, int> _offlineIds = new Dictionary<EntityId, int>();
        private static readonly Dictionary<int, GameObject> _offlineObjects = new Dictionary<int, GameObject>();

        public static event Action<ShotFiredInfo> ShotFired;

        // 跳ねた弾の続きを ShotFired で出すと、銃声・マズルフラッシュ・薬莢・反動まで跳ねた所でもう一度出てしまうので分ける。
        public static event Action<ShotFiredInfo> ShotContinued;

        public static event Action<ImpactInfo> Impact;

        public static event Action<HitConfirm> HitConfirmed;

        public static event Action<DamageTakenInfo> DamageTaken;

        public static event Action<KillFeedEntry> Killed;

        public static event Action<ExplosionInfo> Exploded;

        public static event Action<LandingInfo> Landed;

        // 戦闘の結果 (体力・判断) は BossController が先に変え終えてから流す。演出の有無で戦いが変わらないように。
        public static event Action<BossPhaseChangedInfo> BossPhaseChanged;

        public static event Action<MatchResult> MatchFinished;

        public static event Action<AircraftPartBrokenInfo> AircraftPartBroken;

        public static ShotAttribution? CurrentAttribution => _attribution;

        // ルートを辿らないのは、敵や標的が 1 つの親の下にまとめて置かれ、ルートでは全員が同じ Id になるため。
        public static int OfflineId(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return 0;
            }

            var entityId = gameObject.GetEntityId();
            if (!_offlineIds.TryGetValue(entityId, out var id))
            {
                id = _offlineIds.Count + 1;
                _offlineIds.Add(entityId, id);
                _offlineObjects.Add(id, gameObject);
            }

            return id;
        }

        public static GameObject OfflineObject(int id)
        {
            return _offlineObjects.TryGetValue(id, out var gameObject) && gameObject != null ? gameObject : null;
        }

        public static AttributionScope Attribute(GameObject killer, string weaponId)
        {
            var previous = _attribution;
            _attribution = killer != null ? new ShotAttribution(OfflineId(killer), weaponId) : default(ShotAttribution?);
            return new AttributionScope(previous);
        }

        // 倒れるのが命中より後になるもの (崩れ落ちる破片など) が、壊した時の帰属を持ち越せるよう、前に読んだ帰属を受け取る形も用意する。
        public static AttributionScope Attribute(ShotAttribution? attribution)
        {
            var previous = _attribution;
            _attribution = attribution;
            return new AttributionScope(previous);
        }

        public static void RaiseShotFired(GameObject shooter, Vector3 origin, Vector3 direction, string weaponId, bool tracer = true)
        {
            Invoke(ShotFired, new ShotFiredInfo(shooter, origin, direction, weaponId, tracer));
        }

        public static void RaiseShotContinued(GameObject shooter, Vector3 origin, Vector3 direction, string weaponId)
        {
            Invoke(ShotContinued, new ShotFiredInfo(shooter, origin, direction, weaponId));
        }

        public static void RaiseImpact(Vector3 point, Vector3 normal, Collider collider)
        {
            Invoke(Impact, new ImpactInfo(point, normal, collider));
        }

        public static void RaiseImpact(ImpactInfo impact)
        {
            Invoke(Impact, impact);
        }

        public static void RaiseHitConfirmed(HitConfirm confirm)
        {
            Invoke(HitConfirmed, confirm);
        }

        public static void RaiseDamageTaken(Vector3? sourcePosition, int amount)
        {
            Invoke(DamageTaken, new DamageTakenInfo(sourcePosition, amount));
        }

        public static void RaiseKilled(KillFeedEntry entry)
        {
            Invoke(Killed, entry);
        }

        public static void RaiseExploded(Vector3 center, string weaponId)
        {
            Invoke(Exploded, new ExplosionInfo(center, weaponId));
        }

        public static void RaiseLanded(GameObject body, float fallSpeed)
        {
            Invoke(Landed, new LandingInfo(body, fallSpeed));
        }

        public static void RaiseBossPhaseChanged(BossPhaseChangedInfo info)
        {
            Invoke(BossPhaseChanged, info);
        }

        public static void RaiseMatchFinished(MatchResult result)
        {
            Invoke(MatchFinished, result);
        }

        public static void RaiseAircraftPartBroken(AircraftPartBrokenInfo info)
        {
            Invoke(AircraftPartBroken, info);
        }

        public static void ClearSubscribers()
        {
            ShotFired = null;
            ShotContinued = null;
            Impact = null;
            HitConfirmed = null;
            DamageTaken = null;
            Killed = null;
            Exploded = null;
            Landed = null;
            BossPhaseChanged = null;
            MatchFinished = null;
            AircraftPartBroken = null;
            _attribution = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ClearSubscribers();
            _offlineIds.Clear();
            _offlineObjects.Clear();
        }

        private static void Invoke<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public readonly struct AttributionScope : IDisposable
        {
            private readonly ShotAttribution? _previous;

            internal AttributionScope(ShotAttribution? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                _attribution = _previous;
            }
        }
    }

    public readonly struct ShotAttribution
    {
        // default では null になるので、読むときに空文字へ寄せる。
        private readonly string _weaponId;

        public int KillerId { get; }

        public string WeaponId => _weaponId ?? string.Empty;

        public ShotAttribution(int killerId, string weaponId)
        {
            KillerId = killerId;
            _weaponId = weaponId ?? string.Empty;
        }
    }

    public readonly struct ShotFiredInfo
    {
        private readonly string _weaponId;

        public GameObject Shooter { get; }

        public Vector3 Origin { get; }

        public Vector3 Direction { get; }

        public string WeaponId => _weaponId ?? string.Empty;

        // 飛ぶ弾は弾そのものが見えるので、まっすぐな曳光弾を重ねて出さないよう撃つ側が外せるようにする。
        public bool Tracer { get; }

        public ShotFiredInfo(GameObject shooter, Vector3 origin, Vector3 direction, string weaponId, bool tracer = true)
        {
            Shooter = shooter;
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : direction;
            _weaponId = weaponId ?? string.Empty;
            Tracer = tracer;
        }
    }

    public readonly struct ImpactInfo
    {
        // default では null になるので、読むときに既定の材質へ寄せる。
        private readonly string _materialId;
        private readonly HitZone _zone;
        private readonly bool _hasZone;

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public Collider Collider { get; }

        public string MaterialId => _materialId ?? SurfaceMaterial.DefaultId;

        public bool IsCharacter { get; }

        public HitZone? Zone => _hasZone ? _zone : (HitZone?)null;

        public ImpactInfo(Vector3 point, Vector3 normal, Collider collider)
            : this(point, normal, collider, SurfaceMaterial.IdOf(collider), IsCharacterCollider(collider, out var zone), zone)
        {
        }

        public ImpactInfo(Vector3 point, Vector3 normal, Collider collider, string materialId, bool isCharacter, HitZone? zone)
        {
            if (zone.HasValue)
            {
                if (!isCharacter)
                {
                    throw new ArgumentException("only a character impact can have a hit zone", nameof(zone));
                }

                if (!Enum.IsDefined(typeof(HitZone), zone.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(zone), zone.Value, "unknown hit zone");
                }
            }

            Point = point;
            Normal = normal;
            Collider = collider;
            _materialId = SurfaceMaterial.Normalize(materialId);
            IsCharacter = isCharacter;
            _zone = zone ?? default;
            _hasZone = zone.HasValue;
        }

        public override string ToString()
        {
            return "material=" + MaterialId + " character=" + IsCharacter + " zone=" + (Zone.HasValue ? Zone.Value.ToString() : "none");
        }

        private static bool IsCharacterCollider(Collider collider, out HitZone? zone)
        {
            zone = null;
            if (collider == null)
            {
                return false;
            }

            var hitbox = collider.GetComponentInParent<Hitbox>(true);
            if (hitbox != null)
            {
                zone = hitbox.Zone;
                return true;
            }

            return collider.GetComponentInParent<Damageable>(true) != null;
        }
    }

    public readonly struct BossPhaseChangedInfo
    {
        public BossPhaseChangedInfo(GameObject boss, Damageable damageable, string bossId, BossPhaseChange change)
        {
            Boss = boss;
            Damageable = damageable;
            BossId = bossId ?? string.Empty;
            PreviousIndex = change.PreviousIndex;
            Index = change.Index;
            PreviousPhaseId = change.Previous != null ? change.Previous.Id : string.Empty;
            PhaseId = change.Current != null ? change.Current.Id : string.Empty;
            HealthFraction = change.HealthFraction;
        }

        public GameObject Boss { get; }

        // HUD の体力バーが Health を読むのに使う。Boss から引き直させないよう一緒に渡す。
        public Damageable Damageable { get; }

        public string BossId { get; }

        public int PreviousIndex { get; }

        public int Index { get; }

        public string PreviousPhaseId { get; }

        public string PhaseId { get; }

        public float HealthFraction { get; }

        // 復活などで最初の段階へ戻ったとき。演出が「段階が進んだ」と取り違えないよう見分けられるようにする。
        public bool IsReset => Index < PreviousIndex;
    }

    public readonly struct AircraftPartBrokenInfo
    {
        private readonly string _partId;

        public AircraftPartBrokenInfo(GameObject aircraft, string partId, AircraftDamageEffect effect, Vector3 position, AircraftCondition condition)
        {
            Aircraft = aircraft;
            _partId = partId ?? string.Empty;
            Effect = effect;
            Position = position;
            Condition = condition;
        }

        public GameObject Aircraft { get; }

        public string PartId => _partId ?? string.Empty;

        public AircraftDamageEffect Effect { get; }

        // 演出 (煙・火花) を壊れた部位から出せるよう、命中した部位の位置を渡す。
        public Vector3 Position { get; }

        public AircraftCondition Condition { get; }
    }

    public readonly struct DamageTakenInfo
    {
        public Vector3? SourcePosition { get; }

        public int Amount { get; }

        public DamageTakenInfo(Vector3? sourcePosition, int amount)
        {
            SourcePosition = sourcePosition;
            Amount = amount;
        }
    }

    public readonly struct ExplosionInfo
    {
        private readonly string _weaponId;

        public Vector3 Center { get; }

        public string WeaponId => _weaponId ?? string.Empty;

        public ExplosionInfo(Vector3 center, string weaponId)
        {
            Center = center;
            _weaponId = weaponId ?? string.Empty;
        }
    }

    public readonly struct LandingInfo
    {
        public GameObject Body { get; }

        public float FallSpeed { get; }

        // 呼ぶのは移動の処理の途中なので、演出のための値が壊れていても例外で移動を止めず、着地の強さ 0 として流す。
        public LandingInfo(GameObject body, float fallSpeed)
        {
            Body = body;
            FallSpeed = fallSpeed >= 0f && !float.IsInfinity(fallSpeed) ? fallSpeed : 0f;
        }
    }
}
