using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Fps.Runtime
{
    /// <summary>
    /// キャラの足音・着地音・被弾の声を鳴らす。プレイヤー・NetPlayer・敵の本体に付ける (<see cref="CharacterComponents.Attach"/>)。
    /// <list type="bullet">
    /// <item>足音と着地音: 毎フレームの終わりに本体の位置の差を測り、同じ本体の <see cref="ICharacterMotionSource"/> の接地・しゃがみ・ダッシュと
    /// 一緒に <see cref="FootstepCadence"/> へ渡す。位置の差で測るので、自分で動かすプレイヤー・NavMesh で動く敵・
    /// サーバーから届いた位置を補間して表示するネット対戦の他人のどれも同じ規則で鳴る。
    /// ダッシュ中かが同期されていない (他人の) キャラは、速さから推す (<see cref="FootstepCadence.IsSprintingBySpeed"/>)。</item>
    /// <item>被弾の声: 本体に <see cref="Damageable"/> があればその <see cref="Damageable.Damaged"/> で、無ければ <see cref="ICharacterHurtSource"/> で、
    /// この画面で操作しているキャラなら <see cref="CombatEvents.DamageTaken"/>、他人なら <see cref="ICharacterHurtSource.Hurt"/> で鳴らす。
    /// 連射や散弾で続けて当たっても <see cref="HurtVoiceCooldownSeconds"/> の間は鳴らし直さない。</item>
    /// </list>
    /// 足音と着地音は、鳴らすときに足元へ短く Raycast して床の材質 (<see cref="SurfaceMaterial.IdOf"/>) を引き、
    /// <see cref="SurfaceCatalog"/> の組からランダムに選ぶ (直前と同じ音は避ける)。組が無ければ <see cref="FootstepSurfaceCatalog.CollectClips"/> の順に
    /// default の組、<see cref="Catalog"/> の <see cref="PresentationCatalog.Footstep"/> / <see cref="PresentationCatalog.Landing"/> を使う。
    /// 足元の材質が変わったら <see cref="FootstepLogTag"/> 付きでログに出す。
    /// 被弾の声は <see cref="Catalog"/> の <see cref="PresentationCatalog.HurtVoice"/>。
    /// 無いものは鳴らさないだけで、動きや戦闘の処理は止めない。AudioSource は初めて鳴らすときに本体へ足す (足と声で 2 つ)。
    /// 他人の足音と着地音を鳴らしたら、字幕のために <see cref="SubtitleFeed.Raise"/> で知らせる。
    /// 銃声・着弾・リロードの音は <see cref="WeaponPresentation"/> が鳴らす。
    /// 専用サーバーでは <c>Fps.Net.HeadlessPresentation</c> が止める。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAudio : MonoBehaviour
    {
        public const string LogTag = "[Audio]";

        /// <summary>足元の材質が変わったときのログの頭。</summary>
        public const string FootstepLogTag = "[Footstep]";

        /// <summary>足元を調べる Raycast を、足の高さからこれだけ (m) 上から始める。坂や段差で足が床に少し埋まっていても床を拾える。</summary>
        public const float SurfaceProbeLift = 0.3f;

        /// <summary>足の高さからこれだけ (m) 下まで床を探す。接地の判定の誤差と、ネット対戦の補間で浮いた分を拾える長さ。</summary>
        public const float SurfaceProbeDepth = 0.5f;

        private const int SurfaceProbeBufferSize = 8;

        /// <summary>この距離 (m) までは音量を下げない。</summary>
        public const float DefaultMinDistance = 1.5f;

        /// <summary>この距離 (m) で聞こえなくなる。遠くの敵の足音で近くの音を埋めないよう、直線で 0 まで下げる。</summary>
        public const float DefaultMaxDistance = 30f;

        /// <summary>被弾の声を鳴らしてから次を鳴らせるまでの秒数。</summary>
        public const float HurtVoiceCooldownSeconds = 0.4f;

        /// <summary>足音の高さを毎回この幅 (±) で揺らす。同じ音の繰り返しに聞こえないようにする。</summary>
        public const float FootstepPitchJitter = 0.06f;

        [SerializeField, Tooltip("音を引く設定。空なら Resources の PresentationCatalog を探す")]
        private PresentationCatalog _catalog;

        [SerializeField, Tooltip("床の材質ごとの足音と着地音。空なら Resources の FootstepSurfaceCatalog を探し、無ければ上の設定の足音を使う")]
        private FootstepSurfaceCatalog _surfaceCatalog;

        [SerializeField, Min(0.01f), Tooltip("この距離 (m) までは音量を下げない")]
        private float _minDistance = DefaultMinDistance;

        [SerializeField, Min(0.02f), Tooltip("この距離 (m) で聞こえなくなる")]
        private float _maxDistance = DefaultMaxDistance;

        [SerializeField, Range(0f, 1f), Tooltip("被弾の声の音量")]
        private float _hurtVolume = 1f;

        private static readonly Func<int, int> RandomBelow = count => Random.Range(0, count);
        private static bool _missingSurfaceCatalogWarned;

        private readonly FootstepCadence _cadence = new FootstepCadence();
        private readonly List<AudioClip> _candidates = new List<AudioClip>();
        private readonly RaycastHit[] _probeHits = new RaycastHit[SurfaceProbeBufferSize];
        private bool _catalogResolved;
        private bool _surfaceCatalogResolved;
        private AudioClip _lastFootstepClip;
        private AudioClip _lastLandingClip;
        private string _lastSurfaceId;
        private Collider _bodyCollider;
        private bool _started;
        private bool _subscribed;
        private ICharacterMotionSource _motion;
        private Damageable _damageable;
        private ICharacterHurtSource _hurtSource;
        private AudioSource _feet;
        private AudioSource _voice;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _hurtCooldown;

        /// <summary>
        /// 音を引く設定。Inspector で入れていなければ、初めて読んだときに <see cref="PresentationCatalog.Load"/> で探し、無ければ null (鳴らさない)。
        /// 代入すると探さずにそれを使う (null で音を止める)。
        /// </summary>
        public PresentationCatalog Catalog
        {
            get
            {
                if (_catalog == null && !_catalogResolved)
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
            }
        }

        /// <summary>
        /// 床の材質ごとの足音と着地音。Inspector で入れていなければ、初めて読んだときに <see cref="FootstepSurfaceCatalog.Load"/> で探す。
        /// 無ければ (1 度だけ警告して) null で、<see cref="Catalog"/> の足音・着地音を鳴らす。代入すると探さずにそれを使う。
        /// </summary>
        public FootstepSurfaceCatalog SurfaceCatalog
        {
            get
            {
                if (_surfaceCatalog == null && !_surfaceCatalogResolved)
                {
                    _surfaceCatalog = FootstepSurfaceCatalog.Load();
                    _surfaceCatalogResolved = true;
                    if (_surfaceCatalog == null && !_missingSurfaceCatalogWarned)
                    {
                        _missingSurfaceCatalogWarned = true;
                        Debug.LogWarning(
                            $"{FootstepLogTag} no footstep surface catalog at Resources/{FootstepSurfaceCatalog.ResourcePath}; "
                            + "footsteps use the presentation catalog's sounds", this);
                    }
                }

                return _surfaceCatalog;
            }
            set
            {
                _surfaceCatalog = value;
                _surfaceCatalogResolved = true;
            }
        }

        /// <summary>最後に足音か着地音を鳴らしたときの足元の材質の Id。まだ鳴らしていなければ null。</summary>
        public string LastSurfaceId => _lastSurfaceId;

        /// <summary>足音と着地音を決める規則。</summary>
        public FootstepCadence Cadence => _cadence;

        /// <summary>接地・しゃがみ・ダッシュを読む相手。見つかっていなければ null (足音と着地音を鳴らさない)。</summary>
        public ICharacterMotionSource MotionSource => _motion;

        private void Start()
        {
            // 本体の部品は同じフレームのうちに後から足されることがある (GameBootstrap) ので、揃った後の Start で探す
            _motion = GetComponent<ICharacterMotionSource>();
            _damageable = GetComponent<Damageable>();
            _hurtSource = _damageable == null ? GetComponent<ICharacterHurtSource>() : null;
            _bodyCollider = GetComponent<Collider>();
            if (_motion == null)
            {
                Debug.LogWarning($"{LogTag} '{name}' has no {nameof(ICharacterMotionSource)}; footsteps and landings stay silent", this);
            }

            _started = true;
            Subscribe();
        }

        private void OnEnable()
        {
            ResetTracking();
            if (_started)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (_hurtCooldown > 0f)
            {
                _hurtCooldown -= Time.deltaTime;
            }

            if (_motion == null)
            {
                return;
            }

            // 移動は Update (プレイヤー・ネットの補間) と NavMesh の更新で終わっているので、ここで測ると 1 フレームぶんの動きが揃う
            var position = transform.position;
            if (!_hasLastPosition || (_damageable != null && _damageable.IsDead))
            {
                _lastPosition = position;
                _hasLastPosition = true;
                return;
            }

            var delta = position - _lastPosition;
            _lastPosition = position;
            var deltaSeconds = Time.deltaTime;
            if (!(deltaSeconds > 0f))
            {
                return;
            }

            var horizontal = new Vector2(delta.x, delta.z).magnitude;
            var crouched = _motion.IsCrouched;
            var sprinting = ResolveSprinting(horizontal / deltaSeconds);
            var cue = _cadence.Advance(horizontal, delta.y, deltaSeconds, _motion.IsGrounded, crouched, sprinting);
            Play(cue);
        }

        private bool ResolveSprinting(float horizontalSpeed)
        {
            if (_motion.TryGetSprinting(out var sprinting))
            {
                return sprinting;
            }

            var walkSpeed = _motion.WalkSpeed;
            return walkSpeed > 0f && FootstepCadence.IsSprintingBySpeed(horizontalSpeed, walkSpeed);
        }

        private void Play(FootstepCue cue)
        {
            if (!cue.HasSound)
            {
                return;
            }

            // どちらのカタログも無ければ鳴らす音が無いので、足元を調べる Raycast も省く
            if (SurfaceCatalog == null && Catalog == null)
            {
                return;
            }

            PlayFootstep(cue.Kind, cue.Volume, ProbeSurfaceId());
        }

        /// <summary>
        /// materialId の床で kind (足音か着地) の音を volume で鳴らす。候補は <see cref="FootstepSurfaceCatalog.CollectClips"/> で引き、
        /// 直前に同じ種類で鳴らした音を避けて選ぶ。materialId が前回と違えば <see cref="FootstepLogTag"/> 付きでログに出す。
        /// 音が無い・kind が足音でも着地でもなければ何もせず null。鳴らしたらそのクリップを返す。
        /// </summary>
        internal AudioClip PlayFootstep(FootstepCueKind kind, float volume, string materialId)
        {
            if (kind != FootstepCueKind.Footstep && kind != FootstepCueKind.Landing)
            {
                return null;
            }

            materialId = SurfaceMaterial.Normalize(materialId);
            var isLanding = kind == FootstepCueKind.Landing;
            var source = FootstepSurfaceCatalog.CollectClips(SurfaceCatalog, Catalog, materialId, kind, _candidates);
            var clip = FootstepSurfaceCatalog.PickAvoidingRepeat(_candidates, isLanding ? _lastLandingClip : _lastFootstepClip, RandomBelow);
            _candidates.Clear();
            if (clip == null)
            {
                return null;
            }

            if (isLanding)
            {
                _lastLandingClip = clip;
            }
            else
            {
                _lastFootstepClip = clip;
            }

            // 毎歩ログに出すと埋もれるので、材質が変わったときだけ出す
            if (!string.Equals(_lastSurfaceId, materialId, StringComparison.Ordinal))
            {
                Debug.Log(
                    $"{FootstepLogTag} '{name}' surface {_lastSurfaceId ?? "(none)"} -> {materialId} ({kind}, clip '{clip.name}' from {source})", this);
                _lastSurfaceId = materialId;
            }

            var feet = EnsureSource(ref _feet);
            feet.pitch = isLanding ? 1f : 1f + Random.Range(-FootstepPitchJitter, FootstepPitchJitter);
            feet.PlayOneShot(clip, volume);

            // 自分の足音は歩くたびに鳴り続け、周りの合図を読む邪魔になるだけなので字幕にしない。
            if (!IsLocalCharacter)
            {
                SubtitleFeed.Raise(SoundCueKind.Footstep, transform.position, Mathf.Max(_maxDistance, _minDistance + 0.01f));
            }

            return clip;
        }

        /// <summary>
        /// 足元の床の材質の Id。足の高さの <see cref="SurfaceProbeLift"/> 上から <see cref="SurfaceProbeDepth"/> 下まで Raycast し、
        /// 自分の本体の (子を含む) コライダーとトリガーを除いた一番近い面の <see cref="SurfaceMaterial.IdOf"/>。何も無ければ <see cref="SurfaceMaterial.DefaultId"/>。
        /// </summary>
        internal string ProbeSurfaceId()
        {
            var origin = FeetPosition() + (Vector3.up * SurfaceProbeLift);
            var count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _probeHits, SurfaceProbeLift + SurfaceProbeDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = _probeHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || !(hit.distance < nearestDistance))
                {
                    continue;
                }

                nearest = hit.collider;
                nearestDistance = hit.distance;
            }

            // 残した当たりがコライダーを掴んだままにならないよう消す
            Array.Clear(_probeHits, 0, count);
            return SurfaceMaterial.IdOf(nearest);
        }

        // 本体の原点が足元とは限らない (プレイヤーは CharacterController の中心) ので、本体のコライダーの下端を足の高さにする
        private Vector3 FeetPosition()
        {
            var body = _started ? _bodyCollider : GetComponent<Collider>();
            if (body != null && body.enabled)
            {
                var bounds = body.bounds;
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            return transform.position;
        }

        /// <summary>この画面で操作しているキャラか (<see cref="Damageable.IsLocalPlayer"/> / <see cref="ICharacterHurtSource.IsLocalPlayer"/>)。</summary>
        public bool IsLocalCharacter =>
            _damageable != null ? _damageable.IsLocalPlayer : _hurtSource != null && _hurtSource.IsLocalPlayer;

        /// <summary>
        /// 被弾の声を鳴らす。<see cref="HurtVoiceCooldownSeconds"/> の間に続けて呼んでも 1 回だけ鳴らす。
        /// 音が無ければ何もしない。鳴らしたら true。
        /// </summary>
        public bool PlayHurtVoice()
        {
            if (_hurtCooldown > 0f)
            {
                return false;
            }

            var catalog = Catalog;
            var clip = catalog != null ? catalog.HurtVoice : null;
            if (clip == null)
            {
                return false;
            }

            _hurtCooldown = HurtVoiceCooldownSeconds;
            EnsureSource(ref _voice).PlayOneShot(clip, _hurtVolume);
            return true;
        }

        private AudioSource EnsureSource(ref AudioSource source)
        {
            if (source != null)
            {
                return source;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = _minDistance;
            source.maxDistance = Mathf.Max(_maxDistance, _minDistance + 0.01f);
            return source;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // ドメインを読み直さずに Play Mode に入ったときも、カタログが無いことを 1 度は知らせる
            _missingSurfaceCatalogWarned = false;
        }

        private void ResetTracking()
        {
            _cadence.Reset();
            _hasLastPosition = false;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            if (_damageable != null)
            {
                _damageable.Damaged += OnDamaged;
                _damageable.HealthReset += OnHealthReset;
            }
            else if (_hurtSource != null)
            {
                _hurtSource.Hurt += OnHurt;
                CombatEvents.DamageTaken += OnDamageTaken;
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            if (_damageable != null)
            {
                _damageable.Damaged -= OnDamaged;
                _damageable.HealthReset -= OnHealthReset;
            }
            else if (_hurtSource != null)
            {
                _hurtSource.Hurt -= OnHurt;
                CombatEvents.DamageTaken -= OnDamageTaken;
            }
        }

        private void OnDamaged(Damageable _, DamageReport report)
        {
            if (report.Amount > 0)
            {
                PlayHurtVoice();
            }
        }

        // リスポーンで戻った位置への移動を、足音や着地として数えない
        private void OnHealthReset(Health _)
        {
            ResetTracking();
        }

        private void OnHurt(int amount)
        {
            if (amount > 0 && !_hurtSource.IsLocalPlayer)
            {
                PlayHurtVoice();
            }
        }

        // DamageTaken はこの画面で操作しているキャラ宛てなので、どの本体の分かは届かない。自分のキャラの本体だけが鳴らす
        private void OnDamageTaken(DamageTakenInfo info)
        {
            if (info.Amount > 0 && _hurtSource.IsLocalPlayer)
            {
                PlayHurtVoice();
            }
        }
    }
}
