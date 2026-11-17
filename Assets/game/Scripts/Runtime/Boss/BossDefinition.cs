using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Boss", menuName = "Shooter/Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("段階。上から順に進む。最初の段階は始めから入っている")]
        private BossPhaseEntry[] _phases = BossController.DefaultPhases();

        [Header("Zone multipliers")]
        [SerializeField, Min(0f)]
        private float _headMultiplier = (float)DamageCalculator.HeadMultiplier;

        [SerializeField, Min(0f)]
        private float _bodyMultiplier = (float)DamageCalculator.BodyMultiplier;

        [SerializeField, Min(0f)]
        private float _limbMultiplier = (float)DamageCalculator.LimbMultiplier;

        [SerializeField, Min(0f)]
        private float _weakPointMultiplier = (float)DamageCalculator.WeakPointMultiplier;

        [Header("Strafe")]
        [SerializeField, Min(0f), Tooltip("倍率 1 のときの左右へ往復する速さ (m/s)")]
        private float _strafeSpeed = BossStrafeMotion.DefaultSpeed;

        [SerializeField, Min(0f), Tooltip("置いた位置から左右へ動く幅の半分 (m)")]
        private float _strafeHalfWidth = BossStrafeMotion.DefaultHalfWidth;

        public IReadOnlyList<BossPhaseEntry> Phases => _phases ?? Array.Empty<BossPhaseEntry>();

        public float StrafeSpeed => _strafeSpeed;

        public float StrafeHalfWidth => _strafeHalfWidth;

        public ZoneDamageMultipliers CreateZoneMultipliers()
        {
            try
            {
                return new ZoneDamageMultipliers(_headMultiplier, _bodyMultiplier, _limbMultiplier, _weakPointMultiplier);
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public BossPhases CreatePhases()
        {
            try
            {
                return BossController.CreatePhases(_phases);
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        public void Configure(IReadOnlyList<BossPhaseEntry> phases, ZoneDamageMultipliers multipliers, float strafeSpeed, float strafeHalfWidth)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            if (multipliers == null)
            {
                throw new ArgumentNullException(nameof(multipliers));
            }

            RequireStrafe(strafeSpeed, nameof(strafeSpeed));
            RequireStrafe(strafeHalfWidth, nameof(strafeHalfWidth));

            var copy = new BossPhaseEntry[phases.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = phases[i] ?? throw new ArgumentException("phase entry " + i + " is null", nameof(phases));
            }

            _ = BossController.CreatePhases(copy);

            _phases = copy;
            _headMultiplier = (float)multipliers.Head;
            _bodyMultiplier = (float)multipliers.Body;
            _limbMultiplier = (float)multipliers.Limb;
            _weakPointMultiplier = (float)multipliers.WeakPoint;
            _strafeSpeed = strafeSpeed;
            _strafeHalfWidth = strafeHalfWidth;
        }

        public void ValidateStrafe()
        {
            try
            {
                RequireStrafe(_strafeSpeed, nameof(StrafeSpeed));
                RequireStrafe(_strafeHalfWidth, nameof(StrafeHalfWidth));
            }
            catch (ArgumentException e)
            {
                throw Invalid(e);
            }
        }

        private static void RequireStrafe(float value, string paramName)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be non-negative and finite");
            }
        }

        private InvalidOperationException Invalid(Exception e)
        {
            return new InvalidOperationException($"boss definition '{SourceName}' is invalid: {e.Message}", e);
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed boss definition)" : name;
    }
}
