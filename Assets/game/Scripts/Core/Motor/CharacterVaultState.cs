using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 軌跡は <see cref="VaultProbe.PlanPath"/> が作る形 (始点が 0 の 4 頂点、速さ <see cref="VaultProbe.Speed"/>) に限り、始点以外の 3 頂点だけを持つ。
    /// 軌跡を作るときの当たりの検査は始めた tick にしか行わないので、巻き戻した tick から再シミュレーションしても頂点を持っていれば同じ道をたどれる。
    /// </summary>
    public readonly struct CharacterVaultState : IEquatable<CharacterVaultState>
    {
        public const int PointCount = 4;

        private CharacterVaultState(Vector3 rise, Vector3 cross, Vector3 land, float elapsed)
        {
            IsVaulting = true;
            Rise = rise;
            Cross = cross;
            Land = land;
            Elapsed = elapsed;
        }

        public static CharacterVaultState None => default;

        public bool IsVaulting { get; }

        public Vector3 Rise { get; }

        public Vector3 Cross { get; }

        public Vector3 Land { get; }

        public float Elapsed { get; }

        public bool IsValid => IsFinite(Rise) && IsFinite(Cross) && IsFinite(Land) && IsFinite(Elapsed) && Elapsed >= 0f;

        public static CharacterVaultState Following(VaultPath path, float elapsed)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var points = path.Points;
            if (points.Count != PointCount || points[0] != Vector3.Zero || !path.Speed.Equals(VaultProbe.Speed))
            {
                throw new ArgumentException("path must be planned by VaultProbe.PlanPath", nameof(path));
            }

            RequireElapsed(elapsed);
            return new CharacterVaultState(points[1], points[2], points[3], elapsed);
        }

        public static CharacterVaultState FromPoints(Vector3 rise, Vector3 cross, Vector3 land, float elapsed)
        {
            if (!IsFinite(rise) || !IsFinite(cross) || !IsFinite(land))
            {
                throw new ArgumentOutOfRangeException(nameof(rise), "vault points must be finite");
            }

            RequireElapsed(elapsed);
            return new CharacterVaultState(rise, cross, land, elapsed);
        }

        public CharacterVaultState WithElapsed(float elapsed)
        {
            if (!IsVaulting)
            {
                throw new InvalidOperationException("not vaulting");
            }

            RequireElapsed(elapsed);
            return new CharacterVaultState(Rise, Cross, Land, elapsed);
        }

        public bool HasSamePath(VaultPath path)
        {
            if (!IsVaulting || path == null || path.Points.Count != PointCount)
            {
                return false;
            }

            var points = path.Points;
            return points[1].Equals(Rise) && points[2].Equals(Cross) && points[3].Equals(Land);
        }

        public VaultPath ToPath()
        {
            if (!IsVaulting)
            {
                throw new InvalidOperationException("not vaulting");
            }

            return new VaultPath(new[] { Vector3.Zero, Rise, Cross, Land }, VaultProbe.Speed);
        }

        public bool Equals(CharacterVaultState other)
        {
            return IsVaulting == other.IsVaulting
                && Rise.Equals(other.Rise)
                && Cross.Equals(other.Cross)
                && Land.Equals(other.Land)
                && Elapsed.Equals(other.Elapsed);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterVaultState other && Equals(other);
        }

        public static bool operator ==(CharacterVaultState left, CharacterVaultState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterVaultState left, CharacterVaultState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (IsVaulting ? 1 : 0) ^ (Rise.GetHashCode() * 397) ^ Cross.GetHashCode() ^ Land.GetHashCode() ^ Elapsed.GetHashCode();
            }
        }

        private static void RequireElapsed(float elapsed)
        {
            if (!(elapsed >= 0f) || float.IsInfinity(elapsed))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "elapsed must be finite and not negative");
            }
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
