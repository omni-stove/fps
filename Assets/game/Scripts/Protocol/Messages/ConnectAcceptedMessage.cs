namespace Shooter.Protocol
{
    // マップはクライアントの -map ではなくここで受け取る。-map はホスト・サーバーだけが選び、クライアントは
    // つないだサーバーのマップへ合わせる (Netcode のシーン管理と同じ意味) ため。
    // 移動の値 (Motor) もここで受け取る。サーバーは起動したルールのファイルの値で動かし、クライアントはどのルールで
    // 起動したかを知らないので、自分の持つ既定値で予測すると片方だけ変えたときに毎 tick ずれるため。
    public sealed class ConnectAcceptedMessage : IMessage
    {
        public const byte CurrentVersion = 11;

        // .NET のサーバーと Netcode のプレイヤーの既定と同じにする。
        public const float DefaultKillPlaneY = -50f;

        public ushort Id => MessageIds.ConnectAccepted;

        public byte Version => CurrentVersion;

        public int PlayerId { get; set; }

        public string ServerVersion { get; set; } = "";

        public int TickRate { get; set; } = 1;

        public int ServerTick { get; set; }

        public string Map { get; set; } = "";

        // スナップショットは今の体力だけを運ぶので、HUD のゲージの満タンをここで知る。
        public int MaxHealth { get; set; } = 1;

        public Shooter.Core.NetMotorSettings Motor { get; set; } = Shooter.Core.NetMotorSettings.Default;

        public float KillPlaneY { get; set; } = DefaultKillPlaneY;

        // 移動の値と同じく、クライアントが自分の既定で予測するとサーバーだけ変えたときに飛んでいる間ずっとずれるので、ここで受け取る。
        public Shooter.Core.KnockbackSpec Knockback { get; set; } = Shooter.Core.KnockbackSpec.Default;

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PlayerId);
            writer.WriteString(ServerVersion, Shooter.Core.BuildVersion.MaxLength);
            writer.WriteInt32(TickRate);
            writer.WriteInt32(ServerTick);
            writer.WriteString(Map, Shooter.Core.NetLaunchOptions.MaxMapNameLength);
            writer.WriteInt32(MaxHealth);
            WriteMotor(writer, Motor ?? throw new System.InvalidOperationException("motor settings must be set"));
            writer.WriteSingle(KillPlaneY);
            WriteKnockback(writer, Knockback ?? throw new System.InvalidOperationException("knockback settings must be set"));
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "player id");
            ServerVersion = reader.ReadString(Shooter.Core.BuildVersion.MaxLength, ServerVersion);
            TickRate = reader.ReadInt32AtLeast(1, "tick rate");
            ServerTick = reader.ReadInt32AtLeast(0, "server tick");
            Map = reader.ReadString(Shooter.Core.NetLaunchOptions.MaxMapNameLength, Map);
            if (!Shooter.Core.NetLaunchOptions.IsMapName(Map))
            {
                throw new MessageReadException("map '" + Map + "' is not a scene name");
            }

            MaxHealth = reader.ReadInt32AtLeast(1, "max health");
            Motor = ReadMotor(reader);
            KillPlaneY = reader.ReadFiniteSingle("kill plane y");
            Knockback = ReadKnockback(reader);
        }

        private static void WriteKnockback(MessageWriter writer, Shooter.Core.KnockbackSpec spec)
        {
            WriteForce(writer, spec.Explosion);
            WriteForce(writer, spec.Melee);
            WriteForce(writer, spec.Charge);
            writer.WriteSingle(spec.MinSpeed);
            writer.WriteSingle(spec.MinAirSeconds);
            writer.WriteSingle(spec.MaxAirSeconds);
            writer.WriteSingle(spec.AirControl);
            writer.WriteSingle(spec.Gravity);
            writer.WriteSingle(spec.RecoverSeconds);
            writer.WriteSingle(spec.LandingSnapRadius);
            writer.WriteSingle(spec.PlayerWeight);
            writer.WriteSingle(spec.EnemyWeight);
        }

        private static void WriteForce(MessageWriter writer, Shooter.Core.KnockbackForce force)
        {
            writer.WriteSingle(force.Speed);
            writer.WriteSingle(force.UpwardRatio);
            writer.WriteSingle(force.WeightFactor);
        }

        private static Shooter.Core.KnockbackSpec ReadKnockback(MessageReader reader)
        {
            var values = new float[18];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = reader.ReadFiniteSingle("knockback value");
            }

            try
            {
                return new Shooter.Core.KnockbackSpec(
                    new Shooter.Core.KnockbackForce(values[0], values[1], values[2]),
                    new Shooter.Core.KnockbackForce(values[3], values[4], values[5]),
                    new Shooter.Core.KnockbackForce(values[6], values[7], values[8]),
                    values[9], values[10], values[11], values[12], values[13], values[14], values[15], values[16], values[17]);
            }
            catch (System.ArgumentException e)
            {
                throw new MessageReadException("knockback settings are out of range: " + e.Message, e);
            }
        }

        private static void WriteMotor(MessageWriter writer, Shooter.Core.NetMotorSettings settings)
        {
            var motor = settings.Motor;
            writer.WriteSingle(motor.WalkSpeed);
            writer.WriteSingle(motor.SprintSpeed);
            writer.WriteSingle(motor.JumpHeight);
            writer.WriteSingle(motor.Gravity);
            writer.WriteSingle(motor.AirControl);
            writer.WriteSingle(motor.CrouchSpeed);
            writer.WriteSingle(motor.SafeFallSpeed);
            writer.WriteSingle(motor.LethalFallSpeed);
            writer.WriteSingle(motor.DodgeDistance);
            writer.WriteSingle(motor.DodgeSeconds);
            writer.WriteSingle(motor.VaultMaxHeight);
            writer.WriteSingle(motor.MantleMaxHeight);
            writer.WriteSingle(motor.MantleSpeed);
            writer.WriteSingle(motor.DodgeCooldownSeconds);
            writer.WriteSingle(motor.VaultSpeed);
            writer.WriteSingle(motor.VaultReach);
            writer.WriteSingle(motor.VaultMaxAngleDegrees);
            writer.WriteSingle(motor.VaultMinHeight);
            writer.WriteSingle(motor.VaultMaxDepth);
            writer.WriteSingle(motor.CoverMaxEnterDistance);
            writer.WriteSingle(motor.CoverMaxEnterAngleDegrees);
            writer.WriteSingle(motor.CoverLowWallHeight);
            writer.WriteSingle(motor.CoverPeekSideDistance);
            writer.WriteSingle(motor.CoverEnterSeconds);
            writer.WriteSingle(motor.CoverBackHoldSeconds);
            writer.WriteSingle(motor.CoverPeekSlideSeconds);
            writer.WriteSingle(motor.LadderTopEntryDepth);
            writer.WriteSingle(motor.LadderMinApproachDot);
            writer.WriteSingle(motor.LadderMinApproachSpeed);
            WriteDirection(writer, motor.WalkDirection);
            WriteDirection(writer, motor.SprintDirection);
            var carry = motor.CarryLoad;
            writer.WriteByte((byte)carry.WeightSource);
            writer.WriteByte((byte)carry.MultiplierSource);
            writer.WriteSingle(carry.FreeWeight);
            writer.WriteSingle(carry.SpeedLossPerKg);
            writer.WriteSingle(carry.MinWeightMultiplier);
            writer.WriteByte((byte)carry.Gaits);

            var stance = settings.Stance;
            writer.WriteSingle(stance.Standing.Height);
            writer.WriteSingle(stance.Standing.CenterY);
            writer.WriteSingle(stance.Standing.EyeY);
            writer.WriteSingle(stance.Crouched.Height);
            writer.WriteSingle(stance.Prone.Height);
            writer.WriteSingle(stance.Radius);

            var mover = settings.Mover;
            writer.WriteSingle(mover.SlopeLimitDegrees);
            writer.WriteSingle(mover.StepOffset);
            writer.WriteSingle(mover.SkinWidth);
            writer.WriteInt32(mover.LayerMask);

            var prone = settings.Prone;
            writer.WriteSingle(prone.Height);
            writer.WriteSingle(prone.Speed);
            writer.WriteSingle(prone.MaxPitchDegrees);
            writer.WriteSingle(prone.SpreadScale);
            writer.WriteSingle(prone.DiveDistance);
            writer.WriteSingle(prone.DiveSeconds);

            writer.WriteSingle(motor.LeanSideOffset);
            writer.WriteSingle(motor.LeanAngleDegrees);
            writer.WriteSingle(motor.LeanSpeed);
            writer.WriteInt32(motor.AirJumps);
            writer.WriteSingle(motor.AirJumpSpeed);
            writer.WriteSingle(motor.JumpGraceSeconds);
            writer.WriteSingle(motor.CoverEnterSpeed);

            var swim = settings.Swim;
            writer.WriteSingle(swim.Speed);
            writer.WriteSingle(swim.Buoyancy);
            writer.WriteSingle(swim.BreathSeconds);

            var wallRun = settings.WallRun;
            writer.WriteBool(wallRun.Enabled);
            writer.WriteSingle(wallRun.MaxSeconds);
            writer.WriteSingle(wallRun.MinSpeed);
            writer.WriteSingle(wallRun.GravityScale);
            writer.WriteSingle(wallRun.JumpOffSpeed);
        }

        private static void WriteDirection(MessageWriter writer, Shooter.Core.MoveDirectionScale direction)
        {
            writer.WriteSingle(direction.Forward);
            writer.WriteSingle(direction.Backward);
            writer.WriteSingle(direction.Side);
        }

        // 範囲の検査は Core の型の作り方に任せ、通らない値は読めないメッセージとして断る
        private static Shooter.Core.NetMotorSettings ReadMotor(MessageReader reader)
        {
            var motor = new float[29];
            for (var i = 0; i < motor.Length; i++)
            {
                motor[i] = reader.ReadFiniteSingle("motor value");
            }

            var direction = new float[6];
            for (var i = 0; i < direction.Length; i++)
            {
                direction[i] = reader.ReadFiniteSingle("move direction scale");
            }

            var weightSource = (Shooter.Core.CarrySource)reader.ReadByte();
            var multiplierSource = (Shooter.Core.CarrySource)reader.ReadByte();
            var freeWeight = reader.ReadFiniteSingle("carry free weight");
            var speedLossPerKg = reader.ReadFiniteSingle("carry speed loss per kg");
            var minWeightMultiplier = reader.ReadFiniteSingle("carry min weight multiplier");
            var gaits = (Shooter.Core.CarryGaits)reader.ReadByte();

            var stance = new float[6];
            for (var i = 0; i < stance.Length; i++)
            {
                stance[i] = reader.ReadFiniteSingle("body size");
            }

            var slope = reader.ReadFiniteSingle("slope limit");
            var step = reader.ReadFiniteSingle("step offset");
            var skin = reader.ReadFiniteSingle("skin width");
            var layerMask = reader.ReadInt32();
            var prone = new float[6];
            for (var i = 0; i < prone.Length; i++)
            {
                prone[i] = reader.ReadFiniteSingle("prone value");
            }

            var leanSideOffset = reader.ReadFiniteSingle("lean side offset");
            var leanAngleDegrees = reader.ReadFiniteSingle("lean angle");
            var leanSpeed = reader.ReadFiniteSingle("lean speed");
            var airJumps = reader.ReadInt32AtLeast(0, "air jumps");
            var airJumpSpeed = reader.ReadFiniteSingle("air jump speed");
            var jumpGraceSeconds = reader.ReadFiniteSingle("jump grace seconds");
            var coverEnterSpeed = reader.ReadFiniteSingle("cover enter speed");
            var swim = new float[3];
            for (var i = 0; i < swim.Length; i++)
            {
                swim[i] = reader.ReadFiniteSingle("swim value");
            }

            var wallRunEnabled = reader.ReadBool();
            var wallRun = new float[4];
            for (var i = 0; i < wallRun.Length; i++)
            {
                wallRun[i] = reader.ReadFiniteSingle("wall run value");
            }

            try
            {
                return new Shooter.Core.NetMotorSettings(
                    new Shooter.Core.MotorSpec(
                        motor[0], motor[1], motor[2], motor[3], motor[4], motor[5], motor[6], motor[7], motor[8], motor[9], motor[10], motor[11], motor[12],
                        new Shooter.Core.MoveDirectionScale(direction[0], direction[1], direction[2]),
                        new Shooter.Core.MoveDirectionScale(direction[3], direction[4], direction[5]),
                        new Shooter.Core.CarryLoadSpec(weightSource, multiplierSource, freeWeight, speedLossPerKg, minWeightMultiplier, gaits),
                        motor[13], motor[14], motor[15], motor[16], motor[17], motor[18],
                        motor[19], motor[20], motor[21], motor[22], motor[23], motor[24], motor[25], motor[26], motor[27], motor[28],
                        leanSideOffset, leanAngleDegrees, leanSpeed, airJumps, airJumpSpeed, jumpGraceSeconds, coverEnterSpeed),
                    new Shooter.Core.StanceGeometry(stance[0], stance[1], stance[2], stance[3], stance[4], stance[5]),
                    new Shooter.Core.CapsuleMoverSpec(slope, step, skin, layerMask),
                    new Shooter.Core.ProneSpec(prone[0], prone[1], prone[2], prone[3], prone[4], prone[5]),
                    new Shooter.Core.SwimTuning(swim[0], swim[1], swim[2]),
                    new Shooter.Core.WallRunTuning(wallRunEnabled, wallRun[0], wallRun[1], wallRun[2], wallRun[3]));
            }
            catch (System.ArgumentException e)
            {
                throw new MessageReadException("motor settings are out of range: " + e.Message, e);
            }
        }
    }
}
