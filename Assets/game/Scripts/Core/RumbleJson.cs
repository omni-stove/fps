using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>ゲームパッドの振動の調整値 (<see cref="RumbleSpec"/>) を、平らな JSON から読む。書いていない項目は既定値。</summary>
    public static class RumbleJson
    {
        public const string FireLowItem = "fireLow";
        public const string FireHighItem = "fireHigh";
        public const string FireSecondsItem = "fireSeconds";
        public const string DamageLowItem = "damageLow";
        public const string DamageHighItem = "damageHigh";
        public const string DamageSecondsItem = "damageSeconds";
        public const string ExplosionLowItem = "explosionLow";
        public const string ExplosionHighItem = "explosionHigh";
        public const string ExplosionSecondsItem = "explosionSeconds";
        public const string ExplosionRadiusItem = "explosionRadius";

        private const string _kind = "rumble JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            FireLowItem,
            FireHighItem,
            FireSecondsItem,
            DamageLowItem,
            DamageHighItem,
            DamageSecondsItem,
            ExplosionLowItem,
            ExplosionHighItem,
            ExplosionSecondsItem,
            ExplosionRadiusItem,
        };

        /// <exception cref="ArgumentNullException">text が null。</exception>
        /// <exception cref="TuningJsonException">JSON が壊れている、知らない項目がある、値の型か範囲が違う。</exception>
        public static RumbleSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new RumbleSpec(
                        fields.ReadFloat(FireLowItem, RumbleSpec.DefaultFireLow),
                        fields.ReadFloat(FireHighItem, RumbleSpec.DefaultFireHigh),
                        fields.ReadFloat(FireSecondsItem, RumbleSpec.DefaultFireSeconds),
                        fields.ReadFloat(DamageLowItem, RumbleSpec.DefaultDamageLow),
                        fields.ReadFloat(DamageHighItem, RumbleSpec.DefaultDamageHigh),
                        fields.ReadFloat(DamageSecondsItem, RumbleSpec.DefaultDamageSeconds),
                        fields.ReadFloat(ExplosionLowItem, RumbleSpec.DefaultExplosionLow),
                        fields.ReadFloat(ExplosionHighItem, RumbleSpec.DefaultExplosionHigh),
                        fields.ReadFloat(ExplosionSecondsItem, RumbleSpec.DefaultExplosionSeconds),
                        fields.ReadFloat(ExplosionRadiusItem, RumbleSpec.DefaultExplosionRadius));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(e.ParamName) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        // RumbleSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static bool Known(string name)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
