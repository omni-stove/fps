using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 目的地へ向かっているのに進めていないことを見つけ、立て直し (<see cref="StuckRecovery"/>) を軽いものから順に返す。
    /// 進めないまま <see cref="StuckSeconds"/> 経つたびに次の段階を 1 回だけ返し、
    /// <see cref="StuckRecovery.Warp"/> の次は <see cref="StuckRecovery.Repath"/> からやり直す。位置と時間は外から渡し、移動そのものはしない。
    /// </summary>
    /// <remarks>
    /// 詰まる前は、最後に進めた地点から <see cref="ProgressDistance"/> 動けば進めたとみなす (追う目標が逃げていても誤って詰まりとしない)。
    /// 立て直しを始めた後は、目的地までの距離が詰まった時点より <see cref="ProgressDistance"/> 縮んだときだけ立ち直ったとみなす。
    /// 寄り道で横へ動いたり、同じ所を行き来したりしただけで段階が最初に戻るのを防ぐため。
    /// 立て直しの途中で目的地が <see cref="GoalChangeDistance"/> 以上変わったら、段階も経過時間もそのままに、新しい目的地までの距離から測り直す。
    /// </remarks>
    public sealed class StuckMonitor
    {
        /// <summary>立て直しの途中で目的地へ近づけたときに、それまで進めずにいた秒数を渡して呼ばれる。</summary>
        public event Action<float> Recovered;

        /// <summary>進めないまま次の段階へ移るまでの秒数。</summary>
        public float StuckSeconds { get; }

        /// <summary>これだけ動けば (立て直し中は目的地へこれだけ近づけば) 進めたとみなす距離 (メートル)。</summary>
        public float ProgressDistance { get; }

        /// <summary>立て直しの途中で目的地がこれ以上変わったら、新しい目的地までの距離から測り直す距離 (メートル)。</summary>
        public float GoalChangeDistance { get; }

        /// <summary>最後に返した段階。進めている間は <see cref="StuckRecovery.None"/>。</summary>
        public StuckRecovery Stage { get; private set; }

        /// <summary>最後に進めた (または最後の段階を返した) ときから、進めずにいる秒数。</summary>
        public float StalledSeconds { get; private set; }

        /// <summary>最後に進めたときから、進めずにいる合計秒数。</summary>
        public float TotalStalledSeconds { get; private set; }

        private Vector3 _anchor;
        private Vector3 _goal;
        private bool _tracking;
        private float _stuckGoalDistance;

        /// <param name="stuckSeconds">次の段階へ移るまでの秒数。正の有限値。</param>
        /// <param name="progressDistance">進めたとみなす距離。正の有限値。</param>
        /// <param name="goalChangeDistance">別の目的地とみなす距離。正の有限値。</param>
        public StuckMonitor(float stuckSeconds, float progressDistance, float goalChangeDistance)
        {
            RequirePositive(stuckSeconds, nameof(stuckSeconds));
            RequirePositive(progressDistance, nameof(progressDistance));
            RequirePositive(goalChangeDistance, nameof(goalChangeDistance));

            StuckSeconds = stuckSeconds;
            ProgressDistance = progressDistance;
            GoalChangeDistance = goalChangeDistance;
        }

        /// <summary>見張りをやめ、段階と経過時間を最初に戻す。次の <see cref="Tick"/> の位置から測り直す。</summary>
        public void Reset()
        {
            _tracking = false;
            Stage = StuckRecovery.None;
            StalledSeconds = 0f;
            TotalStalledSeconds = 0f;
        }

        /// <summary>
        /// 時間を進め、この tick に試す立て直しを返す。何もしなくてよければ <see cref="StuckRecovery.None"/>。
        /// 向かう先が無ければ (<paramref name="goal"/> が null、着いた後も含む) 止まっていて当然なので <see cref="Reset"/> する。
        /// </summary>
        /// <param name="deltaSeconds">前回からの経過時間 (秒)。0 以上の有限値。</param>
        /// <param name="position">今の位置。有限値。</param>
        /// <param name="goal">向かっている先。無ければ null。有限値。</param>
        public StuckRecovery Tick(float deltaSeconds, Vector3 position, Vector3? goal)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a finite, non-negative value");
            }

            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (goal.HasValue && !IsFinite(goal.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(goal), goal, "goal must be finite");
            }

            if (!goal.HasValue)
            {
                Reset();
                return StuckRecovery.None;
            }

            if (!_tracking)
            {
                Track(position, goal.Value);
                return StuckRecovery.None;
            }

            if (Stage == StuckRecovery.None)
            {
                _goal = goal.Value;
                if (Vector3.DistanceSquared(position, _anchor) >= ProgressDistance * ProgressDistance)
                {
                    Track(position, goal.Value);
                    return StuckRecovery.None;
                }
            }
            else if (Vector3.DistanceSquared(goal.Value, _goal) >= GoalChangeDistance * GoalChangeDistance)
            {
                // 段階も経過時間も戻さない。目的地をあきらめても体が挟まったままなら次は Warp まで進める必要があり、
                // 動き続ける目標を追う間も時間は積み上げないと段階が先へ進まない。
                _goal = goal.Value;
                _stuckGoalDistance = Vector3.Distance(position, goal.Value);
            }
            else if (Vector3.Distance(position, goal.Value) <= _stuckGoalDistance - ProgressDistance)
            {
                var stalled = TotalStalledSeconds;
                Reset();
                Track(position, goal.Value);
                Recovered?.Invoke(stalled);
                return StuckRecovery.None;
            }

            StalledSeconds += deltaSeconds;
            TotalStalledSeconds += deltaSeconds;
            if (StalledSeconds < StuckSeconds)
            {
                return StuckRecovery.None;
            }

            if (Stage == StuckRecovery.None)
            {
                _stuckGoalDistance = Vector3.Distance(position, goal.Value);
            }

            StalledSeconds = 0f;
            Stage = Stage == StuckRecovery.Warp ? StuckRecovery.Repath : Stage + 1;
            return Stage;
        }

        /// <summary>3 成分すべてが有限なら true。</summary>
        public static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }

        private void Track(Vector3 position, Vector3 goal)
        {
            _anchor = position;
            _goal = goal;
            _tracking = true;
            StalledSeconds = 0f;
            TotalStalledSeconds = 0f;
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be a finite, positive value");
            }
        }
    }
}
