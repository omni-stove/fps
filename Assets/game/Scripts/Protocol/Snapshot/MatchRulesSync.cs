using Shooter.Core;

namespace Shooter.Protocol
{
    public struct MatchRulesSync
    {
        // Jump を持つと、ルールが届く前の default の値でクライアントの予測が跳べなくなるため、禁じるかで持つ。
        private bool _jumpForbidden;

        public DeathBody DeathBody { get; set; }
        public DeathCamera DeathCamera { get; set; }
        public RespawnMode RespawnMode { get; set; }
        public bool InfiniteReserve { get; set; }
        public Cover Cover { get; set; }
        public BlindFire BlindFire { get; set; }
        public Dodge Dodge { get; set; }
        public Vault Vault { get; set; }
        public HudDensity HudDensity { get; set; }
        public Ping Ping { get; set; }
        public Prone Prone { get; set; }
        public Ladder Ladder { get; set; }

        // 開けるかで予測の動きが変わるので、ほかの移動のルールと同じく送る
        public Parachute Parachute { get; set; }
        public ReloadMode ReloadMode { get; set; }

        // 近接とストリークはクライアントが振る見た目と報酬の枠を出すかを決めるのに要るが、どのルールで動くかはサーバーしか知らないので送る
        public Melee Melee { get; set; }
        public Streaks Streaks { get; set; }

        public TextChat TextChat { get; set; }
        public Spectating Spectating { get; set; }
        public Abilities Abilities { get; set; }

        public string WeatherId { get; set; }

        public string TimeOfDayId { get; set; }

        public Jump Jump
        {
            get => _jumpForbidden ? Jump.Off : Jump.On;
            set
            {
                // Enum.IsDefined は値を箱に入れ、毎 tick 読むルールで GC を起こすので使わない。
                if (value != Jump.On && value != Jump.Off)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(value), value, "value is not a defined Jump");
                }

                _jumpForbidden = value == Jump.Off;
            }
        }

        public static MatchRulesSync From(GameRules rules)
        {
            if (rules == null)
            {
                throw new System.ArgumentNullException(nameof(rules));
            }

            var actions = rules.Actions;
            return new MatchRulesSync
            {
                DeathBody = rules.DeathBody,
                DeathCamera = rules.DeathCamera,
                RespawnMode = rules.RespawnMode,
                InfiniteReserve = rules.InfiniteReserve,
                Cover = actions.Cover,
                BlindFire = actions.BlindFire,
                Dodge = actions.Dodge,
                Vault = actions.Vault,
                HudDensity = rules.HudDensity,
                Ping = actions.Ping,
                Prone = actions.Prone,
                Ladder = actions.Ladder,
                Parachute = actions.Parachute,
                ReloadMode = rules.ReloadMode,
                Jump = actions.Jump,
                Melee = actions.Melee,
                Streaks = rules.Streaks,
                TextChat = rules.TextChat,
                Spectating = rules.Spectating,
                Abilities = actions.Abilities,
            };
        }

        public bool SameAs(in MatchRulesSync other)
        {
            return DeathBody == other.DeathBody
                && DeathCamera == other.DeathCamera
                && RespawnMode == other.RespawnMode
                && InfiniteReserve == other.InfiniteReserve
                && Cover == other.Cover
                && BlindFire == other.BlindFire
                && Dodge == other.Dodge
                && Vault == other.Vault
                && HudDensity == other.HudDensity
                && Ping == other.Ping
                && Prone == other.Prone
                && Ladder == other.Ladder
                && Parachute == other.Parachute
                && ReloadMode == other.ReloadMode
                && Jump == other.Jump
                && Melee == other.Melee
                && Streaks == other.Streaks
                && TextChat == other.TextChat
                && Spectating == other.Spectating
                && Abilities == other.Abilities
                && string.Equals(WeatherId ?? "", other.WeatherId ?? "", System.StringComparison.Ordinal)
                && string.Equals(TimeOfDayId ?? "", other.TimeOfDayId ?? "", System.StringComparison.Ordinal);
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)DeathBody);
            writer.WriteByte((byte)DeathCamera);
            writer.WriteByte((byte)RespawnMode);
            writer.WriteBool(InfiniteReserve);
            writer.WriteByte((byte)Cover);
            writer.WriteByte((byte)BlindFire);
            writer.WriteByte((byte)Dodge);
            writer.WriteByte((byte)Vault);
            writer.WriteByte((byte)HudDensity);
            writer.WriteByte((byte)Ping);
            writer.WriteByte((byte)Prone);
            writer.WriteByte((byte)Ladder);
            writer.WriteByte((byte)Parachute);
            writer.WriteByte((byte)ReloadMode);
            writer.WriteByte((byte)Jump);
            writer.WriteByte((byte)Melee);
            writer.WriteByte((byte)Streaks);
            writer.WriteByte((byte)TextChat);
            writer.WriteByte((byte)Spectating);
            writer.WriteByte((byte)Abilities);
            writer.WriteString(WeatherId, MatchProtocol.MaxContentIdBytes);
            writer.WriteString(TimeOfDayId, MatchProtocol.MaxContentIdBytes);
        }

        internal static MatchRulesSync Read(MessageReader reader, in MatchRulesSync previous)
        {
            return new MatchRulesSync
            {
                DeathBody = (DeathBody)reader.ReadByteBelow(EnumRange<DeathBody>.Count, "death body"),
                DeathCamera = (DeathCamera)reader.ReadByteBelow(EnumRange<DeathCamera>.Count, "death camera"),
                RespawnMode = (RespawnMode)reader.ReadByteBelow(EnumRange<RespawnMode>.Count, "respawn mode"),
                InfiniteReserve = reader.ReadBool(),
                Cover = (Cover)reader.ReadByteBelow(EnumRange<Cover>.Count, "cover rule"),
                BlindFire = (BlindFire)reader.ReadByteBelow(EnumRange<BlindFire>.Count, "blind fire rule"),
                Dodge = (Dodge)reader.ReadByteBelow(EnumRange<Dodge>.Count, "dodge rule"),
                Vault = (Vault)reader.ReadByteBelow(EnumRange<Vault>.Count, "vault rule"),
                HudDensity = (HudDensity)reader.ReadByteBelow(EnumRange<HudDensity>.Count, "hud density"),
                Ping = (Ping)reader.ReadByteBelow(EnumRange<Ping>.Count, "ping rule"),
                Prone = (Prone)reader.ReadByteBelow(EnumRange<Prone>.Count, "prone rule"),
                Ladder = (Ladder)reader.ReadByteBelow(EnumRange<Ladder>.Count, "ladder rule"),
                Parachute = (Parachute)reader.ReadByteBelow(EnumRange<Parachute>.Count, "parachute rule"),
                ReloadMode = (ReloadMode)reader.ReadByteBelow(EnumRange<ReloadMode>.Count, "reload mode"),
                Jump = (Jump)reader.ReadByteBelow(EnumRange<Jump>.Count, "jump rule"),
                Melee = (Melee)reader.ReadByteBelow(EnumRange<Melee>.Count, "melee rule"),
                Streaks = (Streaks)reader.ReadByteBelow(EnumRange<Streaks>.Count, "streaks rule"),
                TextChat = (TextChat)reader.ReadByteBelow(EnumRange<TextChat>.Count, "text chat rule"),
                Spectating = (Spectating)reader.ReadByteBelow(EnumRange<Spectating>.Count, "spectating rule"),
                Abilities = (Abilities)reader.ReadByteBelow(EnumRange<Abilities>.Count, "abilities rule"),
                WeatherId = reader.ReadString(MatchProtocol.MaxContentIdBytes, previous.WeatherId),
                TimeOfDayId = reader.ReadString(MatchProtocol.MaxContentIdBytes, previous.TimeOfDayId),
            };
        }
    }
}
