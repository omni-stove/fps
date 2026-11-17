using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class EnemyBrain
    {
        public event Action<EnemyState, EnemyState> StateChanged;

        private EnemySpec _spec;
        private Vector3[] _route = Array.Empty<Vector3>();
        private int _routeIndex;
        private Vector3 _lastSeen;
        private Vector3 _searchPoint;
        private float _reactionElapsed;
        private bool _engaging;
        private int _shotsInAttack;
        private float _lostElapsed;
        private float _searchElapsed;
        private CoverPointSelector _cover;
        private int _coverIndex = -1;
        private float _coverElapsed;
        private Vector3 _coverThreat;

        // 着けなかった点。また選んで同じ点で詰まり続けないよう、巡回や待機に戻るか遮蔽点の一覧が変わるまで選ばない。
        private readonly HashSet<int> _unreachableCover = new HashSet<int>();

        private readonly List<IEnemyBehavior> _behaviors = new List<IEnemyBehavior>();
        private readonly List<IBrainPerceptionContributor> _contributors = new List<IBrainPerceptionContributor>();

        public EnemySpec Spec => _spec;
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public bool IsDead => State == EnemyState.Dead;

        public EnemyCoverPhase CoverPhase { get; private set; }

        public int CoverPointIndex => _coverIndex;

        public CoverPoint? CurrentCoverPoint => _cover != null && _coverIndex >= 0 ? _cover.Points[_coverIndex] : (CoverPoint?)null;

        public int ShotsInAttack => _shotsInAttack;

        public float NextShotSpreadDegrees => _spec.SpreadDegreesForShot(_shotsInAttack);

        // Fire の決定でも弾切れや連射間隔で撃てないことがあるので、撃てたかどうかは呼び出し側が伝える。
        public void RecordShot()
        {
            if (_shotsInAttack < int.MaxValue)
            {
                _shotsInAttack++;
            }
        }

        public bool IsAiming => EnemyGait.IsAiming(State, CoverPhase) || (State == EnemyState.Chase && _engaging);

        public float AimRemainingSeconds => IsAiming ? Math.Max(0f, _spec.ReactionSeconds - _reactionElapsed) : 0f;

        public int PatrolIndex => _routeIndex;

        public EnemyBrain(EnemySpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        // 状態や経過時間は持ち越す。ボスの段階の切り替えで、戦っている最中に判断が初めからやり直しにならないように。
        public void ChangeSpec(EnemySpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public void SetPatrolRoute(IReadOnlyList<Vector3> route)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            var copy = new Vector3[route.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = route[i];
            }

            _route = copy;
            _routeIndex = 0;

            if (State == EnemyState.Idle && _route.Length > 0)
            {
                SetState(EnemyState.Patrol);
            }
            else if (State == EnemyState.Patrol && _route.Length == 0)
            {
                SetState(EnemyState.Idle);
            }
        }

        public void UseCover(CoverPointSelector selector)
        {
            if (ReferenceEquals(selector, _cover))
            {
                return;
            }

            ReleaseCoverPoint();
            _unreachableCover.Clear();
            _cover = selector;
        }

        public void Kill()
        {
            if (IsDead)
            {
                return;
            }

            ReleaseCoverPoint();
            CoverPhase = EnemyCoverPhase.None;
            SetState(EnemyState.Dead);
        }

        public bool Revive()
        {
            if (!IsDead)
            {
                return false;
            }

            StartOver();
            return true;
        }

        public void Restart()
        {
            ReleaseCoverPoint();
            StartOver();
        }

        public bool AbandonGoal()
        {
            switch (State)
            {
                case EnemyState.TakeCover when CoverPhase == EnemyCoverPhase.Moving && _coverIndex >= 0:
                    _unreachableCover.Add(_coverIndex);
                    ReleaseCoverPoint();
                    return true;
                case EnemyState.Patrol when _route.Length > 0:
                    _routeIndex = (_routeIndex + 1) % _route.Length;
                    return true;
                case EnemyState.Search:
                case EnemyState.Chase:
                    StartSearch(_searchPoint);
                    _searchElapsed = _spec.SearchSeconds;
                    return true;
                default:
                    return false;
            }
        }

        // 最後に見た位置まで忘れるのは、倒れた目標の最後に見た位置へ向かわないようにするため。
        public bool StandDown()
        {
            switch (State)
            {
                case EnemyState.Chase:
                case EnemyState.Attack:
                case EnemyState.TakeCover:
                case EnemyState.Search:
                    ReleaseCoverPoint();
                    CoverPhase = EnemyCoverPhase.None;
                    _lastSeen = default;
                    _searchPoint = default;
                    _reactionElapsed = 0f;
                    _shotsInAttack = 0;
                    _lostElapsed = 0f;
                    _searchElapsed = 0f;
                    _coverElapsed = 0f;
                    _coverThreat = default;
                    _unreachableCover.Clear();
                    SetState(_route.Length > 0 ? EnemyState.Patrol : EnemyState.Idle);
                    return true;
                default:
                    return false;
            }
        }

        public EnemyDecision Tick(float deltaSeconds, EnemyPerception perception)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (IsDead)
            {
                return EnemyDecision.None;
            }

            if (_contributors.Count > 0)
            {
                var contributors = _contributors.ToArray();
                foreach (var contributor in contributors)
                {
                    perception = contributor.Contribute(this, deltaSeconds, perception);
                    if (IsDead)
                    {
                        return EnemyDecision.None;
                    }
                }
            }

            var decision = Decide(deltaSeconds, perception);

            // StateChanged の購読者が Kill() した場合も、死亡後は何も決定しない。
            if (IsDead)
            {
                return EnemyDecision.None;
            }

            // 振る舞いの中で登録や解除をされても、この tick に呼ぶ相手が変わらないよう写しを回す。
            if (_behaviors.Count > 0)
            {
                var behaviors = _behaviors.ToArray();
                foreach (var behavior in behaviors)
                {
                    if (behavior.TryOverride(this, deltaSeconds, perception, decision, out var overridden))
                    {
                        decision = overridden;
                    }

                    if (IsDead)
                    {
                        return EnemyDecision.None;
                    }
                }
            }

            return decision;
        }

        public IReadOnlyList<IEnemyBehavior> Behaviors => _behaviors;

        // 登録した順に呼び、後の振る舞いは前の振る舞いが上書きした決定を受け取る。
        public void AddBehavior(IEnemyBehavior behavior)
        {
            if (behavior == null)
            {
                throw new ArgumentNullException(nameof(behavior));
            }

            if (_behaviors.Contains(behavior))
            {
                throw new ArgumentException("behavior is already added", nameof(behavior));
            }

            _behaviors.Add(behavior);
        }

        public bool RemoveBehavior(IEnemyBehavior behavior)
        {
            if (behavior == null)
            {
                throw new ArgumentNullException(nameof(behavior));
            }

            return _behaviors.Remove(behavior);
        }

        public IReadOnlyList<IBrainPerceptionContributor> PerceptionContributors => _contributors;

        // 登録した順に呼び、後のものは前のものが書き換えた知覚を受け取る。振る舞い (AddBehavior) にも、書き換えた後の知覚を渡す。
        public void AddPerceptionContributor(IBrainPerceptionContributor contributor)
        {
            if (contributor == null)
            {
                throw new ArgumentNullException(nameof(contributor));
            }

            if (_contributors.Contains(contributor))
            {
                throw new ArgumentException("contributor is already added", nameof(contributor));
            }

            _contributors.Add(contributor);
        }

        public bool RemovePerceptionContributor(IBrainPerceptionContributor contributor)
        {
            if (contributor == null)
            {
                throw new ArgumentNullException(nameof(contributor));
            }

            return _contributors.Remove(contributor);
        }

        private EnemyDecision Decide(float deltaSeconds, EnemyPerception p)
        {
            var seen = p.HasLineOfSight && p.Awareness >= EnemyPerception.FullAwareness && p.Target.HasValue
                && Vector3.Distance(p.Self, p.Target.Value) <= _spec.SightRange;

            if ((State == EnemyState.TakeCover || ShouldTakeCover(p))
                && TryCover(deltaSeconds, p, seen, out var cover))
            {
                return cover;
            }

            if (seen)
            {
                return Engage(deltaSeconds, p.Self, p.Target.Value);
            }

            // TakeCover をやめたばかりなら、Chase と同じく最後に脅威のいた位置へ向かう。
            if (State == EnemyState.Chase || State == EnemyState.Attack || State == EnemyState.TakeCover)
            {
                return Pursue(deltaSeconds);
            }

            var heard = p.HeardNoise && p.NoisePosition.HasValue;
            if (State == EnemyState.Search)
            {
                if (heard)
                {
                    StartSearch(p.NoisePosition.Value);
                    return SearchDecision();
                }

                _searchElapsed += deltaSeconds;
                if (_searchElapsed < _spec.SearchSeconds)
                {
                    return SearchDecision();
                }

                return Routine(p.Self);
            }

            if (heard)
            {
                StartSearch(p.NoisePosition.Value);
                return SearchDecision();
            }

            return Routine(p.Self);
        }

        private EnemyDecision Engage(float deltaSeconds, Vector3 self, Vector3 target)
        {
            _lostElapsed = 0f;
            _lastSeen = target;

            var distance = Vector3.Distance(self, target);
            if (distance > _spec.FireRange)
            {
                _engaging = false;
                SetState(EnemyState.Chase);
                return new EnemyDecision(target, target, false);
            }

            // 撃ちながら近づいて Attack に入ったときは構え直さない。入った途端に撃つのをやめると、近づくほど撃たなくなって見える。
            if (_engaging && (State == EnemyState.Attack || State == EnemyState.Chase))
            {
                _reactionElapsed += deltaSeconds;
            }
            else
            {
                _reactionElapsed = 0f;
                _shotsInAttack = 0;
            }

            _engaging = true;
            var ready = _reactionElapsed >= _spec.ReactionSeconds;
            if (distance <= _spec.AttackRange)
            {
                SetState(EnemyState.Attack);
                return new EnemyDecision(null, target, ready);
            }

            SetState(EnemyState.Chase);
            return new EnemyDecision(ready ? target : (Vector3?)null, target, ready);
        }

        private bool ShouldTakeCover(EnemyPerception p)
        {
            return _cover != null && (p.WasHit || p.HealthFraction < _spec.CoverHealthFraction);
        }

        private static Vector3? ThreatOf(EnemyPerception p)
        {
            if (p.Target.HasValue)
            {
                return p.Target.Value;
            }

            return p.WasHit ? p.HitSource : null;
        }

        private bool TryCover(float deltaSeconds, EnemyPerception p, bool seen, out EnemyDecision decision)
        {
            decision = EnemyDecision.None;
            if (seen)
            {
                _lastSeen = p.Target.Value;
            }

            var threat = ThreatOf(p);
            if (_cover == null || !threat.HasValue || !CoverPoint.IsFinite(threat.Value) || !CoverPoint.IsFinite(p.Self))
            {
                LeaveCover();
                return false;
            }

            _coverThreat = threat.Value;

            // 回り込まれて今の点が脅威の方向を遮らなくなったら、放して選び直す。
            if (_coverIndex >= 0 && !_cover.Protects(_coverIndex, _coverThreat))
            {
                ReleaseCoverPoint();
            }

            if (_coverIndex < 0)
            {
                var next = _cover.Select(p.Self, _coverThreat, this, _unreachableCover);
                if (next < 0 || !_cover.TryClaim(next, this))
                {
                    LeaveCover();
                    return false;
                }

                _coverIndex = next;
                CoverPhase = EnemyCoverPhase.Moving;
            }

            if (State != EnemyState.TakeCover)
            {
                _lostElapsed = 0f;
            }

            decision = CoverStep(deltaSeconds, p.Self, seen);

            // 決定を作った後に移す。StateChanged の購読者が Kill() して点を放しても、ここでは点を読まない。
            SetState(EnemyState.TakeCover);
            return true;
        }

        private EnemyDecision CoverStep(float deltaSeconds, Vector3 self, bool seen)
        {
            var point = _cover.Points[_coverIndex].Position;
            switch (CoverPhase)
            {
                case EnemyCoverPhase.Moving:
                    if (Vector3.Distance(self, point) > _spec.ArrivalDistance)
                    {
                        return new EnemyDecision(point, _coverThreat, false, false);
                    }

                    StartHiding();
                    return HideDecision();
                case EnemyCoverPhase.Hiding:
                    _coverElapsed += deltaSeconds;
                    if (_coverElapsed < _spec.CoverHideSeconds)
                    {
                        return HideDecision();
                    }

                    CoverPhase = EnemyCoverPhase.Peeking;
                    _coverElapsed = 0f;
                    _reactionElapsed = 0f;
                    _shotsInAttack = 0;
                    return PeekDecision(self, seen);
                default:
                    _coverElapsed += deltaSeconds;
                    if (_coverElapsed >= _spec.CoverPeekSeconds)
                    {
                        StartHiding();
                        return HideDecision();
                    }

                    _reactionElapsed = _coverElapsed;
                    return PeekDecision(self, seen);
            }
        }

        private void StartHiding()
        {
            CoverPhase = EnemyCoverPhase.Hiding;
            _coverElapsed = 0f;
        }

        private EnemyDecision HideDecision()
        {
            return new EnemyDecision(null, _coverThreat, false, true);
        }

        private EnemyDecision PeekDecision(Vector3 self, bool seen)
        {
            var inRange = Vector3.Distance(self, _coverThreat) <= _spec.FireRange;
            return new EnemyDecision(null, _coverThreat, seen && inRange && _reactionElapsed >= _spec.ReactionSeconds, false);
        }

        private void LeaveCover()
        {
            if (State == EnemyState.TakeCover)
            {
                _lastSeen = _coverThreat;
                _lostElapsed = 0f;
            }

            ReleaseCoverPoint();
            CoverPhase = EnemyCoverPhase.None;
        }

        private void ReleaseCoverPoint()
        {
            _cover?.Release(this);
            _coverIndex = -1;
        }

        private void StartOver()
        {
            _routeIndex = 0;
            _lastSeen = default;
            _searchPoint = default;
            _reactionElapsed = 0f;
            _shotsInAttack = 0;
            _lostElapsed = 0f;
            _searchElapsed = 0f;
            _coverElapsed = 0f;
            _coverThreat = default;
            _coverIndex = -1;
            _unreachableCover.Clear();
            CoverPhase = EnemyCoverPhase.None;
            SetState(_route.Length > 0 ? EnemyState.Patrol : EnemyState.Idle);
        }

        private EnemyDecision Pursue(float deltaSeconds)
        {
            _engaging = false;
            _lostElapsed += deltaSeconds;
            if (_lostElapsed >= _spec.LoseSightSeconds)
            {
                StartSearch(_lastSeen);
                return SearchDecision();
            }

            SetState(EnemyState.Chase);
            return new EnemyDecision(_lastSeen, _lastSeen, false);
        }

        private void StartSearch(Vector3 point)
        {
            _searchPoint = point;
            _searchElapsed = 0f;
            _lostElapsed = 0f;
            SetState(EnemyState.Search);
        }

        private EnemyDecision SearchDecision()
        {
            return new EnemyDecision(_searchPoint, _searchPoint, false);
        }

        private EnemyDecision Routine(Vector3 self)
        {
            _unreachableCover.Clear();
            if (_route.Length == 0)
            {
                SetState(EnemyState.Idle);
                return EnemyDecision.None;
            }

            SetState(EnemyState.Patrol);
            if (Vector3.Distance(self, _route[_routeIndex]) <= _spec.ArrivalDistance)
            {
                _routeIndex = (_routeIndex + 1) % _route.Length;
            }

            return new EnemyDecision(_route[_routeIndex], null, false);
        }

        private void SetState(EnemyState next)
        {
            if (State == next)
            {
                return;
            }

            var previous = State;
            State = next;
            if (next != EnemyState.Attack && next != EnemyState.Chase)
            {
                _engaging = false;
            }

            StateChanged?.Invoke(previous, next);
        }
    }
}
