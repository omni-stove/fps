using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 体の寸法の既定は Unity のネット対戦のプレイヤー (NetSceneBuilder の CharacterController と NetworkPlayerMotor の既定) と同じにする。
    // クライアントの予測と同じ寸法でないと、毎 tick 位置が食い違って巻き戻されるため。
    internal sealed class ServerMotorSettings
    {
        public const float DefaultStandingHeight = 2f;
        public const float DefaultRadius = 0.5f;
        public const float DefaultStandingCenterY = 0f;
        public const float DefaultEyeY = 0.6f;
        public const float DefaultCrouchHeight = 1.2f;
        public const float DefaultKillPlaneY = -50f;

        public static readonly ServerMotorSettings Default = new ServerMotorSettings(
            MotorSpec.Default,
            new StanceGeometry(
                DefaultStandingHeight,
                DefaultStandingCenterY,
                DefaultEyeY,
                DefaultCrouchHeight,
                NetMotorSettings.FitProneHeight(ProneSpec.DefaultHeight, DefaultCrouchHeight, DefaultRadius),
                DefaultRadius),
            CapsuleMoverSpec.Default,
            GameRules.Default,
            DefaultKillPlaneY,
            Vector3.Zero);

        public ServerMotorSettings(
            MotorSpec motor, StanceGeometry stance, CapsuleMoverSpec mover, GameRules rules, float killPlaneY, Vector3 spawnPosition)
            : this(motor, stance, mover, ProneSpec.Default, rules, killPlaneY, spawnPosition)
        {
        }

        public ServerMotorSettings(
            MotorSpec motor,
            StanceGeometry stance,
            CapsuleMoverSpec mover,
            ProneSpec prone,
            GameRules rules,
            float killPlaneY,
            Vector3 spawnPosition)
            : this(motor, stance, mover, prone, SwimTuning.Default, WallRunTuning.Default, rules, killPlaneY, spawnPosition)
        {
        }

        public ServerMotorSettings(
            MotorSpec motor,
            StanceGeometry stance,
            CapsuleMoverSpec mover,
            ProneSpec prone,
            SwimTuning swim,
            WallRunTuning wallRun,
            GameRules rules,
            float killPlaneY,
            Vector3 spawnPosition)
            : this(motor, stance, mover, prone, swim, wallRun, ParachuteTuning.Default, rules, killPlaneY, spawnPosition)
        {
        }

        public ServerMotorSettings(
            MotorSpec motor,
            StanceGeometry stance,
            CapsuleMoverSpec mover,
            ProneSpec prone,
            SwimTuning swim,
            WallRunTuning wallRun,
            ParachuteTuning parachute,
            GameRules rules,
            float killPlaneY,
            Vector3 spawnPosition)
            : this(motor, stance, mover, prone, swim, wallRun, parachute, ZiplineTuning.Default, rules, killPlaneY, spawnPosition)
        {
        }

        public ServerMotorSettings(
            MotorSpec motor,
            StanceGeometry stance,
            CapsuleMoverSpec mover,
            ProneSpec prone,
            SwimTuning swim,
            WallRunTuning wallRun,
            ParachuteTuning parachute,
            ZiplineTuning zipline,
            GameRules rules,
            float killPlaneY,
            Vector3 spawnPosition)
        {
            Zipline = zipline ?? throw new ArgumentNullException(nameof(zipline));
            Prone = prone ?? throw new ArgumentNullException(nameof(prone));
            Swim = swim ?? throw new ArgumentNullException(nameof(swim));
            WallRun = wallRun ?? throw new ArgumentNullException(nameof(wallRun));
            Parachute = parachute ?? throw new ArgumentNullException(nameof(parachute));
            Motor = motor ?? throw new ArgumentNullException(nameof(motor));
            Stance = stance ?? throw new ArgumentNullException(nameof(stance));
            Mover = mover ?? throw new ArgumentNullException(nameof(mover));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (float.IsNaN(killPlaneY))
            {
                throw new ArgumentOutOfRangeException(nameof(killPlaneY), killPlaneY, "killPlaneY must not be NaN");
            }

            if (!float.IsFinite(spawnPosition.X) || !float.IsFinite(spawnPosition.Y) || !float.IsFinite(spawnPosition.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(spawnPosition), spawnPosition, "spawnPosition must be finite");
            }

            if (!(stance.Radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(stance), stance.Radius, "the body radius must be positive");
            }

            KillPlaneY = killPlaneY;
            SpawnPosition = spawnPosition;
        }

        public MotorSpec Motor { get; }

        public StanceGeometry Stance { get; }

        public CapsuleMoverSpec Mover { get; }

        public ProneSpec Prone { get; }

        public SwimTuning Swim { get; }

        public WallRunTuning WallRun { get; }

        public ParachuteTuning Parachute { get; }

        public ZiplineTuning Zipline { get; }

        public GameRules Rules { get; }

        public float KillPlaneY { get; }

        public Vector3 SpawnPosition { get; }

        public ServerMotorSettings WithRules(GameRules rules)
        {
            return new ServerMotorSettings(Motor, Stance, Mover, Prone, Swim, WallRun, Parachute, Zipline, rules, KillPlaneY, SpawnPosition);
        }

        public ServerMotorSettings WithSpawnPosition(Vector3 spawnPosition)
        {
            return new ServerMotorSettings(Motor, Stance, Mover, Prone, Swim, WallRun, Parachute, Zipline, Rules, KillPlaneY, spawnPosition);
        }
    }
}
