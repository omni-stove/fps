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
    /// 目標が視線の通る SightRange 内にいれば Chase、AttackRange 内なら Attack（発見から ReactionSeconds 後に撃つ）。
    /// 見失ったら最後に見た位置へ向かい、LoseSightSeconds 後に Search、SearchSeconds 後に Patrol（ルートが無ければ Idle）に戻る。
    /// Idle・Patrol・Search 中に音を聞いたら、その位置を Search する。
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
        private float _lostElapsed;
        private float _searchElapsed;

        public EnemySpec Spec => _spec;
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public bool IsDead => State == EnemyState.Dead;

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

        /// <summary>死亡させる。以後の Tick はすべて空の決定を返す。</summary>
        public void Kill()
        {
            if (IsDead)
            {
                return;
            }

            SetState(EnemyState.Dead);
        }

        /// <summary>
        /// 今向かっている先に届かないとき、その目的地をあきらめる。
        /// Patrol なら次の巡回地点へ進め、Search なら打ち切って次の tick で Patrol (ルートが無ければ Idle) に戻す。
        /// 見失った後の Chase なら Search を飛ばして同じく戻す。目標が見えていれば次の tick でまた追う。
        /// 目的地を持たない状態 (Idle・Attack・Dead) では何もせず false を返す。
        /// </summary>
        public bool AbandonGoal()
        {
            switch (State)
            {
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

            var decision = Decide(deltaSeconds, perception);

            // StateChanged の購読者が Kill() した場合も、死亡後は何も決定しない。
            return IsDead ? EnemyDecision.None : decision;
        }

        private EnemyDecision Decide(float deltaSeconds, EnemyPerception p)
        {
            if (p.HasLineOfSight && p.Target.HasValue
                && Vector3.Distance(p.Self, p.Target.Value) <= _spec.SightRange)
            {
                return Engage(deltaSeconds, p.Self, p.Target.Value);
            }

            if (State == EnemyState.Chase || State == EnemyState.Attack)
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
            var alreadyEngaged = State == EnemyState.Chase || State == EnemyState.Attack;
            _reactionElapsed = alreadyEngaged ? _reactionElapsed + deltaSeconds : 0f;
            _lostElapsed = 0f;
            _lastSeen = target;

            if (Vector3.Distance(self, target) <= _spec.AttackRange)
            {
                SetState(EnemyState.Attack);
                return new EnemyDecision(null, target, _reactionElapsed >= _spec.ReactionSeconds);
            }

            SetState(EnemyState.Chase);
            return new EnemyDecision(target, target, false);
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
            _reactionElapsed = 0f;
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
