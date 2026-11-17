using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // 着弾には撃った者が載らないので、直前の ShotFired を出したインスタンスが出す (どちらの発生元も、1 発ごとに射撃→着弾の順に続けて出す)。
    // 部品をキャラの外の PoolRoot の下に置くのは、キャラが動いても着弾の跡を動かさないため。
    // 銃痕だけは撃った者が消えても残るよう、シーンに 1 つの ImpactDecals に貼る。
    [DisallowMultipleComponent]
    public sealed class WeaponPresentation : MonoBehaviour
    {
        public const string LogTag = "[Presentation]";

        public const int MuzzleFlashCapacity = 4;

        public const int ImpactSparkCapacity = 12;

        public const int TracerCapacity = 12;

        public const int SoundCapacity = 12;

        public const float TracerSpeed = 400f;

        public const float TracerLength = 3f;

        public const float TracerWidth = 0.03f;

        public const float MaxTracerDistance = 150f;

        // 銃口の先の壁で線が逆向きに見えないよう、これより近くに当たった弾には曳光弾を出さない。
        public const float MinTracerDistance = 1f;

        public const float SoundMinDistance = 2f;

        public const float SoundMaxDistance = 120f;

        public static readonly Vector3 MuzzleOffset = WeaponSockets.DefaultMuzzleOffset;

        private static readonly Color DefaultTracerColor = new Color(1f, 0.85f, 0.5f);

        // 有効なインスタンス。撃った者が分からない弾と着弾を出す代わりは先頭。
        private static readonly List<WeaponPresentation> _enabled = new List<WeaponPresentation>();
        private static readonly HashSet<string> _warnedWeaponIds = new HashSet<string>(StringComparer.Ordinal);
        private static WeaponPresentation _lastShotPresenter;
        private static bool _warnedMissingCatalog;

        private readonly Dictionary<GameObject, PresentationPool<PooledEffect>> _effectPools =
            new Dictionary<GameObject, PresentationPool<PooledEffect>>();

        private readonly List<PooledTracer> _tracerScratch = new List<PooledTracer>();
        private PresentationPool<PooledTracer> _tracers;
        private PresentationPool<AudioSource> _sounds;
        private int _impactSoundFrame = -1;
        private int _impactSoundsThisFrame;
        private Transform _poolRoot;
        private AudioSource _bodySource;

        private PresentationCatalog _catalog;
        private bool _catalogResolved;
        private SurfaceImpactCatalog _surfaceImpacts;
        private bool _surfaceImpactsResolved;
        private ImpactDecals _decals;
        private GameObject _tracerSource;
        private Material _tracerMaterial;
        private Gradient _tracerGradient;

        private WeaponController _controller;
        private Damageable _damageable;
        private bool _resolved;
        private bool? _isLocal;
        private bool _subscribedReload;
        private WeaponSockets _sockets = WeaponSockets.None;

        // 直前の射撃の曳光弾。続けて届く着弾で行き先を当たった点に縮める。
        private PooledTracer _pendingTracer;
        private int _pendingTracerFrame = -1;

        public PresentationCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = PresentationCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
                _tracerSource = null;
            }
        }

        public SurfaceImpactCatalog SurfaceImpacts
        {
            get
            {
                if (!_surfaceImpactsResolved)
                {
                    _surfaceImpacts = SurfaceImpactCatalog.Load();
                    _surfaceImpactsResolved = true;
                }

                return _surfaceImpacts;
            }
            set
            {
                _surfaceImpacts = value;
                _surfaceImpactsResolved = true;
            }
        }

        public ImpactDecals Decals
        {
            get
            {
                if (_decals == null)
                {
                    _decals = ImpactDecals.For(gameObject.scene);
                }

                return _decals;
            }
            set => _decals = value;
        }

        public bool IsLocal
        {
            get
            {
                if (_isLocal.HasValue)
                {
                    return _isLocal.Value;
                }

                EnsureResolved();
                return _controller != null || (_damageable != null && _damageable.IsLocalPlayer);
            }
            set
            {
                _isLocal = value;
                if (_bodySource != null)
                {
                    _bodySource.spatialBlend = value ? 0f : 1f;
                }
            }
        }

        public Transform WeaponModel
        {
            get => _sockets.ModelRoot;
            set => _sockets = value != null ? new WeaponSockets(value) : WeaponSockets.None;
        }

        public WeaponSockets Sockets => _sockets;

        public Transform PoolRoot => _poolRoot;

        public PresentationPool<PooledTracer> Tracers => _tracers;

        public PresentationPool<AudioSource> Sounds => _sounds;

        public AudioSource BodySource => _bodySource;

        public PresentationPool<PooledEffect> EffectPoolFor(GameObject prefab)
        {
            return prefab != null && _effectPools.TryGetValue(prefab, out var pool) ? pool : null;
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
            CombatEvents.ShotContinued -= HandleShotContinued;
            CombatEvents.ShotContinued += HandleShotContinued;
            CombatEvents.Impact -= HandleImpact;
            CombatEvents.Impact += HandleImpact;

            EnsureResolved();
            if (_controller != null && !_subscribedReload)
            {
                _controller.ReloadStarted += HandleReloadStarted;
                _subscribedReload = true;
            }
        }

        private void OnDisable()
        {
            _enabled.Remove(this);
            if (_lastShotPresenter == this)
            {
                _lastShotPresenter = null;
            }

            if (_enabled.Count == 0)
            {
                CombatEvents.ShotFired -= HandleShotFired;
                CombatEvents.ShotContinued -= HandleShotContinued;
                CombatEvents.Impact -= HandleImpact;
            }

            if (_subscribedReload && _controller != null)
            {
                _controller.ReloadStarted -= HandleReloadStarted;
            }

            _subscribedReload = false;
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

        public void PlayShot(ShotFiredInfo shot)
        {
            var now = Time.time;
            _pendingTracer = null;
            _pendingTracerFrame = Time.frameCount;

            var catalog = Catalog;
            if (catalog == null)
            {
                WarnMissingCatalog();
                return;
            }

            var own = shot.Shooter != null && FindFor(shot.Shooter) == this;
            var direction = shot.Direction.sqrMagnitude > 0f ? shot.Direction : transform.forward;
            var muzzlePose = (own ? _sockets : WeaponSockets.None).MuzzlePose(shot.Origin, direction);
            var muzzle = muzzlePose.position;

            if (catalog.TryGetWeapon(shot.WeaponId, out var assets))
            {
                if (assets.Fire != null)
                {
                    if (own)
                    {
                        PlayOnBody(assets.Fire);
                    }
                    else
                    {
                        PlayAt(assets.Fire, shot.Origin, now);
                    }

                    // 手元の銃声は耳元 (弾の始点) で鳴り位置では向きが決まらないので、銃を向けている先から聞こえたことにする。
                    var heardAt = own && IsLocal ? shot.Origin + direction : shot.Origin;
                    SubtitleFeed.Raise(SoundCueKind.Gunshot, heardAt, SoundMaxDistance);
                }

                var flash = assets.MuzzleFlashFor(own && IsLocal);
                if (flash != null)
                {
                    SpawnEffect(flash, MuzzleFlashCapacity, muzzle, muzzlePose.rotation, now);
                }
            }
            else
            {
                WarnUnknownWeapon(shot.WeaponId);
            }

            if (shot.Tracer)
            {
                _pendingTracer = LaunchTracer(catalog, muzzle, muzzle + (direction * MaxTracerDistance), now);
            }
        }

        public void PlayContinuation(ShotFiredInfo shot)
        {
            var now = Time.time;
            _pendingTracer = null;
            _pendingTracerFrame = Time.frameCount;

            var catalog = Catalog;
            if (catalog == null)
            {
                WarnMissingCatalog();
                return;
            }

            var direction = shot.Direction.sqrMagnitude > 0f ? shot.Direction : transform.forward;
            _pendingTracer = LaunchTracer(catalog, shot.Origin, shot.Origin + (direction * MaxTracerDistance), now);
        }

        public void PlayImpact(ImpactInfo impact)
        {
            var now = Time.time;
            var tracer = _pendingTracerFrame == Time.frameCount ? _pendingTracer : null;
            _pendingTracer = null;

            var catalog = Catalog;
            var surfaces = SurfaceImpacts;
            var surface = surfaces != null ? surfaces.Resolve(impact.MaterialId) : null;
            Debug.Log(FormatImpactLog(impact, ImpactSourceLabel(surfaces, surface)), this);

            if (catalog == null)
            {
                WarnMissingCatalog();
            }

            if (tracer != null)
            {
                RetargetTracer(tracer, impact.Point, now);
            }

            GameObject effect;
            AudioClip sound;
            if (surfaces != null)
            {
                effect = surface?.Effect;
                sound = surface?.Sound;
                if (surface != null && surface.Decal != null && !impact.IsCharacter)
                {
                    Decals.Place(impact, surface);
                }
            }
            else
            {
                effect = catalog != null ? catalog.ImpactSpark : null;
                sound = catalog != null ? catalog.ImpactSound : null;
            }

            var normal = impact.Normal.sqrMagnitude > 0f ? impact.Normal.normalized : Vector3.up;
            if (effect != null)
            {
                SpawnEffect(effect, ImpactSparkCapacity, impact.Point, Quaternion.LookRotation(normal), now);
            }

            if (sound != null && TakeImpactSoundSlot(catalog))
            {
                PlayAt(sound, impact.Point, now);
            }
        }

        public void PlayReload(string weaponId)
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                WarnMissingCatalog();
                return;
            }

            if (!catalog.TryGetWeapon(weaponId, out var assets))
            {
                WarnUnknownWeapon(weaponId);
                return;
            }

            if (assets.Reload != null)
            {
                PlayOnBody(assets.Reload);
                SubtitleFeed.Raise(SoundCueKind.Reload, IsLocal ? default(Vector3?) : transform.position, SoundMaxDistance);
            }
        }

        public void Tick(float now)
        {
            foreach (var pool in _effectPools.Values)
            {
                pool.ReleaseDue(now);
            }

            _sounds?.ReleaseDue(now);

            if (_tracers == null)
            {
                return;
            }

            _tracers.CopyActive(_tracerScratch);
            foreach (var tracer in _tracerScratch)
            {
                if (TracerSegment(tracer.From, tracer.To, now - tracer.StartTime, out var tail, out var head))
                {
                    tracer.Line.SetPosition(0, tail);
                    tracer.Line.SetPosition(1, head);
                }
                else
                {
                    _tracers.Release(tracer);
                }
            }

            _tracerScratch.Clear();
        }

        public static Vector3 MuzzlePosition(Vector3 origin, Vector3 direction)
        {
            return WeaponSockets.None.MuzzlePose(origin, direction).position;
        }

        public static string FormatImpactLog(ImpactInfo impact, string source)
        {
            return LogTag + " impact " + impact + " source=" + source;
        }

        public static string ImpactSourceLabel(SurfaceImpactCatalog surfaces, SurfaceImpactAssets surface)
        {
            if (surfaces == null)
            {
                return "legacy";
            }

            return surface != null ? "surface:" + surface.MaterialId : "none";
        }

        public static bool TracerSegment(Vector3 from, Vector3 to, float elapsed, out Vector3 tail, out Vector3 head)
        {
            var path = to - from;
            var distance = path.magnitude;
            var travelled = Mathf.Max(0f, elapsed) * TracerSpeed;
            var tailDistance = travelled - TracerLength;
            if (distance <= 0f || tailDistance >= distance)
            {
                tail = to;
                head = to;
                return false;
            }

            var direction = path / distance;
            head = from + (direction * Mathf.Min(travelled, distance));
            tail = from + (direction * Mathf.Clamp(tailDistance, 0f, distance));
            return true;
        }

        public static float TracerSeconds(Vector3 from, Vector3 to)
        {
            return (Vector3.Distance(from, to) + TracerLength) / TracerSpeed;
        }

        public static WeaponPresentation FindFor(GameObject shooter)
        {
            if (shooter == null)
            {
                return null;
            }

            var presenter = shooter.GetComponentInParent<WeaponPresentation>();
            return presenter != null && presenter.enabled ? presenter : null;
        }

        private static void HandleShotFired(ShotFiredInfo shot)
        {
            var presenter = FindFor(shot.Shooter);
            if (presenter == null || !presenter.isActiveAndEnabled)
            {
                presenter = Fallback();
            }

            _lastShotPresenter = presenter;
            if (presenter != null)
            {
                presenter.PlayShot(shot);
            }
        }

        private static void HandleShotContinued(ShotFiredInfo shot)
        {
            var presenter = FindFor(shot.Shooter);
            if (presenter == null || !presenter.isActiveAndEnabled)
            {
                presenter = Fallback();
            }

            _lastShotPresenter = presenter;
            if (presenter != null)
            {
                presenter.PlayContinuation(shot);
            }
        }

        private static void HandleImpact(ImpactInfo impact)
        {
            var presenter = _lastShotPresenter != null && _lastShotPresenter.isActiveAndEnabled ? _lastShotPresenter : Fallback();
            _lastShotPresenter = null;
            if (presenter != null)
            {
                presenter.PlayImpact(impact);
            }
        }

        private static WeaponPresentation Fallback()
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
            _enabled.Clear();
            _warnedWeaponIds.Clear();
            _lastShotPresenter = null;
            _warnedMissingCatalog = false;
        }

        private void HandleReloadStarted(Weapon weapon)
        {
            if (weapon != null)
            {
                PlayReload(weapon.Spec.Id);
            }
        }

        private void EnsureResolved()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _controller = GetComponentInChildren<WeaponController>(true);
            _damageable = GetComponent<Damageable>();
        }

        private void PlayOnBody(AudioClip clip)
        {
            if (_bodySource == null)
            {
                _bodySource = gameObject.AddComponent<AudioSource>();
                _bodySource.playOnAwake = false;
                ConfigureSpatial(_bodySource);
            }

            _bodySource.spatialBlend = IsLocal ? 0f : 1f;
            _bodySource.PlayOneShot(clip);
        }

        private bool TakeImpactSoundSlot(PresentationCatalog catalog)
        {
            if (_impactSoundFrame != Time.frameCount)
            {
                _impactSoundFrame = Time.frameCount;
                _impactSoundsThisFrame = 0;
            }

            var limit = catalog != null ? catalog.MaxImpactSoundsPerFrame : PresentationCatalog.DefaultMaxImpactSoundsPerFrame;
            if (_impactSoundsThisFrame >= limit)
            {
                return false;
            }

            _impactSoundsThisFrame++;
            return true;
        }

        private void PlayAt(AudioClip clip, Vector3 position, float now)
        {
            _sounds ??= new PresentationPool<AudioSource>(SoundCapacity, CreateSound, null, StopSound);
            var source = _sounds.Rent(now + clip.length + 0.05f);
            source.transform.position = position;
            source.clip = clip;
            source.Play();
        }

        private AudioSource CreateSound()
        {
            var soundObject = new GameObject("Sound");
            soundObject.transform.SetParent(EnsurePoolRoot(), false);
            var source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            ConfigureSpatial(source);
            return source;
        }

        private static void ConfigureSpatial(AudioSource source)
        {
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = SoundMinDistance;
            source.maxDistance = SoundMaxDistance;
            source.dopplerLevel = 0f;
        }

        private static void StopSound(AudioSource source)
        {
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }

        private void SpawnEffect(GameObject prefab, int capacity, Vector3 position, Quaternion rotation, float now)
        {
            if (!_effectPools.TryGetValue(prefab, out var pool))
            {
                pool = new PresentationPool<PooledEffect>(capacity, () => CreateEffect(prefab), null, HideEffect);
                _effectPools.Add(prefab, pool);
            }

            var effect = pool.Rent(now);
            pool.Extend(effect, now + effect.Seconds);
            effect.PlayAt(position, rotation);
        }

        private PooledEffect CreateEffect(GameObject prefab)
        {
            return PooledEffect.Instantiate(prefab, EnsurePoolRoot());
        }

        private static void HideEffect(PooledEffect effect)
        {
            effect.Hide();
        }

        private PooledTracer LaunchTracer(PresentationCatalog catalog, Vector3 from, Vector3 to, float now)
        {
            if (Vector3.Distance(from, to) < MinTracerDistance || !ResolveTracerLook(catalog))
            {
                return null;
            }

            _tracers ??= new PresentationPool<PooledTracer>(TracerCapacity, CreateTracer, null, HideTracer);
            var tracer = _tracers.Rent(now + TracerSeconds(from, to));
            tracer.From = from;
            tracer.To = to;
            tracer.StartTime = now;
            tracer.Line.sharedMaterial = _tracerMaterial;
            tracer.Line.colorGradient = _tracerGradient;

            // 出した瞬間に原点との間に線が出ないよう、表示する前に今の位置を置く。
            TracerSegment(from, to, 0f, out var tail, out var head);
            tracer.Line.SetPosition(0, tail);
            tracer.Line.SetPosition(1, head);
            tracer.Line.gameObject.SetActive(true);
            return tracer;
        }

        private void RetargetTracer(PooledTracer tracer, Vector3 point, float now)
        {
            if (Vector3.Distance(tracer.From, point) < MinTracerDistance)
            {
                _tracers.Release(tracer);
                return;
            }

            tracer.To = point;
            _tracers.Extend(tracer, tracer.StartTime + TracerSeconds(tracer.From, point));
            if (TracerSegment(tracer.From, tracer.To, now - tracer.StartTime, out var tail, out var head))
            {
                tracer.Line.SetPosition(0, tail);
                tracer.Line.SetPosition(1, head);
            }
        }

        // 曳光弾の材質と色は、カタログの曳光弾のエフェクトの描き方から借りる (粒ではなく線で描くため)。
        private bool ResolveTracerLook(PresentationCatalog catalog)
        {
            var source = catalog.Tracer;
            if (source == null)
            {
                return false;
            }

            if (source == _tracerSource)
            {
                return _tracerMaterial != null;
            }

            _tracerSource = source;
            var renderer = source.GetComponentInChildren<Renderer>(true);
            _tracerMaterial = renderer != null ? renderer.sharedMaterial : null;
            if (_tracerMaterial == null)
            {
                Debug.LogWarning($"{LogTag} tracer effect '{source.name}' has no renderer material; tracers are not drawn", this);
                return false;
            }

            var particles = source.GetComponentInChildren<ParticleSystem>(true);
            var color = particles != null ? particles.main.startColor.color : DefaultTracerColor;
            _tracerGradient = new Gradient();
            _tracerGradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 1f) });
            return true;
        }

        private PooledTracer CreateTracer()
        {
            var tracerObject = new GameObject("Tracer");
            tracerObject.SetActive(false);
            tracerObject.transform.SetParent(EnsurePoolRoot(), false);
            var line = tracerObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = TracerWidth;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return new PooledTracer(line);
        }

        private static void HideTracer(PooledTracer tracer)
        {
            if (tracer.Line != null)
            {
                tracer.Line.gameObject.SetActive(false);
            }
        }

        private Transform EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return _poolRoot;
            }

            var root = new GameObject($"{name} WeaponPresentation");
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
            foreach (var pool in _effectPools.Values)
            {
                pool.ReleaseDue(float.PositiveInfinity);
            }

            _tracers?.ReleaseDue(float.PositiveInfinity);
            _sounds?.ReleaseDue(float.PositiveInfinity);
            _pendingTracer = null;
        }

        private void DestroyPools()
        {
            foreach (var pool in _effectPools.Values)
            {
                pool.Clear(null);
            }

            _effectPools.Clear();
            _tracers?.Clear(null);
            _sounds?.Clear(null);
            _tracers = null;
            _sounds = null;
            _pendingTracer = null;

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

        private void WarnMissingCatalog()
        {
            if (_warnedMissingCatalog)
            {
                return;
            }

            _warnedMissingCatalog = true;
            Debug.LogWarning(
                $"{LogTag} no PresentationCatalog at Resources/{PresentationCatalog.ResourcePath}; weapon sounds and effects are off. "
                + "Run Shooter/Build Presentation Assets (Shooter.Editor.PresentationAssetBuilder.Build)",
                this);
        }

        private void WarnUnknownWeapon(string weaponId)
        {
            if (_warnedWeaponIds.Add(weaponId ?? string.Empty))
            {
                Debug.LogWarning($"{LogTag} weapon '{weaponId}' has no sounds or effects in the presentation catalog", this);
            }
        }
    }

    public sealed class PooledEffect
    {
        public const float DefaultSeconds = 1f;

        public PooledEffect(GameObject root, ParticleSystem[] systems, float seconds)
        {
            Root = root;
            Systems = systems ?? Array.Empty<ParticleSystem>();
            Seconds = seconds;
        }

        public static PooledEffect Instantiate(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            instance.SetActive(false);

            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            var seconds = 0f;
            foreach (var system in systems)
            {
                // プレハブは単発で自分を消す設定 (Destroy) なので、使い回せるよう止めるだけにする。
                var main = system.main;
                main.stopAction = ParticleSystemStopAction.None;
                seconds = Mathf.Max(seconds, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }

            return new PooledEffect(instance, systems, systems.Length > 0 ? seconds : DefaultSeconds);
        }

        public void PlayAt(Vector3 position, Quaternion rotation)
        {
            if (Root == null)
            {
                return;
            }

            Root.transform.SetPositionAndRotation(position, rotation);
            Root.SetActive(true);
            foreach (var system in Systems)
            {
                if (system != null)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.Play(false);
                }
            }
        }

        public void Hide()
        {
            if (Root == null)
            {
                return;
            }

            foreach (var system in Systems)
            {
                if (system != null)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            Root.SetActive(false);
        }

        public GameObject Root { get; }

        public ParticleSystem[] Systems { get; }

        public float Seconds { get; }
    }

    public sealed class PooledTracer
    {
        public PooledTracer(LineRenderer line)
        {
            Line = line;
        }

        public LineRenderer Line { get; }

        public Vector3 From { get; set; }

        public Vector3 To { get; set; }

        public float StartTime { get; set; }
    }
}
