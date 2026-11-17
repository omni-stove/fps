using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 撃たれて壊れるのは置ける物の土台 (Deployable) が受け持つ。ここは有効になるまでの時間と、敵が入ったら爆発することだけを持つ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Deployable))]
    public sealed class Mine : MonoBehaviour
    {
        public const string LogTag = MineLog.Tag;

        // 足元の面ちょうどから爆発させると、相手へ引く遮りの線が床の面に沿って床に当たることがあるので、少し浮かせる。
        public const float BlastLift = 0.1f;

        private Deployable _deployable;
        private MineSpec _spec;
        private MineState _state;
        private AudioClip _armedSound;
        private float _armedVolume;
        private AudioSource _source;

        public Deployable Deployable => _deployable;

        public GameObject Owner => _deployable != null ? _deployable.Owner : null;

        public Side? Side => _deployable != null ? _deployable.Side : null;

        public MineSpec Spec => _spec;

        public MineState State => _state;

        public bool IsPlaced => _state != null && _deployable != null && !_deployable.IsRemoved;

        public bool IsArmed => IsPlaced && _state.IsArmed;

        public Vector3 BlastCenter => transform.position + (Vector3.up * BlastLift);

        public void Arm(MineSpec spec, AudioClip armedSound, float armedVolume)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(armedVolume >= 0f) || armedVolume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(armedVolume), armedVolume, "armedVolume must be between 0 and 1");
            }

            if (_state != null)
            {
                throw new InvalidOperationException($"mine '{name}' is already armed");
            }

            var deployable = GetComponent<Deployable>();
            if (deployable == null || deployable.Damageable == null)
            {
                throw new InvalidOperationException($"mine '{name}' must be armed after its Deployable is initialized");
            }

            _deployable = deployable;
            _spec = spec;
            _state = new MineState(spec);
            _armedSound = armedSound;
            _armedVolume = armedVolume;
            var damage = spec.Explosion.Damage;
            Debug.Log(
                MineLog.Arming(
                    deployable.OwnerId, deployable.KindId, deployable.Serial, spec.ArmSeconds, spec.TriggerRadius, damage.MaxDamage, damage.Radius),
                this);
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsPlaced)
            {
                return;
            }

            if (_state.Tick(deltaSeconds))
            {
                AnnounceArmed();
            }

            if (!_state.IsArmed)
            {
                return;
            }

            var intruder = FindIntruder();
            if (intruder != null && _state.TryTrigger(true))
            {
                Detonate(intruder);
            }
        }

        public bool IsTriggeredBy(GameObject other)
        {
            if (other == null)
            {
                return false;
            }

            var owner = Owner;
            var isOwner = owner != null && other.transform.IsChildOf(owner.transform);
            var member = SideMember.Of(other);
            var isBot = other.GetComponentInParent<EnemyAgent>() != null;
            return MineState.IsTriggeredBy(isOwner, Side, member != null ? member.Side : (Side?)null, isBot);
        }

        private Damageable FindIntruder()
        {
            var self = _deployable.Damageable;
            var seen = new HashSet<Damageable>();
            foreach (var collider in Physics.OverlapSphere(transform.position, _spec.TriggerRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var body = BodyOf(collider);
                if (body == null || body == self || !seen.Add(body) || body.IsDead)
                {
                    continue;
                }

                // 置いた物どうし (隣の地雷や盾) では起きない。
                if (body.GetComponent<Deployable>() != null)
                {
                    continue;
                }

                if (IsTriggeredBy(body.gameObject))
                {
                    return body;
                }
            }

            return null;
        }

        private void Detonate(Damageable intruder)
        {
            var deployable = _deployable;
            var owner = deployable.Owner;
            var kindId = deployable.KindId;
            var center = BlastCenter;
            var explosion = _spec.Explosion;
            Debug.Log(MineLog.Triggered(deployable.OwnerId, kindId, deployable.Serial, intruder.Id, center.ToNumerics()), this);

            // 自分の耐久も爆発の範囲に入り、先に爆発させると「壊された」で消えるので、消してから爆発させる。
            deployable.Remove(DeployRemoval.Detonated);
            Explosion.Detonate(center, explosion, owner, kindId);
        }

        private void AnnounceArmed()
        {
            if (_armedSound != null)
            {
                if (_source == null)
                {
                    _source = gameObject.AddComponent<AudioSource>();
                    _source.playOnAwake = false;
                    _source.spatialBlend = 1f;
                }

                _source.PlayOneShot(_armedSound, _armedVolume * AppliedControlSettings.Current.EffectsVolume);
            }

            Debug.Log(MineLog.Armed(_deployable.OwnerId, _deployable.KindId, _deployable.Serial), this);
        }

        private static Damageable BodyOf(Collider collider)
        {
            var hitbox = Hitbox.FindFor(collider);
            if (hitbox != null && hitbox.Owner != null)
            {
                return hitbox.Owner;
            }

            return collider.GetComponentInParent<Damageable>();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
