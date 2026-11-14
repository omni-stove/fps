using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 武器のモデルの中の、演出を出す位置 (ソケット)。モデルの配下 (自分を含む) から <see cref="MuzzleName"/> と
    /// <see cref="ShellEjectName"/> の名前の Transform を探して覚え、+Z をその向きとして使う。
    /// ソケットが無い (モデルが無い・その名前の子が無い・破棄された) ときは、弾の始点 (目) から右・下・前へ
    /// <see cref="DefaultMuzzleOffset"/> と <see cref="DefaultShellEjectOffset"/> だけずらした既定の位置を返す。
    /// </summary>
    /// <remarks>
    /// 探すのは作ったときの 1 回だけ。武器のモデルを差し替えたら作り直す。
    /// </remarks>
    public sealed class WeaponSockets
    {
        /// <summary>銃口のソケットの名前。マズルフラッシュと曳光弾をここから出す。</summary>
        public const string MuzzleName = "Socket_Muzzle";

        /// <summary>排莢口のソケットの名前。薬莢を +Z の向きへ飛ばす。</summary>
        public const string ShellEjectName = "Socket_ShellEject";

        /// <summary>
        /// 銃口のソケットが無いときの位置を、弾の始点 (目) からの右・下・前の距離 (m) で決める。銃のモデルが無いので、
        /// 一人称で炎が視界の真ん中をふさがないよう右下にずらす。
        /// </summary>
        public static readonly Vector3 DefaultMuzzleOffset = new Vector3(0.15f, -0.12f, 0.6f);

        /// <summary>
        /// 排莢口のソケットが無いときの位置を、弾の始点 (目) からの右・下・前の距離 (m) で決める。銃口より手前の、銃の右側。
        /// </summary>
        public static readonly Vector3 DefaultShellEjectOffset = new Vector3(0.18f, -0.1f, 0.3f);

        /// <summary>ソケットを持たない (どちらも既定の位置を返す) もの。</summary>
        public static readonly WeaponSockets None = new WeaponSockets(null);

        /// <param name="modelRoot">武器のモデルの根。null ならソケットを持たない。</param>
        public WeaponSockets(Transform modelRoot)
        {
            ModelRoot = modelRoot;
            Muzzle = FindSocket(modelRoot, MuzzleName);
            ShellEject = FindSocket(modelRoot, ShellEjectName);
        }

        /// <summary>探した武器のモデルの根。無ければ null。</summary>
        public Transform ModelRoot { get; }

        /// <summary>銃口のソケット。無いか破棄されていれば null。</summary>
        public Transform Muzzle { get; }

        /// <summary>排莢口のソケット。無いか破棄されていれば null。</summary>
        public Transform ShellEject { get; }

        /// <summary>
        /// 銃口の位置と向き (+Z が銃口の向き)。ソケットがあればその位置と回転、無ければ origin から direction へ撃ったときの
        /// <see cref="DefaultMuzzleOffset"/> の位置と direction の向き。
        /// </summary>
        public Pose MuzzlePose(Vector3 origin, Vector3 direction)
        {
            return SocketPose(Muzzle, origin, direction, DefaultMuzzleOffset, false);
        }

        /// <summary>
        /// 排莢口の位置と向き (+Z が薬莢を飛ばす向き)。ソケットがあればその位置と回転、無ければ origin から direction へ撃ったときの
        /// <see cref="DefaultShellEjectOffset"/> の位置と、撃つ向きから見て水平な右の向き。
        /// </summary>
        public Pose ShellEjectPose(Vector3 origin, Vector3 direction)
        {
            return SocketPose(ShellEject, origin, direction, DefaultShellEjectOffset, true);
        }

        /// <summary>
        /// 弾の始点 origin から direction へ撃ったときに、direction を前、水平な右を右として offset だけずらした位置。
        /// 真上か真下へ撃ったときは水平な右が決まらないので、ずらすのは前だけにする。
        /// </summary>
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

        /// <summary>root とその子孫 (無効なものを含む) から、名前が name と一致する (大文字小文字を区別する) 最初の Transform を探す。無ければ null。</summary>
        /// <exception cref="ArgumentException">name が空。</exception>
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
