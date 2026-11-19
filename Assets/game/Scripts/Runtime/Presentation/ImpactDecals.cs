using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Shooter.Runtime
{
    // 撃った者 (WeaponPresentation) とは別の GameObject に持たせるのは、撃った者が倒れて消えても壁の銃痕を残すため。
    // 当たった物が動いたら銃痕も一緒に動かす (DecalAttachment)。
    // 当たったコライダーが消える・切られる・非アクティブになったら (壊せる部品が壊れたときなど)、宙に残らないよう剥がす。
    [DisallowMultipleComponent]
    public sealed class ImpactDecals : MonoBehaviour
    {
        public const string LogTag = WeaponPresentation.LogTag;

        public const string ObjectName = "ImpactDecals";

        public const int DefaultCapacity = 64;

        // 面と同じ深さに置くと描画が交互にちらつくため、面から浮かせる。
        public const float SurfaceOffset = 0.005f;

        private static Mesh _quad;

        [SerializeField, Min(1), Tooltip("同時に貼っておく銃痕の数の上限。超えたら古いものから剥がす")]
        private int _capacity = DefaultCapacity;

        private readonly List<PooledDecal> _scratch = new List<PooledDecal>();
        private PresentationPool<PooledDecal> _pool;

        public int Capacity
        {
            get => Mathf.Max(1, _capacity);
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "capacity must be at least 1");
                }

                if (value == _capacity)
                {
                    return;
                }

                _capacity = value;
                DestroyDecals();
            }
        }

        public PresentationPool<PooledDecal> Pool => _pool;

        public int ActiveCount => _pool?.ActiveCount ?? 0;

        internal static Mesh QuadMesh
        {
            get
            {
                if (_quad == null)
                {
                    _quad = CreateQuad();
                }

                return _quad;
            }
        }

        public static ImpactDecals For(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = SceneManager.GetActiveScene();
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<ImpactDecals>(out var existing))
                    {
                        return existing;
                    }
                }
            }

            var holder = new GameObject(ObjectName);
            if (scene.IsValid() && scene.isLoaded && holder.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(holder, scene);
            }

            return holder.AddComponent<ImpactDecals>();
        }

        public PooledDecal Place(ImpactInfo impact, SurfaceImpactAssets surface, float rollDegrees)
        {
            if (impact.IsCharacter || surface == null || surface.Decal == null)
            {
                return null;
            }

            _pool ??= new PresentationPool<PooledDecal>(Capacity, CreateDecal, null, HideDecal);
            if (_pool.ActiveCount >= _pool.Capacity)
            {
                _pool.CopyActive(_scratch);
                Debug.Log(FormatEvictedLog(_scratch[0].MaterialId, _pool.Capacity), this);
                _scratch.Clear();
            }

            // 時間では剥がさず、上限を超えたときだけ古いものから剥がす。
            var decal = _pool.Rent(float.PositiveInfinity);
            var pose = DecalPose(impact.Point, impact.Normal, rollDegrees);
            var size = surface.DecalSize > 0f ? surface.DecalSize : SurfaceImpactAssets.DefaultDecalSize;
            decal.MaterialId = impact.MaterialId;
            decal.Attachment.Attach(impact.Collider, impact.Point, pose.rotation, Vector3.forward * SurfaceOffset);
            decal.Renderer.sharedMaterial = surface.Decal;
            decal.Root.transform.SetPositionAndRotation(pose.position, pose.rotation);
            decal.Root.transform.localScale = new Vector3(size, size, 1f);
            decal.Root.SetActive(true);

            Debug.Log(FormatPlacedLog(impact.MaterialId, surface.MaterialId, _pool.ActiveCount, _pool.Capacity), this);
            return decal;
        }

        public PooledDecal Place(ImpactInfo impact, SurfaceImpactAssets surface)
        {
            return Place(impact, surface, Random.Range(0f, 360f));
        }

        public void Clear()
        {
            _pool?.ReleaseDue(float.PositiveInfinity);
        }

        public int ReleaseDetached()
        {
            if (_pool == null || _pool.ActiveCount == 0)
            {
                return 0;
            }

            var released = 0;
            _pool.CopyActive(_scratch);
            foreach (var decal in _scratch)
            {
                if (decal.IsDetached)
                {
                    _pool.Release(decal);
                    released++;
                }
            }

            _scratch.Clear();
            if (released > 0)
            {
                Debug.Log(FormatDetachedLog(released, _pool.ActiveCount, _pool.Capacity), this);
            }

            return released;
        }

        public int FollowAnchors()
        {
            if (_pool == null || _pool.ActiveCount == 0)
            {
                return 0;
            }

            var moved = 0;
            _pool.CopyActive(_scratch);
            foreach (var decal in _scratch)
            {
                if (decal.Attachment.Follow(decal.Root.transform))
                {
                    moved++;
                }
            }

            _scratch.Clear();
            return moved;
        }

        public static Pose DecalPose(Vector3 point, Vector3 normal, float rollDegrees)
        {
            var n = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;

            // 真上・真下を向く面では上向きの基準が法線と重なって向きが決まらないので、基準を前に替える。
            var reference = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            var rotation = Quaternion.LookRotation(n, reference) * Quaternion.AngleAxis(rollDegrees, Vector3.forward);
            return new Pose(point + (n * SurfaceOffset), rotation);
        }

        public static string FormatPlacedLog(string materialId, string surfaceId, int active, int capacity)
        {
            return LogTag + " decal placed material=" + SurfaceMaterial.Normalize(materialId)
                + " surface=" + SurfaceMaterial.Normalize(surfaceId)
                + " active=" + active.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatEvictedLog(string materialId, int capacity)
        {
            return LogTag + " decal released oldest material=" + SurfaceMaterial.Normalize(materialId)
                + " capacity=" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatDetachedLog(int released, int active, int capacity)
        {
            return LogTag + " decal released detached count=" + released.ToString(CultureInfo.InvariantCulture)
                + " active=" + active.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        // 壊れた部品は GameObject を残したままコライダーと描画を切るだけなので、消滅の通知を待てず毎フレーム確かめる。
        private void LateUpdate()
        {
            ReleaseDetached();
            FollowAnchors();
        }

        private void OnDestroy()
        {
            // 部品はすべてこの GameObject の子なので一緒に消える。プールだけ忘れる。
            _pool = null;
        }

        private PooledDecal CreateDecal()
        {
            var decalObject = new GameObject("Decal");
            decalObject.SetActive(false);
            decalObject.transform.SetParent(transform, false);
            decalObject.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
            var renderer = decalObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return new PooledDecal(decalObject, renderer);
        }

        private static void HideDecal(PooledDecal decal)
        {
            decal.Attachment.Clear();
            if (decal.Root != null)
            {
                decal.Root.SetActive(false);
            }
        }

        private void DestroyDecals()
        {
            if (_pool == null)
            {
                return;
            }

            _pool.Clear(decal =>
            {
                if (decal.Root == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    Destroy(decal.Root);
                }
                else
                {
                    DestroyImmediate(decal.Root);
                }
            });
            _pool = null;
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "ImpactDecalQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            });
            mesh.SetNormals(new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });

            // パーティクル用のシェーダーは頂点色を掛けるので、白を入れて材質の色をそのまま出す。
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 1, 3, 2, 1, 2, 0 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public sealed class PooledDecal
    {
        public PooledDecal(GameObject root, MeshRenderer renderer)
        {
            Root = root;
            Renderer = renderer;
        }

        public GameObject Root { get; }

        public MeshRenderer Renderer { get; }

        public string MaterialId { get; set; } = SurfaceMaterial.DefaultId;

        public DecalAttachment Attachment { get; } = new DecalAttachment();

        public Collider Anchor => Attachment.Collider;

        public bool IsDetached => Attachment.IsDetached;
    }
}
