namespace Fps.Net
{
    /// <summary>Unity のベクトルと <see cref="System.Numerics"/> のベクトル (Fps.Core が使う) の変換と検査。</summary>
    public static class NetMath
    {
        public static System.Numerics.Vector2 ToNumerics(UnityEngine.Vector2 v)
        {
            return new System.Numerics.Vector2(v.x, v.y);
        }

        public static System.Numerics.Vector3 ToNumerics(UnityEngine.Vector3 v)
        {
            return new System.Numerics.Vector3(v.x, v.y, v.z);
        }

        public static UnityEngine.Vector3 ToUnity(System.Numerics.Vector3 v)
        {
            return new UnityEngine.Vector3(v.X, v.Y, v.Z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(UnityEngine.Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }
    }
}
