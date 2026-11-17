using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // ネット対戦では自分で付けず、ShowShared で受け取ったサーバーの印を出すだけにする。付けるのも追うのもサーバーで、ここで付けると味方と食い違うため。
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で読んだ押下を、同じフレームのうちに使う。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class PingController : MonoBehaviour
    {
        public const string LogTag = PingLog.Tag;

        private static readonly List<PingController> _active = new List<PingController>();

        [SerializeField, Min(0.01f), Tooltip("付けてから消えるまでの秒数")]
        private float _lifetimeSeconds = PingSpec.DefaultLifetimeSeconds;

        [SerializeField, Min(1), Tooltip("同時に付けられる数。超えたら最も古い印を消す")]
        private int _maxMarkers = PingSpec.DefaultMaxMarkers;

        [SerializeField, Min(0.01f), Tooltip("照準の先を探す距離 (m)")]
        private float _maxDistance = PingSpec.DefaultMaxDistance;

        [SerializeField, Range(0f, 89f), Tooltip("照準が敵から外れていても敵に付ける角度 (度)")]
        private float _enemyAimToleranceDegrees = PingSpec.DefaultEnemyAimToleranceDegrees;

        [SerializeField, Min(0f), Tooltip("続けて付けるときに空ける秒数")]
        private float _minIntervalSeconds = PingSpec.DefaultMinIntervalSeconds;

        private readonly Dictionary<int, Damageable> _targets = new Dictionary<int, Damageable>();
        private readonly List<PingMarker> _removed = new List<PingMarker>();
        private readonly List<int> _enemyIds = new List<int>();
        private readonly List<Damageable> _candidateTargets = new List<Damageable>();
        private readonly List<PingCandidate> _candidates = new List<PingCandidate>();

        private PingSpec _spec;
        private bool _specApplied;
        private PingBoard _board;
        private Shooter.Core.Ping _rule = Shooter.Core.Ping.Off;
        private bool _configured;
        private Damageable _self;
        private ViewRig _rig;
        private PlayerActionReader _reader;
        private IReadOnlyList<PingMarker> _published;
        private PingRefusal _lastRefusal;
        private SharedPingMarkers _shared;
        private int _sharedVersion;

        public event Action<PingController> Changed;

        public Shooter.Core.Ping Rule => _rule;

        public bool UsesPing => _configured && _rule == Shooter.Core.Ping.On;

        public PingSpec Spec => _spec ?? (_spec = CreateSpec());

        public IReadOnlyList<PingMarker> Markers =>
            _shared != null ? _shared.Markers
            : _board != null ? _board.Markers
            : (IReadOnlyList<PingMarker>)Array.Empty<PingMarker>();

        public bool IsShared => _shared != null;

        public PingRefusal LastRefusal => _lastRefusal;

        public Camera ViewCamera
        {
            get
            {
                var rig = Rig;
                return rig != null ? rig.Camera : null;
            }
        }

        // 既定を Time.time にするのは、ポーズで時間が止まると寿命も止めるため。テストが時刻を差し込めるよう差し替えられるようにする。
        public Func<float> Clock { get; set; }

        private float Now => Clock != null ? Clock() : Time.time;

        private ViewRig Rig
        {
            get
            {
                if (_rig == null)
                {
                    _rig = GetComponent<ViewRig>();
                }

                return _rig;
            }
        }

        public PingSpec CreateSpec()
        {
            return new PingSpec(_lifetimeSeconds, _maxMarkers, _maxDistance, _enemyAimToleranceDegrees, _minIntervalSeconds);
        }

        public void ApplySpec(PingSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _lifetimeSeconds = spec.LifetimeSeconds;
            _maxMarkers = spec.MaxMarkers;
            _maxDistance = spec.MaxDistance;
            _enemyAimToleranceDegrees = spec.EnemyAimToleranceDegrees;
            _minIntervalSeconds = spec.MinIntervalSeconds;
            _specApplied = true;
            if (!_configured)
            {
                _spec = spec;
            }
        }

        private void Awake()
        {
            // 無効なまま置かれた本体は Awake が有効になるまで遅れるので、その間に Play 中のコードが ApplySpec で渡した値を JSON で消さない。
            if (Application.isPlaying && !_specApplied)
            {
                ApplySpec(KitTuning.Ping);
            }
        }

        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // 不正な値に付けるまで気づかないことがないよう、状態を変える前に作って確かめる。
            var spec = CreateSpec();
            var cleared = _board != null ? _board.Clear(null) : 0;
            _targets.Clear();
            _shared = null;
            _spec = spec;
            _board = new PingBoard(spec);
            _rule = rules.Actions.Ping;
            _configured = true;
            _lastRefusal = PingRefusal.None;
            _self = GetComponent<Damageable>();
            _reader = GetComponent<PlayerActionReader>();
            Register();

            if (cleared > 0)
            {
                Debug.Log(PingLog.Cleared(cleared), this);
            }

            Debug.Log(PingLog.Rule(_rule, spec), this);
            NotifyChanged();
        }

        // 中身を写さずに参照で持つ。印はネットワークの知らせのたびに変わり、表示は Version の変化を見て読み直せば足りるため。
        public void ShowShared(SharedPingMarkers markers)
        {
            if (markers == null)
            {
                throw new ArgumentNullException(nameof(markers));
            }

            var cleared = _board != null ? _board.Clear(null) : 0;
            _targets.Clear();
            _board = null;
            _shared = markers;
            _sharedVersion = markers.Version;
            _rule = Shooter.Core.Ping.On;
            _configured = true;
            _lastRefusal = PingRefusal.None;
            _self = GetComponent<Damageable>();

            if (cleared > 0)
            {
                Debug.Log(PingLog.Cleared(cleared), this);
            }

            Debug.Log(PingLog.Shared(), this);
            NotifyChanged();
        }

        public bool TryPing(out PingMarker marker)
        {
            marker = default;
            if (!CanPing())
            {
                return false;
            }

            var rig = Rig;
            if (rig == null || !rig.IsInitialized)
            {
                return Refuse(PingRefusal.NoView);
            }

            rig.ResolveAim(Spec.MaxDistance, out var origin, out var direction);
            return TryPingAlong(origin, direction, out marker);
        }

        public bool TryPingAlong(Vector3 origin, Vector3 direction, out PingMarker marker)
        {
            marker = default;
            if (!IsFinite(origin) || !IsFinite(direction) || direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException($"the aim must be finite and point somewhere (origin={origin}, direction={direction})");
            }

            if (!CanPing())
            {
                return false;
            }

            var spec = Spec;
            System.Numerics.Vector3? surface = null;
            Damageable surfaceTarget = null;
            if (Hitscan.TryFirstHit(origin, direction, spec.MaxDistance, transform, out var hit))
            {
                surface = hit.point.ToNumerics();
                surfaceTarget = OwnerOf(hit.collider);
            }

            CollectCandidates(origin, spec.MaxDistance);
            var surfaceIndex = surfaceTarget != null ? _candidateTargets.IndexOf(surfaceTarget) : -1;
            var result = PingAim.Resolve(spec, origin.ToNumerics(), direction.ToNumerics(), surface, surfaceIndex, _candidates);
            if (!result.HasTarget)
            {
                ClearCandidates();
                return Refuse(PingRefusal.NoTarget);
            }

            var target = result.Kind == PingKind.Enemy ? _candidateTargets[result.CandidateIndex] : null;
            ClearCandidates();

            var placement = _board.Place(result, target != null ? KeyOf(target) : (int?)null, Now);
            if (placement.Replaced.HasValue)
            {
                _targets.Remove(placement.Replaced.Value.Id);
            }

            if (target != null)
            {
                _targets[placement.Marker.Id] = target;
            }

            _lastRefusal = PingRefusal.None;
            marker = placement.Marker;
            Debug.Log(PingLog.Placed(placement, target != null ? target.Id : null), this);
            NotifyChanged();
            return true;
        }

        public bool TryMarkEnemy(Damageable target, float lifetimeSeconds, out PingMarker marker)
        {
            marker = default;
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (float.IsNaN(lifetimeSeconds) || float.IsInfinity(lifetimeSeconds) || lifetimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), lifetimeSeconds, "lifetimeSeconds must be positive and finite");
            }

            if (!CanPing())
            {
                return false;
            }

            if (target.IsDead)
            {
                return Refuse(PingRefusal.NoTarget);
            }

            var placement = _board.Reveal(KeyOf(target), target.AimPoint.ToNumerics(), Now, lifetimeSeconds);
            _targets[placement.Marker.Id] = target;
            _lastRefusal = PingRefusal.None;
            marker = placement.Marker;
            Debug.Log(PingLog.Revealed(placement, target.Id), this);
            NotifyChanged();
            return true;
        }

        public void Tick(float now)
        {
            if (_shared != null)
            {
                if (_shared.Version != _sharedVersion)
                {
                    _sharedVersion = _shared.Version;
                    NotifyChanged();
                }

                return;
            }

            if (_board == null || _board.Count == 0)
            {
                return;
            }

            var changed = false;
            _enemyIds.Clear();
            foreach (var marker in _board.Markers)
            {
                if (marker.Kind == PingKind.Enemy)
                {
                    _enemyIds.Add(marker.Id);
                }
            }

            foreach (var id in _enemyIds)
            {
                _targets.TryGetValue(id, out var target);
                var alive = target != null && !target.IsDead;
                var tracking = _board.TryGet(id, out var current) && current.IsTracking;
                var visible = alive && tracking && (_board.IsRevealed(id) || Sees(target));
                var position = visible ? target.AimPoint.ToNumerics() : System.Numerics.Vector3.Zero;
                switch (_board.Track(id, alive, visible, position, out var tracked))
                {
                    case PingTrackResult.Moved:
                        changed = true;
                        break;
                    case PingTrackResult.Lost:
                        Debug.Log(PingLog.Lost(tracked, target != null ? target.Id : null), this);
                        changed = true;
                        break;
                    case PingTrackResult.Gone:
                        Debug.Log(PingLog.TargetGone(tracked, target != null ? target.Id : null), this);
                        _targets.Remove(id);
                        changed = true;
                        break;
                }
            }

            _removed.Clear();
            if (_board.Expire(now, _removed) > 0)
            {
                foreach (var expired in _removed)
                {
                    _targets.Remove(expired.Id);
                    Debug.Log(PingLog.Expired(expired), this);
                }

                _removed.Clear();
                changed = true;
            }

            if (changed)
            {
                NotifyChanged();
            }
        }

        public void ClearMarkers()
        {
            if (_shared != null)
            {
                var sharedCount = _shared.Clear();
                if (sharedCount > 0)
                {
                    _sharedVersion = _shared.Version;
                    Debug.Log(PingLog.Cleared(sharedCount), this);
                    NotifyChanged();
                }

                return;
            }

            if (_board == null || _board.Count == 0)
            {
                return;
            }

            var count = _board.Clear(null);
            _targets.Clear();
            Debug.Log(PingLog.Cleared(count), this);
            NotifyChanged();
        }

        public bool TryGetMarker(int id, out PingMarker marker)
        {
            if (_shared != null)
            {
                return _shared.TryGet(id, out marker);
            }

            if (_board == null)
            {
                marker = default;
                return false;
            }

            return _board.TryGet(id, out marker);
        }

        public Damageable TargetOf(int id)
        {
            return _targets.TryGetValue(id, out var target) && target != null ? target : null;
        }

        public bool IsMarked(Damageable target)
        {
            return target != null && _board != null && _board.IsMarked(KeyOf(target));
        }

        public Side? PingerSide
        {
            get
            {
                var member = SideMember.Of(gameObject);
                return member != null ? member.Side : (Side?)null;
            }
        }

        public static bool IsMarkedFor(Side side, Damageable target)
        {
            if (target == null)
            {
                return false;
            }

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var controller = _active[i];
                if (controller == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                if (!controller.enabled
                    || !controller.gameObject.activeInHierarchy
                    || !controller.UsesPing
                    || !controller.IsMarked(target))
                {
                    continue;
                }

                var pinger = controller.PingerSide;
                if (pinger.HasValue && pinger.Value == side)
                {
                    return true;
                }
            }

            return false;
        }

        // 一覧から外すのは破棄のときだけにし、無効の間は IsMarkedFor が飛ばす。Edit Mode では OnEnable が呼ばれないので Configure でも入れる。
        private void Register()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDestroy()
        {
            _active.Remove(this);
            Unpublish();
        }

        private void LateUpdate()
        {
            if (!_configured)
            {
                return;
            }

            if (_reader == null && _shared == null)
            {
                _reader = GetComponent<PlayerActionReader>();
            }

            // ポーズ画面のボタンを押したつもりで印が付かないよう、開いている間は読まない。
            // サーバーの印を出しているときは、押すたびに断りのログを出さないよう読まない (入力はネットの側が送る)。
            if (_shared == null
                && _reader != null
                && _reader.isActiveAndEnabled
                && (_reader.PressedCommands & PlayerCommands.Ping) != 0
                && !GameUi.IsPauseOpen)
            {
                TryPing(out _);
            }

            Tick(Now);
        }

        // 印の並びは中身が変わる同じ参照なので、変わるたびに出し直して UI に知らせる。
        private void NotifyChanged()
        {
            if (UsesPing)
            {
                _published = Markers;
                GameSignals.Pings.Publish(_published);
            }
            else
            {
                Unpublish();
            }

            Changed?.Invoke(this);
        }

        // 後から別のプレイヤーが出した印までは消さない。
        private void Unpublish()
        {
            if (_published != null)
            {
                GameSignals.Pings.ClearIfCurrent(_published);
                _published = null;
            }
        }

        private bool CanPing()
        {
            if (_shared != null)
            {
                return Refuse(PingRefusal.Remote);
            }

            if (!UsesPing)
            {
                return Refuse(PingRefusal.RuleOff);
            }

            if (_self != null && _self.IsDead)
            {
                return Refuse(PingRefusal.Dead);
            }

            return true;
        }

        private bool Refuse(PingRefusal reason)
        {
            _lastRefusal = reason;
            Debug.Log(PingLog.Refused(reason), this);
            return false;
        }

        // 遮るものの判定はカメラから行い、画面に映っているかと揃える。カメラが無ければ体の位置から見る。
        private bool Sees(Damageable target)
        {
            var camera = ViewCamera;
            var eye = camera != null ? camera.transform.position : transform.position;
            return Hitscan.HasLineOfSight(eye, target, Spec.MaxDistance, transform);
        }

        private void CollectCandidates(Vector3 origin, float maxDistance)
        {
            ClearCandidates();
            foreach (var damageable in FindObjectsByType<Damageable>(FindObjectsInactive.Exclude))
            {
                if (!IsHostile(damageable) || damageable.IsDead)
                {
                    continue;
                }

                var position = damageable.AimPoint;
                if (Vector3.Distance(origin, position) > maxDistance)
                {
                    continue;
                }

                _candidateTargets.Add(damageable);
                _candidates.Add(new PingCandidate(
                    position.ToNumerics(), true, Hitscan.HasLineOfSight(origin, damageable, maxDistance, transform)));
            }
        }

        private void ClearCandidates()
        {
            _candidateTargets.Clear();
            _candidates.Clear();
        }

        // 自分と、陣営が分かっていて同じ味方には敵の印を付けない (当たればその場所に付く)。
        private bool IsHostile(Damageable damageable)
        {
            if (damageable == null || damageable == _self || damageable.transform.IsChildOf(transform))
            {
                return false;
            }

            // 置いた盾は SideMember を持たないので、弾を通す相手 (持ち主・同じ陣営) のものは味方として外す。
            var deployable = damageable.GetComponent<Deployable>();
            if (deployable != null && deployable.LetsThrough(gameObject))
            {
                return false;
            }

            var mine = SideMember.Of(gameObject);
            var theirs = SideMember.Of(damageable.gameObject);
            return mine == null || theirs == null || !mine.IsAllyOf(theirs);
        }

        private static Damageable OwnerOf(Collider collider)
        {
            var hitbox = Hitbox.FindFor(collider);
            if (hitbox != null && hitbox.Owner != null)
            {
                return hitbox.Owner;
            }

            return collider.GetComponentInParent<Damageable>();
        }

        private static int KeyOf(Damageable target) => CombatEvents.OfflineId(target.gameObject);

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        // ドメインの再読み込みを切った Play でも、前の Play の破棄済みのものを残さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive()
        {
            _active.Clear();
        }
    }
}
