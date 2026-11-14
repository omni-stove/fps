using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// キャラが撃たれたときの血の演出を受け持つ。<see cref="CombatEvents.Impact"/> のうちキャラへの着弾
    /// (<see cref="ImpactInfo.IsCharacter"/>) にだけ、部位 (<see cref="ImpactInfo.Zone"/>) に応じた血しぶきを出し、
    /// 弾の進む向きに <see cref="HitFeedbackCatalog.WallDecalMaxDistance"/> だけ Raycast して、背後に壁があれば血痕を付ける。
    /// 素材は <see cref="Catalog"/> (<see cref="HitFeedbackCatalog"/>) から引く。キャラ (プレイヤー・NetPlayer・敵) の本体に
    /// <see cref="CharacterComponents.Attach"/> で付く。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="CombatEvents.Impact"/> は有効なインスタンス全体で 1 回だけ購読し、有効なうちの先頭のインスタンスが出す
    /// (血はどのキャラの演出でもないので、撃った者に関係なく 1 か所で出す)。</item>
    /// <item>着弾には弾の向きが載らないので、同じフレームの直前の <see cref="CombatEvents.ShotFired"/> の向きを使う
    /// (どちらの発生元も、1 発ごとに射撃→着弾の順に続けて出す)。無ければ当たった面の法線の逆向き。</item>
    /// <item>血しぶきと血痕は <see cref="PresentationPool{T}"/> で使い回し、上限を超えたら古いものから取り上げる。
    /// 部品はキャラの外の <see cref="PoolRoot"/> の下に置く。血痕は付けた壁のコライダーが消えるか無効になったら外す。</item>
    /// <item><see cref="BloodEnabled"/> を false にすると、新しい血を出さず、出ている血も次の <see cref="Tick"/> で消す。</item>
    /// </list>
    /// 専用サーバーでは <c>Fps.Net.HeadlessPresentation</c> が止め、ここでも有効になったときに自分を止める。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BloodPresenter : MonoBehaviour
    {
        public const string LogTag = "[Blood]";

        /// <summary>血しぶきのプール (エフェクトごと) の上限。</summary>
        public const int SprayCapacity = 8;

        /// <summary>血痕のプールの上限。</summary>
        public const int DecalCapacity = 24;

        /// <summary>血痕を壁の面から浮かせる距離 (m)。面と重なってちらつかないよう。</summary>
        public const float DecalSurfaceOffset = 0.01f;

        /// <summary>背後の壁を探す Raycast で拾える数。撃たれたキャラ自身の当たり判定を飛ばすので 1 より多く取る。</summary>
        public const int WallProbeBufferSize = 16;

        private static readonly List<BloodPresenter> _enabled = new List<BloodPresenter>();
        private static readonly RaycastHit[] _wallHits = new RaycastHit[WallProbeBufferSize];
        private static bool _bloodEnabled = true;
        private static Vector3 _lastShotDirection;
        private static int _lastShotFrame = -1;
        private static bool _warnedMissingCatalog;

        private readonly Dictionary<GameObject, PresentationPool<PooledEffect>> _sprayPools =
            new Dictionary<GameObject, PresentationPool<PooledEffect>>();

        private readonly Dictionary<PooledEffect, Collider> _decalAnchors = new Dictionary<PooledEffect, Collider>();
        private readonly List<PooledEffect> _decalScratch = new List<PooledEffect>();
        private PresentationPool<PooledEffect> _decals;
        private GameObject _decalSource;
        private Vector3 _decalBaseScale = Vector3.one;
        private Transform _poolRoot;

        private HitFeedbackCatalog _catalog;
        private bool _catalogResolved;

        /// <summary>
        /// 血の表示をまとめて出すか。false にすると新しい血しぶきと血痕を出さず (ログも出さない)、出ている血は次の <see cref="Tick"/> で消す。
        /// 設定画面から切り替える口。Play を始めるたびに true に戻る。
        /// </summary>
        public static bool BloodEnabled
        {
            get => _bloodEnabled;
            set => _bloodEnabled = value;
        }

        /// <summary>
        /// 素材を引くカタログ。初めて読んだときに <see cref="HitFeedbackCatalog.Load"/> で探し、無ければ null (血を出さず、警告を 1 度だけ出す)。
        /// 代入すると探さずにそれを使う。
        /// </summary>
        public HitFeedbackCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = HitFeedbackCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
            }
        }

        /// <summary>プールの部品を置く親。初めて部品を作るまでは null。</summary>
        public Transform PoolRoot => _poolRoot;

        /// <summary>血痕のプール。初めて血痕を付けるまでは null。</summary>
        public PresentationPool<PooledEffect> Decals => _decals;

        /// <summary>prefab の血しぶきのプール。まだ一度も出していなければ null。</summary>
        public PresentationPool<PooledEffect> SprayPoolFor(GameObject prefab)
        {
            return prefab != null && _sprayPools.TryGetValue(prefab, out var pool) ? pool : null;
        }

        /// <summary>貸している血痕 decal を付けた壁のコライダー。貸していなければ null。</summary>
        public Collider DecalAnchorOf(PooledEffect decal)
        {
            return decal != null && _decalAnchors.TryGetValue(decal, out var anchor) ? anchor : null;
        }

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            if (!_enabled.Contains(this))
            {
                _enabled.Add(this);
            }

            // ClearSubscribers で外されていても戻るよう、有効になるたびに付け直す (二重には付かない)。
            CombatEvents.ShotFired -= HandleShotFired;
            CombatEvents.ShotFired += HandleShotFired;
            CombatEvents.Impact -= HandleImpact;
            CombatEvents.Impact += HandleImpact;
        }

        private void OnDisable()
        {
            _enabled.Remove(this);
            if (_enabled.Count == 0)
            {
                CombatEvents.ShotFired -= HandleShotFired;
                CombatEvents.Impact -= HandleImpact;
            }

            ReleaseAll();
        }

        private void OnDestroy()
        {
            DestroyPools();
        }

        private void Update()
        {
            Tick(Time.time);
        }

        /// <summary>
        /// impact がキャラへの着弾なら、部位の血しぶきを当たった点に出し、direction (弾の進む向き) の先
        /// <see cref="HitFeedbackCatalog.WallDecalMaxDistance"/> 以内に壁があれば血痕を付ける。
        /// キャラ以外への着弾・<see cref="BloodEnabled"/> が false・カタログが無いときは何も出さない。
        /// 出したものは <see cref="FormatSprayLog"/>・<see cref="FormatDecalLog"/> の形でログに出す。
        /// </summary>
        /// <param name="impact">着弾。</param>
        /// <param name="direction">弾の進む向き。null か長さが 0 なら <see cref="BulletDirection"/> の代わりの向き。</param>
        /// <returns>血しぶきか血痕のどちらかを出したら true。</returns>
        public bool PlayImpact(ImpactInfo impact, Vector3? direction)
        {
            if (!_bloodEnabled || !impact.IsCharacter)
            {
                return false;
            }

            var catalog = Catalog;
            if (catalog == null)
            {
                WarnMissingCatalog();
                return false;
            }

            var now = Time.time;
            var forward = BulletDirection(impact.Normal, direction);
            var shown = false;

            var spray = catalog.SprayFor(impact.Zone);
            if (spray != null)
            {
                SpawnSpray(spray, impact.Point, Quaternion.LookRotation(forward), now);
                Debug.Log(FormatSprayLog(impact.Zone, impact.Point), this);
                shown = true;
            }

            if (catalog.WallDecal != null && TryFindWall(impact.Point, forward, catalog.WallDecalMaxDistance, out var wall))
            {
                PlaceDecal(catalog, wall, now);
                Debug.Log(FormatDecalLog(wall.collider, wall.distance), this);
                shown = true;
            }

            return shown;
        }

        /// <summary>時刻 now (<see cref="Time.time"/> と同じ物差し) まで進め、出し終えた血しぶき・時間の来た血痕・壁が消えた血痕をプールに返す。</summary>
        public void Tick(float now)
        {
            if (!_bloodEnabled)
            {
                ReleaseAll();
                return;
            }

            foreach (var pool in _sprayPools.Values)
            {
                pool.ReleaseDue(now);
            }

            if (_decals == null)
            {
                return;
            }

            _decals.ReleaseDue(now);
            _decals.CopyActive(_decalScratch);
            foreach (var decal in _decalScratch)
            {
                var anchor = DecalAnchorOf(decal);
                if (anchor == null || !anchor.enabled || !anchor.gameObject.activeInHierarchy)
                {
                    _decals.Release(decal);
                }
            }

            _decalScratch.Clear();
        }

        /// <summary>
        /// 弾の進む向き。direction が使えればその単位ベクトル、使えなければ当たった面の法線 normal の逆向き、それも無ければ +Z。
        /// </summary>
        public static Vector3 BulletDirection(Vector3 normal, Vector3? direction)
        {
            if (direction.HasValue && direction.Value.sqrMagnitude > 1e-8f)
            {
                return direction.Value.normalized;
            }

            return normal.sqrMagnitude > 1e-8f ? -normal.normalized : Vector3.forward;
        }

        /// <summary>
        /// point から direction へ maxDistance までの間で、いちばん近い壁 (トリガーとキャラの当たり判定を除くコライダー) を探す。
        /// 撃たれたキャラ自身の別の部位の当たり判定も飛ばす。
        /// </summary>
        public static bool TryFindWall(Vector3 point, Vector3 direction, float maxDistance, out RaycastHit wall)
        {
            wall = default;
            if (maxDistance <= 0f || direction.sqrMagnitude <= 1e-8f)
            {
                return false;
            }

            var count = Physics.RaycastNonAlloc(
                point, direction.normalized, _wallHits, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var found = false;
            for (var i = 0; i < count; i++)
            {
                var hit = _wallHits[i];
                if (hit.collider == null || IsCharacter(hit.collider))
                {
                    continue;
                }

                if (!found || hit.distance < wall.distance)
                {
                    wall = hit;
                    found = true;
                }
            }

            Array.Clear(_wallHits, 0, count);
            return found;
        }

        /// <summary>血しぶきを出したときのログ。例: <c>[Blood] spray zone=Head point=(0.00, 1.70, 10.00)</c>。部位が無ければ <c>zone=none</c>。</summary>
        public static string FormatSprayLog(HitZone? zone, Vector3 point)
        {
            return LogTag + " spray zone=" + (zone.HasValue ? zone.Value.ToString() : "none") + " point=" + FormatPoint(point);
        }

        /// <summary>血痕を付けたときのログ。例: <c>[Blood] decal wall=Wall material=default distance=0.84</c>。</summary>
        public static string FormatDecalLog(Collider wall, float distance)
        {
            var wallName = wall != null ? wall.name : "(unknown)";
            return LogTag + " decal wall=" + wallName + " material=" + SurfaceMaterial.IdOf(wall)
                + " distance=" + distance.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string FormatPoint(Vector3 point)
        {
            return "("
                + point.x.ToString("0.00", CultureInfo.InvariantCulture) + ", "
                + point.y.ToString("0.00", CultureInfo.InvariantCulture) + ", "
                + point.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
        }

        private static bool IsCharacter(Collider collider)
        {
            // NetPlayer は Hitbox も Damageable も持たず、本体の CharacterController のカプセルが当たり判定になる。
            return collider.GetComponentInParent<Hitbox>(true) != null
                || collider.GetComponentInParent<Damageable>(true) != null
                || collider.GetComponentInParent<CharacterController>(true) != null;
        }

        private static void HandleShotFired(ShotFiredInfo shot)
        {
            _lastShotDirection = shot.Direction;
            _lastShotFrame = Time.frameCount;
        }

        private static void HandleImpact(ImpactInfo impact)
        {
            if (!_bloodEnabled || !impact.IsCharacter)
            {
                return;
            }

            var presenter = First();
            if (presenter != null)
            {
                var direction = _lastShotFrame == Time.frameCount ? _lastShotDirection : (Vector3?)null;
                presenter.PlayImpact(impact, direction);
            }
        }

        private static BloodPresenter First()
        {
            foreach (var presenter in _enabled)
            {
                if (presenter != null && presenter.isActiveAndEnabled)
                {
                    return presenter;
                }
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ResetStatics();
        }

        /// <summary>Play の開始時と同じく、静的な状態 (有効なインスタンス・<see cref="BloodEnabled"/>・直前の弾の向き・警告済みの印) を初めに戻す。</summary>
        internal static void ResetStatics()
        {
            _enabled.Clear();
            _bloodEnabled = true;
            _lastShotDirection = Vector3.zero;
            _lastShotFrame = -1;
            _warnedMissingCatalog = false;
        }

        private void SpawnSpray(GameObject prefab, Vector3 position, Quaternion rotation, float now)
        {
            if (!_sprayPools.TryGetValue(prefab, out var pool))
            {
                pool = new PresentationPool<PooledEffect>(SprayCapacity, () => PooledEffect.Instantiate(prefab, EnsurePoolRoot()), null, HideEffect);
                _sprayPools.Add(prefab, pool);
            }

            var effect = pool.Rent(now);
            pool.Extend(effect, now + effect.Seconds);
            effect.PlayAt(position, rotation);
        }

        private void PlaceDecal(HitFeedbackCatalog catalog, RaycastHit wall, float now)
        {
            var prefab = catalog.WallDecal;
            if (_decals != null && _decalSource != prefab)
            {
                // 素材が差し替わったら、前の素材で作った血痕は使い回せないので作り直す。
                DestroyDecals();
            }

            if (_decals == null)
            {
                _decalSource = prefab;
                _decalBaseScale = prefab.transform.localScale;
                _decals = new PresentationPool<PooledEffect>(
                    DecalCapacity, () => PooledEffect.Instantiate(prefab, EnsurePoolRoot()), null, HideDecal);
            }

            var decal = _decals.Rent(now + catalog.WallDecalLifetime);
            _decalAnchors[decal] = wall.collider;

            var normal = wall.normal.sqrMagnitude > 1e-8f ? wall.normal.normalized : -Vector3.forward;
            var roll = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.forward);
            decal.Root.transform.localScale = _decalBaseScale * catalog.WallDecalSize;
            decal.PlayAt(wall.point + (normal * DecalSurfaceOffset), Quaternion.LookRotation(-normal) * roll);
        }

        private void HideDecal(PooledEffect decal)
        {
            _decalAnchors.Remove(decal);
            decal.Hide();
        }

        private static void HideEffect(PooledEffect effect)
        {
            effect.Hide();
        }

        private Transform EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return _poolRoot;
            }

            var root = new GameObject($"{name} BloodPresenter");
            var scene = gameObject.scene;
            if (scene.IsValid() && scene.isLoaded && root.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            _poolRoot = root.transform;
            return _poolRoot;
        }

        private void ReleaseAll()
        {
            foreach (var pool in _sprayPools.Values)
            {
                pool.ReleaseDue(float.PositiveInfinity);
            }

            _decals?.ReleaseDue(float.PositiveInfinity);
        }

        private void DestroyDecals()
        {
            if (_decals == null)
            {
                return;
            }

            _decals.Clear(DestroyEffect);
            _decals = null;
            _decalSource = null;
            _decalAnchors.Clear();
        }

        private void DestroyPools()
        {
            foreach (var pool in _sprayPools.Values)
            {
                pool.Clear(null);
            }

            _sprayPools.Clear();
            _decals?.Clear(null);
            _decals = null;
            _decalSource = null;
            _decalAnchors.Clear();

            if (_poolRoot != null)
            {
                // 部品はすべてこの下にあるので、親ごと消せば残らない。
                DestroyObject(_poolRoot.gameObject);
            }

            _poolRoot = null;
        }

        private static void DestroyEffect(PooledEffect effect)
        {
            if (effect.Root != null)
            {
                DestroyObject(effect.Root);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void WarnMissingCatalog()
        {
            if (_warnedMissingCatalog)
            {
                return;
            }

            _warnedMissingCatalog = true;
            Debug.LogWarning(
                $"{LogTag} no HitFeedbackCatalog at Resources/{HitFeedbackCatalog.ResourcePath}; blood is off. "
                + "Run Fps/Build All Presentation Assets (Fps.Editor.PresentationAssetSteps.BuildAll)",
                this);
        }
    }
}
