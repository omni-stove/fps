using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
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
            float chargeFraction = 0f,
            ProjectileSettings projectileLook = null,
            Action<ShotOutcome> landed = null,
            Func<System.Numerics.Vector3?> homingTarget = null,
            ILockOnDecoy homingDecoy = null)
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
            ProjectileLook = projectileLook;
            Landed = landed;
            HomingTarget = homingTarget;
            HomingDecoy = homingDecoy;
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

        // 見た目と音は WeaponDefinition の側にしか無く RoundSpec に載らないので、撃つ側が武器の定義から渡す。null ならコードで作る。
        public ProjectileSettings ProjectileLook { get; }

        // 飛ぶ弾は Deliver が返った後に当たるので、当たったことを撃った側へ後から知らせる。
        public Action<ShotOutcome> Landed { get; }

        // 誘導する弾は飛んでいる間ずっと相手の今の位置へ曲がるので、位置ではなく引く関数を渡す。null なら追わない。
        public Func<System.Numerics.Vector3?> HomingTarget { get; }

        // 追う相手の撒く囮。null なら囮に逸れない。
        public ILockOnDecoy HomingDecoy { get; }
    }
}
