using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>
    /// クライアントが表示と予測に使う試合のルール (Netcode の <c>NetMatchRules</c> の中身)。判定はサーバーが自分のルールでやり直す。
    /// </summary>
    public struct MatchRulesSync
    {
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

        /// <exception cref="System.ArgumentNullException">rules が null。</exception>
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
                && Ping == other.Ping;
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
        }

        internal static MatchRulesSync Read(MessageReader reader)
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
            };
        }
    }
}
