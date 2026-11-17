namespace Shooter.Runtime
{
    // 座標系の変換はせず成分をそのまま写す。Y が上・左手系の解釈は呼び出し側の約束。
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
