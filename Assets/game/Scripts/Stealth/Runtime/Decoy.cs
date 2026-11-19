using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 敵に見せるのは DecoyPerception (敵ごとの IPerceptionContributor) の役目で、ここは置いた物として呼び続けて時間で消えることだけを持つ。
    // 撃たれて壊れるのは置ける物の土台 (Deployable) が受け持つ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Deployable))]
    public sealed class Decoy : MonoBehaviour, IClientQuietedBehaviour
    {
        public const string LogTag = DecoyLog.Tag;

        private static readonly List<Decoy> _armed = new List<Decoy>();

        private Deployable _deployable;
        private DecoySpec _spec;
        private DecoyState _state;
        private AudioClip _callSound;
        private float _callVolume;
        private AudioSource _source;

        // 敵の知覚が毎 tick シーンを探さずに済むよう、呼んでいる間のものだけをここに持つ。
        public static IReadOnlyList<Decoy> Armed => _armed;

        public bool IsArmed => _state != null && _deployable != null && !_deployable.IsRemoved;

        public Deployable Deployable => _deployable;

        public Damageable Damageable => _deployable != null ? _deployable.Damageable : null;

        public GameObject Owner => _deployable != null ? _deployable.Owner : null;

        public Side? Side => _deployable != null ? _deployable.Side : null;

        public DecoySpec Spec => _spec;

        public DecoyState State => _state;

        public int Calls => _state != null ? _state.Calls : 0;

        public AudioClip CallSound => _callSound;

        public void Arm(DecoySpec spec, AudioClip callSound, float callVolume)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(callVolume >= 0f) || callVolume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(callVolume), callVolume, "callVolume must be between 0 and 1");
            }

            if (_state != null)
            {
                throw new InvalidOperationException($"decoy '{name}' is already armed");
            }

            var deployable = GetComponent<Deployable>();
            if (deployable == null || deployable.Damageable == null)
            {
                throw new InvalidOperationException($"decoy '{name}' must be armed after its Deployable is initialized");
            }

            _deployable = deployable;
            _spec = spec;
            _state = new DecoyState(spec);
            _callSound = callSound;
            _callVolume = callVolume;

            // 発見ゲージのルールは見られる側が持つので、置いた人のルールを写す。人の形なので姿勢は立ち (StealthTarget の既定) のまま。
            var ownerStealth = deployable.Owner.GetComponent<StealthTarget>();
            var stealth = GetComponent<StealthTarget>();
            if (stealth == null)
            {
                stealth = gameObject.AddComponent<StealthTarget>();
            }

            stealth.Rule = ownerStealth != null ? ownerStealth.Rule : Stealth.Off;

            // Edit Mode では OnDisable / OnDestroy が呼ばれないので、消えたことは Deployable から受けて一覧から外す。
            deployable.Removed += OnRemoved;
            Register();
            DecoyPerception.AttachAll();
            Debug.Log(
                DecoyLog.Armed(
                    deployable.OwnerId, deployable.KindId, deployable.Serial, spec.LifetimeSeconds, spec.CallIntervalSeconds, spec.CallRadius, stealth.Rule),
                this);
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsArmed)
            {
                return;
            }

            if (_state.Tick(deltaSeconds) > 0)
            {
                Call();
            }

            var over = _state.Over;
            if (over.HasValue)
            {
                _deployable.Remove(over.Value);
            }
        }

        // 1 回の Tick で何度呼んでも、音とログは 1 回にする。重ねて鳴らしても聞き分けられない。
        private void Call()
        {
            if (_callSound != null)
            {
                if (_source == null)
                {
                    _source = gameObject.AddComponent<AudioSource>();
                    _source.playOnAwake = false;
                    _source.spatialBlend = 1f;
                    _source.maxDistance = Mathf.Max(_spec.CallRadius, _source.minDistance);
                }

                _source.PlayOneShot(_callSound, _callVolume * AppliedControlSettings.Current.EffectsVolume);
            }

            Debug.Log(DecoyLog.Called(_deployable.OwnerId, _deployable.KindId, _deployable.Serial, Damageable.GroundPoint.ToNumerics()), this);
        }

        private void OnRemoved(Deployable deployable, DeployRemoval reason)
        {
            deployable.Removed -= OnRemoved;
            _armed.Remove(this);
        }

        private void Register()
        {
            _armed.RemoveAll(d => d == null);
            if (!_armed.Contains(this))
            {
                _armed.Add(this);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnEnable()
        {
            if (IsArmed)
            {
                Register();
            }
        }

        private void OnDisable()
        {
            _armed.Remove(this);
        }

        private void OnDestroy()
        {
            _armed.Remove(this);
        }
    }
}
