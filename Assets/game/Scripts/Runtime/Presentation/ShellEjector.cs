using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // WeaponPresentation の経路に乗せず CombatEvents.ShotFired を各インスタンスが自分で購読するのは、薬莢の有無で銃声や着弾の演出の順番を変えないため。
    [DisallowMultipleComponent]
    public sealed class ShellEjector : MonoBehaviour
    {
        public const string LogTag = WeaponPresentation.LogTag;

        public const int ShellCapacity = 16;

        public const float ShellLifetimeSeconds = 4f;

        public const float EjectSpeed = 2.2f;

        public const float EjectUpSpeed = 1.6f;

        // 毎回同じ軌道で重ならないよう、飛ばす速さにばらつきを足す。
        public const float EjectJitter = 0.3f;

        public const float EjectSpin = 25f;

        public const float LandSoundMinDistance = 1f;

        // 銃声より小さい音なので近くでしか聞こえないようにする。
        public const float LandSoundMaxDistance = 25f;

        // Physics.DefaultRaycastLayers に含まれないレイヤーに置き、射撃の Raycast を遮らないようにする (DebrisPool.DebrisLayer と同じ)。
        public const int ShellLayer = DebrisPool.DebrisLayer;

        private static readonly HashSet<string> _warnedWeaponIds = new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<GameObject, PresentationPool<PooledShell>> _pools =
            new Dictionary<GameObject, PresentationPool<PooledShell>>();

        private WeaponExtrasCatalog _catalog;
        private bool _catalogResolved;
        private WeaponPresentation _presentation;
        private bool _presentationResolved;
        private Collider[] _ownColliders;
        private Transform _poolRoot;

        public WeaponExtrasCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = WeaponExtrasCatalog.Load();
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

        public WeaponSockets Sockets
        {
            get
            {
                if (!_presentationResolved)
                {
                    _presentation = GetComponent<WeaponPresentation>();
                    _presentationResolved = true;
                }

                return _presentation != null ? _presentation.Sockets : WeaponSockets.None;
            }
        }

        public Transform PoolRoot => _poolRoot;

        public PresentationPool<PooledShell> PoolFor(GameObject prefab)
        {
            return prefab != null && _pools.TryGetValue(prefab, out var pool) ? pool : null;
        }

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            // ClearSubscribers で外されていても戻るよう、有効になるたびに付け直す (二重には付かない)。
            CombatEvents.ShotFired -= HandleShotFired;
            CombatEvents.ShotFired += HandleShotFired;
        }

        private void OnDisable()
        {
            CombatEvents.ShotFired -= HandleShotFired;
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

        public PooledShell Eject(ShotFiredInfo shot)
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                WeaponExtrasCatalog.WarnMissingOnce(this);
                return null;
            }

            if (!catalog.TryGetWeapon(shot.WeaponId, out var assets))
            {
                if (_warnedWeaponIds.Add(shot.WeaponId))
                {
                    Debug.LogWarning($"{LogTag} weapon '{shot.WeaponId}' has no shell casing in the weapon extras catalog", this);
                }

                return null;
            }

            var prefab = assets.ShellPrefab;
            if (prefab == null)
            {
                return null;
            }

            var now = Time.time;
            var direction = shot.Direction.sqrMagnitude > 0f ? shot.Direction : transform.forward;
            var pose = Sockets.ShellEjectPose(shot.Origin, direction);

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new PresentationPool<PooledShell>(ShellCapacity, () => CreateShell(prefab), null, HideShell);
                _pools.Add(prefab, pool);
            }

            var shell = pool.Rent(now + ShellLifetimeSeconds);
            shell.WeaponId = shot.WeaponId;
            shell.LandSound = assets.ShellLandSound;
            shell.Landed = false;
            shell.Root.transform.SetPositionAndRotation(pose.position, pose.rotation);
            shell.Root.SetActive(true);
            IgnoreOwnBody(shell);

            var velocity = EjectVelocity(pose.rotation, UnityEngine.Random.insideUnitSphere * EjectJitter);
            if (shell.Body != null)
            {
                shell.Body.isKinematic = false;
                shell.Body.linearVelocity = velocity;
                shell.Body.angularVelocity = pose.rotation * new Vector3(0f, EjectSpin, EjectSpin * 0.3f);
            }

            Debug.Log(FormatEjectLog(shot.WeaponId, pose.position), this);
            return shell;
        }

        public bool Land(PooledShell shell)
        {
            if (shell == null || shell.Landed || shell.Root == null || !shell.Root.activeSelf)
            {
                return false;
            }

            shell.Landed = true;
            Debug.Log(FormatLandLog(shell.WeaponId, shell.LandSound), this);
            if (shell.LandSound != null && shell.Source != null)
            {
                shell.Source.PlayOneShot(shell.LandSound);
            }

            return true;
        }

        public void Tick(float now)
        {
            foreach (var pool in _pools.Values)
            {
                pool.ReleaseDue(now);
            }
        }

        public static Vector3 EjectVelocity(Quaternion rotation, Vector3 jitter)
        {
            return (rotation * Vector3.forward * EjectSpeed) + (Vector3.up * EjectUpSpeed) + jitter;
        }

        public static string FormatEjectLog(string weaponId, Vector3 position)
        {
            return LogTag + " shell eject weapon=" + (weaponId ?? string.Empty) + " pos=("
                + position.x.ToString("F2", CultureInfo.InvariantCulture) + ","
                + position.y.ToString("F2", CultureInfo.InvariantCulture) + ","
                + position.z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        public static string FormatLandLog(string weaponId, AudioClip sound)
        {
            return LogTag + " shell land weapon=" + (weaponId ?? string.Empty) + " sound=" + (sound != null ? sound.name : "none");
        }

        public static ShellEjector FindFor(GameObject shooter)
        {
            if (shooter == null)
            {
                return null;
            }

            var ejector = shooter.GetComponentInParent<ShellEjector>();
            return ejector != null && ejector.enabled ? ejector : null;
        }

        private void HandleShotFired(ShotFiredInfo shot)
        {
            if (FindFor(shot.Shooter) == this)
            {
                Eject(shot);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _warnedWeaponIds.Clear();
        }

        private PooledShell CreateShell(GameObject prefab)
        {
            var instance = Instantiate(prefab, EnsurePoolRoot(), false);
            instance.name = prefab.name;
            instance.SetActive(false);
            SetLayer(instance.transform, ShellLayer);

            if (instance.GetComponentInChildren<Collider>(true) == null)
            {
                instance.AddComponent<BoxCollider>();
            }

            var body = instance.GetComponentInChildren<Rigidbody>(true);
            if (body == null)
            {
                body = instance.AddComponent<Rigidbody>();
                body.mass = 0.01f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            var source = instance.GetComponent<AudioSource>();
            if (source == null)
            {
                source = instance.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = LandSoundMinDistance;
            source.maxDistance = LandSoundMaxDistance;
            source.dopplerLevel = 0f;

            var shell = new PooledShell(instance, body, source, instance.GetComponentsInChildren<Collider>(true));
            var casing = body.GetComponent<ShellCasing>();
            if (casing == null)
            {
                casing = body.gameObject.AddComponent<ShellCasing>();
            }

            casing.Bind(this, shell);
            return shell;
        }

        private static void HideShell(PooledShell shell)
        {
            if (shell.Root == null)
            {
                return;
            }

            if (shell.Source != null)
            {
                shell.Source.Stop();
            }

            shell.Root.SetActive(false);
        }

        // 無効にしたコライダーは衝突の除外を忘れるので、出すたびに付け直す。
        private void IgnoreOwnBody(PooledShell shell)
        {
            _ownColliders ??= GetComponentsInChildren<Collider>(true);
            foreach (var own in _ownColliders)
            {
                if (own == null)
                {
                    continue;
                }

                foreach (var shellCollider in shell.Colliders)
                {
                    if (shellCollider != null)
                    {
                        Physics.IgnoreCollision(own, shellCollider, true);
                    }
                }
            }
        }

        private static void SetLayer(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            for (var i = 0; i < target.childCount; i++)
            {
                SetLayer(target.GetChild(i), layer);
            }
        }

        private Transform EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return _poolRoot;
            }

            var root = new GameObject($"{name} ShellEjector");
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
            foreach (var pool in _pools.Values)
            {
                pool.ReleaseDue(float.PositiveInfinity);
            }
        }

        private void DestroyPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear(null);
            }

            _pools.Clear();
            if (_poolRoot != null)
            {
                // 部品はすべてこの下にあるので、親ごと消せば残らない。
                if (Application.isPlaying)
                {
                    Destroy(_poolRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(_poolRoot.gameObject);
                }
            }

            _poolRoot = null;
        }
    }

    public sealed class PooledShell
    {
        public PooledShell(GameObject root, Rigidbody body, AudioSource source, Collider[] colliders)
        {
            Root = root;
            Body = body;
            Source = source;
            Colliders = colliders ?? Array.Empty<Collider>();
        }

        public GameObject Root { get; }

        public Rigidbody Body { get; }

        public AudioSource Source { get; }

        public Collider[] Colliders { get; }

        public string WeaponId { get; set; } = string.Empty;

        public AudioClip LandSound { get; set; }

        public bool Landed { get; set; }
    }
}
