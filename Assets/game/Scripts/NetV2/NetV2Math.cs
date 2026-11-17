namespace Shooter.NetV2
{
    internal static class NetV2Math
    {
        public static UnityEngine.Vector3 ToUnity(System.Numerics.Vector3 v)
        {
            return new UnityEngine.Vector3(v.X, v.Y, v.Z);
        }

        public static System.Numerics.Vector3 ToNumerics(UnityEngine.Vector3 v)
        {
            return new System.Numerics.Vector3(v.x, v.y, v.z);
        }

        public static System.Numerics.Vector2 ToNumerics(UnityEngine.Vector2 v)
        {
            return new System.Numerics.Vector2(v.x, v.y);
        }

        public static UnityEngine.Quaternion ToUnity(System.Numerics.Quaternion q)
        {
            return new UnityEngine.Quaternion(q.X, q.Y, q.Z, q.W);
        }

        public static System.Numerics.Quaternion ToNumerics(UnityEngine.Quaternion q)
        {
            return new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);
        }

        public static UnityEngine.Quaternion YawRotation(float yaw)
        {
            return UnityEngine.Quaternion.Euler(0f, yaw * UnityEngine.Mathf.Rad2Deg, 0f);
        }
    }
}
