using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class DistractionThrowAbility : IAbility
    {
        private readonly DistractionSpec _spec;
        private readonly GameObject _visualPrefab;
        private readonly AudioClip _landingSound;
        private bool _thrown;

        public DistractionThrowAbility(DistractionSpec spec, GameObject visualPrefab, AudioClip landingSound)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _visualPrefab = visualPrefab;
            _landingSound = landingSound;
        }

        public DistractionSpec Spec => _spec;

        public ThrownDistraction LastThrown { get; private set; }

        public bool CanUse(AbilityContext context)
        {
            return context.Owner != null;
        }

        public void Begin(AbilityContext context)
        {
            _thrown = false;
        }

        // 効果が出るまでの秒数 (振りかぶり) を待ってから投げるので、Begin ではなく効果が出た Tick で投げる。
        public bool Tick(AbilityContext context, float deltaSeconds)
        {
            if (!context.IsEffective)
            {
                return true;
            }

            if (!_thrown)
            {
                _thrown = true;
                LastThrown = Throw(context.Owner);
            }

            return false;
        }

        public void End(AbilityContext context, bool interrupted)
        {
            _thrown = false;
        }

        public ThrownDistraction Throw(GameObject owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            GrenadeThrower.ResolveRelease(owner.transform, ThrownGrenade.UntypedSpec, out var position, out var direction);
            var velocity = GrenadeThrower.LaunchVelocity(direction, _spec.ThrowSpeed, _spec.ThrowLiftSpeed);
            var thrown = ThrownDistraction.Spawn(owner, position, velocity, _spec, _visualPrefab, _landingSound);
            Debug.Log(ThrownDistraction.FormatThrownLog(position, velocity), owner);
            return thrown;
        }
    }
}
