using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    public struct PlayerSnapshot : ISnapshotEntity<PlayerSnapshot>
    {
        private const int _motionFlagCount = 5;
        private const int _identityFlagCount = 2;
        private const float _leanSteps = 127f;

        public int PlayerId { get; set; }
        public PlayerFields Present { get; set; }

        public Team Team { get; set; }
        public bool IsBot { get; set; }

        // 部分を分けずに Identity に載せるのは、部分のビットに空きが無く、陣営と同じく入り直すとき以外ほぼ変わらないため。
        public string CosmeticId { get; set; }

        // 陣営と同じく試合の間ほぼ変わらないので、部分を分けずに Identity に載せる
        public byte SquadId { get; set; }

        // 行を消さずに旗で隠すのは、名簿・スコア・残りの人数を行から数えているため。動きの値は送る側が空にする。
        // 見えるかが変わるたびに切り替わるが、部分のビットに空きが無いので、Bot の旗と同じバイトに載せて Identity で送る
        public bool Concealed { get; set; }

        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }

        public float Yaw { get; set; }
        public bool IsGrounded { get; set; }
        public bool IsCrouched { get; set; }

        public bool IsProne { get; set; }
        public bool IsAiming { get; set; }

        // 部分のビットに空きが無く、落ちる速さと一緒に変わるので、Motion の旗のバイトに載せる
        public bool IsParachuting { get; set; }

        public CoverSync Cover { get; set; }
        public DodgeSnapshot Dodge { get; set; }
        public VaultSync Vault { get; set; }
        public CharacterProneState Prone { get; set; }
        public CharacterLadderState Ladder { get; set; }

        public int Health { get; set; }

        public Vector3 LastHitDirection { get; set; }

        public DownedSync Downed { get; set; }

        public VehicleSeatSync Vehicle { get; set; }

        public string WeaponId { get; set; }

        public int Kills { get; set; }
        public int Deaths { get; set; }

        public bool HasKiller { get; set; }

        public int KillerId { get; set; }

        public int SpawnProtectionSeconds { get; set; }

        public float Lean { get; set; }

        public bool Swimming { get; set; }

        public WallRunSide WallRunSide { get; set; }

        public byte AirJumpsUsed { get; set; }

        // 持ち主の予測が巻き戻した tick から猶予の続きを数えられるよう、地上のジャンプの猶予の残りも載せる
        public float JumpGraceLeft { get; set; }

        public int Assists { get; set; }

        public int GunGameTier { get; set; }

        public int StreakKills { get; set; }

        public int EntityId => PlayerId;

        public int PresentBits => (int)Present;

        public int AllBits => (int)PlayerFields.All;

        public int OptionalBits => (int)PlayerFields.Optional;

        public int ChangedBits(in PlayerSnapshot baseline)
        {
            var bits = PlayerFields.None;
            if (Team != baseline.Team
                || IsBot != baseline.IsBot
                || Concealed != baseline.Concealed
                || SquadId != baseline.SquadId
                || !string.Equals(CosmeticId ?? "", baseline.CosmeticId ?? "", System.StringComparison.Ordinal))
            {
                bits |= PlayerFields.Identity;
            }

            if (!Same.Bits(Position, baseline.Position)
                || !Same.Bits(Velocity, baseline.Velocity)
                || !Same.Bits(Yaw, baseline.Yaw)
                || IsGrounded != baseline.IsGrounded
                || IsCrouched != baseline.IsCrouched
                || IsProne != baseline.IsProne
                || IsAiming != baseline.IsAiming
                || IsParachuting != baseline.IsParachuting)
            {
                bits |= PlayerFields.Motion;
            }

            if (!Cover.SameAs(baseline.Cover))
            {
                bits |= PlayerFields.Cover;
            }

            if (Dodge.IsRolling != baseline.Dodge.IsRolling
                || !Same.Bits(Dodge.Direction, baseline.Dodge.Direction)
                || !Same.Bits(Dodge.Elapsed, baseline.Dodge.Elapsed)
                || !Same.Bits(Dodge.CooldownRemaining, baseline.Dodge.CooldownRemaining))
            {
                bits |= PlayerFields.Dodge;
            }

            if (!Vault.SameAs(baseline.Vault))
            {
                bits |= PlayerFields.Vault;
            }

            if (Prone.IsActive != baseline.Prone.IsActive
                || Prone.WasSprinting != baseline.Prone.WasSprinting
                || Prone.WasCrouching != baseline.Prone.WasCrouching
                || Prone.IsDiving != baseline.Prone.IsDiving
                || !Same.Bits(Prone.DiveDirection, baseline.Prone.DiveDirection)
                || !Same.Bits(Prone.DiveElapsed, baseline.Prone.DiveElapsed))
            {
                bits |= PlayerFields.Prone;
            }

            if (Ladder.Index != baseline.Ladder.Index
                || Ladder.Entry != baseline.Ladder.Entry
                || !Same.Bits(Ladder.Height, baseline.Ladder.Height)
                || Ladder.IsInputLatched != baseline.Ladder.IsInputLatched
                || Ladder.LatchedDownSign != baseline.Ladder.LatchedDownSign
                || !Same.Bits(Ladder.RegrabRemaining, baseline.Ladder.RegrabRemaining))
            {
                bits |= PlayerFields.Ladder;
            }

            if (Health != baseline.Health || !Same.Bits(LastHitDirection, baseline.LastHitDirection))
            {
                bits |= PlayerFields.Health;
            }

            if (!Downed.SameAs(baseline.Downed))
            {
                bits |= PlayerFields.Downed;
            }

            if (!string.Equals(WeaponId ?? "", baseline.WeaponId ?? "", System.StringComparison.Ordinal))
            {
                bits |= PlayerFields.Weapon;
            }

            if (Kills != baseline.Kills || Deaths != baseline.Deaths || HasKiller != baseline.HasKiller
                || (HasKiller && KillerId != baseline.KillerId))
            {
                bits |= PlayerFields.Score;
            }

            if (SpawnProtectionSeconds != baseline.SpawnProtectionSeconds)
            {
                bits |= PlayerFields.SpawnProtection;
            }

            if (!Vehicle.SameAs(baseline.Vehicle))
            {
                bits |= PlayerFields.Vehicle;
            }

            // 範囲の外の Lean を既定と同じに丸めて載せずに済ませると、書くときに断れないので変わったとみなす
            if (!IsLeanInRange(Lean)
                || LeanStep(Lean) != LeanStep(baseline.Lean)
                || Swimming != baseline.Swimming
                || WallRunSide != baseline.WallRunSide
                || AirJumpsUsed != baseline.AirJumpsUsed
                || !Same.Bits(JumpGraceLeft, baseline.JumpGraceLeft))
            {
                bits |= PlayerFields.Traversal;
            }

            if (Assists != baseline.Assists)
            {
                bits |= PlayerFields.Assists;
            }

            if (GunGameTier != baseline.GunGameTier || StreakKills != baseline.StreakKills)
            {
                bits |= PlayerFields.Progress;
            }

            return (int)bits;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            var fields = (PlayerFields)bits;
            if ((fields & PlayerFields.Identity) != 0)
            {
                writer.WriteByte((byte)Team);
                writer.WriteByte((byte)((IsBot ? 1 : 0) | (Concealed ? 2 : 0)));
                writer.WriteString(CosmeticId, MatchProtocol.MaxContentIdBytes);
                writer.WriteByte(SquadId);
            }

            if ((fields & PlayerFields.Motion) != 0)
            {
                writer.WriteVector3(Position);
                writer.WriteVector3(Velocity);
                writer.WriteSingle(Yaw);
                if (IsCrouched && IsProne)
                {
                    throw new System.InvalidOperationException("a player cannot be both crouched and prone");
                }

                writer.WriteByte((byte)((IsGrounded ? 1 : 0) | (IsCrouched ? 2 : 0) | (IsAiming ? 4 : 0) | (IsProne ? 8 : 0)
                    | (IsParachuting ? 16 : 0)));
            }

            if ((fields & PlayerFields.Cover) != 0)
            {
                Cover.Write(writer);
            }

            if ((fields & PlayerFields.Dodge) != 0)
            {
                writer.WriteBool(Dodge.IsRolling);
                writer.WriteVector2(Dodge.Direction);
                writer.WriteSingle(Dodge.Elapsed);
                writer.WriteSingle(Dodge.CooldownRemaining);
            }

            if ((fields & PlayerFields.Vault) != 0)
            {
                Vault.Write(writer);
            }

            if ((fields & PlayerFields.Health) != 0)
            {
                writer.WriteInt32(Health);
                writer.WriteVector3(LastHitDirection);
            }

            if ((fields & PlayerFields.Downed) != 0)
            {
                Downed.Write(writer);
            }

            if ((fields & PlayerFields.Weapon) != 0)
            {
                writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            }

            if ((fields & PlayerFields.Score) != 0)
            {
                if (HasKiller && KillerId < 0)
                {
                    throw new System.InvalidOperationException("killer id must not be negative");
                }

                writer.WriteInt32(Kills);
                writer.WriteInt32(Deaths);
                writer.WriteInt32(HasKiller ? KillerId : PlayerIds.None);
            }

            if ((fields & PlayerFields.SpawnProtection) != 0)
            {
                writer.WriteInt32(SpawnProtectionSeconds);
            }

            if ((fields & PlayerFields.Prone) != 0)
            {
                WriteProne(writer, Prone);
            }

            if ((fields & PlayerFields.Ladder) != 0)
            {
                WriteLadder(writer, Ladder);
            }

            if ((fields & PlayerFields.Vehicle) != 0)
            {
                Vehicle.Write(writer);
            }

            if ((fields & PlayerFields.Traversal) != 0)
            {
                if (!IsLeanInRange(Lean))
                {
                    throw new System.InvalidOperationException("lean must be within -1 to 1");
                }

                writer.WriteByte(unchecked((byte)LeanStep(Lean)));
                writer.WriteBool(Swimming);
                writer.WriteByte((byte)WallRunSide);
                writer.WriteByte(AirJumpsUsed);
                if (!(JumpGraceLeft >= 0f) || float.IsInfinity(JumpGraceLeft))
                {
                    throw new System.InvalidOperationException("jump grace must be finite and not negative");
                }

                writer.WriteSingle(JumpGraceLeft);
            }

            if ((fields & PlayerFields.Assists) != 0)
            {
                if (Assists < 0)
                {
                    throw new System.InvalidOperationException("assists must not be negative");
                }

                writer.WriteInt32(Assists);
            }

            if ((fields & PlayerFields.Progress) != 0)
            {
                if (GunGameTier < 0 || StreakKills < 0)
                {
                    throw new System.InvalidOperationException("gun game tier and streak kills must not be negative");
                }

                writer.WriteInt32(GunGameTier);
                writer.WriteInt32(StreakKills);
            }
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            PlayerId = id;
            var fields = (PlayerFields)bits;
            Present = fields;
            if ((fields & PlayerFields.Identity) != 0)
            {
                Team = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "team");
                var flags = reader.ReadByteBelow(1 << _identityFlagCount, "player identity flags");
                IsBot = (flags & 1) != 0;
                Concealed = (flags & 2) != 0;
                CosmeticId = reader.ReadString(MatchProtocol.MaxContentIdBytes, CosmeticId);
                SquadId = reader.ReadByte();
            }

            if ((fields & PlayerFields.Motion) != 0)
            {
                Position = reader.ReadFiniteVector3("player position");
                Velocity = reader.ReadFiniteVector3("player velocity");
                Yaw = reader.ReadFiniteSingle("player yaw");
                var flags = reader.ReadByteBelow(1 << _motionFlagCount, "player motion flags");
                IsGrounded = (flags & 1) != 0;
                IsCrouched = (flags & 2) != 0;
                IsAiming = (flags & 4) != 0;
                IsProne = (flags & 8) != 0;
                IsParachuting = (flags & 16) != 0;
                if (IsCrouched && IsProne)
                {
                    throw new MessageReadException("a player is both crouched and prone");
                }
            }

            if ((fields & PlayerFields.Cover) != 0)
            {
                Cover = CoverSync.Read(reader);
            }

            if ((fields & PlayerFields.Dodge) != 0)
            {
                Dodge = new DodgeSnapshot(
                    reader.ReadBool(),
                    reader.ReadFiniteVector2("dodge direction"),
                    reader.ReadFiniteSingle("dodge time"),
                    reader.ReadFiniteSingle("dodge cooldown"));
            }

            if ((fields & PlayerFields.Vault) != 0)
            {
                Vault = VaultSync.Read(reader);
            }

            if ((fields & PlayerFields.Health) != 0)
            {
                Health = reader.ReadInt32AtLeast(0, "health");
                LastHitDirection = reader.ReadFiniteVector3("last hit direction");
            }

            if ((fields & PlayerFields.Downed) != 0)
            {
                Downed = DownedSync.Read(reader);
            }

            if ((fields & PlayerFields.Weapon) != 0)
            {
                WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            }

            if ((fields & PlayerFields.Score) != 0)
            {
                Kills = reader.ReadInt32AtLeast(0, "kills");
                Deaths = reader.ReadInt32AtLeast(0, "deaths");
                var killer = reader.ReadInt32AtLeast(PlayerIds.None, "killer");
                HasKiller = killer != PlayerIds.None;
                KillerId = HasKiller ? killer : 0;
            }

            if ((fields & PlayerFields.SpawnProtection) != 0)
            {
                SpawnProtectionSeconds = reader.ReadInt32AtLeast(0, "spawn protection seconds");
            }

            if ((fields & PlayerFields.Prone) != 0)
            {
                Prone = ReadProne(reader);
            }

            if ((fields & PlayerFields.Ladder) != 0)
            {
                Ladder = ReadLadder(reader);
            }

            if ((fields & PlayerFields.Vehicle) != 0)
            {
                Vehicle = VehicleSeatSync.Read(reader);
            }

            if ((fields & PlayerFields.Traversal) != 0)
            {
                var lean = unchecked((sbyte)reader.ReadByte());
                if (lean < -(int)_leanSteps)
                {
                    throw new MessageReadException("lean must be within -1 to 1");
                }

                Lean = lean / _leanSteps;
                Swimming = reader.ReadBool();
                WallRunSide = (WallRunSide)reader.ReadByteBelow(EnumRange<WallRunSide>.Count, "wall run side");
                AirJumpsUsed = reader.ReadByte();
                JumpGraceLeft = reader.ReadFiniteSingle("jump grace");
                if (JumpGraceLeft < 0f)
                {
                    throw new MessageReadException("jump grace must not be negative");
                }
            }

            if ((fields & PlayerFields.Assists) != 0)
            {
                Assists = reader.ReadInt32AtLeast(0, "assists");
            }

            if ((fields & PlayerFields.Progress) != 0)
            {
                GunGameTier = reader.ReadInt32AtLeast(0, "gun game tier");
                StreakKills = reader.ReadInt32AtLeast(0, "streak kills");
            }
        }

        public void FillFrom(in PlayerSnapshot baseline)
        {
            var missing = PlayerFields.All & ~Present;
            if ((missing & PlayerFields.Identity) != 0)
            {
                Team = baseline.Team;
                IsBot = baseline.IsBot;
                Concealed = baseline.Concealed;
                CosmeticId = baseline.CosmeticId;
                SquadId = baseline.SquadId;
            }

            if ((missing & PlayerFields.Motion) != 0)
            {
                Position = baseline.Position;
                Velocity = baseline.Velocity;
                Yaw = baseline.Yaw;
                IsGrounded = baseline.IsGrounded;
                IsCrouched = baseline.IsCrouched;
                IsProne = baseline.IsProne;
                IsAiming = baseline.IsAiming;
                IsParachuting = baseline.IsParachuting;
            }

            if ((missing & PlayerFields.Cover) != 0)
            {
                Cover = baseline.Cover;
            }

            if ((missing & PlayerFields.Dodge) != 0)
            {
                Dodge = baseline.Dodge;
            }

            if ((missing & PlayerFields.Vault) != 0)
            {
                Vault = baseline.Vault;
            }

            if ((missing & PlayerFields.Health) != 0)
            {
                Health = baseline.Health;
                LastHitDirection = baseline.LastHitDirection;
            }

            if ((missing & PlayerFields.Downed) != 0)
            {
                Downed = baseline.Downed;
            }

            if ((missing & PlayerFields.Weapon) != 0)
            {
                WeaponId = baseline.WeaponId;
            }

            if ((missing & PlayerFields.Score) != 0)
            {
                Kills = baseline.Kills;
                Deaths = baseline.Deaths;
                HasKiller = baseline.HasKiller;
                KillerId = baseline.KillerId;
            }

            if ((missing & PlayerFields.SpawnProtection) != 0)
            {
                SpawnProtectionSeconds = baseline.SpawnProtectionSeconds;
            }

            if ((missing & PlayerFields.Prone) != 0)
            {
                Prone = baseline.Prone;
            }

            if ((missing & PlayerFields.Ladder) != 0)
            {
                Ladder = baseline.Ladder;
            }

            if ((missing & PlayerFields.Vehicle) != 0)
            {
                Vehicle = baseline.Vehicle;
            }

            if ((missing & PlayerFields.Traversal) != 0)
            {
                Lean = baseline.Lean;
                Swimming = baseline.Swimming;
                WallRunSide = baseline.WallRunSide;
                AirJumpsUsed = baseline.AirJumpsUsed;
                JumpGraceLeft = baseline.JumpGraceLeft;
            }

            if ((missing & PlayerFields.Assists) != 0)
            {
                Assists = baseline.Assists;
            }

            if ((missing & PlayerFields.Progress) != 0)
            {
                GunGameTier = baseline.GunGameTier;
                StreakKills = baseline.StreakKills;
            }

            Present = PlayerFields.All;
        }

        private static bool IsLeanInRange(float lean)
        {
            return lean >= -1f && lean <= 1f;
        }

        private static int LeanStep(float lean)
        {
            var clamped = lean >= -1f ? (lean <= 1f ? lean : 1f) : -1f;
            return (int)System.Math.Round(clamped * _leanSteps);
        }

        private static void WriteProne(MessageWriter writer, in CharacterProneState prone)
        {
            writer.WriteByte((byte)((prone.IsActive ? 1 : 0) | (prone.WasSprinting ? 2 : 0) | (prone.WasCrouching ? 4 : 0)
                | (prone.IsDiving ? 8 : 0)));
            if (prone.IsDiving)
            {
                writer.WriteVector2(prone.DiveDirection);
                writer.WriteSingle(prone.DiveElapsed);
            }
        }

        private static CharacterProneState ReadProne(MessageReader reader)
        {
            var flags = reader.ReadByteBelow(16, "prone flags");
            var diving = (flags & 8) != 0;
            var direction = Vector2.Zero;
            var elapsed = 0f;
            if (diving)
            {
                direction = reader.ReadFiniteVector2("dive direction");
                elapsed = reader.ReadFiniteSingle("dive time");
                if (direction.LengthSquared() <= 0f || elapsed < 0f)
                {
                    throw new MessageReadException("a dive needs a direction and a time that is not negative");
                }
            }

            return new CharacterProneState((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, diving, direction, elapsed);
        }

        private static void WriteLadder(MessageWriter writer, in CharacterLadderState ladder)
        {
            writer.WriteInt32(ladder.Index);
            writer.WriteSingle(ladder.RegrabRemaining);
            if (!ladder.IsClimbing)
            {
                return;
            }

            writer.WriteByte((byte)ladder.Entry);
            writer.WriteSingle(ladder.Height);
            writer.WriteBool(ladder.IsInputLatched);
            writer.WriteByte((byte)(ladder.LatchedDownSign + 1));
        }

        private static CharacterLadderState ReadLadder(MessageReader reader)
        {
            var index = reader.ReadInt32AtLeast(CharacterLadderState.NoLadder, "ladder index");
            var regrab = reader.ReadFiniteSingle("ladder regrab time");
            if (regrab < 0f)
            {
                throw new MessageReadException("ladder regrab time must not be negative");
            }

            if (index == CharacterLadderState.NoLadder)
            {
                return CharacterLadderState.Off(regrab);
            }

            var entry = (LadderEntry)reader.ReadByteBelow(EnumRange<LadderEntry>.Count, "ladder entry");
            if (entry == LadderEntry.None)
            {
                throw new MessageReadException("a climbing player has no ladder entry");
            }

            var height = reader.ReadFiniteSingle("ladder height");
            var latched = reader.ReadBool();
            var sign = reader.ReadByteBelow(3, "ladder latched sign") - 1;
            return CharacterLadderState.Climbing(index, entry, height, latched, sign);
        }
    }
}
