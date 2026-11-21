using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    public sealed partial class DeathBodyPresenter
    {
        private void HideBody(Renderer[] renderers)
        {
            foreach (var bodyRenderer in renderers)
            {
                if (bodyRenderer != null)
                {
                    bodyRenderer.enabled = false;
                }
            }

            // CharacterController は移動の持ち主 (PlayerController やネット対戦の本体) が有効・無効を決める
            foreach (var bodyCollider in GetComponentsInChildren<Collider>(true))
            {
                if (bodyCollider != null && !(bodyCollider is CharacterController))
                {
                    bodyCollider.enabled = false;
                }
            }
        }

        private GameObject BuildShell(Renderer[] renderers, out int parts)
        {
            parts = 0;
            var shell = new GameObject(Id + ShellSuffix);
            try
            {
                shell.layer = ShellLayer;
                if (gameObject.scene.IsValid() && shell.scene != gameObject.scene)
                {
                    SceneManager.MoveGameObjectToScene(shell, gameObject.scene);
                }

                shell.transform.SetPositionAndRotation(transform.position, transform.rotation);

                var shellColliders = new List<Collider>();
                foreach (var source in renderers)
                {
                    if (source != null && IsShownWithBody(source.transform) && TryCopyPart(source, shell.transform, shellColliders))
                    {
                        parts++;
                    }
                }

                if (parts == 0)
                {
                    DestroySafely(shell);
                    return null;
                }

                var body = shell.AddComponent<Rigidbody>();
                body.mass = ShellMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                IgnoreBodyColliders(shellColliders);
                return shell;
            }
            catch
            {
                DestroySafely(shell);
                foreach (var mesh in _bakedMeshes)
                {
                    DestroySafely(mesh);
                }

                _bakedMeshes.Clear();
                throw;
            }
        }

        // 体そのもののアクティブを見ないのは、倒れた体を非アクティブにする購読者 (KillTargetObjective) が先に走っても、
        // 抜け殻を倒れる前の見た目で作るため。
        private bool IsShownWithBody(Transform part)
        {
            for (var current = part; current != null && current != transform; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryCopyPart(Renderer source, Transform shell, List<Collider> shellColliders)
        {
            if (!TryGetPartMesh(source, out var mesh, out var scale))
            {
                return false;
            }

            var part = CreateDetachedPart(source, shell, scale);
            AddMeshCopy(part, mesh, source);

            // 丸い体 (Capsule など) でも転がり続けないよう、箱で受ける
            var bounds = mesh.bounds;
            var box = part.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, MinSizeIn(scale));
            shellColliders.Add(box);
            return true;
        }

        private bool TryGetPartMesh(Renderer source, out Mesh mesh, out Vector3 worldScale)
        {
            mesh = null;
            worldScale = Vector3.one;
            if (source is SkinnedMeshRenderer skinned)
            {
                if (skinned.sharedMesh == null)
                {
                    return false;
                }

                // 倒れた瞬間の姿勢で固める。拡縮も焼き込むので、部品の拡縮は 1 にする
                mesh = new Mesh { name = skinned.sharedMesh.name + ShellSuffix };
                skinned.BakeMesh(mesh, true);
                _bakedMeshes.Add(mesh);
                return true;
            }

            if (source is MeshRenderer)
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    return false;
                }

                mesh = filter.sharedMesh;
                worldScale = source.transform.lossyScale;
                return true;
            }

            return false;
        }

        private static GameObject CreateDetachedPart(Renderer source, Transform parent, Vector3 worldScale)
        {
            var part = new GameObject(source.name);
            part.layer = ShellLayer;
            part.transform.SetParent(parent, false);
            part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var parentScale = parent.lossyScale;
            part.transform.localScale = new Vector3(
                worldScale.x / NonZero(parentScale.x),
                worldScale.y / NonZero(parentScale.y),
                worldScale.z / NonZero(parentScale.z));
            return part;
        }

        private static float NonZero(float value)
        {
            return Mathf.Abs(value) < 1e-4f ? (value < 0f ? -1e-4f : 1e-4f) : value;
        }

        private static void AddMeshCopy(GameObject host, Mesh mesh, Renderer source)
        {
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = host.AddComponent<MeshRenderer>();
            CopyLook(copy, source);
        }

        private static void CopyLook(Renderer copy, Renderer source)
        {
            copy.sharedMaterials = source.sharedMaterials;

            // 一人称の持ち主は自分の体を影だけにしているが、抜け殻は誰からも見えるようにする
            copy.shadowCastingMode = ShadowCastingMode.On;
            copy.receiveShadows = source.receiveShadows;
        }

        private static Vector3 MinSizeIn(Vector3 scale)
        {
            return new Vector3(
                MinColliderSize / Mathf.Max(Mathf.Abs(scale.x), 1e-4f),
                MinColliderSize / Mathf.Max(Mathf.Abs(scale.y), 1e-4f),
                MinColliderSize / Mathf.Max(Mathf.Abs(scale.z), 1e-4f));
        }

        // 抜け殻は体と同じ場所に重ねて作るので、体に残っている有効なコライダー (CharacterController) とぶつかると弾き飛ばされる。
        // 止めたコライダーとは組めない (Unity がエラーを出す) ので外す。
        private void IgnoreBodyColliders(List<Collider> shellColliders)
        {
            foreach (var bodyCollider in GetComponentsInChildren<Collider>(false))
            {
                if (bodyCollider == null || !bodyCollider.enabled)
                {
                    continue;
                }

                foreach (var shellCollider in shellColliders)
                {
                    Physics.IgnoreCollision(bodyCollider, shellCollider, true);
                }
            }
        }
    }
}
