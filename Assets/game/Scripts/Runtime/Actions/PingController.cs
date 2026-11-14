using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// Ping の入力 (<see cref="PlayerActionReader.PressedCommands"/> の <see cref="PlayerCommands.Ping"/>) で、照準の先の敵か場所に印を付ける。
    /// 何に付けるか・寿命・数の上限は <see cref="PingAim"/> と <see cref="PingBoard"/> が決め、ここはシーンから照準の Ray・敵・見えているかを測る。
    /// <list type="bullet">
    /// <item>付けられるかはルール (<see cref="ActionRules.Ping"/>) で決まり、<see cref="Configure"/> (オフラインは <see cref="PlayerActions.Attach"/> が呼ぶ)
    /// で On を受け取るまでは付けない。</item>
    /// <item>敵の印は、カメラから敵が見えている間だけ敵を追い、見失うと最後の位置に残る。敵が倒れたら消す。</item>
    /// <item>印は HUD の表示 (<see cref="IPingView"/>、<see cref="PingUi"/> が作る) に渡す。</item>
    /// <item>同じ陣営 (<see cref="SideMember"/>) のオフラインの bot は、印の付いた敵を優先して狙う (<see cref="IsMarkedFor"/>)。</item>
    /// </list>
    /// 寿命・数の上限・届く距離・敵に吸い付く角度は Inspector の値から作る <see cref="Spec"/> に従う。ネット対戦では付けず、
    /// <see cref="ShowShared"/> で受け取ったサーバーの印を出すだけにする (付けるのも追うのもサーバーで、ここで付けると味方と食い違うため)。
    /// </summary>
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で読んだ押下を、同じフレームのうちに使う。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class PingController : MonoBehaviour
    {
        public const string LogTag = PingLog.Tag;

        /// <summary>HUD が見つかっていないときに探し直す間隔 (秒)。</summary>
        public const float ViewSearchIntervalSeconds = 1f;

        private static readonly List<PingController> _active = new List<PingController>();

        [SerializeField, Min(0.01f), Tooltip("付けてから消えるまでの秒数")]
        private float _lifetimeSeconds = PingSpec.DefaultLifetimeSeconds;

        [SerializeField, Min(1), Tooltip("同時に付けられる数。超えたら最も古い印を消す")]
        private int _maxMarkers = PingSpec.DefaultMaxMarkers;

        [SerializeField, Min(0.01f), Tooltip("照準の先を探す距離 (m)")]
        private float _maxDistance = PingSpec.DefaultMaxDistance;

        [SerializeField, Range(0f, 89f), Tooltip("照準が敵から外れていても敵に付ける角度 (度)")]
        private float _enemyAimToleranceDegrees = PingSpec.DefaultEnemyAimToleranceDegrees;

        private readonly Dictionary<int, Damageable> _targets = new Dictionary<int, Damageable>();
        private readonly List<PingMarker> _removed = new List<PingMarker>();
        private readonly List<int> _enemyIds = new List<int>();
        private readonly List<Damageable> _candidateTargets = new List<Damageable>();
        private readonly List<PingCandidate> _candidates = new List<PingCandidate>();

        private PingSpec _spec;
        private PingBoard _board;
        private Fps.Core.Ping _rule = Fps.Core.Ping.Off;
        private bool _configured;
        private Damageable _self;
        private ViewRig _rig;
        private PlayerActionReader _reader;
        private IPingView _view;
        private float _nextViewSearch;
        private PingRefusal _lastRefusal;
        private SharedPingMarkers _shared;
        private int _sharedVersion;

        /// <summary>印が付いた・動いた・消えたときに呼ばれる。表示が読み直す。</summary>
        public event Action<PingController> Changed;

        /// <summary>今のルールで付けられるか。<see cref="Configure"/> の前は Off。</summary>
        public Fps.Core.Ping Rule => _rule;

        public bool UsesPing => _configured && _rule == Fps.Core.Ping.On;

        /// <summary>
        /// 今の値の <see cref="PingSpec"/>。<see cref="Configure"/> の後はそのときの値で、前は Inspector の値から作る。値が不正なら ArgumentOutOfRangeException。
        /// </summary>
        public PingSpec Spec => _spec ?? (_spec = CreateSpec());

        /// <summary>今ある印。古い順。</summary>
        public IReadOnlyList<PingMarker> Markers =>
            _shared != null ? _shared.Markers
            : _board != null ? _board.Markers
            : (IReadOnlyList<PingMarker>)Array.Empty<PingMarker>();

        public bool IsShared => _shared != null;

        /// <summary>最後に付けようとしたときに付けなかった理由。付けたら <see cref="PingRefusal.None"/>。</summary>
        public PingRefusal LastRefusal => _lastRefusal;

        /// <summary>印を出している表示。無ければ null。</summary>
        public IPingView View => GameUi.IsAlive(_view) ? _view : null;

        /// <summary>印を見るカメラ。プレイヤーの視点 (<see cref="ViewRig"/>) のカメラで、無ければ null。</summary>
        public Camera ViewCamera
        {
            get
            {
                var rig = Rig;
                return rig != null ? rig.Camera : null;
            }
        }

        /// <summary>時刻 (秒) を返すもの。null なら <see cref="Time.time"/> (ポーズで時間が止まると寿命も止まる)。テストが差し込む。</summary>
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

        /// <summary>シリアライズされた値から <see cref="PingSpec"/> を作る。値が不正なら ArgumentOutOfRangeException。</summary>
        public PingSpec CreateSpec()
        {
            return new PingSpec(_lifetimeSeconds, _maxMarkers, _maxDistance, _enemyAimToleranceDegrees);
        }

        /// <summary>
        /// spec の値をシリアライズされる値に書き込む。シーン・プレハブの生成スクリプトとテストが差し込むのに使う。
        /// 付いている印には効かず、次の <see cref="Configure"/> から使う。
        /// </summary>
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
            if (!_configured)
            {
                _spec = spec;
            }
        }

        /// <summary>
        /// rules の <see cref="ActionRules.Ping"/> で付けられるかを決め、Inspector の値で設定を作り直す。付いていた印はすべて消す。
        /// 何度呼んでも、最後に渡した rules に従う。Inspector の値が不正 (<see cref="CreateSpec"/>) なら何も変えずに ArgumentOutOfRangeException。
        /// </summary>
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
            _nextViewSearch = 0f;
            Register();

            if (cleared > 0)
            {
                Debug.Log(PingLog.Cleared(cleared), this);
            }

            Debug.Log(PingLog.Rule(_rule, spec), this);
            Changed?.Invoke(this);
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
            _rule = Fps.Core.Ping.On;
            _configured = true;
            _lastRefusal = PingRefusal.None;
            _self = GetComponent<Damageable>();
            _nextViewSearch = 0f;

            if (cleared > 0)
            {
                Debug.Log(PingLog.Cleared(cleared), this);
            }

            Debug.Log(PingLog.Shared(), this);
            Changed?.Invoke(this);
        }

        /// <summary>
        /// 照準 (<see cref="ViewRig.ResolveAim"/>) の先に印を付ける。付けたら true と付けた印を返し、<c>[Ping] placed</c> をログに出す。
        /// 付けなければ false を返し、理由 (<see cref="LastRefusal"/>) を <c>[Ping] refused</c> でログに出す。
        /// </summary>
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

        /// <summary>
        /// origin から direction への照準の先に印を付ける。何に付けるかは <see cref="PingAim.Resolve"/> で決める。
        /// 返り値とログは <see cref="TryPing"/> と同じ。
        /// </summary>
        /// <exception cref="ArgumentException">origin が有限でないか、direction が 0 か有限でない。</exception>
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
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>
        /// now の時点で、敵の印を追い (見えている間は動かし、見えなくなったら <c>[Ping] lost</c>)、倒れた敵の印と寿命の尽きた印 (<c>[Ping] expired</c>) を消す。
        /// Play 中は LateUpdate が <see cref="Time.time"/> (<see cref="Clock"/>) で呼ぶ。
        /// </summary>
        public void Tick(float now)
        {
            if (_shared != null)
            {
                if (_shared.Version != _sharedVersion)
                {
                    _sharedVersion = _shared.Version;
                    Changed?.Invoke(this);
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
                var visible = alive && tracking && Sees(target);
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
                Changed?.Invoke(this);
            }
        }

        /// <summary>印をすべて消す。消したら <c>[Ping] cleared</c> をログに出す。</summary>
        public void ClearMarkers()
        {
            if (_shared != null)
            {
                var sharedCount = _shared.Clear();
                if (sharedCount > 0)
                {
                    _sharedVersion = _shared.Version;
                    Debug.Log(PingLog.Cleared(sharedCount), this);
                    Changed?.Invoke(this);
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
            Changed?.Invoke(this);
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

        /// <summary>敵の印 id が付いている敵。場所の印か、無ければ null。</summary>
        public Damageable TargetOf(int id)
        {
            return _targets.TryGetValue(id, out var target) && target != null ? target : null;
        }

        /// <summary>target に印を付けているか (見失った後も、消えるまでは付いている)。</summary>
        public bool IsMarked(Damageable target)
        {
            return target != null && _board != null && _board.IsMarked(KeyOf(target));
        }

        /// <summary>付ける人の陣営。<see cref="SideMember"/> が無ければ null (味方の bot はいない)。</summary>
        public Side? PingerSide
        {
            get
            {
                var member = SideMember.Of(gameObject);
                return member != null ? member.Side : (Side?)null;
            }
        }

        /// <summary>
        /// side の陣営の誰かが、ルールで付けられる状態で target に印を付けているか。オフラインの bot が狙う敵を選ぶとき
        /// (<see cref="EnemySenses.SelectHostile"/>) に見る。
        /// </summary>
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

        /// <summary>印を view に出す。前の表示は手放す。null なら出すのをやめ、HUD から探し直す。</summary>
        public void BindView(IPingView view)
        {
            if (ReferenceEquals(view, _view))
            {
                return;
            }

            if (GameUi.IsAlive(_view))
            {
                _view.Show(null);
            }

            _view = view;
            if (view != null)
            {
                view.Show(this);
                Debug.Log(PingLog.View(view.GetType().Name), this);
            }
            else
            {
                _nextViewSearch = 0f;
            }
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
            if (GameUi.IsAlive(_view))
            {
                _view.Show(null);
            }

            _view = null;
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
            EnsureView();
        }

        private void EnsureView()
        {
            if (!UsesPing || GameUi.IsAlive(_view) || Time.unscaledTime < _nextViewSearch)
            {
                return;
            }

            _nextViewSearch = Time.unscaledTime + ViewSearchIntervalSeconds;
            var view = PingUi.CreateView(GameUi.FindHud());
            if (view != null)
            {
                BindView(view);
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

        private static int KeyOf(Damageable target) => target.GetInstanceID();

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
