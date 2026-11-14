using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// 撃つたびに排莢口から薬莢を物理で右上へ飛ばし、最初に何かに当たったときに落下音を 1 回鳴らす。撃つ人の本体に付ける。
    /// 素材は <see cref="WeaponExtrasCatalog"/> から武器の Id で引く。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="CombatEvents.ShotFired"/> を各インスタンスが自分で購読し、撃った者 (<see cref="ShotFiredInfo.Shooter"/>) の本体が自分の弾だけを扱う。
    /// <see cref="WeaponPresentation"/> の経路に乗せないのは、薬莢の有無で銃声や着弾の演出の順番を変えないため。</item>
    /// <item>排莢口は同じ本体の <see cref="WeaponPresentation.Sockets"/> の <see cref="WeaponSockets.ShellEjectPose"/>。
    /// WeaponPresentation が無ければソケットの無い既定の位置。</item>
    /// <item>薬莢はプレハブごとの <see cref="PresentationPool{T}"/> で <see cref="ShellCapacity"/> 個まで使い回し、<see cref="ShellLifetimeSeconds"/> で隠す。
    /// 部品はキャラの外の <see cref="PoolRoot"/> の下に置き、撃った本人の体とは当たらないようにする。</item>
    /// </list>
    /// 専用サーバーでは有効になったときに自分を止める (<c>Fps.Net.HeadlessPresentation</c> も止める)。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ShellEjector : MonoBehaviour
    {
        public const string LogTag = WeaponPresentation.LogTag;

        /// <summary>薬莢のプール (薬莢のプレハブごと) の上限。</summary>
        public const int ShellCapacity = 16;

        /// <summary>出してから隠すまでの秒数。</summary>
        public const float ShellLifetimeSeconds = 4f;

        /// <summary>排莢口の向き (+Z) へ飛ばす速さ (m/s)。</summary>
        public const float EjectSpeed = 2.2f;

        /// <summary>上へ足す速さ (m/s)。</summary>
        public const float EjectUpSpeed = 1.6f;

        /// <summary>飛ばす速さに足すばらつきの最大 (m/s)。毎回同じ軌道で重ならないように。</summary>
        public const float EjectJitter = 0.3f;

        /// <summary>薬莢の回る速さ (rad/s)。</summary>
        public const float EjectSpin = 25f;

        /// <summary>落下音が減衰し始める距離 (m)。</summary>
        public const float LandSoundMinDistance = 1f;

        /// <summary>落下音が聞こえなくなる距離 (m)。銃声より小さい音なので近くでしか聞こえない。</summary>
        public const float LandSoundMaxDistance = 25f;

        /// <summary>
        /// 薬莢を置くレイヤー。<see cref="Physics.DefaultRaycastLayers"/> に含まれないので、射撃の Raycast を遮らない
        /// (<see cref="DebrisPool.DebrisLayer"/> と同じ)。
        /// </summary>
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

        /// <summary>
        /// 素材を引く設定。初めて読んだときに <see cref="WeaponExtrasCatalog.Load"/> で探し、無ければ null (薬莢を出さない)。
        /// 代入すると探さずにそれを使う。
        /// </summary>
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

        /// <summary>薬莢を出す位置。同じ本体の <see cref="WeaponPresentation.Sockets"/>、無ければ <see cref="WeaponSockets.None"/>。</summary>
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

        /// <summary>プールの部品を置く親。初めて薬莢を出すまでは null。</summary>
        public Transform PoolRoot => _poolRoot;

        /// <summary>prefab の薬莢のプール。まだ一度も出していなければ null。</summary>
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

        /// <summary>
        /// shot の 1 発の薬莢を排莢口から飛ばし、出した薬莢を返す。カタログ・武器の素材・薬莢のプレハブのどれかが無ければ何もせず null
        /// (カタログが無いときと武器が無いときは 1 度だけ警告する)。
        /// </summary>
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

        /// <summary>
        /// shell が最初に何かに当たったときに呼ぶ (<see cref="ShellCasing"/> が呼ぶ)。落下音を薬莢の位置で 1 回鳴らして true。
        /// 貸していない・もう鳴らした薬莢なら何もせず false。
        /// </summary>
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

        /// <summary>時刻 now (<see cref="Time.time"/> と同じ物差し) まで進め、出してから <see cref="ShellLifetimeSeconds"/> 経った薬莢を隠す。</summary>
        public void Tick(float now)
        {
            foreach (var pool in _pools.Values)
            {
                pool.ReleaseDue(now);
            }
        }

        /// <summary>排莢口の向き rotation の +Z へ <see cref="EjectSpeed"/>、上へ <see cref="EjectUpSpeed"/> で飛ばし、jitter を足した速度。</summary>
        public static Vector3 EjectVelocity(Quaternion rotation, Vector3 jitter)
        {
            return (rotation * Vector3.forward * EjectSpeed) + (Vector3.up * EjectUpSpeed) + jitter;
        }

        /// <summary>薬莢を出したときのログ。例: <c>[Presentation] shell eject weapon=rifle-id pos=(0.18,-0.10,0.30)</c>。</summary>
        public static string FormatEjectLog(string weaponId, Vector3 position)
        {
            return LogTag + " shell eject weapon=" + (weaponId ?? string.Empty) + " pos=("
                + position.x.ToString("F2", CultureInfo.InvariantCulture) + ","
                + position.y.ToString("F2", CultureInfo.InvariantCulture) + ","
                + position.z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>薬莢が接地したときのログ。例: <c>[Presentation] shell land weapon=rifle-id sound=ShellDrop</c>。音が無ければ <c>sound=none</c>。</summary>
        public static string FormatLandLog(string weaponId, AudioClip sound)
        {
            return LogTag + " shell land weapon=" + (weaponId ?? string.Empty) + " sound=" + (sound != null ? sound.name : "none");
        }

        /// <summary>shooter の本体 (自分か親) の ShellEjector。無いか無効なら null。</summary>
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

    /// <summary><see cref="ShellEjector"/> がプールで使い回す薬莢 1 つ。</summary>
    public sealed class PooledShell
    {
        public PooledShell(GameObject root, Rigidbody body, AudioSource source, Collider[] colliders)
        {
            Root = root;
            Body = body;
            Source = source;
            Colliders = colliders ?? Array.Empty<Collider>();
        }

        /// <summary>プレハブから作ったインスタンス。</summary>
        public GameObject Root { get; }

        public Rigidbody Body { get; }

        /// <summary>落下音を鳴らす音源。</summary>
        public AudioSource Source { get; }

        public Collider[] Colliders { get; }

        /// <summary>この薬莢を出した弾の武器の Id。</summary>
        public string WeaponId { get; set; } = string.Empty;

        /// <summary>接地したときに鳴らす音。無ければ null。</summary>
        public AudioClip LandSound { get; set; }

        /// <summary>出してから落下音を鳴らしたか。</summary>
        public bool Landed { get; set; }
    }
}
