using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    public sealed partial class DeathBodyPresenter
    {
        private GameObject TryPresentRagdoll(Renderer[] renderers, Vector3? hitDirection, int damage)
        {
            if (!_useRagdoll)
            {
                return null;
            }

            var catalog = ResolveRagdollCatalog();
            if (catalog == null)
            {
                return null;
            }

            try
            {
                catalog.Validate();
            }
            catch (RagdollCatalogException e)
            {
                if (_warnedInvalidRagdollCatalogs.Add(catalog))
                {
                    Debug.LogWarning($"{LogTag} ragdoll skipped id={Id} reason=invalid catalog: {e.Message}", this);
                }

                return null;
            }

            var rigRoot = RagdollRoot();
            if (!TryFindRagdollBones(rigRoot, catalog, out var bones, out var missing))
            {
                Debug.Log($"{LogTag} ragdoll skipped id={Id} reason=bone '{missing}' not found under '{rigRoot.name}'", this);
                return null;
            }

            var shell = BuildRagdoll(rigRoot, bones, catalog, renderers, out var parts, out var ragdoll);
            _shell = shell;
            _shellBody = null;
            _ragdoll = ragdoll;
            _shellCatalog = catalog;
            _remaining = _lifetime;
            if (Application.isPlaying)
            {
                // 塊の抜け殻と同じく、体の GameObject ごと止められても寿命で必ず消す
                Destroy(shell, _lifetime);
            }

            var push = PushRagdoll(hitDirection, damage, out var speed);
            AnchorShell();
            Debug.Log(
                $"{LogTag} collapse id={Id} ragdoll bones={bones.Count.ToString(CultureInfo.InvariantCulture)} parts={parts.ToString(CultureInfo.InvariantCulture)}"
                + $" push={FormatDirection(push)} speed={FormatSpeed(speed)} damage={damage.ToString(CultureInfo.InvariantCulture)}"
                + $" settle={catalog.SettleSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s lifetime={_lifetime.ToString("0.##", CultureInfo.InvariantCulture)}s",
                this);
            return shell;
        }

        private RagdollCatalog ResolveRagdollCatalog()
        {
            if (_ragdollCatalog != null)
            {
                return _ragdollCatalog;
            }

            var loaded = RagdollCatalog.Load();
            if (loaded == null && !_warnedMissingRagdollCatalog)
            {
                _warnedMissingRagdollCatalog = true;
                Debug.LogWarning(
                    $"{LogTag} no RagdollCatalog at Resources/{RagdollCatalog.ResourcePath}; bodies collapse as one piece. "
                    + "Run Shooter/Build All Presentation Assets (Shooter.Editor.PresentationAssetSteps.BuildAll)",
                    this);
            }

            return loaded;
        }

        private Transform RagdollRoot()
        {
            var animator = GetComponent<CharacterAnimator>();
            return animator != null && animator.Rig != null ? animator.Rig : transform;
        }

        // 同じ名前が複数あれば根に近いものを使う。見た目の部品が偶然ボーンと同じ名前でも、ボーンの方が浅いので取り違えにくい。
        private static bool TryFindRagdollBones(
            Transform rigRoot, RagdollCatalog catalog, out List<(RagdollBone Entry, Transform Source)> bones, out string missing)
        {
            var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
            var queue = new Queue<Transform>();
            queue.Enqueue(rigRoot);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!byName.ContainsKey(current.name))
                {
                    byName.Add(current.name, current);
                }

                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
            }

            bones = new List<(RagdollBone Entry, Transform Source)>(catalog.Bones.Count);
            foreach (var entry in catalog.Bones)
            {
                if (!byName.TryGetValue(entry.Bone, out var source))
                {
                    missing = entry.Bone;
                    bones = null;
                    return false;
                }

                bones.Add((entry, source));
            }

            missing = null;
            return true;
        }

        private GameObject BuildRagdoll(
            Transform rigRoot,
            List<(RagdollBone Entry, Transform Source)> bones,
            RagdollCatalog catalog,
            Renderer[] renderers,
            out int parts,
            out DeathRagdoll ragdoll)
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

                shell.transform.SetPositionAndRotation(rigRoot.position, rigRoot.rotation);
                shell.transform.localScale = rigRoot.lossyScale;

                // 非アクティブな枝のボーンも物理には要るので、Transform はすべて写し、見た目だけ見えているものに絞る
                var clones = new Dictionary<Transform, Transform> { [rigRoot] = shell.transform };
                CloneHierarchy(rigRoot, shell.transform, clones);

                var bodies = new Dictionary<Transform, Rigidbody>();
                var orderedBodies = new List<Rigidbody>(bones.Count);
                var shellColliders = new List<Collider>(bones.Count);
                foreach (var (entry, source) in bones)
                {
                    var boneObject = clones[source].gameObject;
                    var body = boneObject.AddComponent<Rigidbody>();
                    body.mass = entry.Mass;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    bodies.Add(source, body);
                    orderedBodies.Add(body);
                    shellColliders.Add(AddRagdollCollider(boneObject, entry));
                }

                Transform anchor = null;
                foreach (var (entry, source) in bones)
                {
                    var parentBody = NearestBodyAbove(source, rigRoot, bodies);
                    if (parentBody == null)
                    {
                        anchor ??= clones[source];
                        continue;
                    }

                    AddRagdollJoint(clones[source].gameObject, parentBody, entry);
                }

                anchor ??= shell.transform;
                foreach (var source in renderers)
                {
                    if (source != null && IsShownWithBody(source.transform) && TryCopyRagdollPart(source, clones, anchor))
                    {
                        parts++;
                    }
                }

                IgnoreBodyColliders(shellColliders);

                ragdoll = shell.AddComponent<DeathRagdoll>();
                ragdoll.Initialize(Id, orderedBodies, catalog.SettleSeconds, catalog.FootSpeedRatio);
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

        private static void CloneHierarchy(Transform source, Transform clone, Dictionary<Transform, Transform> clones)
        {
            foreach (Transform child in source)
            {
                var copy = new GameObject(child.name);
                copy.layer = ShellLayer;
                var copyTransform = copy.transform;
                copyTransform.SetParent(clone, false);
                copyTransform.localPosition = child.localPosition;
                copyTransform.localRotation = child.localRotation;
                copyTransform.localScale = child.localScale;
                clones.Add(child, copyTransform);
                CloneHierarchy(child, copyTransform, clones);
            }
        }

        private static Rigidbody NearestBodyAbove(Transform source, Transform rigRoot, Dictionary<Transform, Rigidbody> bodies)
        {
            if (source == rigRoot)
            {
                return null;
            }

            for (var current = source.parent; current != null; current = current.parent)
            {
                if (bodies.TryGetValue(current, out var body))
                {
                    return body;
                }

                if (current == rigRoot)
                {
                    break;
                }
            }

            return null;
        }

        private static Collider AddRagdollCollider(GameObject bone, RagdollBone entry)
        {
            var size = new Vector3(Mathf.Abs(entry.Size.x), Mathf.Abs(entry.Size.y), Mathf.Abs(entry.Size.z));
            switch (entry.Shape)
            {
                case RagdollShape.Capsule:
                    var axis = entry.LongestAxis;
                    var capsule = bone.AddComponent<CapsuleCollider>();
                    capsule.center = entry.Center;
                    capsule.direction = axis;
                    capsule.height = size[axis];
                    capsule.radius = Mathf.Max(size[(axis + 1) % 3], size[(axis + 2) % 3]) * 0.5f;
                    return capsule;

                case RagdollShape.Sphere:
                    var sphere = bone.AddComponent<SphereCollider>();
                    sphere.center = entry.Center;
                    sphere.radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f;
                    return sphere;

                default:
                    var box = bone.AddComponent<BoxCollider>();
                    box.center = entry.Center;
                    box.size = size;
                    return box;
            }
        }

        private static void AddRagdollJoint(GameObject bone, Rigidbody parentBody, RagdollBone entry)
        {
            var joint = bone.AddComponent<CharacterJoint>();
            joint.connectedBody = parentBody;
            joint.anchor = Vector3.zero;
            joint.axis = entry.TwistAxis.normalized;
            joint.swingAxis = entry.SwingAxis.normalized;
            joint.lowTwistLimit = new SoftJointLimit { limit = entry.LowTwist };
            joint.highTwistLimit = new SoftJointLimit { limit = entry.HighTwist };
            joint.swing1Limit = new SoftJointLimit { limit = entry.Swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = entry.Swing2 };

            // 一瞬で大きく押すので、関節が伸びきって手足が飛ばないよう可動域の外へ出た分を戻す
            joint.enableProjection = true;
        }

        private bool TryCopyRagdollPart(Renderer source, Dictionary<Transform, Transform> clones, Transform anchor)
        {
            if (source is SkinnedMeshRenderer skinned
                && skinned.sharedMesh != null
                && clones.TryGetValue(source.transform, out var skinHost)
                && TryMapBones(skinned, clones, out var mappedBones, out var mappedRoot))
            {
                // 焼かずにボーンを写した側へ付け替えるので、倒れていく間も体の形が関節に付いて曲がる
                var copy = skinHost.gameObject.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skinned.sharedMesh;
                copy.bones = mappedBones;
                copy.rootBone = mappedRoot;
                copy.localBounds = skinned.localBounds;
                copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
                for (var i = 0; i < skinned.sharedMesh.blendShapeCount; i++)
                {
                    copy.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
                }

                CopyLook(copy, source);
                return true;
            }

            if (source is MeshRenderer && clones.TryGetValue(source.transform, out var host))
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    return false;
                }

                AddMeshCopy(host.gameObject, filter.sharedMesh, source);
                return true;
            }

            // リグの外の見た目と、リグの外のボーンで曲がる皮は、根のボーンに付けて一緒に倒す
            if (!TryGetPartMesh(source, out var mesh, out var scale))
            {
                return false;
            }

            AddMeshCopy(CreateDetachedPart(source, anchor, scale), mesh, source);
            return true;
        }

        private static bool TryMapBones(
            SkinnedMeshRenderer skinned, Dictionary<Transform, Transform> clones, out Transform[] mapped, out Transform root)
        {
            var sourceBones = skinned.bones;
            mapped = new Transform[sourceBones.Length];
            root = null;
            for (var i = 0; i < sourceBones.Length; i++)
            {
                if (sourceBones[i] != null && !clones.TryGetValue(sourceBones[i], out mapped[i]))
                {
                    return false;
                }
            }

            return skinned.rootBone == null || clones.TryGetValue(skinned.rootBone, out root);
        }

        private Vector3 PushRagdoll(Vector3? hitDirection, int damage, out float speed)
        {
            var direction = HorizontalDirection(hitDirection, transform);
            speed = _shellCatalog != null ? _shellCatalog.SpeedFor(damage) : 0f;

            // 倒れた後に対応表が書き換えられて NaN などになっても、押す側で例外にしない
            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                speed = 0f;
            }

            if (_ragdoll != null)
            {
                _ragdoll.Push(direction, speed);
            }

            return direction;
        }

        private static string FormatSpeed(float speed)
        {
            return speed.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
