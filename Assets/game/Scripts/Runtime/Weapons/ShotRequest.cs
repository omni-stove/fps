using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public readonly struct ShotRequest
    {
        public ShotRequest(
            GameObject shooter,
            Transform shooterRoot,
            Vector3? sourcePosition,
            RoundSpec round,
            ImpactSpec impact,
            string weaponId,
            int layerMask,
            ChargeSpec charge = null,
            float chargeFraction = 0f)
        {
            if (!(chargeFraction >= 0f) || chargeFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(chargeFraction), chargeFraction, "chargeFraction must be between 0 and 1");
            }

            Shooter = shooter;
            ShooterRoot = shooterRoot;
            SourcePosition = sourcePosition;
            Round = round ?? throw new ArgumentNullException(nameof(round));
            Impact = impact ?? throw new ArgumentNullException(nameof(impact));
            WeaponId = weaponId;
            LayerMask = layerMask;
            Charge = charge;
            ChargeFraction = chargeFraction;
        }

        // 撃った者が分からない弾 (仕掛けや試験の弾) もあるので null を許す。
        public GameObject Shooter { get; }

        public Transform ShooterRoot { get; }

        public Vector3? SourcePosition { get; }

        public RoundSpec Round { get; }

        public ImpactSpec Impact { get; }

        public string WeaponId { get; }

        public int LayerMask { get; }

        public ChargeSpec Charge { get; }

        public float ChargeFraction { get; }
    }
}
