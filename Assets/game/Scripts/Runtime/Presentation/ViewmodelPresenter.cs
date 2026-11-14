using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ViewmodelMotion))]
    public sealed class ViewmodelPresenter : MonoBehaviour
    {
        public const string LogTag = "[Viewmodel]";
        public const string SpawnedLogPrefix = LogTag + " spawned";
        public const string SwappedLogPrefix = LogTag + " swapped";
        public const string RemovedLogPrefix = LogTag + " removed";

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

        private GameObject _instance;
        private string _weaponId;
        private WeaponSockets _sockets = WeaponSockets.None;

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

        public GameObject Instance => _instance;

        public string CurrentWeaponId => _instance != null ? _weaponId : null;

        public WeaponSockets Sockets => _instance != null ? _sockets : WeaponSockets.None;

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
                return;
            }

            var entry = FindEntry(wanted);
            if (entry == null)
            {
                Remove(ReasonNoViewmodel);
                return;
            }

            Spawn(wanted, entry);
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

            _instance = instance;
            _weaponId = weaponId;
            _sockets = new WeaponSockets(instance.transform);
            if (_presentation != null)
            {
                _presentation.WeaponModel = instance.transform;
            }

            _motion.Settings = SwaySettingsFor(entry);
            _motion.ResetMotion();

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
            _sockets = WeaponSockets.None;
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
                + "Run Fps/Build All Presentation Assets (Fps.Editor.PresentationAssetSteps.BuildAll)",
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
