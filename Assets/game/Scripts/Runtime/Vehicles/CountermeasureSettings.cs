using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class CountermeasureSettings
    {
        public static readonly Vector3 DefaultEjectLocalOffset = MapVehicleCountermeasures.DefaultEjectOffset.ToUnity();
        public static readonly Vector3 DefaultEjectLocalDirection = MapVehicleCountermeasures.DefaultEjectDirection.ToUnity();

        [SerializeField, Min(0), Tooltip("乗った時に持っているフレアの数")]
        private int _count = CountermeasureSpec.DefaultCount;

        [SerializeField, Min(0f), Tooltip("1 回撒いてから次に撒けるまでの秒")]
        private float _intervalSeconds = CountermeasureSpec.DefaultIntervalSeconds;

        [SerializeField, Min(0.01f), Tooltip("撒いたフレアが燃えて弾を誘う秒")]
        private float _lifetimeSeconds = CountermeasureSpec.DefaultLifetimeSeconds;

        [SerializeField, Range(0.1f, 90f), Tooltip("狙う側から見て、乗り物とフレアの間の角度 (度) がこれ以内なら、捕捉が外れ誘導弾がフレアへ向かう")]
        private float _decoyConeDegrees = CountermeasureSpec.DefaultDecoyConeDegrees;

        [SerializeField, Min(0f), Tooltip("撒いたフレアが乗り物から離れる速さ (m/s)。乗り物の速度に足す")]
        private float _ejectSpeed = CountermeasureSpec.DefaultEjectSpeed;

        [SerializeField, Min(0f), Tooltip("フレアに掛かる重力の倍率。0 なら落ちない")]
        private float _gravityMultiplier = CountermeasureSpec.DefaultGravityMultiplier;

        [SerializeField, Tooltip("フレアを出す所。乗り物の原点から見た向き (m)")]
        private Vector3 _ejectLocalOffset = DefaultEjectLocalOffset;

        [SerializeField, Tooltip("フレアを撒く向き。乗り物から見た向きで、長さは問わない。0 なら真下")]
        private Vector3 _ejectLocalDirection = DefaultEjectLocalDirection;

        [SerializeField, Tooltip("フレアの見た目。空なら光る球を作る")]
        private GameObject _flarePrefab;

        [SerializeField, Tooltip("撒いた時に鳴らす音。空なら鳴らさない")]
        private AudioClip _deploySound;

        public int Count
        {
            get => _count;
            set => _count = value;
        }

        public float IntervalSeconds
        {
            get => _intervalSeconds;
            set => _intervalSeconds = value;
        }

        public float LifetimeSeconds
        {
            get => _lifetimeSeconds;
            set => _lifetimeSeconds = value;
        }

        public float DecoyConeDegrees
        {
            get => _decoyConeDegrees;
            set => _decoyConeDegrees = value;
        }

        public float EjectSpeed
        {
            get => _ejectSpeed;
            set => _ejectSpeed = value;
        }

        public float GravityMultiplier
        {
            get => _gravityMultiplier;
            set => _gravityMultiplier = value;
        }

        public Vector3 EjectLocalOffset
        {
            get => _ejectLocalOffset;
            set => _ejectLocalOffset = value;
        }

        public Vector3 EjectLocalDirection
        {
            get => _ejectLocalDirection;
            set => _ejectLocalDirection = value;
        }

        public GameObject FlarePrefab
        {
            get => _flarePrefab;
            set => _flarePrefab = value;
        }

        public AudioClip DeploySound
        {
            get => _deploySound;
            set => _deploySound = value;
        }

        public CountermeasureSpec ToSpec()
        {
            return new CountermeasureSpec(_count, _intervalSeconds, _lifetimeSeconds, _decoyConeDegrees, _ejectSpeed, _gravityMultiplier);
        }

        public void CopyFrom(CountermeasureSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _count = spec.Count;
            _intervalSeconds = spec.IntervalSeconds;
            _lifetimeSeconds = spec.LifetimeSeconds;
            _decoyConeDegrees = spec.DecoyConeDegrees;
            _ejectSpeed = spec.EjectSpeed;
            _gravityMultiplier = spec.GravityMultiplier;
        }
    }
}
