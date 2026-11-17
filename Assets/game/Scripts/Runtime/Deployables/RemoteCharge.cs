using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 撃たれて壊れるのは置ける物の土台 (Deployable) が受け持つ。ここは貼った先に付いていくことと、起爆されたら爆発することだけを持つ。
    // 貼った先の子にしないのは、人や標的の子にすると、その体の当たり判定や GetComponentInParent が C4 の Damageable を拾うため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Deployable))]
    public sealed class RemoteCharge : MonoBehaviour
    {
        public const string LogTag = RemoteChargeLog.Tag;

        // 貼った面ちょうどから爆発させると、相手へ引く遮りの線が面に沿って面に当たることがあるので、面から少し離す。
        public const float BlastLift = 0.1f;

        private Deployable _deployable;
        private RemoteChargeSpec _spec;
        private Transform _anchor;
        private Vector3 _localPosition;
        private Quaternion _localRotation;

        public Deployable Deployable => _deployable;

        public RemoteChargeSpec Spec => _spec;

        public Transform Anchor => _anchor;

        public bool IsPlaced => _spec != null && _deployable != null && !_deployable.IsRemoved;

        public Vector3 BlastCenter => transform.position + (transform.up * BlastLift);

        // 先にすべて消してから爆発させるのは、隣の C4 が先の爆発で「壊された」として消え、爆発しなくなるのを防ぐため。
        public static int DetonateAll(IReadOnlyList<Deployable> placed)
        {
            if (placed == null)
            {
                throw new ArgumentNullException(nameof(placed));
            }

            var charges = new List<RemoteCharge>();
            foreach (var deployable in placed)
            {
                var charge = deployable != null ? deployable.GetComponent<RemoteCharge>() : null;
                if (charge != null && charge.IsPlaced)
                {
                    charges.Add(charge);
                }
            }

            if (charges.Count == 0)
            {
                return 0;
            }

            var first = charges[0].Deployable;
            Debug.Log(RemoteChargeLog.Detonating(first.OwnerId, first.KindId, charges.Count), first);

            var blasts = new List<(Vector3 Center, ExplosionSpec Explosion, GameObject Owner, string KindId)>(charges.Count);
            foreach (var charge in charges)
            {
                charge.Follow();
                var deployable = charge.Deployable;
                var center = charge.BlastCenter;
                blasts.Add((center, charge.Spec.Explosion, deployable.Owner, deployable.KindId));
                Debug.Log(RemoteChargeLog.Detonated(deployable.OwnerId, deployable.KindId, deployable.Serial, center.ToNumerics()), deployable);
                deployable.Remove(DeployRemoval.Detonated);
            }

            foreach (var blast in blasts)
            {
                Explosion.Detonate(blast.Center, blast.Explosion, blast.Owner, blast.KindId);
            }

            return charges.Count;
        }

        public void Arm(RemoteChargeSpec spec, Collider surface, AudioClip attachedSound, float attachedVolume)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(attachedVolume >= 0f) || attachedVolume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(attachedVolume), attachedVolume, "attachedVolume must be between 0 and 1");
            }

            if (_spec != null)
            {
                throw new InvalidOperationException($"remote charge '{name}' is already armed");
            }

            var deployable = GetComponent<Deployable>();
            if (deployable == null || deployable.Damageable == null)
            {
                throw new InvalidOperationException($"remote charge '{name}' must be armed after its Deployable is initialized");
            }

            _deployable = deployable;
            _spec = spec;
            AttachTo(surface);
            Debug.Log(
                RemoteChargeLog.Attached(deployable.OwnerId, deployable.KindId, deployable.Serial, DescribeSurface(surface)),
                this);

            if (attachedSound != null)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.PlayOneShot(attachedSound, attachedVolume * AppliedControlSettings.Current.EffectsVolume);
            }
        }

        public void Follow()
        {
            if (_anchor == null)
            {
                return;
            }

            transform.SetPositionAndRotation(_anchor.TransformPoint(_localPosition), _anchor.rotation * _localRotation);
        }

        private void AttachTo(Collider surface)
        {
            if (surface == null)
            {
                return;
            }

            _anchor = surface.transform;
            _localPosition = _anchor.InverseTransformPoint(transform.position);
            _localRotation = Quaternion.Inverse(_anchor.rotation) * transform.rotation;

            // 人に貼った C4 の当たり判定が、その人の移動 (CharacterController) を押し返さないようにする。
            var body = surface.GetComponentInParent<Damageable>();
            if (body == null)
            {
                return;
            }

            var own = GetComponentsInChildren<Collider>(true);
            foreach (var other in body.GetComponentsInChildren<Collider>(true))
            {
                foreach (var mine in own)
                {
                    Physics.IgnoreCollision(mine, other, true);
                }
            }
        }

        private static string DescribeSurface(Collider surface)
        {
            if (surface == null)
            {
                return null;
            }

            var body = surface.GetComponentInParent<Damageable>();
            return body != null ? body.Id : surface.name;
        }

        private void LateUpdate()
        {
            if (IsPlaced)
            {
                Follow();
            }
        }
    }
}
