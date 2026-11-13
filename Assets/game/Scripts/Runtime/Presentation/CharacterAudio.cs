using Fps.Core;
using UnityEngine;

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
    /// 音は <see cref="Catalog"/> の <see cref="PresentationCatalog.Footstep"/> / <see cref="PresentationCatalog.Landing"/> / <see cref="PresentationCatalog.HurtVoice"/>。
    /// 無いものは鳴らさないだけで、動きや戦闘の処理は止めない。AudioSource は初めて鳴らすときに本体へ足す (足と声で 2 つ)。
    /// 専用サーバーでは <c>Fps.Net.HeadlessPresentation</c> が止める。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAudio : MonoBehaviour
    {
        public const string LogTag = "[Audio]";

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

        [SerializeField, Min(0.01f), Tooltip("この距離 (m) までは音量を下げない")]
        private float _minDistance = DefaultMinDistance;

        [SerializeField, Min(0.02f), Tooltip("この距離 (m) で聞こえなくなる")]
        private float _maxDistance = DefaultMaxDistance;

        [SerializeField, Range(0f, 1f), Tooltip("被弾の声の音量")]
        private float _hurtVolume = 1f;

        private readonly FootstepCadence _cadence = new FootstepCadence();
        private bool _catalogResolved;
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

            var catalog = Catalog;
            if (catalog == null)
            {
                return;
            }

            var isLanding = cue.Kind == FootstepCueKind.Landing;
            var clip = isLanding ? catalog.Landing : catalog.Footstep;
            if (clip == null)
            {
                return;
            }

            var feet = EnsureSource(ref _feet);
            feet.pitch = isLanding ? 1f : 1f + Random.Range(-FootstepPitchJitter, FootstepPitchJitter);
            feet.PlayOneShot(clip, cue.Volume);
        }

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
