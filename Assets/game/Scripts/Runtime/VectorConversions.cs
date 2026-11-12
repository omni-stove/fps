namespace Fps.Runtime
{
    /// <summary>
    /// Fps.Core が使う <see cref="System.Numerics"/> のベクトルと UnityEngine のベクトルを相互に変換する。
    /// どちらも同じ成分 (Y が上、左手系の解釈は呼び出し側の約束) をそのまま写すだけで、座標系の変換はしない。
    /// </summary>
    public static class VectorConversions
    {
        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 value)
        {
            return new UnityEngine.Vector3(value.X, value.Y, value.Z);
        }

        public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 value)
        {
            return new System.Numerics.Vector3(value.x, value.y, value.z);
        }

        public static UnityEngine.Vector2 ToUnity(this System.Numerics.Vector2 value)
        {
            return new UnityEngine.Vector2(value.X, value.Y);
        }

        public static System.Numerics.Vector2 ToNumerics(this UnityEngine.Vector2 value)
        {
            return new System.Numerics.Vector2(value.x, value.y);
        }
    }
}
