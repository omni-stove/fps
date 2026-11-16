namespace Fps.Protocol
{
    /// <summary>サーバーが承認したときに返す、クライアントのプレイヤーとサーバーの時計。</summary>
    /// <remarks>
    /// マップはクライアントの <c>-map</c> ではなくここで受け取る。<c>-map</c> はホスト・サーバーだけが選び、クライアントは
    /// つないだサーバーのマップへ合わせる (Netcode のシーン管理と同じ意味) ため。
    /// 移動の値 (<see cref="Motor"/>) もここで受け取る。サーバーは起動したルールのファイルの値で動かし、クライアントはどのルールで
    /// 起動したかを知らないので、自分の持つ既定値で予測すると片方だけ変えたときに毎 tick ずれるため。
    /// </remarks>
    public sealed class ConnectAcceptedMessage : IMessage
    {
        public const byte CurrentVersion = 4;

        /// <summary><see cref="KillPlaneY"/> の既定。.NET のサーバーと Netcode のプレイヤーの既定と同じ。</summary>
        public const float DefaultKillPlaneY = -50f;

        public ushort Id => MessageIds.ConnectAccepted;

        public byte Version => CurrentVersion;

        /// <summary>このクライアントのプレイヤーの Id。スナップショットとイベントはこの Id で自分を指す。</summary>
        public int PlayerId { get; set; }

        public string ServerVersion { get; set; } = "";

        /// <summary>サーバーの 1 秒あたりの tick 数。</summary>
        public int TickRate { get; set; } = 1;

        /// <summary>承認した時点のサーバーの tick。</summary>
        public int ServerTick { get; set; }

        /// <summary>サーバーの試合のマップ (シーン名。<see cref="Fps.Core.NetLaunchOptions.IsMapName"/> に通る)。</summary>
        public string Map { get; set; } = "";

        /// <summary>試合の最大の体力。スナップショットは今の体力だけを運ぶので、HUD のゲージの満タンをここで知る。</summary>
        public int MaxHealth { get; set; } = 1;

        /// <summary>サーバーがプレイヤーを動かす値 (移動・体の寸法・カプセルの移動・伏せ)。</summary>
        public Fps.Core.NetMotorSettings Motor { get; set; } = Fps.Core.NetMotorSettings.Default;

        /// <summary>この高さより下に落ちたら出現地点へ戻す (サーバーの移動と同じ値)。</summary>
        public float KillPlaneY { get; set; } = DefaultKillPlaneY;

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PlayerId);
            writer.WriteString(ServerVersion, Fps.Core.BuildVersion.MaxLength);
            writer.WriteInt32(TickRate);
            writer.WriteInt32(ServerTick);
            writer.WriteString(Map, Fps.Core.NetLaunchOptions.MaxMapNameLength);
            writer.WriteInt32(MaxHealth);
            WriteMotor(writer, Motor ?? throw new System.InvalidOperationException("motor settings must be set"));
            writer.WriteSingle(KillPlaneY);
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "player id");
            ServerVersion = reader.ReadString(Fps.Core.BuildVersion.MaxLength, ServerVersion);
            TickRate = reader.ReadInt32AtLeast(1, "tick rate");
            ServerTick = reader.ReadInt32AtLeast(0, "server tick");
            Map = reader.ReadString(Fps.Core.NetLaunchOptions.MaxMapNameLength, Map);
            if (!Fps.Core.NetLaunchOptions.IsMapName(Map))
            {
                throw new MessageReadException("map '" + Map + "' is not a scene name");
            }

            MaxHealth = reader.ReadInt32AtLeast(1, "max health");
            Motor = ReadMotor(reader);
            KillPlaneY = reader.ReadFiniteSingle("kill plane y");
        }

        private static void WriteMotor(MessageWriter writer, Fps.Core.NetMotorSettings settings)
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
            WriteDirection(writer, motor.WalkDirection);
            WriteDirection(writer, motor.SprintDirection);

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
        }

        private static void WriteDirection(MessageWriter writer, Fps.Core.MoveDirectionScale direction)
        {
            writer.WriteSingle(direction.Forward);
            writer.WriteSingle(direction.Backward);
            writer.WriteSingle(direction.Side);
        }

        // 範囲の検査は Core の型の作り方に任せ、通らない値は読めないメッセージとして断る
        private static Fps.Core.NetMotorSettings ReadMotor(MessageReader reader)
        {
            var motor = new float[13];
            for (var i = 0; i < motor.Length; i++)
            {
                motor[i] = reader.ReadFiniteSingle("motor value");
            }

            var direction = new float[6];
            for (var i = 0; i < direction.Length; i++)
            {
                direction[i] = reader.ReadFiniteSingle("move direction scale");
            }

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

            try
            {
                return new Fps.Core.NetMotorSettings(
                    new Fps.Core.MotorSpec(
                        motor[0], motor[1], motor[2], motor[3], motor[4], motor[5], motor[6], motor[7], motor[8], motor[9], motor[10], motor[11], motor[12],
                        new Fps.Core.MoveDirectionScale(direction[0], direction[1], direction[2]),
                        new Fps.Core.MoveDirectionScale(direction[3], direction[4], direction[5])),
                    new Fps.Core.StanceGeometry(stance[0], stance[1], stance[2], stance[3], stance[4], stance[5]),
                    new Fps.Core.CapsuleMoverSpec(slope, step, skin, layerMask),
                    new Fps.Core.ProneSpec(prone[0], prone[1], prone[2], prone[3], prone[4], prone[5]));
            }
            catch (System.ArgumentException e)
            {
                throw new MessageReadException("motor settings are out of range: " + e.Message, e);
            }
        }
    }
}
