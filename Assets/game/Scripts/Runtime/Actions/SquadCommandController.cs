using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// SquadCommand の入力 (<see cref="PlayerActionReader.HeldCommands"/> の <see cref="PlayerCommands.SquadCommand"/>) で、味方の bot (<see cref="EnemyAgent"/>) に指示を出す。
    /// <list type="bullet">
    /// <item>短く押して離すと、照準の先に応じて指示を出す。敵を指していればその敵を狙わせ (<see cref="EnemyAgent.OrderAttack"/>)、
    /// 地面や物を指していればそこへ行って守らせる (<see cref="EnemyAgent.OrderHold"/>)。何を指したかは印 (Ping) と同じく <see cref="PingAim.Resolve"/> で決める。</item>
    /// <item><see cref="SquadOrderSpec.HoldSeconds"/> 押し続けると、指示を取り消して付いてこさせる (<see cref="EnemyAgent.OrderFollow"/>)。</item>
    /// <item>指示は、自分と同じ陣営 (<see cref="SideMember"/>) で生きている bot に出す。分隊のあるモード (<see cref="ModeRunnerBase.HasSquads"/>) では
    /// 離れていても分隊の全員に、そうでなければ <see cref="SquadOrderSpec.NearbyRadius"/> 以内の bot にだけ出す (<see cref="SquadOrderRecipients"/>)。</item>
    /// <item>出したら <c>[Squad] order</c>、出さなかったら理由を <c>[Squad] refused</c> でログに出す。</item>
    /// <item>狙う・守る指示の印は HUD の表示 (<see cref="ISquadOrderView"/>、<see cref="SquadOrderUi"/> が作る) に渡す。
    /// 狙う敵が倒れるか、指示を受けた bot が誰もその指示を続けていなければ印を消す。</item>
    /// </list>
    /// 値は Inspector の値から作る <see cref="Spec"/> に従う。オフラインの bot だけが相手で、ネット対戦の bot には出さない。
    /// </summary>
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で読んだ押下を、同じフレームのうちに使う。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class SquadCommandController : MonoBehaviour
    {
        public const string LogTag = SquadOrderLog.Tag;

        /// <summary>HUD が見つかっていないときに探し直す間隔 (秒)。</summary>
        public const float ViewSearchIntervalSeconds = 1f;

        [SerializeField, Min(0.01f), Tooltip("押し続けてこれだけ経ったら、指示を取り消して付いてこさせる秒数")]
        private float _holdSeconds = SquadOrderSpec.DefaultHoldSeconds;

        [SerializeField, Min(0.01f), Tooltip("照準の先を探す距離 (m)")]
        private float _maxDistance = SquadOrderSpec.DefaultMaxDistance;

        [SerializeField, Range(0f, 89f), Tooltip("照準が敵から外れていても敵を指したとみなす角度 (度)")]
        private float _enemyAimToleranceDegrees = SquadOrderSpec.DefaultEnemyAimToleranceDegrees;

        [SerializeField, Min(0.01f), Tooltip("分隊の無いモードで、指示が届く同じ陣営の bot までの距離 (m)")]
        private float _nearbyRadius = SquadOrderSpec.DefaultNearbyRadius;

        [SerializeField, Min(0.01f), Tooltip("付いてくる bot が、これより離れたら寄ってくる距離 (m)")]
        private float _followDistance = SquadOrderSpec.DefaultFollowDistance;

        [SerializeField, Min(0.01f), Tooltip("付いてくる bot が、撃ち合っていてもこれより離れたら戻ってくる距離 (m)")]
        private float _leashDistance = SquadOrderSpec.DefaultLeashDistance;

        private readonly List<EnemyAgent> _bots = new List<EnemyAgent>();
        private readonly List<SquadOrderCandidate> _botCandidates = new List<SquadOrderCandidate>();
        private readonly List<int> _recipientIndices = new List<int>();
        private readonly List<EnemyAgent> _recipients = new List<EnemyAgent>();
        private readonly List<Damageable> _candidateTargets = new List<Damageable>();
        private readonly List<PingCandidate> _candidates = new List<PingCandidate>();

        private SquadOrderSpec _spec;
        private SquadOrderButton _button;
        private bool _configured;
        private Damageable _self;
        private ViewRig _rig;
        private PlayerActionReader _reader;
        private ISquadOrderView _view;
        private float _nextViewSearch;
        private SquadOrderRefusal _lastRefusal;
        private BotErrandKind _currentKind = BotErrandKind.None;
        private Vector3 _currentPoint;
        private Damageable _currentTarget;

        /// <summary>指示の印が出た・動いた・消えたときに呼ばれる。表示が読み直す。</summary>
        public event Action<SquadCommandController> Changed;

        public bool IsConfigured => _configured;

        /// <summary>
        /// 今の値の <see cref="SquadOrderSpec"/>。<see cref="Configure"/> の後はそのときの値で、前は Inspector の値から作る。値が不正なら ArgumentOutOfRangeException。
        /// </summary>
        public SquadOrderSpec Spec => _spec ?? (_spec = CreateSpec());

        /// <summary>最後に指示を出そうとしたときに出さなかった理由。出したら <see cref="SquadOrderRefusal.None"/>。</summary>
        public SquadOrderRefusal LastRefusal => _lastRefusal;

        /// <summary>HUD に印を出している指示の種類 (<see cref="BotErrandKind.Hold"/> か <see cref="BotErrandKind.Attack"/>)。出していなければ None。</summary>
        public BotErrandKind CurrentOrder => _currentKind;

        /// <summary>印の位置。守る指示なら守る場所、狙う指示なら狙う敵の狙う点 (敵が動けば動く)。</summary>
        public Vector3 CurrentPoint => _currentKind == BotErrandKind.Attack && _currentTarget != null ? _currentTarget.AimPoint : _currentPoint;

        /// <summary>狙う指示の敵。無ければ null。</summary>
        public Damageable CurrentTarget => _currentKind == BotErrandKind.Attack ? _currentTarget : null;

        /// <summary>最後の指示を受けた bot。</summary>
        public IReadOnlyList<EnemyAgent> Recipients => _recipients;

        /// <summary>指示の印を出している表示。無ければ null。</summary>
        public ISquadOrderView View => GameUi.IsAlive(_view) ? _view : null;

        /// <summary>印を見るカメラ。プレイヤーの視点 (<see cref="ViewRig"/>) のカメラで、無ければ null。</summary>
        public Camera ViewCamera
        {
            get
            {
                var rig = Rig;
                return rig != null ? rig.Camera : null;
            }
        }

        /// <summary>時刻 (秒) を返すもの。null なら <see cref="Time.time"/>。テストが差し込む。</summary>
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

        /// <summary>シリアライズされた値から <see cref="SquadOrderSpec"/> を作る。値が不正なら ArgumentOutOfRangeException。</summary>
        public SquadOrderSpec CreateSpec()
        {
            return new SquadOrderSpec(_holdSeconds, _maxDistance, _enemyAimToleranceDegrees, _nearbyRadius, _followDistance, _leashDistance);
        }

        /// <summary>
        /// spec の値をシリアライズされる値に書き込む。シーン・プレハブの生成スクリプトとテストが差し込むのに使う。次の <see cref="Configure"/> から使う。
        /// </summary>
        public void ApplySpec(SquadOrderSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _holdSeconds = spec.HoldSeconds;
            _maxDistance = spec.MaxDistance;
            _enemyAimToleranceDegrees = spec.EnemyAimToleranceDegrees;
            _nearbyRadius = spec.NearbyRadius;
            _followDistance = spec.FollowDistance;
            _leashDistance = spec.LeashDistance;
            if (!_configured)
            {
                _spec = spec;
            }
        }

        /// <summary>
        /// Inspector の値で設定を作り直し、押しかけのボタンと出している印を捨てる。bot が受けている指示はそのまま。
        /// Inspector の値が不正 (<see cref="CreateSpec"/>) なら何も変えずに ArgumentOutOfRangeException。
        /// </summary>
        public void Configure()
        {
            // 不正な値に指示を出すまで気づかないことがないよう、状態を変える前に作って確かめる。
            var spec = CreateSpec();
            _spec = spec;
            _button = new SquadOrderButton(spec.HoldSeconds);
            _configured = true;
            _lastRefusal = SquadOrderRefusal.None;
            _self = GetComponent<Damageable>();
            _reader = GetComponent<PlayerActionReader>();
            _nextViewSearch = 0f;
            ClearCurrent();
            Debug.Log(SquadOrderLog.Spec(spec), this);
            Changed?.Invoke(this);
        }

        /// <summary>
        /// 照準 (<see cref="ViewRig.ResolveAim"/>) の先に応じて指示を出す。出したら true と、受けた bot の数を返す。
        /// 出さなければ false を返し、理由 (<see cref="LastRefusal"/>) を <c>[Squad] refused</c> でログに出す。
        /// </summary>
        public bool TryOrder(out int recipients)
        {
            recipients = 0;
            if (!CanOrder(out _))
            {
                return false;
            }

            var rig = Rig;
            if (rig == null || !rig.IsInitialized)
            {
                return Refuse(SquadOrderRefusal.NoView);
            }

            rig.ResolveAim(Spec.MaxDistance, out var origin, out var direction);
            return TryOrderAlong(origin, direction, out recipients);
        }

        /// <summary>
        /// origin から direction への照準の先に応じて指示を出す。何を指したかは <see cref="PingAim.Resolve"/> で決める。
        /// 返り値とログは <see cref="TryOrder"/> と同じ。
        /// </summary>
        /// <exception cref="ArgumentException">origin が有限でないか、direction が 0 か有限でない。</exception>
        public bool TryOrderAlong(Vector3 origin, Vector3 direction, out int recipients)
        {
            recipients = 0;
            if (!IsFinite(origin) || !IsFinite(direction) || direction.sqrMagnitude < 1e-12f)
            {
                throw new ArgumentException($"the aim must be finite and point somewhere (origin={origin}, direction={direction})");
            }

            if (!CanOrder(out var leader))
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

            CollectTargets(origin, spec.MaxDistance);
            var surfaceIndex = surfaceTarget != null ? _candidateTargets.IndexOf(surfaceTarget) : -1;
            var aim = PingAim.Resolve(spec.Aim, origin.ToNumerics(), direction.ToNumerics(), surface, surfaceIndex, _candidates);
            var target = aim.HasTarget && aim.Kind == PingKind.Enemy ? _candidateTargets[aim.CandidateIndex] : null;
            ClearTargets();
            if (!aim.HasTarget)
            {
                return Refuse(SquadOrderRefusal.NoTarget);
            }

            var squads = CollectRecipients(leader);
            var kind = target != null ? BotErrandKind.Attack : BotErrandKind.Hold;
            var point = aim.Position.ToUnity();
            recipients = 0;
            foreach (var bot in _recipients)
            {
                var accepted = target != null ? bot.OrderAttack(target, leader, spec) : bot.OrderHold(point, leader, spec);
                recipients += accepted ? 1 : 0;
            }

            if (recipients == 0)
            {
                _recipients.Clear();
                return Refuse(SquadOrderRefusal.NoRecipients);
            }

            _recipients.RemoveAll(bot => bot.Order.Kind != kind);
            _currentKind = kind;
            _currentPoint = point;
            _currentTarget = target;
            _lastRefusal = SquadOrderRefusal.None;
            Debug.Log(SquadOrderLog.Issued(kind, target != null ? target.Id : null, aim.Position, recipients, squads), this);
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>
        /// 指示を取り消して付いてこさせる (長押し)。付いてきた bot がいれば true とその数を返し、<c>[Squad] order kind=Follow</c> をログに出す。
        /// 出している印は消す。
        /// </summary>
        public bool Recall(out int recipients)
        {
            recipients = 0;
            if (!CanOrder(out var leader))
            {
                return false;
            }

            var squads = CollectRecipients(leader);
            var spec = Spec;
            foreach (var bot in _recipients)
            {
                recipients += bot.OrderFollow(leader, spec) ? 1 : 0;
            }

            if (recipients == 0)
            {
                _recipients.Clear();
                return Refuse(SquadOrderRefusal.NoRecipients);
            }

            _recipients.RemoveAll(bot => bot.Order.Kind != BotErrandKind.Follow);
            var hadMarker = _currentKind != BotErrandKind.None;
            ClearCurrent();
            _lastRefusal = SquadOrderRefusal.None;
            Debug.Log(SquadOrderLog.Issued(BotErrandKind.Follow, null, leader.GroundPosition.ToNumerics(), recipients, squads), this);
            if (hadMarker)
            {
                Changed?.Invoke(this);
            }

            return true;
        }

        /// <summary>
        /// 狙う敵が倒れたか、指示を受けた bot が誰もその指示を続けていなければ印を消す。Play 中は LateUpdate が呼ぶ。
        /// </summary>
        public void Tick()
        {
            if (_currentKind == BotErrandKind.None)
            {
                return;
            }

            var gone = _currentKind == BotErrandKind.Attack && (_currentTarget == null || _currentTarget.IsDead);
            if (!gone)
            {
                gone = true;
                foreach (var bot in _recipients)
                {
                    if (bot != null && bot.Order.Kind == _currentKind && (_currentKind != BotErrandKind.Attack || bot.OrderTarget == _currentTarget))
                    {
                        gone = false;
                        break;
                    }
                }
            }

            if (gone)
            {
                ClearCurrent();
                Changed?.Invoke(this);
            }
        }

        /// <summary>指示の印を view に出す。前の表示は手放す。null なら出すのをやめ、HUD から探し直す。</summary>
        public void BindView(ISquadOrderView view)
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
                Debug.Log(SquadOrderLog.View(view.GetType().Name), this);
            }
            else
            {
                _nextViewSearch = 0f;
            }
        }

        /// <summary>
        /// 分隊のあるモード (<see cref="ModeRunnerBase.HasSquads"/>) を始めていて、member がその参加者なら true。
        /// </summary>
        public static bool InSquadMode(SideMember member)
        {
            if (member == null)
            {
                return false;
            }

            foreach (var runner in FindObjectsByType<ModeRunnerBase>(FindObjectsInactive.Exclude))
            {
                if (runner == null || !runner.IsStarted || !runner.HasSquads)
                {
                    continue;
                }

                foreach (var participant in runner.Members)
                {
                    if (participant == member)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnDestroy()
        {
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

            if (_reader == null)
            {
                _reader = GetComponent<PlayerActionReader>();
            }

            // ポーズ画面のボタンを押したつもりで指示が出ないよう、開いている間は押していないことにする。
            if (GameUi.IsPauseOpen || _reader == null || !_reader.isActiveAndEnabled)
            {
                _button.Reset();
            }
            else
            {
                var down = (_reader.HeldCommands & PlayerCommands.SquadCommand) != 0;
                switch (_button.Tick(down, Now))
                {
                    case SquadOrderGesture.Tap:
                        TryOrder(out _);
                        break;
                    case SquadOrderGesture.Hold:
                        Recall(out _);
                        break;
                }
            }

            Tick();
            EnsureView();
        }

        private void EnsureView()
        {
            if (GameUi.IsAlive(_view) || Time.unscaledTime < _nextViewSearch)
            {
                return;
            }

            _nextViewSearch = Time.unscaledTime + ViewSearchIntervalSeconds;
            var view = SquadOrderUi.CreateView(GameUi.FindHud());
            if (view != null)
            {
                BindView(view);
            }
        }

        private bool CanOrder(out SideMember leader)
        {
            leader = null;
            if (!_configured)
            {
                Configure();
            }

            if (_self != null && _self.IsDead)
            {
                return Refuse(SquadOrderRefusal.Dead);
            }

            leader = SideMember.Of(gameObject);
            if (leader == null)
            {
                return Refuse(SquadOrderRefusal.NoSide);
            }

            return true;
        }

        private bool Refuse(SquadOrderRefusal reason)
        {
            _lastRefusal = reason;
            Debug.Log(SquadOrderLog.Refused(reason), this);
            return false;
        }

        private void ClearCurrent()
        {
            _currentKind = BotErrandKind.None;
            _currentPoint = Vector3.zero;
            _currentTarget = null;
        }

        // _recipients に指示を出す bot を入れ、分隊のあるモードかを返す。
        private bool CollectRecipients(SideMember leader)
        {
            _bots.Clear();
            _botCandidates.Clear();
            _recipients.Clear();
            foreach (var agent in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Exclude))
            {
                if (agent == null || !agent.IsInitialized || agent.transform == transform || agent.transform.IsChildOf(transform))
                {
                    continue;
                }

                var member = agent.GetComponent<SideMember>();
                if (member == null)
                {
                    continue;
                }

                _bots.Add(agent);
                _botCandidates.Add(new SquadOrderCandidate(
                    member.Side, member.GroundPosition.ToNumerics(), member.IsAlive && !agent.Brain.IsDead));
            }

            var squads = InSquadMode(leader);
            SquadOrderRecipients.Select(
                leader.Side, leader.GroundPosition.ToNumerics(), squads, Spec.NearbyRadius, _botCandidates, _recipientIndices);
            foreach (var index in _recipientIndices)
            {
                _recipients.Add(_bots[index]);
            }

            _bots.Clear();
            _botCandidates.Clear();
            _recipientIndices.Clear();
            return squads;
        }

        private void CollectTargets(Vector3 origin, float maxDistance)
        {
            ClearTargets();
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

        private void ClearTargets()
        {
            _candidateTargets.Clear();
            _candidates.Clear();
        }

        // 自分と、陣営が分かっていて同じ味方は狙わせない (当たればその場所を守らせる)。
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

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
