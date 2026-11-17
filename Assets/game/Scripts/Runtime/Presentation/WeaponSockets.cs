using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // 探すのは作ったときの 1 回だけなので、武器のモデルを差し替えたり、付けた物 (WeaponAttachmentModels) を付け直したりしたら作り直す。
    public sealed class WeaponSockets
    {
        public const string MuzzleName = "Socket_Muzzle";

        public const string ShellEjectName = "Socket_ShellEject";

        public const string SightName = "Socket_Sight";

        // 銃のモデルが無いので、一人称で炎が視界の真ん中をふさがないよう右下にずらす。
        public static readonly Vector3 DefaultMuzzleOffset = new Vector3(0.15f, -0.12f, 0.6f);

        public static readonly Vector3 DefaultShellEjectOffset = new Vector3(0.18f, -0.1f, 0.3f);

        public static readonly WeaponSockets None = new WeaponSockets(null);

        public WeaponSockets(Transform modelRoot)
        {
            ModelRoot = modelRoot;
            Muzzle = FindPreferringAttachments(modelRoot, MuzzleName);
            ShellEject = FindPreferringAttachments(modelRoot, ShellEjectName);
            Sight = FindPreferringAttachments(modelRoot, SightName);
        }

        public Transform ModelRoot { get; }

        public Transform Muzzle { get; }

        public Transform ShellEject { get; }

        public Transform Sight { get; }

        public Pose MuzzlePose(Vector3 origin, Vector3 direction)
        {
            return SocketPose(Muzzle, origin, direction, DefaultMuzzleOffset, false);
        }

        public Pose ShellEjectPose(Vector3 origin, Vector3 direction)
        {
            return SocketPose(ShellEject, origin, direction, DefaultShellEjectOffset, true);
        }

        // 真上か真下へ撃ったときは水平な右が決まらないので、ずらすのは前だけにする。
        public static Vector3 OffsetPosition(Vector3 origin, Vector3 direction, Vector3 offset)
        {
            var forward = Forward(direction);
            if (!TryRight(forward, out var right))
            {
                return origin + (forward * offset.z);
            }

            var up = Vector3.Cross(forward, right);
            return origin + (right * offset.x) + (up * offset.y) + (forward * offset.z);
        }

        public static Transform FindSocket(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("socket name must not be empty", nameof(name));
            }

            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindSocket(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // 消音器の先の銃口や照準器の照準の点のように、付けた物 (WeaponAttachmentModel) の中のソケットが武器のモデルのものより優先する。後に付けた物ほど優先する
        private static Transform FindPreferringAttachments(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            var attached = root.GetComponentsInChildren<WeaponAttachmentModel>(true);
            for (var i = attached.Length - 1; i >= 0; i--)
            {
                var found = FindSocket(attached[i].transform, name);
                if (found != null)
                {
                    return found;
                }
            }

            return FindSocket(root, name);
        }

        private static Pose SocketPose(Transform socket, Vector3 origin, Vector3 direction, Vector3 offset, bool faceRight)
        {
            if (socket != null)
            {
                return new Pose(socket.position, socket.rotation);
            }

            var forward = Forward(direction);
            var facing = faceRight && TryRight(forward, out var right) ? right : forward;
            return new Pose(OffsetPosition(origin, direction, offset), Quaternion.LookRotation(facing));
        }

        private static Vector3 Forward(Vector3 direction)
        {
            return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
        }

        private static bool TryRight(Vector3 forward, out Vector3 right)
        {
            right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            right.Normalize();
            return true;
        }
    }
}
