using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct PlayerActionState : IEquatable<PlayerActionState>
    {
        public const PlayerActionConditions AllFlags =
            PlayerActionConditions.Sprinting
            | PlayerActionConditions.Crouching
            | PlayerActionConditions.Dodging
            | PlayerActionConditions.Vaulting
            | PlayerActionConditions.Grounded
            | PlayerActionConditions.InCover
            | PlayerActionConditions.BlindFiring
            | PlayerActionConditions.Aiming
            | PlayerActionConditions.Firing
            | PlayerActionConditions.Reloading
            | PlayerActionConditions.Downed
            | PlayerActionConditions.Meleeing
            | PlayerActionConditions.Staggered
            | PlayerActionConditions.Sliding
            | PlayerActionConditions.Mantling
            | PlayerActionConditions.Prone
            | PlayerActionConditions.OnLadder
            | PlayerActionConditions.Ziplining
            | PlayerActionConditions.Parachuting
            | PlayerActionConditions.InVehicle
            | PlayerActionConditions.AbilityMoving;

        // 連射間隔がこれより短い (またはセミオートを 1 回だけ撃った) ときも、表示や見た目が 1 フレームで消えないようにする。
        public const float MinFiringSeconds = 0.1f;

        // WeaponSpec の散らばりの角度の上限と揃えてある。
        public const float MaxSpreadDegrees = 90f;

        private readonly string _weaponName;

        public PlayerActionConditions Flags { get; }

        public float ReloadProgress { get; }

        // 成功するとその場でリロードが終わるので、IsReloading が false でも次のリロードを始めるまで Success を残す。
        public ActiveReloadResult? ActiveReload { get; }

        public ShoulderSide Shoulder { get; }

        public ViewMode ViewMode { get; }

        public string WeaponName => _weaponName ?? string.Empty;

        public float SpreadDegrees { get; }

        // Loadout.SwitchLowering と同じく、0 が構えた状態、1 がしまい切った状態。
        public float SwitchLowering { get; }

        public bool IsSprinting => Has(PlayerActionConditions.Sprinting);
        public bool IsCrouching => Has(PlayerActionConditions.Crouching);
        public bool IsDodging => Has(PlayerActionConditions.Dodging);
        public bool IsVaulting => Has(PlayerActionConditions.Vaulting);
        public bool IsGrounded => Has(PlayerActionConditions.Grounded);
        public bool IsInCover => Has(PlayerActionConditions.InCover);
        public bool IsBlindFiring => Has(PlayerActionConditions.BlindFiring);
        public bool IsAiming => Has(PlayerActionConditions.Aiming);
        public bool IsFiring => Has(PlayerActionConditions.Firing);
        public bool IsReloading => Has(PlayerActionConditions.Reloading);
        public bool IsDowned => Has(PlayerActionConditions.Downed);
        public bool IsMeleeing => Has(PlayerActionConditions.Meleeing);
        public bool IsStaggered => Has(PlayerActionConditions.Staggered);
        public bool IsSliding => Has(PlayerActionConditions.Sliding);
        public bool IsMantling => Has(PlayerActionConditions.Mantling);
        public bool IsProne => Has(PlayerActionConditions.Prone);
        public bool IsOnLadder => Has(PlayerActionConditions.OnLadder);
        public bool IsZiplining => Has(PlayerActionConditions.Ziplining);
        public bool IsParachuting => Has(PlayerActionConditions.Parachuting);
        public bool IsInVehicle => Has(PlayerActionConditions.InVehicle);
        public bool IsAbilityMoving => Has(PlayerActionConditions.AbilityMoving);

        public PlayerActionState(
            PlayerActionConditions flags,
            float reloadProgress,
            ActiveReloadResult? activeReload,
            ShoulderSide shoulder,
            ViewMode viewMode,
            string weaponName,
            float spreadDegrees,
            float switchLowering = 0f)
        {
            if ((flags & ~AllFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flags), flags, "flags contains undefined bits");
            }

            if (!(reloadProgress >= 0f && reloadProgress <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(reloadProgress), reloadProgress, "reloadProgress must be between 0 and 1");
            }

            if ((flags & PlayerActionConditions.Reloading) == 0 && reloadProgress != 0f)
            {
                throw new ArgumentException("reloadProgress must be 0 while not reloading", nameof(reloadProgress));
            }

            if (activeReload.HasValue && !Enum.IsDefined(typeof(ActiveReloadResult), activeReload.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(activeReload), activeReload, "activeReload is not a defined ActiveReloadResult");
            }

            if (!Enum.IsDefined(typeof(ShoulderSide), shoulder))
            {
                throw new ArgumentOutOfRangeException(nameof(shoulder), shoulder, "shoulder is not a defined ShoulderSide");
            }

            if (!Enum.IsDefined(typeof(ViewMode), viewMode))
            {
                throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, "viewMode is not a defined ViewMode");
            }

            if (!(spreadDegrees >= 0f && spreadDegrees <= MaxSpreadDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(spreadDegrees), spreadDegrees, "spreadDegrees must be between 0 and 90");
            }

            if (!(switchLowering >= 0f && switchLowering <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(switchLowering), switchLowering, "switchLowering must be between 0 and 1");
            }

            Flags = flags;
            ReloadProgress = reloadProgress;
            ActiveReload = activeReload;
            Shoulder = shoulder;
            ViewMode = viewMode;
            _weaponName = weaponName ?? string.Empty;
            SpreadDegrees = spreadDegrees;
            SwitchLowering = switchLowering;
        }

        public static bool IsFiringAfter(float secondsSinceShot, float fireInterval)
        {
            if (!(secondsSinceShot >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(secondsSinceShot), secondsSinceShot, "secondsSinceShot must not be negative");
            }

            if (!(fireInterval >= 0f) || float.IsInfinity(fireInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(fireInterval), fireInterval, "fireInterval must be a non-negative finite number");
            }

            return secondsSinceShot <= Math.Max(fireInterval, MinFiringSeconds);
        }

        public bool Equals(PlayerActionState other)
        {
            return Flags == other.Flags
                && ReloadProgress.Equals(other.ReloadProgress)
                && ActiveReload == other.ActiveReload
                && Shoulder == other.Shoulder
                && ViewMode == other.ViewMode
                && string.Equals(WeaponName, other.WeaponName, StringComparison.Ordinal)
                && SpreadDegrees.Equals(other.SpreadDegrees)
                && SwitchLowering.Equals(other.SwitchLowering);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerActionState other && Equals(other);
        }

        public static bool operator ==(PlayerActionState left, PlayerActionState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerActionState left, PlayerActionState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Flags, ReloadProgress, ActiveReload, (int)Shoulder, (int)ViewMode, WeaponName, SpreadDegrees, SwitchLowering);
        }

        public override string ToString()
        {
            var activeReload = ActiveReload.HasValue ? ActiveReload.Value.ToString() : "-";
            return string.Format(
                CultureInfo.InvariantCulture,
                "flags={0} reload={1:0.00} activeReload={2} shoulder={3} view={4} weapon={5} spread={6:0.0} switch={7:0.00}",
                Flags,
                ReloadProgress,
                activeReload,
                Shoulder,
                ViewMode,
                WeaponName,
                SpreadDegrees,
                SwitchLowering);
        }

        private bool Has(PlayerActionConditions flag)
        {
            return (Flags & flag) != 0;
        }
    }
}
