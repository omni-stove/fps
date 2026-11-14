using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    public sealed partial class EnemyAgent
    {
        private Vector3? _destination;
        private bool _warnedOffMesh;
        private StuckMonitor _stuck;
        private Vector3? _goal;
        private Vector3? _detour;
        private float _detourEndsAt;
        private Vector3 _spawnPosition;
        private Vector3 _lastFinitePosition;
        private float _nextOffMeshRetry;

        private void ApplyPatrolRoute()
        {
            var route = new System.Numerics.Vector3[_patrolRoute.Length];
            for (var i = 0; i < route.Length; i++)
            {
                route[i] = SnapToNavMesh(_patrolRoute[i]).ToNumerics();
            }

            _brain.SetPatrolRoute(route);
        }

        private Vector3 SnapToNavMesh(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var hit, RouteSnapRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }

            Debug.LogWarning($"{LogTag} id={Id} patrol point {point} is not on the NavMesh; using it as is", this);
            return point;
        }

        private void MoveTo(System.Numerics.Vector3? moveTo)
        {
            _goal = moveTo.HasValue ? moveTo.Value.ToUnity() : (Vector3?)null;
            if (!_agent.isOnNavMesh && !TryReturnToNavMesh())
            {
                return;
            }

            if (!_goal.HasValue)
            {
                bool hadDetour = _detour.HasValue;
                _detour = null;
                if (_destination.HasValue || hadDetour)
                {
                    _agent.ResetPath();
                    _destination = null;
                }

                return;
            }

            if (_detour.HasValue)
            {
                if (Time.time < _detourEndsAt && HorizontalDistance(GroundPosition, _detour.Value) > EnemyBrain.ArrivalDistance)
                {
                    return;
                }

                _detour = null;
                _destination = null;
            }

            var destination = _goal.Value;
            if (_destination.HasValue && (destination - _destination.Value).sqrMagnitude < RepathDistance * RepathDistance)
            {
                return;
            }

            _destination = destination;
            _agent.isStopped = false;
            if (!_agent.SetDestination(destination))
            {
                Debug.LogWarning($"{LogTag} id={Id} cannot path to {destination}", this);
            }
        }

        /// <summary>
        /// NavMesh から外れていたら近くの NavMesh に戻す。戻れなければ <see cref="OffMeshRetryInterval"/> ごとに試し直し、
        /// 警告は 1 回だけ出す (戻れないまま進めずにいれば、<see cref="WatchProgress"/> が出現地点へ瞬間移動させる)。
        /// 戻れたら (または外れていなければ) true。
        /// </summary>
        private bool TryReturnToNavMesh()
        {
            if (_agent.isOnNavMesh)
            {
                return true;
            }

            if (Time.time < _nextOffMeshRetry)
            {
                return false;
            }

            _nextOffMeshRetry = Time.time + OffMeshRetryInterval;
            if (NavMesh.SamplePosition(GroundPosition, out var hit, OffMeshSnapRadius, NavMesh.AllAreas) && _agent.Warp(hit.position))
            {
                Debug.LogWarning($"{LogTag} id={Id} returned to NavMesh from={GroundPosition.ToString("F1")} to={hit.position.ToString("F1")}", this);
                _destination = null;
                _warnedOffMesh = false;
                return true;
            }

            if (!_warnedOffMesh)
            {
                Debug.LogWarning($"{LogTag} id={Id} が NavMesh の上にいないので移動できません。近くに NavMesh が無いので、進めないままなら出現地点へ戻します。", this);
                _warnedOffMesh = true;
            }

            return false;
        }

        /// <summary>
        /// 位置が NaN や無限大になっていたら、最後に正常だった位置へ戻す。戻せなかったらこのフレームは何もしない (false)。
        /// </summary>
        private bool RecoverNonFinitePosition()
        {
            var position = transform.position;
            if (StuckMonitor.IsFinite(position.ToNumerics()))
            {
                _lastFinitePosition = position;
                return true;
            }

            Debug.LogWarning($"{LogTag} id={Id} position was not finite ({position}); restoring {_lastFinitePosition.ToString("F1")}", this);
            transform.position = _lastFinitePosition;
            _destination = null;
            _detour = null;
            _stuck.Reset();
            return StuckMonitor.IsFinite(transform.position.ToNumerics());
        }

        /// <summary>進めているかを見張り、進めていなければ <see cref="StuckMonitor"/> の返した立て直しをする。</summary>
        private void WatchProgress(float deltaSeconds)
        {
            // 寄り道中も元の目的地に近づけたかで測る。寄り道先で測ると、寄り道しただけで立ち直ったことになる。
            var ground = GroundPosition;
            var heading = _goal.HasValue && HorizontalDistance(ground, _goal.Value) > EnemyBrain.ArrivalDistance
                ? _goal
                : null;
            // 経路の角の取得は毎回ヒープを確保するので、経路の向きを使う立て直し中だけ計算する。
            var waypoint = _stuck.Stage == StuckRecovery.None ? null : PathWaypoint(heading);
            var recovery = _stuck.Tick(
                deltaSeconds,
                ground.ToNumerics(),
                heading.HasValue ? heading.Value.ToNumerics() : (System.Numerics.Vector3?)null,
                waypoint.HasValue ? waypoint.Value.ToNumerics() : (System.Numerics.Vector3?)null);
            if (recovery == StuckRecovery.None)
            {
                return;
            }

            var path = _agent.isOnNavMesh ? _agent.pathStatus.ToString() : "OffMesh";
            Debug.Log(FormatStuckLog(Id, recovery, _brain.State, _stuck.TotalStalledSeconds, ground, heading, path), this);
            switch (recovery)
            {
                case StuckRecovery.Repath:
                    Repath();
                    break;
                case StuckRecovery.Sidestep:
                    Sidestep(ground, heading);
                    break;
                case StuckRecovery.AbandonGoal:
                    _brain.AbandonGoal();
                    Repath();
                    break;
                case StuckRecovery.Warp:
                    Warp(ground);
                    break;
            }
        }

        /// <summary>
        /// 目的地への今の経路で、<see cref="StuckProgressDistance"/> 以上先にある最初の角 (無ければ最後の角)。
        /// 寄り道中は経路の終点が寄り道先なので null。寄り道先へ歩いただけで立ち直ったことにしないため。
        /// </summary>
        private Vector3? PathWaypoint(Vector3? goal)
        {
            if (!goal.HasValue || _detour.HasValue || !_agent.isOnNavMesh || !_agent.hasPath || _agent.pathPending)
            {
                return null;
            }

            var corners = _agent.path.corners;
            if (corners.Length == 0)
            {
                return null;
            }

            var ground = GroundPosition;
            foreach (var corner in corners)
            {
                if (HorizontalDistance(ground, corner) >= StuckProgressDistance)
                {
                    return corner;
                }
            }

            return corners[corners.Length - 1];
        }

        private void Repath()
        {
            _detour = null;
            _destination = null;
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }

        /// <summary>
        /// 近くの、経路が最後までつながる地点へ少しだけ寄り道させる。見つからなければ経路の引き直しだけにする。
        /// 目的地へ <see cref="StuckProgressDistance"/> 以上近づく地点は選ばない。選ぶと、寄り道しただけで
        /// <see cref="StuckMonitor"/> が立ち直ったとみなし、届かない目的地でも段階が先へ進まなくなる。
        /// </summary>
        private void Sidestep(Vector3 ground, Vector3? goal)
        {
            Repath();
            if (!_agent.isOnNavMesh)
            {
                return;
            }

            var path = new NavMeshPath();
            for (var i = 0; i < SidestepAttempts; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * SidestepRadius;
                var candidate = ground + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, SidestepRadius, NavMesh.AllAreas)
                    || HorizontalDistance(ground, hit.position) < SidestepMinDistance
                    || (goal.HasValue && MovesTowardGoal(ground, hit.position, goal.Value))
                    || !_agent.CalculatePath(hit.position, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                _detour = hit.position;
                _detourEndsAt = Time.time + SidestepSeconds;
                _agent.isStopped = false;
                _agent.SetPath(path);
                return;
            }
        }

        // StuckMonitor の寄り道中の判定 (基準点から目的地の方向への射影) と同じ基準にそろえる。
        private static bool MovesTowardGoal(Vector3 from, Vector3 to, Vector3 goal)
        {
            var toGoal = goal - from;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude <= 0f)
            {
                return false;
            }

            var move = to - from;
            move.y = 0f;
            return Vector3.Dot(move, toGoal.normalized) >= StuckProgressDistance;
        }

        /// <summary>
        /// 経路上で <see cref="WarpLookAhead"/> より先の角へ瞬間移動させる。そういう角が無く NavMesh からも外れていれば出現地点へ戻す。
        /// どちらでもなければ (目的地のすぐ手前で詰まっているだけなら) 動かさない。
        /// </summary>
        private void Warp(Vector3 ground)
        {
            Vector3? to = null;
            if (_agent.isOnNavMesh && _agent.hasPath)
            {
                foreach (var corner in _agent.path.corners)
                {
                    if (HorizontalDistance(ground, corner) > WarpLookAhead)
                    {
                        to = corner;
                        break;
                    }
                }
            }
            else if (!_agent.isOnNavMesh)
            {
                to = _spawnPosition;
            }

            Repath();
            if (!to.HasValue)
            {
                Debug.Log($"{LogTag} id={Id} warp skipped: no point ahead on the path", this);
                return;
            }

            if (!_agent.Warp(to.Value))
            {
                Debug.LogWarning($"{LogTag} id={Id} warp to {to.Value.ToString("F1")} failed", this);
                return;
            }

            // 詰まった地点で測った基準のままだと、目的地から遠い出現地点へ戻した後は普通に歩けていても立て直しが走り続ける。
            _stuck.Reset();
            Debug.Log($"{LogTag} id={Id} warped from={ground.ToString("F1")} to={to.Value.ToString("F1")}", this);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void Face(System.Numerics.Vector3? lookAt, float deltaSeconds)
        {
            if (!lookAt.HasValue)
            {
                // 向きの指定が無ければ、NavMeshAgent に進む向きを向かせる。
                _agent.updateRotation = true;
                return;
            }

            _agent.updateRotation = false;
            var direction = lookAt.Value.ToUnity() - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var goal = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, _turnSpeed * deltaSeconds);
        }
    }
}
