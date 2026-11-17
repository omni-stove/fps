using System;
using System.Numerics;

namespace Shooter.Core
{
    // 詰まる前は、最後に進めた地点から ProgressDistance 動けば進めたとみなす (追う目標が逃げていても誤って詰まりとしないため)。
    // 立て直しを始めた後は、基準点から経路の進む向き (分からなければ目的地の方向) へ ProgressDistance 以上動いたときだけ立ち直ったとみなす。
    // 寄り道で横へ動いたり、同じ所を行き来したりしただけで段階が最初に戻るのを防ぐため。
    public sealed class StuckMonitor
    {
        public event Action<float> Recovered;

        public float StuckSeconds { get; }

        public float ProgressDistance { get; }

        public float GoalChangeDistance { get; }

        public StuckRecovery Stage { get; private set; }

        public float StalledSeconds { get; private set; }

        public float TotalStalledSeconds { get; private set; }

        private Vector3 _anchor;
        private Vector3 _goal;
        private Vector3? _pathDirection;
        private bool _tracking;

        public StuckMonitor(float stuckSeconds, float progressDistance, float goalChangeDistance)
        {
            RequirePositive(stuckSeconds, nameof(stuckSeconds));
            RequirePositive(progressDistance, nameof(progressDistance));
            RequirePositive(goalChangeDistance, nameof(goalChangeDistance));

            StuckSeconds = stuckSeconds;
            ProgressDistance = progressDistance;
            GoalChangeDistance = goalChangeDistance;
        }

        public void Reset()
        {
            _tracking = false;
            Stage = StuckRecovery.None;
            StalledSeconds = 0f;
            TotalStalledSeconds = 0f;
        }

        // 向かう先が無ければ (goal が null、着いた後も含む) 止まっていて当然なので、詰まりとせず Reset する。
        public StuckRecovery Tick(float deltaSeconds, Vector3 position, Vector3? goal, Vector3? pathWaypoint = null)
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

            if (pathWaypoint.HasValue && !IsFinite(pathWaypoint.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(pathWaypoint), pathWaypoint, "pathWaypoint must be finite");
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
                // 基準点も今の位置へ移す。目標が動くたびに距離を測り直すと、速い目標を歩いて追う敵は距離が縮まず立ち直れない。
                _goal = goal.Value;
                _anchor = position;
                _pathDirection = null;
            }
            else if (MadeProgress(position, goal.Value, pathWaypoint))
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

            StalledSeconds = 0f;
            // 立て直しは今の位置から始まる。詰まる前の 0.5m 未満の移動を残すと、寄り道が目的地の方向へ少し動いただけで立ち直りと判定される。
            _anchor = position;
            _pathDirection = null;
            Stage = Stage == StuckRecovery.Warp ? StuckRecovery.Repath : Stage + 1;
            return Stage;
        }

        public static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }

        // 経路が壁を回り込むなら、最初の区間は目的地の直線方向から外れるので、経路が分かるときはその向きで測る。
        // 目的地までの残り距離では測らない。逃げる目標を追う間は、普通に歩けていても残り距離は延び続ける。
        private bool MadeProgress(Vector3 position, Vector3 goal, Vector3? pathWaypoint)
        {
            if (!pathWaypoint.HasValue)
            {
                // 寄り道の間など経路の向きが分からない間に動いた分を、後で古い向きに当てはめないよう測り直させる。
                _pathDirection = null;
                return MovedTowardGoal(position, goal);
            }

            if (!_pathDirection.HasValue)
            {
                var toWaypoint = pathWaypoint.Value - position;
                var length = toWaypoint.Length();
                if (length > 0f)
                {
                    _pathDirection = toWaypoint / length;
                }

                return false;
            }

            return Vector3.Dot(position - _anchor, _pathDirection.Value) >= ProgressDistance;
        }

        // 目的地までの距離だけだと、動かない敵へ目標のほうが近づいただけで立ち直ったことになってしまう。
        private bool MovedTowardGoal(Vector3 position, Vector3 goal)
        {
            var toGoal = goal - _anchor;
            var length = toGoal.Length();
            if (!(length > 0f))
            {
                return false;
            }

            return Vector3.Dot(position - _anchor, toGoal / length) >= ProgressDistance;
        }

        private void Track(Vector3 position, Vector3 goal)
        {
            _anchor = position;
            _pathDirection = null;
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
