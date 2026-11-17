using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Shooter.Runtime
{
    // 足音を本体の位置の差で測るのは、自分で動かすプレイヤー・NavMesh で動く敵・サーバーから届いた位置を補間して表示するネット対戦の他人のどれも同じ規則で鳴らすため。
    [DisallowMultipleComponent]
    public sealed class CharacterAudio : MonoBehaviour
    {
        public const string LogTag = "[Audio]";

        public const string FootstepLogTag = "[Footstep]";

        // 坂や段差で足が床に少し埋まっていても床を拾えるよう、足の高さより上から Raycast を始める。
        public const float SurfaceProbeLift = 0.3f;

        // 接地の判定の誤差と、ネット対戦の補間で浮いた分を拾える長さ。
        public const float SurfaceProbeDepth = 0.5f;

        private const int SurfaceProbeBufferSize = 8;

        public const float DefaultMinDistance = 1.5f;

        // 遠くの敵の足音で近くの音を埋めないよう、直線で 0 まで下げる。
        public const float DefaultMaxDistance = 30f;

        // 連射や散弾で続けて当たっても被弾の声を鳴らし直さないための間。
        public const float HurtVoiceCooldownSeconds = 0.4f;

        // 同じ音の繰り返しに聞こえないよう、足音の高さを毎回揺らす。
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

        public string LastSurfaceId => _lastSurfaceId;

        public FootstepCadence Cadence => _cadence;

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

            // 破棄されたコライダーは == null でも下の型パターン (is) では null に見えず、寸法を読むと例外になるので、ここで引き直す
            if (body == null)
            {
                body = GetComponent<Collider>();
                if (_started)
                {
                    _bodyCollider = body;
                }

                if (body == null)
                {
                    return transform.position;
                }
            }

            if (body.enabled)
            {
                var bounds = body.bounds;
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            // 補間表示の他人は CharacterController が無効で bounds が当てにならないので、寸法から下端を求める
            var scaleY = Mathf.Abs(transform.lossyScale.y);
            var position = transform.position;
            if (body is CharacterController controller)
            {
                position.y += (controller.center.y - (controller.height * 0.5f)) * scaleY;
            }
            else if (body is CapsuleCollider capsule)
            {
                position.y += (capsule.center.y - (capsule.height * 0.5f)) * scaleY;
            }
            else if (body is BoxCollider box)
            {
                position.y += (box.center.y - (box.size.y * 0.5f)) * scaleY;
            }
            else if (body is SphereCollider sphere)
            {
                position.y += (sphere.center.y - sphere.radius) * scaleY;
            }

            return position;
        }

        public bool IsLocalCharacter =>
            _damageable != null ? _damageable.IsLocalPlayer : _hurtSource != null && _hurtSource.IsLocalPlayer;

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
