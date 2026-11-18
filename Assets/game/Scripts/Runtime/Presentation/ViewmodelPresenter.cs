using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ViewmodelMotion))]
    public sealed class ViewmodelPresenter : MonoBehaviour
    {
        public const string LogTag = "[Viewmodel]";
        public const string SpawnedLogPrefix = LogTag + " spawned";
        public const string SwappedLogPrefix = LogTag + " swapped";
        public const string RemovedLogPrefix = LogTag + " removed";
        public const string AttachedLogPrefix = LogTag + " attachments";

        public const string ReasonThirdPerson = "third-person";
        public const string ReasonNotLocalView = "not-local-view";
        public const string ReasonDead = "dead";
        public const string ReasonUnarmed = "unarmed";
        public const string ReasonNoViewmodel = "no-viewmodel";
        public const string ReasonDisabled = "disabled";
        public const string ReasonHolstered = "holstered";

        private static readonly HashSet<string> _warnedWeaponIds = new HashSet<string>(StringComparer.Ordinal);
        private static bool _warnedMissingCatalog;

        private ViewmodelCatalog _catalog;
        private bool _catalogResolved;
        private bool _resolved;
        private ViewRig _rig;
        private WeaponController _weapon;
        private Damageable _damageable;
        private WeaponPresentation _presentation;
        private ViewmodelMotion _motion;
        private AimDownSights _ads;

        private GameObject _instance;
        private Vector3 _hipPosition;
        private Vector3 _adsPosition;
        private string _weaponId;
        private WeaponSockets _sockets = WeaponSockets.None;
        private ViewmodelEntry _entry;
        private IReadOnlyList<string> _attachments = WeaponAttachments.None;
        private AttachmentModelCatalog _attachmentModels;
        private bool _attachmentModelsResolved;
        private bool _hiddenBySight;
        private Material _paint;

        public ViewmodelCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = ViewmodelCatalog.Load();
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

        // ネット対戦の NetPlayer のように WeaponController を持たない本体が、持っている武器の Id を渡すための差し込み口。
        public Func<string> WeaponIdSource { get; set; }

        public Func<string, IReadOnlyList<string>> AttachmentsSource { get; set; }

        public AttachmentModelCatalog AttachmentModels
        {
            get
            {
                if (!_attachmentModelsResolved)
                {
                    _attachmentModels = AttachmentModelCatalog.Load();
                    _attachmentModelsResolved = true;
                }

                return _attachmentModels;
            }
            set
            {
                _attachmentModels = value;
                _attachmentModelsResolved = true;
            }
        }

        // 迷彩の材質を渡す差し込み口。空なら着けている迷彩 (GameSignals.Cosmetics) を引く。
        public Func<Material> PaintSource { get; set; }

        public Material CurrentPaint => _instance != null ? _paint : null;

        public IReadOnlyList<string> CurrentAttachments => _instance != null ? _attachments : WeaponAttachments.None;

        public GameObject Instance => _instance;

        public string CurrentWeaponId => _instance != null ? _weaponId : null;

        public WeaponSockets Sockets => _instance != null ? _sockets : WeaponSockets.None;

        public bool IsHiddenBySight => _instance != null && _hiddenBySight;

        public Func<SightView> SightSource { get; set; }

        public ViewmodelMotion Motion
        {
            get
            {
                Resolve();
                return _motion;
            }
        }

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            // 親ごと無効にされている最中は子の SetActive が許されないので、隠さずに壊すだけにする。
            Remove(ReasonDisabled, false);
        }

        private void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            Resolve();
            var reason = HiddenReason();
            if (reason != null)
            {
                Remove(reason);
                return;
            }

            var wanted = WantedWeaponId();
            if (string.IsNullOrEmpty(wanted))
            {
                Remove(ReasonUnarmed);
                return;
            }

            if (_instance != null && string.Equals(wanted, _weaponId, StringComparison.Ordinal))
            {
                var attachments = WantedAttachments(wanted);
                if (!WeaponAttachments.SameAs(attachments, _attachments))
                {
                    Attach(attachments);
                    HideForSight(_hiddenBySight, true);
                    Debug.Log($"{AttachedLogPrefix} weapon={wanted} attached={WeaponAttachments.Describe(attachments)} sight={_sockets.Sight != null}", this);
                }
                else
                {
                    var paint = WantedPaint();
                    if (paint != _paint)
                    {
                        ApplyPaint(paint);
                    }
                }

                PlaceForAim();
                return;
            }

            var entry = FindEntry(wanted);
            if (entry == null)
            {
                Remove(ReasonNoViewmodel);
                return;
            }

            Spawn(wanted, entry);
            PlaceForAim();
        }

        public static Vector3 AimedRootPosition(Vector3 adsOffset, Vector3 sightInModel)
        {
            return adsOffset - sightInModel;
        }

        private void PlaceForAim()
        {
            if (_instance == null)
            {
                return;
            }

            var progress = Mathf.Clamp01(_motion.AdsProgress());
            _instance.transform.localPosition = Vector3.Lerp(_hipPosition, _adsPosition, progress);
            HideForSight(CurrentSightView().HidesViewmodel, false);
        }

        private SightView CurrentSightView()
        {
            if (SightSource != null)
            {
                return SightSource();
            }

            // 組み立てる側が AimDownSights をこれより後に付けるので、見つかるまで探し直す。
            if (_ads == null)
            {
                _ads = GetComponent<AimDownSights>();
            }

            return _ads != null && _ads.IsInitialized ? _ads.SightView : SightView.None;
        }

        // 消して出し直すと付けた物とソケットを作り直すことになり、銃口の炎の出る位置も失うので、描くのだけを止める。
        private void HideForSight(bool hide, bool force)
        {
            if (_instance == null || (!force && hide == _hiddenBySight))
            {
                return;
            }

            _hiddenBySight = hide;
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = !hide;
            }

            if (!force)
            {
                Debug.Log($"{LogTag} sight hidden={(hide ? "true" : "false")} weapon={_weaponId}", this);
            }
        }

        public static string FormatSpawnLog(string weaponId, string prefabName, bool hasMuzzle)
        {
            return $"{SpawnedLogPrefix} weapon={weaponId} prefab={prefabName} muzzle={hasMuzzle}";
        }

        public static string FormatSwapLog(string fromWeaponId, string toWeaponId, string prefabName, bool hasMuzzle)
        {
            return $"{SwappedLogPrefix} weapon={fromWeaponId} -> {toWeaponId} prefab={prefabName} muzzle={hasMuzzle}";
        }

        public static string FormatRemoveLog(string weaponId, string reason)
        {
            return $"{RemovedLogPrefix} weapon={weaponId} reason={reason}";
        }

        private string HiddenReason()
        {
            if (_rig == null || !_rig.IsInitialized || !_rig.enabled || !_rig.IsLocalView)
            {
                return ReasonNotLocalView;
            }

            if (_rig.Mode != ViewMode.FirstPerson)
            {
                return ReasonThirdPerson;
            }

            if (_damageable != null && _damageable.IsDead)
            {
                return ReasonDead;
            }

            if (_weapon != null && _weapon.IsHolstered)
            {
                return ReasonHolstered;
            }

            return null;
        }

        private string WantedWeaponId()
        {
            if (WeaponIdSource != null)
            {
                return WeaponIdSource();
            }

            var weapon = _weapon != null ? _weapon.Weapon : null;
            return weapon != null ? weapon.Spec.Id : null;
        }

        private IReadOnlyList<string> WantedAttachments(string weaponId)
        {
            if (AttachmentsSource != null)
            {
                return AttachmentsSource(weaponId) ?? WeaponAttachments.None;
            }

            return _weapon != null && _weapon.IsInitialized ? _weapon.AttachmentsOf(weaponId) : WeaponAttachments.None;
        }

        private void Attach(IReadOnlyList<string> attachments)
        {
            var catalog = _weapon != null ? _weapon.AttachmentCatalog : KitTuning.Items;
            WeaponAttachmentModels.Apply(_instance.transform, attachments, catalog, AttachmentModels);
            _attachments = attachments;
            ApplyPaint(WantedPaint());
            _sockets = new WeaponSockets(_instance.transform);
            var pivot = _instance.transform.parent;
            var sight = _sockets.Sight != null && pivot != null ? pivot.InverseTransformPoint(_sockets.Sight.position) - _instance.transform.localPosition : Vector3.zero;
            _adsPosition = AimedRootPosition(_entry.AdsOffset, sight);
            if (_presentation != null)
            {
                _presentation.WeaponModel = _instance.transform;
            }
        }

        private ViewmodelEntry FindEntry(string weaponId)
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                WarnMissingCatalog();
                return null;
            }

            if (!catalog.TryGetWeapon(weaponId, out var entry) || entry.Prefab == null)
            {
                if (_warnedWeaponIds.Add(weaponId))
                {
                    Debug.LogWarning($"{LogTag} weapon '{weaponId}' has no viewmodel prefab in the viewmodel catalog; no viewmodel is shown", this);
                }

                return null;
            }

            return entry;
        }

        private void Spawn(string weaponId, ViewmodelEntry entry)
        {
            var pivot = _motion.EnsurePivot();
            if (pivot == null)
            {
                return;
            }

            var previousId = _instance != null ? _weaponId : null;
            DestroyInstance();

            var instance = Instantiate(entry.Prefab, pivot, false);
            instance.name = entry.Prefab.name;
            instance.transform.localPosition = entry.Offset;
            instance.transform.localRotation = Quaternion.identity;
            PrepareForView(instance);
            _hipPosition = entry.Offset;

            _instance = instance;
            _weaponId = weaponId;
            _entry = entry;
            _attachments = WantedAttachments(weaponId);
            if (_attachments.Count > 0)
            {
                WeaponAttachmentModels.Apply(instance.transform, _attachments, _weapon != null ? _weapon.AttachmentCatalog : KitTuning.Items, AttachmentModels);
            }

            _paint = null;
            ApplyPaint(WantedPaint());
            _sockets = new WeaponSockets(instance.transform);
            var sight = _sockets.Sight != null ? pivot.InverseTransformPoint(_sockets.Sight.position) - entry.Offset : Vector3.zero;
            _adsPosition = AimedRootPosition(entry.AdsOffset, sight);
            if (_presentation != null)
            {
                _presentation.WeaponModel = instance.transform;
            }

            _motion.Settings = SwaySettingsFor(entry);
            _motion.ResetMotion();
            _hiddenBySight = false;

            var hasMuzzle = _sockets.Muzzle != null;
            Debug.Log(
                previousId == null
                    ? FormatSpawnLog(weaponId, instance.name, hasMuzzle)
                    : FormatSwapLog(previousId, weaponId, instance.name, hasMuzzle),
                this);
        }

        private void Remove(string reason, bool hideFirst = true)
        {
            if (_instance == null)
            {
                // 外から壊されていても、次に同じ武器を出すときに作り直させる。
                _weaponId = null;
                _sockets = WeaponSockets.None;
                return;
            }

            var weaponId = _weaponId;
            DestroyInstance(hideFirst);
            Debug.Log(FormatRemoveLog(weaponId, reason), this);
        }

        private void DestroyInstance(bool hideFirst = true)
        {
            if (_instance == null)
            {
                return;
            }

            if (_presentation != null && _presentation.WeaponModel == _instance.transform)
            {
                _presentation.WeaponModel = null;
            }

            // Destroy はフレームの終わりまで残り、同じフレームに出した次のモデルと重なって見えるので、先に隠す。
            if (hideFirst)
            {
                _instance.SetActive(false);
            }

            DestroySafely(_instance);
            _instance = null;
            _weaponId = null;
            _paint = null;
            _sockets = WeaponSockets.None;
        }

        private Material WantedPaint()
        {
            return PaintSource != null ? PaintSource() : CosmeticLooks.WornPaint();
        }

        private void ApplyPaint(Material paint)
        {
            if (_instance == null)
            {
                return;
            }

            var changed = paint != _paint;
            CamoPaint.Apply(_instance, paint);
            _paint = paint;
            if (changed)
            {
                Debug.Log($"{LogTag} paint weapon={_weaponId} paint={(paint != null ? paint.name : "-")}", this);
            }
        }

        private ViewmodelSwaySettings SwaySettingsFor(ViewmodelEntry entry)
        {
            if (entry.TryGetSwaySettings(out var settings, out var error))
            {
                return settings;
            }

            if (_warnedWeaponIds.Add("sway:" + entry.WeaponId))
            {
                Debug.LogWarning($"{LogTag} weapon '{entry.WeaponId}' has invalid sway values ({error}); using the defaults", this);
            }

            return ViewmodelSwaySettings.Default;
        }

        private static void PrepareForView(GameObject instance)
        {
            // 体の当たり判定と射撃の Ray に混ざらないよう、モデルの当たりは捨てる。
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                DestroySafely(collider);
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
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

        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _rig = GetComponent<ViewRig>();
            _weapon = GetComponentInChildren<WeaponController>(true);
            _damageable = GetComponent<Damageable>();
            _presentation = GetComponent<WeaponPresentation>();
            _motion = GetComponent<ViewmodelMotion>();
            if (_motion == null)
            {
                _motion = gameObject.AddComponent<ViewmodelMotion>();
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
                $"{LogTag} no ViewmodelCatalog at Resources/{ViewmodelCatalog.ResourcePath}; first-person viewmodels are off. "
                + "Run Shooter/Build All Presentation Assets (Shooter.Editor.PresentationAssetSteps.BuildAll)",
                this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlay()
        {
            _warnedWeaponIds.Clear();
            _warnedMissingCatalog = false;
        }
    }
}
