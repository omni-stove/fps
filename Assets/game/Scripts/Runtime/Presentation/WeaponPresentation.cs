using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// 武器の演出 (銃声・マズルフラッシュ・曳光弾・着弾の火花と音・リロード音) を受け持つ。撃つ人 (プレイヤー・NetPlayer・敵) の本体に付ける。
    /// 音とエフェクトは <see cref="Catalog"/> (<see cref="PresentationCatalog"/>) から武器の Id で引く。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="CombatEvents.ShotFired"/> と <see cref="CombatEvents.Impact"/> は、有効なインスタンス全体で 1 回だけ購読し、
    /// 撃った者 (<see cref="ShotFiredInfo.Shooter"/>) の本体のインスタンスに渡す。撃った者が分からない・演出が無い弾は、有効なうちの 1 つが出す。
    /// 着弾には撃った者が載らないので、直前の ShotFired を出したインスタンスが出す (どちらの発生元も、1 発ごとに射撃→着弾の順に続けて出す)。</item>
    /// <item>自分 (<see cref="IsLocal"/>) の銃声とリロード音は 2D、他人の銃声は本体の 3D の音源、着弾と撃った者の分からない銃声は
    /// その場所に置いた 3D の音源で鳴らし、距離で減衰させる。</item>
    /// <item>マズルフラッシュ・着弾の火花・曳光弾 (短い <see cref="LineRenderer"/>)・3D の音源は <see cref="PresentationPool{T}"/> で使い回し、
    /// 上限を超えたら古いものから取り上げる。部品はキャラの外の <see cref="PoolRoot"/> の下に置く (キャラが動いても着弾の跡は動かない)。</item>
    /// <item>リロード音は、同じ階層の <see cref="WeaponController.ReloadStarted"/> (オフラインのプレイヤー) と、
    /// <see cref="PlayReload"/> を呼ぶ側 (ネット対戦では <c>Fps.Net.NetPlayer</c>) から鳴らす。</item>
    /// <item>銃声とリロード音を鳴らしたら、字幕のために <see cref="SubtitleFeed.Raise"/> で知らせる。自分のリロード音は位置を付けない。</item>
    /// </list>
    /// 専用サーバーでは <c>Fps.Net.HeadlessPresentation</c> が止め、ここでも有効になったときに自分を止める。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WeaponPresentation : MonoBehaviour
    {
        public const string LogTag = "[Presentation]";

        /// <summary>マズルフラッシュのプール (武器のエフェクトごと) の上限。</summary>
        public const int MuzzleFlashCapacity = 4;

        /// <summary>着弾の火花のプールの上限。</summary>
        public const int ImpactSparkCapacity = 12;

        /// <summary>曳光弾のプールの上限。</summary>
        public const int TracerCapacity = 12;

        /// <summary>場所に置く 3D の音源のプールの上限。</summary>
        public const int SoundCapacity = 12;

        /// <summary>曳光弾の速さ (m/s)。</summary>
        public const float TracerSpeed = 400f;

        /// <summary>曳光弾の線の長さ (m)。</summary>
        public const float TracerLength = 3f;

        /// <summary>曳光弾の線の太さ (m)。</summary>
        public const float TracerWidth = 0.03f;

        /// <summary>何にも当たらなかった弾の曳光弾が飛ぶ距離 (m)。</summary>
        public const float MaxTracerDistance = 150f;

        /// <summary>これより近くに当たった弾には曳光弾を出さない (m)。銃口の先の壁で線が逆向きに見えないよう。</summary>
        public const float MinTracerDistance = 1f;

        /// <summary>3D の音が減衰し始める距離 (m)。</summary>
        public const float SoundMinDistance = 2f;

        /// <summary>3D の音が聞こえなくなる距離 (m)。</summary>
        public const float SoundMaxDistance = 120f;

        /// <summary>
        /// 銃口のソケットが無いときの銃口の位置 (<see cref="WeaponSockets.DefaultMuzzleOffset"/>)。
        /// <see cref="WeaponModel"/> に <see cref="WeaponSockets.MuzzleName"/> があればそちらを使う。
        /// </summary>
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
        private Transform _poolRoot;
        private AudioSource _bodySource;

        private PresentationCatalog _catalog;
        private bool _catalogResolved;
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

        /// <summary>
        /// 音とエフェクトを引く設定。初めて読んだときに <see cref="PresentationCatalog.Load"/> で探し、無ければ null (何も出さない)。
        /// 代入すると探さずにそれを使う。
        /// </summary>
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

        /// <summary>
        /// この本体が手元のプレイヤーか (銃声とリロード音を 2D で鳴らす)。代入しなければ、同じ階層に <see cref="WeaponController"/> があるか
        /// <see cref="Damageable.IsLocalPlayer"/> なら true。ネット対戦では <c>Fps.Net.NetPlayer</c> が持ち主かどうかを代入する。
        /// </summary>
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

        /// <summary>
        /// 自分の弾の武器のモデルの根。代入するとその配下からソケットを探し直す (<see cref="Sockets"/>)。null ならソケットを持たない。
        /// 武器を持ち替えてモデルを差し替えたら、新しいモデルを代入し直す。
        /// </summary>
        public Transform WeaponModel
        {
            get => _sockets.ModelRoot;
            set => _sockets = value != null ? new WeaponSockets(value) : WeaponSockets.None;
        }

        /// <summary>
        /// 自分の弾のマズルフラッシュと曳光弾を出す位置と向き (<see cref="WeaponSockets.MuzzlePose"/>)。
        /// 銃口のソケットが無ければ弾の始点から <see cref="MuzzleOffset"/> の位置。
        /// </summary>
        public WeaponSockets Sockets => _sockets;

        /// <summary>プールの部品を置く親。初めて部品を作るまでは null。</summary>
        public Transform PoolRoot => _poolRoot;

        /// <summary>曳光弾のプール。初めて曳光弾を出すまでは null。</summary>
        public PresentationPool<PooledTracer> Tracers => _tracers;

        /// <summary>場所に置く 3D の音源のプール。初めて使うまでは null。</summary>
        public PresentationPool<AudioSource> Sounds => _sounds;

        /// <summary>自分の銃声とリロード音を鳴らす本体の音源。初めて鳴らすまでは null。</summary>
        public AudioSource BodySource => _bodySource;

        /// <summary>prefab のエフェクトのプール。まだ一度も出していなければ null。</summary>
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

        /// <summary>
        /// shot の 1 発の銃声・マズルフラッシュ・曳光弾を出す。shot の撃った者がこの本体なら本体の音源 (自分なら 2D) で、
        /// そうでなければ弾の始点に置いた 3D の音源で鳴らす。武器の Id がカタログに無ければ、銃声とマズルフラッシュは出さずに曳光弾だけ出す。
        /// </summary>
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

                if (assets.MuzzleFlash != null)
                {
                    SpawnEffect(assets.MuzzleFlash, MuzzleFlashCapacity, muzzle, muzzlePose.rotation, now);
                }
            }
            else
            {
                WarnUnknownWeapon(shot.WeaponId);
            }

            _pendingTracer = LaunchTracer(catalog, muzzle, muzzle + (direction * MaxTracerDistance), now);
        }

        /// <summary>
        /// impact の着弾の火花と音を出す。同じフレームの直前の <see cref="PlayShot"/> の曳光弾は、当たった点で止まるようにする。
        /// 素材の有無にかかわらず、1 回ごとに材質と部位を <see cref="FormatImpactLog"/> の形でログに出す。
        /// </summary>
        public void PlayImpact(ImpactInfo impact)
        {
            Debug.Log(FormatImpactLog(impact), this);
            var now = Time.time;
            var tracer = _pendingTracerFrame == Time.frameCount ? _pendingTracer : null;
            _pendingTracer = null;

            var catalog = Catalog;
            if (catalog == null)
            {
                WarnMissingCatalog();
                return;
            }

            if (tracer != null)
            {
                RetargetTracer(tracer, impact.Point, now);
            }

            var normal = impact.Normal.sqrMagnitude > 0f ? impact.Normal.normalized : Vector3.up;
            if (catalog.ImpactSpark != null)
            {
                SpawnEffect(catalog.ImpactSpark, ImpactSparkCapacity, impact.Point, Quaternion.LookRotation(normal), now);
            }

            if (catalog.ImpactSound != null)
            {
                PlayAt(catalog.ImpactSound, impact.Point, now);
            }
        }

        /// <summary>weaponId の武器のリロード音を本体の音源 (自分なら 2D) で鳴らす。カタログに無い武器なら何もしない。</summary>
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

        /// <summary>時刻 now (<see cref="Time.time"/> と同じ物差し) まで進め、曳光弾を動かし、出し終えた部品をプールに返す。</summary>
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

        /// <summary>
        /// 弾の始点 origin から direction へ撃ったときの、銃口のソケットが無いときの銃口の位置
        /// (<see cref="WeaponSockets.None"/> の <see cref="WeaponSockets.MuzzlePose"/>)。
        /// </summary>
        public static Vector3 MuzzlePosition(Vector3 origin, Vector3 direction)
        {
            return WeaponSockets.None.MuzzlePose(origin, direction).position;
        }

        /// <summary>着弾の演出を出すときのログ。例: <c>[Presentation] impact material=default character=True zone=Head</c>。</summary>
        public static string FormatImpactLog(ImpactInfo impact)
        {
            return LogTag + " impact " + impact;
        }

        /// <summary>
        /// from から to へ飛ぶ曳光弾の、撃ってから elapsed 秒後の線 (後ろの端 tail と先の端 head)。先は <see cref="TracerSpeed"/> で進み、
        /// 線の長さは <see cref="TracerLength"/> で、どちらの端も to を越えない。後ろの端が to に着いた (飛び終えた) ら false。
        /// </summary>
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

        /// <summary>from から to までの曳光弾が飛び終えるまでの秒数。</summary>
        public static float TracerSeconds(Vector3 from, Vector3 to)
        {
            return (Vector3.Distance(from, to) + TracerLength) / TracerSpeed;
        }

        /// <summary>shooter の本体 (自分か親) の WeaponPresentation。無いか無効なら null。</summary>
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
            effect.Root.transform.SetPositionAndRotation(position, rotation);
            effect.Root.SetActive(true);
            foreach (var system in effect.Systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(false);
            }
        }

        private PooledEffect CreateEffect(GameObject prefab)
        {
            var instance = Instantiate(prefab, EnsurePoolRoot(), false);
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

            return new PooledEffect(instance, systems, systems.Length > 0 ? seconds : PooledEffect.DefaultSeconds);
        }

        private static void HideEffect(PooledEffect effect)
        {
            if (effect.Root == null)
            {
                return;
            }

            foreach (var system in effect.Systems)
            {
                if (system != null)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            effect.Root.SetActive(false);
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
                + "Run Fps/Build Presentation Assets (Fps.Editor.PresentationAssetBuilder.Build)",
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

    /// <summary><see cref="WeaponPresentation"/> がプールで使い回すエフェクト 1 つ。</summary>
    public sealed class PooledEffect
    {
        /// <summary>粒の系が無いエフェクトを出しておく秒数。</summary>
        public const float DefaultSeconds = 1f;

        public PooledEffect(GameObject root, ParticleSystem[] systems, float seconds)
        {
            Root = root;
            Systems = systems ?? Array.Empty<ParticleSystem>();
            Seconds = seconds;
        }

        /// <summary>プレハブから作ったインスタンス。</summary>
        public GameObject Root { get; }

        /// <summary>インスタンスの中の粒の系 (子を含む)。</summary>
        public ParticleSystem[] Systems { get; }

        /// <summary>1 回出してから消すまでの秒数 (いちばん長く残る粒が消えるまで)。</summary>
        public float Seconds { get; }
    }

    /// <summary><see cref="WeaponPresentation"/> がプールで使い回す曳光弾 1 本。</summary>
    public sealed class PooledTracer
    {
        public PooledTracer(LineRenderer line)
        {
            Line = line;
        }

        public LineRenderer Line { get; }

        /// <summary>飛び始めの点 (銃口)。</summary>
        public Vector3 From { get; set; }

        /// <summary>飛び終える点 (当たった点か、届く限り)。</summary>
        public Vector3 To { get; set; }

        /// <summary>撃った時刻 (<see cref="Time.time"/>)。</summary>
        public float StartTime { get; set; }
    }
}
