using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class ContextMeleeSelector
    {
        public const float DefaultBehindArcDegrees = 90f;

        private const float _minLengthSquared = 1e-8f;

        // 同じ優先度なら先に登録したものを前に置きたいが、List.Sort は安定でないので、差し込む位置で並びを保つ。
        private readonly List<ContextMeleeMove> _moves = new List<ContextMeleeMove>();

        public IReadOnlyList<ContextMeleeMove> Moves => _moves;

        public void Register(ContextMeleeMove move)
        {
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            Unregister(move.Id);
            var index = _moves.Count;
            for (var i = 0; i < _moves.Count; i++)
            {
                if (_moves[i].Priority < move.Priority)
                {
                    index = i;
                    break;
                }
            }

            _moves.Insert(index, move);
        }

        public bool Unregister(string id)
        {
            var index = IndexOf(id);
            if (index < 0)
            {
                return false;
            }

            _moves.RemoveAt(index);
            return true;
        }

        public ContextMeleeMove Find(string id)
        {
            var index = IndexOf(id);
            return index >= 0 ? _moves[index] : null;
        }

        public ContextMeleeMove Select(ContextMeleeSituation situation)
        {
            foreach (var move in _moves)
            {
                if (Matches(move, situation))
                {
                    return move;
                }
            }

            return null;
        }

        public float MaxReach()
        {
            var reach = 0f;
            foreach (var move in _moves)
            {
                reach = Math.Max(reach, move.Reach);
            }

            return reach;
        }

        public static bool Matches(ContextMeleeMove move, ContextMeleeSituation situation)
        {
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            return situation.Has(move.Required)
                && (situation.Conditions & move.Excluded) == ContextMeleeConditions.None
                && situation.Distance <= move.Reach;
        }

        // 向きが水平に決まらない (真上・真下を向いている、重なっている) ときは背後とみなさない。背後からの技を誤って出さないように。
        public static bool IsBehind(Vector3 targetPosition, Vector3 targetForward, Vector3 attackerPosition, float arcDegrees)
        {
            if (!IsFinite(targetPosition) || !IsFinite(targetForward) || !IsFinite(attackerPosition) || !(arcDegrees > 0f))
            {
                return false;
            }

            if (!TryFlatten(targetForward, out var forward) || !TryFlatten(attackerPosition - targetPosition, out var toAttacker))
            {
                return false;
            }

            var halfArc = Math.Min(arcDegrees, MeleeSpec.MaxArcDegrees) / 2.0;
            return Vector3.Dot(-forward, toAttacker) >= Math.Cos(halfArc * Math.PI / 180.0) - 1e-6;
        }

        // 判断が見張りのままでも、発見ゲージが気づきに届いていれば相手はこちらを向き始めているので、気づいていないとはみなさない。
        public static bool IsUnaware(EnemyState state, bool unaware)
        {
            return (state == EnemyState.Idle || state == EnemyState.Patrol) && unaware;
        }

        // 相手の正面から stand 離れた点ではなく、今いる向きのまま相手に寄った点にする。回り込ませると壁や他の敵にめり込みやすい。
        public static Vector3 StandPoint(Vector3 attackerPosition, Vector3 targetPosition, Vector3 targetForward, float standDistance)
        {
            if (!(standDistance >= 0f) || float.IsInfinity(standDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(standDistance), standDistance, "standDistance must be a non-negative finite number");
            }

            if (!TryFlatten(attackerPosition - targetPosition, out var away) && !TryFlatten(targetForward, out away))
            {
                away = Vector3.UnitZ;
            }

            var point = targetPosition + (away * standDistance);
            return new Vector3(point.X, attackerPosition.Y, point.Z);
        }

        private int IndexOf(string id)
        {
            for (var i = 0; i < _moves.Count; i++)
            {
                if (string.Equals(_moves[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryFlatten(Vector3 value, out Vector3 flat)
        {
            var horizontal = new Vector3(value.X, 0f, value.Z);
            var lengthSquared = horizontal.LengthSquared();
            if (!(lengthSquared >= _minLengthSquared) || float.IsInfinity(lengthSquared))
            {
                flat = Vector3.Zero;
                return false;
            }

            flat = horizontal / (float)Math.Sqrt(lengthSquared);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
