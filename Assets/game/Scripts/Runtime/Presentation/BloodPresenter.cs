using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // 血はどのキャラの演出でもないので、CombatEvents.Impact は有効なインスタンス全体で 1 回だけ購読し、撃った者に関係なく先頭のインスタンスが出す。
    // 着弾には弾の向きが載らないので、同じフレームの直前の ShotFired の向きを使う (どちらの発生元も、1 発ごとに射撃→着弾の順に続けて出す)。
    [DisallowMultipleComponent]
    public sealed class BloodPresenter : MonoBehaviour
    {
        public const string LogTag = "[Blood]";

        public const int SprayCapacity = 8;

        public const int DecalCapacity = 24;

        // 血痕を壁の面から浮かせるのは、面と重なってちらつかないようにするため。
        public const float DecalSurfaceOffset = 0.01f;

        // 撃たれたキャラ自身の当たり判定を飛ばすので、背後の壁を探す Raycast は 1 より多く拾う。
        public const int WallProbeBufferSize = 16;

        private static readonly List<BloodPresenter> _enabled = new List<BloodPresenter>();
        private static readonly RaycastHit[] _wallHits = new RaycastHit[WallProbeBufferSize];
        private static bool _bloodEnabled = true;
        private static Vector3 _lastShotDirection;
        private static int _lastShotFrame = -1;
        private static bool _warnedMissingCatalog;

        private readonly Dictionary<GameObject, PresentationPool<PooledEffect>> _sprayPools =
            new Dictionary<GameObject, PresentationPool<PooledEffect>>();

        private readonly Dictionary<PooledEffect, DecalAttachment> _decalAnchors = new Dictionary<PooledEffect, DecalAttachment>();
        private readonly List<PooledEffect> _decalScratch = new List<PooledEffect>();
        private PresentationPool<PooledEffect> _decals;
        private GameObject _decalSource;
        private Vector3 _decalBaseScale = Vector3.one;
        private Transform _poolRoot;

        private HitFeedbackCatalog _catalog;
        private bool _catalogResolved;

        public static bool BloodEnabled
        {
            get => _bloodEnabled;
            set
            {
                if (_bloodEnabled == value)
                {
                    return;
                }

                _bloodEnabled = value;
                Debug.Log(FormatEnabledLog(value));
            }
        }

        public static string FormatEnabledLog(bool enabled)
        {
            return LogTag + " enabled=" + (enabled ? "true" : "false");
        }

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

        public Transform PoolRoot => _poolRoot;

        public PresentationPool<PooledEffect> Decals => _decals;

        public PresentationPool<PooledEffect> SprayPoolFor(GameObject prefab)
        {
            return prefab != null && _sprayPools.TryGetValue(prefab, out var pool) ? pool : null;
        }

        public Collider DecalAnchorOf(PooledEffect decal)
        {
            return decal != null && _decalAnchors.TryGetValue(decal, out var attachment) ? attachment.Collider : null;
        }

        public int FollowDecalAnchors()
        {
            if (_decals == null || _decals.ActiveCount == 0)
            {
                return 0;
            }

            var moved = 0;
            _decals.CopyActive(_decalScratch);
            foreach (var decal in _decalScratch)
            {
                if (_decalAnchors.TryGetValue(decal, out var attachment) && attachment.Follow(decal.Root.transform))
                {
                    moved++;
                }
            }

            _decalScratch.Clear();
            return moved;
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

        // 貼り先を動かすのは Update や物理なので、それらが済んだ後に痕を合わせる。
        private void LateUpdate()
        {
            FollowDecalAnchors();
        }

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
                if (!_decalAnchors.TryGetValue(decal, out var attachment) || attachment.Collider == null || attachment.IsDetached)
                {
                    _decals.Release(decal);
                }
            }

            _decalScratch.Clear();
        }

        public static Vector3 BulletDirection(Vector3 normal, Vector3? direction)
        {
            if (direction.HasValue && direction.Value.sqrMagnitude > 1e-8f)
            {
                return direction.Value.normalized;
            }

            return normal.sqrMagnitude > 1e-8f ? -normal.normalized : Vector3.forward;
        }

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

        public static string FormatSprayLog(HitZone? zone, Vector3 point)
        {
            return LogTag + " spray zone=" + (zone.HasValue ? zone.Value.ToString() : "none") + " point=" + FormatPoint(point);
        }

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
            // ネット対戦の本体は Hitbox も Damageable も持たず、本体の CharacterController のカプセルが当たり判定になる。
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
            if (!_decalAnchors.TryGetValue(decal, out var attachment))
            {
                attachment = new DecalAttachment();
                _decalAnchors[decal] = attachment;
            }

            var normal = wall.normal.sqrMagnitude > 1e-8f ? wall.normal.normalized : -Vector3.forward;
            var roll = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.forward);
            var rotation = Quaternion.LookRotation(-normal) * roll;

            // 血痕の +Z は壁の中を向くので、浮かせる向きは -Z。
            attachment.Attach(wall.collider, wall.point, rotation, Vector3.back * DecalSurfaceOffset);
            decal.Root.transform.localScale = _decalBaseScale * catalog.WallDecalSize;
            decal.PlayAt(wall.point + (normal * DecalSurfaceOffset), rotation);
        }

        private void HideDecal(PooledEffect decal)
        {
            if (_decalAnchors.TryGetValue(decal, out var attachment))
            {
                attachment.Clear();
            }

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
                + "Run Shooter/Build All Presentation Assets (Shooter.Editor.PresentationAssetSteps.BuildAll)",
                this);
        }
    }
}
