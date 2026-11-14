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
                var moved = position - _lastPosition;
                moved.y = 0f;
                var speed = moved.magnitude / deltaSeconds;
                _speed = float.IsNaN(speed) || float.IsInfinity(speed) ? 0f : speed;
            }

            _lastPosition = position;
            _hasLastPosition = true;
        }
    }
}
