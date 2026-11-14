using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾が当たった面に貼る銃痕 (四角い板) を <see cref="PresentationPool{T}"/> で使い回す。シーンに 1 つ置き (<see cref="For"/>)、
    /// 撃った者が誰でも同じ上限 (<see cref="Capacity"/>) を分け合う。上限を超えたらいちばん古い銃痕を剥がして貼り直す。
    /// 銃痕は面の法線に沿って <see cref="SurfaceOffset"/> だけ浮かせ、法線を軸にランダムに回して置く。キャラへの着弾
    /// (<see cref="ImpactInfo.IsCharacter"/>) には貼らない。
    /// </summary>
    /// <remarks>
    /// 撃った者 (<see cref="WeaponPresentation"/>) とは別の GameObject に持たせるのは、撃った者が倒れて消えても壁の銃痕を残すため。
    /// 銃痕は世界の座標に置くので、当たった物が動いたり壊れたりしても付いていかない。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ImpactDecals : MonoBehaviour
    {
        public const string LogTag = WeaponPresentation.LogTag;

        /// <summary><see cref="For"/> が作る GameObject の名前。</summary>
        public const string ObjectName = "ImpactDecals";

        /// <summary>銃痕の数の既定の上限。</summary>
        public const int DefaultCapacity = 64;

        /// <summary>銃痕を面から浮かせる距離 (m)。面と同じ深さに置くと描画が交互にちらつくため。</summary>
        public const float SurfaceOffset = 0.005f;

        private static Mesh _quad;

        [SerializeField, Min(1), Tooltip("同時に貼っておく銃痕の数の上限。超えたら古いものから剥がす")]
        private int _capacity = DefaultCapacity;

        private readonly List<PooledDecal> _scratch = new List<PooledDecal>();
        private PresentationPool<PooledDecal> _pool;

        /// <summary>
        /// 同時に貼っておく銃痕の数の上限。今の数と違う値を入れると、貼ってある銃痕をすべて消して数え直す。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">1 未満。</exception>
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

        /// <summary>銃痕のプール。初めて貼るまでは null。</summary>
        public PresentationPool<PooledDecal> Pool => _pool;

        /// <summary>今貼ってある銃痕の数。</summary>
        public int ActiveCount => _pool?.ActiveCount ?? 0;

        /// <summary>銃痕の板 (一辺 1m、+Z の側から見える)。すべての銃痕で共有する。</summary>
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

        /// <summary>
        /// scene の直下にある <see cref="ImpactDecals"/> を返す。無ければ <see cref="ObjectName"/> の GameObject を scene の直下に作って付ける。
        /// scene が読み込まれていなければ、今の Active なシーンを使う。
        /// </summary>
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

        /// <summary>
        /// impact の当たった点に surface の銃痕を、法線を軸に rollDegrees 度回して貼り、貼った銃痕を返す。
        /// キャラへの着弾・surface が null・surface に銃痕の材質が無いときは何もせず null を返す。
        /// 貼るたびに <see cref="FormatPlacedLog"/> を、上限を超えて古い銃痕を剥がしたら先に <see cref="FormatEvictedLog"/> をログに出す。
        /// </summary>
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
            decal.Renderer.sharedMaterial = surface.Decal;
            decal.Root.transform.SetPositionAndRotation(pose.position, pose.rotation);
            decal.Root.transform.localScale = new Vector3(size, size, 1f);
            decal.Root.SetActive(true);

            Debug.Log(FormatPlacedLog(impact.MaterialId, surface.MaterialId, _pool.ActiveCount, _pool.Capacity), this);
            return decal;
        }

        /// <summary><see cref="Place(ImpactInfo, SurfaceImpactAssets, float)"/> を、回す角度をランダムに選んで呼ぶ。</summary>
        public PooledDecal Place(ImpactInfo impact, SurfaceImpactAssets surface)
        {
            return Place(impact, surface, Random.Range(0f, 360f));
        }

        /// <summary>貼ってある銃痕をすべて剥がす (部品はプールに戻して使い回す)。</summary>
        public void Clear()
        {
            _pool?.ReleaseDue(float.PositiveInfinity);
        }

        /// <summary>
        /// point に法線 normal で当たった面に貼る銃痕の位置と向き。位置は法線の側へ <see cref="SurfaceOffset"/> 浮かせ、
        /// +Z を法線に向け、法線を軸に rollDegrees 度回す。normal が 0 なら上向きの面とみなす。
        /// </summary>
        public static Pose DecalPose(Vector3 point, Vector3 normal, float rollDegrees)
        {
            var n = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;

            // 真上・真下を向く面では上向きの基準が法線と重なって向きが決まらないので、基準を前に替える。
            var reference = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            var rotation = Quaternion.LookRotation(n, reference) * Quaternion.AngleAxis(rollDegrees, Vector3.forward);
            return new Pose(point + (n * SurfaceOffset), rotation);
        }

        /// <summary>銃痕を貼ったときのログ。例: <c>[Presentation] decal placed material=metal surface=metal active=3/64</c>。</summary>
        public static string FormatPlacedLog(string materialId, string surfaceId, int active, int capacity)
        {
            return LogTag + " decal placed material=" + SurfaceMaterial.Normalize(materialId)
                + " surface=" + SurfaceMaterial.Normalize(surfaceId)
                + " active=" + active.ToString(CultureInfo.InvariantCulture) + "/" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>上限を超えて古い銃痕を剥がしたときのログ。例: <c>[Presentation] decal released oldest material=wood capacity=64</c>。</summary>
        public static string FormatEvictedLog(string materialId, int capacity)
        {
            return LogTag + " decal released oldest material=" + SurfaceMaterial.Normalize(materialId)
                + " capacity=" + capacity.ToString(CultureInfo.InvariantCulture);
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

    /// <summary><see cref="ImpactDecals"/> がプールで使い回す銃痕 1 枚。</summary>
    public sealed class PooledDecal
    {
        public PooledDecal(GameObject root, MeshRenderer renderer)
        {
            Root = root;
            Renderer = renderer;
        }

        public GameObject Root { get; }

        public MeshRenderer Renderer { get; }

        /// <summary>貼った面の材質の Id。剥がしたときのログに使う。</summary>
        public string MaterialId { get; set; } = SurfaceMaterial.DefaultId;
    }
}
