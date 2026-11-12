namespace Fps.Runtime
{
    /// <summary>
    /// Unity のベクトルと <see cref="Fps.Core"/> が使う <see cref="System.Numerics.Vector3"/> の相互変換。
    /// 座標系は同じ (Y が上、左手系) なので成分をそのまま写す。
    /// </summary>
    public static class VectorConversions
    {
        public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 v)
        {
            return new System.Numerics.Vector3(v.x, v.y, v.z);
        }

        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 v)
        {
            return new UnityEngine.Vector3(v.X, v.Y, v.Z);
        }
    }
}
