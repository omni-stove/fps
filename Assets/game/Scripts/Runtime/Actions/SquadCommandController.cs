using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で読んだ押下を、同じフレームのうちに使う。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class SquadCommandController : MonoBehaviour
    {
        public const string LogTag = SquadOrderLog.Tag;

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
        private SquadOrderStatus? _published;
        private SquadOrderRefusal _lastRefusal;
        private BotErrandKind _currentKind = BotErrandKind.None;
        private Vector3 _currentPoint;
        private Damageable _currentTarget;

        public event Action<SquadCommandController> Changed;

        public bool IsConfigured => _configured;

        public SquadOrderSpec Spec => _spec ?? (_spec = CreateSpec());

        public SquadOrderRefusal LastRefusal => _lastRefusal;

        public BotErrandKind CurrentOrder => _currentKind;

        public Vector3 CurrentPoint => _currentKind == BotErrandKind.Attack && _currentTarget != null ? _currentTarget.AimPoint : _currentPoint;

        public Damageable CurrentTarget => _currentKind == BotErrandKind.Attack ? _currentTarget : null;

        public IReadOnlyList<EnemyAgent> Recipients => _recipients;

        public Camera ViewCamera
        {
            get
            {
                var rig = Rig;
                return rig != null ? rig.Camera : null;
            }
        }

        // テストが時刻を差し込めるよう差し替えられるようにする。
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

        public SquadOrderSpec CreateSpec()
        {
            return new SquadOrderSpec(_holdSeconds, _maxDistance, _enemyAimToleranceDegrees, _nearbyRadius, _followDistance, _leashDistance);
        }

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
            ClearCurrent();
            Debug.Log(SquadOrderLog.Spec(spec), this);
            NotifyChanged();
        }

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
            NotifyChanged();
            return true;
        }

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
                NotifyChanged();
            }

            return true;
        }

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
                NotifyChanged();
            }
        }

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
            Unpublish();
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
        }

        private void NotifyChanged()
        {
            if (_currentKind != BotErrandKind.None)
            {
                _published = new SquadOrderStatus(_currentKind, _currentPoint, CurrentTarget);
                GameSignals.SquadOrder.Publish(_published);
            }
            else
            {
                Unpublish();
            }

            Changed?.Invoke(this);
        }

        // 後から別のプレイヤーが出した指示までは消さない。
        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.SquadOrder.ClearIfCurrent(_published);
                _published = null;
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

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
