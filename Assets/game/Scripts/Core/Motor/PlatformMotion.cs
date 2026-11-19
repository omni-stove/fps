using System;
using System.Numerics;

namespace Shooter.Core
{
    // default を動かない床と同じにするのは、予測の記録の空きや床に乗っていない入力で体を動かさないため。
    public readonly struct PlatformMotion : IEquatable<PlatformMotion>
    {
        public PlatformMotion(Vector3 pivot, Vector3 translation, float yawDelta)
        {
            RequireFinite(pivot, nameof(pivot));
            RequireFinite(translation, nameof(translation));
            if (!IsFinite(yawDelta))
            {
                throw new ArgumentOutOfRangeException(nameof(yawDelta), yawDelta, "yawDelta must be finite");
            }

            Pivot = pivot;
            Translation = translation;
            YawDelta = yawDelta;
        }

        public static PlatformMotion None => default;

        public Vector3 Pivot { get; }

        public Vector3 Translation { get; }

        // 向きを CharacterMotorSimulation.YawForward と揃えるのは、Turn で回した視点の前と、運ばれた体の向きが食い違わないようにするため。
        public float YawDelta { get; }

        public bool IsStill => Translation == Vector3.Zero && YawDelta == 0f;

        public Vector3 Carry(Vector3 point)
        {
            var offset = new Vector3(point.X - Pivot.X, point.Y - Pivot.Y, point.Z - Pivot.Z);
            var turned = RotateY(offset, YawDelta);
            return new Vector3(
                Pivot.X + Translation.X + turned.X,
                Pivot.Y + Translation.Y + turned.Y,
                Pivot.Z + Translation.Z + turned.Z);
        }

        public Vector3 Displacement(Vector3 point)
        {
            var carried = Carry(point);
            return new Vector3(carried.X - point.X, carried.Y - point.Y, carried.Z - point.Z);
        }

        // シミュレーションは向きを書けないので、視点を持つ呼び手がこれで回す。
        public float Turn(float yaw)
        {
            return yaw + YawDelta;
        }

        // Unity の Quaternion.Euler(0, angle, 0) * v と同じ向きに回す。足す順を書き下すのは、予測とサーバーで結果のビットを揃えるため。
        public static Vector3 RotateY(Vector3 v, float angle)
        {
            if (angle == 0f)
            {
                return v;
            }

            var sin = MathF.Sin(angle);
            var cos = MathF.Cos(angle);
            return new Vector3((v.X * cos) + (v.Z * sin), v.Y, (v.Z * cos) - (v.X * sin));
        }

        public bool Equals(PlatformMotion other)
        {
            return Pivot.Equals(other.Pivot) && Translation.Equals(other.Translation) && YawDelta.Equals(other.YawDelta);
        }

        public override bool Equals(object obj)
        {
            return obj is PlatformMotion other && Equals(other);
        }

        public static bool operator ==(PlatformMotion left, PlatformMotion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlatformMotion left, PlatformMotion right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Pivot, Translation, YawDelta);
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
