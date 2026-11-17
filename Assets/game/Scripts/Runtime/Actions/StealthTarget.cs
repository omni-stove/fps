using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // ルールを敵ごとでなく見られる側に持たせるのは、敵は相手を選び直すので、相手ごとにルールが違う場面 (bot 同士の戦いなど) でも今までどおりにできるようにするため。
    [DisallowMultipleComponent]
    public sealed class StealthTarget : MonoBehaviour
    {
        [SerializeField]
        private Stealth _rule = Stealth.Off;

        // 光源から明るさを測る仕組みはキットに無いので、暗がりの範囲などを持つゲームがこの値を書き換える。
        [SerializeField, Range(0f, 1f), Tooltip("明るさの代わりの係数。1 で明るい所、0 で敵のゲージがまったくたまらない")]
        private float _lightFactor = 1f;

        // プレイヤー以外 (PlayerController の無い目標) が伏せを示すときにここを立てる。しゃがみは ICharacterMotionSource から読む。
        [SerializeField]
        private bool _prone;

        [SerializeField, Min(0f), Tooltip("大きさ 1 の足音が敵に聞こえる距離 (m)。歩き・しゃがみ・ダッシュ・着地の大きさを掛ける。0 なら足音で気づかれない")]
        private float _footstepNoiseRadius = StealthNoiseSpec.DefaultFootstepNoiseRadius;

        private FootstepCadence _cadence = StealthNoiseSpec.Default.CreateCadence();

        private PlayerController _player;
        private ICharacterMotionSource _motion;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private float _speed;

        public Stealth Rule
        {
            get => _rule;
            set
            {
                if (!Enum.IsDefined(typeof(Stealth), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "unknown stealth rule");
                }

                _rule = value;
            }
        }

        public float LightFactor
        {
            get => _lightFactor;
            set
            {
                if (!(value >= 0f) || value > 1f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "light factor must be between 0 and 1");
                }

                _lightFactor = value;
            }
        }

        public bool IsProne
        {
            get => _prone;
            set => _prone = value;
        }

        public float Speed => _speed;

        public float FootstepNoiseRadius
        {
            get => _footstepNoiseRadius;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "footstep noise radius must not be negative and must be finite");
                }

                _footstepNoiseRadius = value;
            }
        }

        public FootstepCadence Cadence => _cadence;

        public void ApplyNoise(StealthNoiseSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            FootstepNoiseRadius = spec.FootstepNoiseRadius;
            _cadence = spec.CreateCadence();
        }

        public AwarenessPosture Posture
        {
            get
            {
                // IsCrouched は伏せているとき false を返すので、伏せはプレイヤーから別に読まないと立ちとして数えてしまう。
                _player ??= GetComponent<PlayerController>();
                if (_prone || (_player != null && _player.IsProne))
                {
                    return AwarenessPosture.Prone;
                }

                _motion ??= GetComponent<ICharacterMotionSource>();
                return _motion != null && _motion.IsCrouched ? AwarenessPosture.Crouching : AwarenessPosture.Standing;
            }
        }

        // 移動の部品ごとに速さの持ち方が違う (CharacterController・NavMeshAgent・ネットの補間) ので、位置の差から測る。
        private void Update()
        {
            var position = transform.position;
            var deltaSeconds = Time.deltaTime;
            if (_hasLastPosition && deltaSeconds > 0f)
            {
                var delta = position - _lastPosition;
                var moved = new Vector3(delta.x, 0f, delta.z);
                var speed = moved.magnitude / deltaSeconds;
                _speed = float.IsNaN(speed) || float.IsInfinity(speed) ? 0f : speed;
                AdvanceFootsteps(delta, deltaSeconds);
            }

            _lastPosition = position;
            _hasLastPosition = true;
        }

        // 足音の素材の有無で敵の耳が変わらないよう、鳴らす側 (CharacterAudio) とは別に同じ歩幅の規則で数える。Off では敵が聞かないので数えない。
        internal bool AdvanceFootsteps(Vector3 delta, float deltaSeconds)
        {
            _motion ??= GetComponent<ICharacterMotionSource>();
            if (_rule != Stealth.On || _motion == null || !(deltaSeconds > 0f))
            {
                return false;
            }

            var horizontal = new Vector2(delta.x, delta.z).magnitude;
            if (!_motion.TryGetSprinting(out var sprinting))
            {
                var walkSpeed = _motion.WalkSpeed;
                sprinting = walkSpeed > 0f && FootstepCadence.IsSprintingBySpeed(horizontal / deltaSeconds, walkSpeed);
            }

            var quiet = Posture != AwarenessPosture.Standing;
            var cue = _cadence.Advance(horizontal, delta.y, deltaSeconds, _motion.IsGrounded, quiet, sprinting && !quiet);
            var radius = cue.Volume * _footstepNoiseRadius;
            if (!cue.HasSound || !(radius > 0f))
            {
                return false;
            }

            StealthNoise.Emit(gameObject, new SoundStimulus(SoundCueKind.Footstep, transform.position.ToNumerics(), radius, false));
            return true;
        }
    }
}
