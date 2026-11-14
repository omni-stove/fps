using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// キャンペーンの敵の判断。毎 tick 知覚を受け取り、どこへ動き・どこを向き・撃つかを決める。
    /// 移動や視線判定（Raycast）は行わず、呼び出し側が決定を実行する。
    /// </summary>
    /// <remarks>
    /// 目標が視線の通る SightRange 内にいれば Chase、AttackRange 内なら Attack（Attack に入ってから ReactionSeconds 後に撃つ。
    /// Chase で追っていた時間は数えないので、射程に入った瞬間には撃たない）。
    /// 撃ち始めの数発は散らばりを大きくし、OpeningShots 発かけて絞る（<see cref="EnemySpec.SpreadDegreesForShot"/>）。
    /// 見失ったら最後に見た位置へ向かい、LoseSightSeconds 後に Search、SearchSeconds 後に Patrol（ルートが無ければ Idle）に戻る。
    /// Idle・Patrol・Search 中に音を聞いたら、その位置を Search する。
    /// <para>
    /// <see cref="UseCover"/> で遮蔽点を渡していれば、撃たれたとき (<see cref="EnemyPerception.WasHit"/>) や体力の割合が
    /// <see cref="EnemySpec.CoverHealthFraction"/> を下回ったときに、脅威の方向を遮る点を選んで TakeCover に移る。
    /// 脅威の位置は目標の位置 (視線が通らなくても渡されていれば使う) で、目標がいなければ撃たれた tick だけ撃った者の位置を使う。
    /// 点に着いたら CoverHideSeconds しゃがみ、CoverPeekSeconds 立って覗き (見えていれば ReactionSeconds 後から撃つ)、を繰り返す。
    /// 目標が点の遮る向きから外れたら (回り込まれたら) 別の点に移り、移れる点が無いか目標がいなくなったら TakeCover をやめて追う。
    /// </para>
    /// </remarks>
    public sealed class EnemyBrain
    {
        /// <summary>巡回地点に着いたとみなす距離（メートル）。</summary>
        public const float ArrivalDistance = 0.5f;

        /// <summary>状態が変わるたびに（変わる前, 変わった後）で呼ばれる。</summary>
        public event Action<EnemyState, EnemyState> StateChanged;

        private readonly EnemySpec _spec;
        private Vector3[] _route = Array.Empty<Vector3>();
        private int _routeIndex;
        private Vector3 _lastSeen;
        private Vector3 _searchPoint;
        private float _reactionElapsed;
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
        private readonly List<IPerceptionContributor> _contributors = new List<IPerceptionContributor>();

        public EnemySpec Spec => _spec;
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public bool IsDead => State == EnemyState.Dead;

        /// <summary>TakeCover の中の段階。TakeCover でなければ None。</summary>
        public EnemyCoverPhase CoverPhase { get; private set; }

        /// <summary>使っている遮蔽点の、<see cref="CoverPointSelector.Points"/> での添字。使っていなければ -1。</summary>
        public int CoverPointIndex => _coverIndex;

        /// <summary>使っている遮蔽点。使っていなければ null。</summary>
        public CoverPoint? CurrentCoverPoint => _cover != null && _coverIndex >= 0 ? _cover.Points[_coverIndex] : (CoverPoint?)null;

        /// <summary>
        /// 今の Attack に入ってから <see cref="RecordShot"/> で記録した発数。Attack に入るたび、TakeCover で覗き始めるたびに 0 に戻る。
        /// </summary>
        public int ShotsInAttack => _shotsInAttack;

        /// <summary>次に撃つ弾の散らばりの半角 (度)。</summary>
        public float NextShotSpreadDegrees => _spec.SpreadDegreesForShot(_shotsInAttack);

        /// <summary>
        /// 実際に 1 発撃ったことを記録し、次の弾の散らばりを絞る。
        /// Fire の決定でも弾切れや連射間隔で撃てないことがあるので、撃てたかどうかは呼び出し側が伝える。
        /// </summary>
        public void RecordShot()
        {
            if (_shotsInAttack < int.MaxValue)
            {
                _shotsInAttack++;
            }
        }

        /// <summary>巡回で次に向かう地点の添字。ルートが無ければ 0。</summary>
        public int PatrolIndex => _routeIndex;

        /// <param name="spec">敵の設定。</param>
        public EnemyBrain(EnemySpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        /// <summary>
        /// 巡回ルートを設定する。到着すると次の地点へ進み、最後の地点の次は最初に戻る。
        /// 空のルートを渡すと巡回をやめ、Patrol 中なら Idle になる。リストは複製して保持する。
        /// </summary>
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

        /// <summary>
        /// 隠れに行く遮蔽点を設定する。同じ一覧を使う敵どうしで同じ selector を渡すと、ほかの敵が使っている点を選ばなくなる。
        /// null を渡すと遮蔽点を使わなくなる。TakeCover 中に変えたら今の点を放し、次の tick に選び直す (選べなければ TakeCover をやめる)。
        /// </summary>
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

        /// <summary>死亡させる。<see cref="Revive"/> するまで Tick はすべて空の決定を返す。使っていた遮蔽点は放す。</summary>
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

        /// <summary>
        /// 死亡から生き返らせる。最後に見た位置・反応や見失いの経過・撃った発数・着けなかった遮蔽点を忘れ、
        /// 巡回ルートの最初の地点から Patrol (ルートが無ければ Idle) で始め直す。生きていれば何もせず false。
        /// </summary>
        public bool Revive()
        {
            if (!IsDead)
            {
                return false;
            }

            StartOver();
            return true;
        }

        /// <summary>
        /// 生死を問わず、<see cref="Revive"/> と同じく覚えていることを忘れて巡回ルートの最初の地点から始め直す。使っていた遮蔽点は放す。
        /// ラウンド制のモードがラウンドの頭で、生き残っていた敵も含めて全員を戻すときに使う。
        /// </summary>
        public void Restart()
        {
            ReleaseCoverPoint();
            StartOver();
        }

        /// <summary>
        /// 今向かっている先に届かないとき、その目的地をあきらめる。
        /// Patrol なら次の巡回地点へ進め、Search なら打ち切って次の tick で Patrol (ルートが無ければ Idle) に戻す。
        /// 見失った後の Chase なら Search を飛ばして同じく戻す。目標が見えていれば次の tick でまた追う。
        /// TakeCover で遮蔽点へ向かっているなら、その点を放して以後選ばず、次の tick に別の点を選び直す (選べなければ TakeCover をやめる)。
        /// 目的地を持たない状態 (Idle・Attack・Dead、遮蔽点に着いた後の TakeCover) では何もせず false を返す。
        /// </summary>
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

        /// <summary>時間を deltaSeconds 進め、この tick の決定を返す。</summary>
        /// <param name="deltaSeconds">前の tick からの経過秒。0 以上。</param>
        /// <param name="perception">この tick の知覚。</param>
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

        public IReadOnlyList<IPerceptionContributor> PerceptionContributors => _contributors;

        // 登録した順に呼び、後のものは前のものが書き換えた知覚を受け取る。振る舞い (AddBehavior) にも、書き換えた後の知覚を渡す。
        public void AddPerceptionContributor(IPerceptionContributor contributor)
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

        public bool RemovePerceptionContributor(IPerceptionContributor contributor)
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

        /// <summary>目標が見えているときの Chase / Attack。</summary>
        private EnemyDecision Engage(float deltaSeconds, Vector3 self, Vector3 target)
        {
            _lostElapsed = 0f;
            _lastSeen = target;

            if (Vector3.Distance(self, target) <= _spec.AttackRange)
            {
                if (State == EnemyState.Attack)
                {
                    _reactionElapsed += deltaSeconds;
                }
                else
                {
                    _reactionElapsed = 0f;
                    _shotsInAttack = 0;
                }

                SetState(EnemyState.Attack);
                return new EnemyDecision(null, target, _reactionElapsed >= _spec.ReactionSeconds);
            }

            SetState(EnemyState.Chase);
            return new EnemyDecision(target, target, false);
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

        /// <summary>
        /// TakeCover に入るか続ける。使える遮蔽点が無いか脅威が分からなければ TakeCover をやめて false を返し、
        /// 呼び出し側がふだんの Chase / Attack の判断を続ける。
        /// </summary>
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
                    if (Vector3.Distance(self, point) > ArrivalDistance)
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
            var inRange = Vector3.Distance(self, _coverThreat) <= _spec.AttackRange;
            return new EnemyDecision(null, _coverThreat, seen && inRange && _reactionElapsed >= _spec.ReactionSeconds, false);
        }

        /// <summary>TakeCover をやめる。状態は呼び出し側の判断 (Chase / Attack) で移る。</summary>
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

        /// <summary>見失った直後。最後に見た位置へ向かい、LoseSightSeconds 経ったら Search に移る。</summary>
        private EnemyDecision Pursue(float deltaSeconds)
        {
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

        /// <summary>ルートがあれば Patrol、なければ Idle。</summary>
        private EnemyDecision Routine(Vector3 self)
        {
            _unreachableCover.Clear();
            if (_route.Length == 0)
            {
                SetState(EnemyState.Idle);
                return EnemyDecision.None;
            }

            SetState(EnemyState.Patrol);
            if (Vector3.Distance(self, _route[_routeIndex]) <= ArrivalDistance)
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
            StateChanged?.Invoke(previous, next);
        }
    }
}
