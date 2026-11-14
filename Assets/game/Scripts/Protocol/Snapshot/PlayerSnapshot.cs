using System.Numerics;
using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>
    /// 1 人のプレイヤー (人と Bot) の、全員に見せる状態。Netcode では NetPlayer・NetworkPlayerMotor・NetPlayerWeapon の同期値と
    /// <c>StateRpc</c>、試合の成績と出現直後の無敵の NetworkList に分かれていたもの。
    /// </summary>
    /// <remarks>
    /// 位置などの移動の状態は、そのプレイヤーが最後に処理された入力の後の値。持ち主は <see cref="OwnerSnapshot.LastProcessedInputTick"/> と
    /// 合わせて予測を照合する。
    /// </remarks>
    public struct PlayerSnapshot : ISnapshotEntity<PlayerSnapshot>
    {
        private const int MotionFlagCount = 3;

        public int PlayerId { get; set; }
        public PlayerFields Present { get; set; }

        public Team Team { get; set; }
        public bool IsBot { get; set; }

        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }

        /// <summary>向き (Y 軸まわり、ラジアン)。</summary>
        public float Yaw { get; set; }
        public bool IsGrounded { get; set; }
        public bool IsCrouched { get; set; }
        public bool IsAiming { get; set; }

        public CoverSync Cover { get; set; }
        public DodgeSnapshot Dodge { get; set; }
        public VaultSync Vault { get; set; }

        /// <summary>0 は倒れているか、まだ試合に出ていない。</summary>
        public int Health { get; set; }

        /// <summary>最後に受けた弾の進む向き。倒れた体を倒す向きにだけ使う。</summary>
        public Vector3 LastHitDirection { get; set; }

        public DownedSync Downed { get; set; }

        /// <summary>持っている武器の Id。持っていなければ空。</summary>
        public string WeaponId { get; set; }

        public int Kills { get; set; }
        public int Deaths { get; set; }

        /// <summary>最後にこのプレイヤーを倒した相手がいるか。</summary>
        public bool HasKiller { get; set; }

        /// <summary>最後にこのプレイヤーを倒した相手。<see cref="HasKiller"/> が false なら使わない。</summary>
        public int KillerId { get; set; }

        /// <summary>出現直後の無敵の残り秒 (表示用の整数)。0 なら無敵でない。</summary>
        public int SpawnProtectionSeconds { get; set; }

        public int EntityId => PlayerId;

        public int PresentBits => (int)Present;

        public int AllBits => (int)PlayerFields.All;

        public int ChangedBits(in PlayerSnapshot baseline)
        {
            var bits = PlayerFields.None;
            if (Team != baseline.Team || IsBot != baseline.IsBot)
            {
                bits |= PlayerFields.Identity;
            }

            if (!Same.Bits(Position, baseline.Position)
                || !Same.Bits(Velocity, baseline.Velocity)
                || !Same.Bits(Yaw, baseline.Yaw)
                || IsGrounded != baseline.IsGrounded
                || IsCrouched != baseline.IsCrouched
                || IsAiming != baseline.IsAiming)
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

            return (int)bits;
        }

        public void WriteBits(MessageWriter writer, int bits)
        {
            var fields = (PlayerFields)bits;
            if ((fields & PlayerFields.Identity) != 0)
            {
                writer.WriteByte((byte)Team);
                writer.WriteBool(IsBot);
            }

            if ((fields & PlayerFields.Motion) != 0)
            {
                writer.WriteVector3(Position);
                writer.WriteVector3(Velocity);
                writer.WriteSingle(Yaw);
                writer.WriteByte((byte)((IsGrounded ? 1 : 0) | (IsCrouched ? 2 : 0) | (IsAiming ? 4 : 0)));
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
        }

        public void ReadBits(MessageReader reader, int id, int bits)
        {
            PlayerId = id;
            var fields = (PlayerFields)bits;
            Present = fields;
            if ((fields & PlayerFields.Identity) != 0)
            {
                Team = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "team");
                IsBot = reader.ReadBool();
            }

            if ((fields & PlayerFields.Motion) != 0)
            {
                Position = reader.ReadFiniteVector3("player position");
                Velocity = reader.ReadFiniteVector3("player velocity");
                Yaw = reader.ReadFiniteSingle("player yaw");
                var flags = reader.ReadByteBelow(1 << MotionFlagCount, "player motion flags");
                IsGrounded = (flags & 1) != 0;
                IsCrouched = (flags & 2) != 0;
                IsAiming = (flags & 4) != 0;
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
        }

        public void FillFrom(in PlayerSnapshot baseline)
        {
            var missing = PlayerFields.All & ~Present;
            if ((missing & PlayerFields.Identity) != 0)
            {
                Team = baseline.Team;
                IsBot = baseline.IsBot;
            }

            if ((missing & PlayerFields.Motion) != 0)
            {
                Position = baseline.Position;
                Velocity = baseline.Velocity;
                Yaw = baseline.Yaw;
                IsGrounded = baseline.IsGrounded;
                IsCrouched = baseline.IsCrouched;
                IsAiming = baseline.IsAiming;
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

            Present = PlayerFields.All;
        }
    }
}
