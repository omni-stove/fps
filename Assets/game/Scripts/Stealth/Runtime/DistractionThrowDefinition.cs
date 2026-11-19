using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "DistractionThrow", menuName = "Shooter/Abilities/Distraction Throw")]
    public sealed class DistractionThrowDefinition : AbilityDefinition
    {
        [SerializeField, Min(0.01f), Tooltip("視線の向きへの初速 (m/s)")]
        private float _throwSpeed = DistractionSpec.DefaultThrowSpeed;

        [SerializeField, Min(0f), Tooltip("初速に足す上向きの速さ (m/s)。水平に狙っても弧を描いて遠くへ届くようにする")]
        private float _throwLiftSpeed = DistractionSpec.DefaultThrowLiftSpeed;

        [SerializeField, Min(0.01f), Tooltip("落ちた音が敵に聞こえる距離 (m)。この中の気づいていない敵が調べに来る")]
        private float _noiseRadius = DistractionSpec.DefaultNoiseRadius;

        [SerializeField, Min(0.01f), Tooltip("何にも当たらないまま、この秒数が経ったら消す")]
        private float _maxFlightSeconds = DistractionSpec.DefaultMaxFlightSeconds;

        [SerializeField, Min(0f), Tooltip("落ちてから消すまでの秒数")]
        private float _lingerSeconds = DistractionSpec.DefaultLingerSeconds;

        [SerializeField, Tooltip("投げる物の見た目。中心が原点。当たり判定は外し、跳ね方は仮の球のまま。空なら仮の球を作る")]
        private GameObject _prefab;

        [SerializeField, Tooltip("落ちたときに鳴らす音。空なら鳴らさない (敵には聞こえる)")]
        private AudioClip _landingSound;

        public float ThrowSpeed
        {
            get => _throwSpeed;
            set => _throwSpeed = value;
        }

        public float ThrowLiftSpeed
        {
            get => _throwLiftSpeed;
            set => _throwLiftSpeed = value;
        }

        public float NoiseRadius
        {
            get => _noiseRadius;
            set => _noiseRadius = value;
        }

        public float MaxFlightSeconds
        {
            get => _maxFlightSeconds;
            set => _maxFlightSeconds = value;
        }

        public float LingerSeconds
        {
            get => _lingerSeconds;
            set => _lingerSeconds = value;
        }

        public GameObject Prefab
        {
            get => _prefab;
            set => _prefab = value;
        }

        public AudioClip LandingSound
        {
            get => _landingSound;
            set => _landingSound = value;
        }

        public DistractionSpec ToDistractionSpec()
        {
            try
            {
                return new DistractionSpec(_throwSpeed, _throwLiftSpeed, _noiseRadius, _maxFlightSeconds, _lingerSeconds);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"distraction ability '{Id}' is invalid: {e.Message}", e);
            }
        }

        public override IAbility CreateAbility()
        {
            return new DistractionThrowAbility(ToDistractionSpec(), _prefab, _landingSound);
        }
    }
}
