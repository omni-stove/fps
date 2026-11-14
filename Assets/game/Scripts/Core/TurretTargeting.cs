using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    public static class TurretTargeting
    {
        private static readonly Side _ours = Side.Alpha;
        private static readonly Side _theirs = Side.Bravo;

        public static bool IsAlly(bool isOwner, Side? turretSide, Side? otherSide)
        {
            return isOwner || (turretSide.HasValue && otherSide.HasValue && turretSide.Value == otherSide.Value);
        }

        // 射撃場のように陣営を持たない場では、bot (敵の AI) とプレイヤーの 2 つに分かれているとみなす。
        // 陣営が片方でも分からなければ、相手が bot のときだけ敵にする (陣営の無いプレイヤーや標的ダミーを撃たない)。
        public static bool IsHostile(bool isOwner, Side? turretSide, Side? otherSide, bool otherIsBot)
        {
            if (IsAlly(isOwner, turretSide, otherSide))
            {
                return false;
            }

            if (turretSide.HasValue && otherSide.HasValue)
            {
                return true;
            }

            return otherIsBot;
        }

        // オフラインには撃った本人へ跳ね返す道筋が無いので、Reflect は Off と同じく撃たずに待つ (味方に当てない) 側に倒す。
        public static bool HoldsFire(FriendlyFire rule, bool allyInLine)
        {
            if (!Enum.IsDefined(typeof(FriendlyFire), rule))
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "rule is not a defined FriendlyFire");
            }

            return allyInLine && rule != FriendlyFire.On;
        }

        // 選び方 (いちばん近い・同じ距離なら先の候補・倒れた相手と位置の壊れた相手は選ばない) を HostileSelector と揃えるため、
        // 敵かどうかを陣営に置き換えて HostileSelector に渡す。陣営の無い相手もいるので、本当の陣営は渡せない。
        public static int Nearest(Vector3 from, IReadOnlyList<TargetOption> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var candidates = new HostileCandidate[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                candidates[i] = new HostileCandidate(option.IsHostile ? _theirs : _ours, option.Position, option.IsAlive);
            }

            return HostileSelector.Nearest(_ours, from, candidates);
        }

        public static float StepYaw(float currentDegrees, float targetDegrees, float maxStepDegrees)
        {
            RequireFinite(currentDegrees, nameof(currentDegrees));
            RequireFinite(targetDegrees, nameof(targetDegrees));
            if (!(maxStepDegrees >= 0f) || float.IsInfinity(maxStepDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(maxStepDegrees), maxStepDegrees, "maxStepDegrees must not be negative and must be finite");
            }

            var delta = DeltaDegrees(currentDegrees, targetDegrees);
            if (Math.Abs(delta) <= maxStepDegrees)
            {
                return Normalize(currentDegrees + delta);
            }

            return Normalize(currentDegrees + (Math.Sign(delta) * maxStepDegrees));
        }

        // -180 より大きく 180 以下。
        public static float DeltaDegrees(float fromDegrees, float toDegrees)
        {
            RequireFinite(fromDegrees, nameof(fromDegrees));
            RequireFinite(toDegrees, nameof(toDegrees));
            var delta = (toDegrees - fromDegrees) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta <= -180f)
            {
                delta += 360f;
            }

            return delta;
        }

        private static float Normalize(float degrees)
        {
            var normalized = degrees % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static void RequireFinite(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be finite");
            }
        }
    }
}
