using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class NetHitLog
    {
        public const string Tag = "[Net]";

        public static string Hit(int attackerId, int victimId, float rewindTicks, HitZone zone, int damage, int healthAfter, DamageOutcome outcome)
        {
            if (!Enum.IsDefined(typeof(HitZone), zone))
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "unknown hit zone");
            }

            if (!Enum.IsDefined(typeof(DamageOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown damage outcome");
            }

            return Tag + " hit attacker=" + attackerId.ToString(CultureInfo.InvariantCulture)
                + " victim=" + victimId.ToString(CultureInfo.InvariantCulture)
                + " rewindTicks=" + rewindTicks.ToString("F2", CultureInfo.InvariantCulture)
                + " zone=" + zone
                + " dmg=" + damage.ToString(CultureInfo.InvariantCulture)
                + " hp=" + healthAfter.ToString(CultureInfo.InvariantCulture)
                + " outcome=" + outcome;
        }

        public static string Hit(
            int attackerId, int victimId, float rewindTicks, HitZone zone, int damage, int healthAfter, DamageOutcome outcome, bool victimCrouched)
        {
            return Hit(attackerId, victimId, rewindTicks, zone, damage, healthAfter, outcome)
                + " victimCrouched=" + (victimCrouched ? "true" : "false");
        }

        // 持ち主の [Net] fire のログの index と dir と突き合わせられるよう、ばらつきを掛けた後の判定の向きを書く。
        public static string Shot(int attackerId, int shotIndex, float coneDegrees, float x, float y, float z)
        {
            return Tag + " shot attacker=" + attackerId.ToString(CultureInfo.InvariantCulture)
                + " index=" + shotIndex.ToString(CultureInfo.InvariantCulture)
                + " cone=" + coneDegrees.ToString("F2", CultureInfo.InvariantCulture)
                + " dir=" + Direction(x, y, z);
        }

        public static string ShotIndexCorrected(int attackerId, int clientIndex, int serverIndex, int generation)
        {
            return Tag + " shot index corrected attacker=" + attackerId.ToString(CultureInfo.InvariantCulture)
                + " client=" + clientIndex.ToString(CultureInfo.InvariantCulture)
                + " server=" + serverIndex.ToString(CultureInfo.InvariantCulture)
                + " generation=" + generation.ToString(CultureInfo.InvariantCulture);
        }

        // クライアントとサーバーのログで同じ書式にするため、向きの書き方はここ 1 か所で決める。
        public static string Direction(float x, float y, float z)
        {
            return "(" + x.ToString("F4", CultureInfo.InvariantCulture)
                + "," + y.ToString("F4", CultureInfo.InvariantCulture)
                + "," + z.ToString("F4", CultureInfo.InvariantCulture) + ")";
        }

        public static string Rejected(int attackerId, ShotRejection reason)
        {
            return Tag + " rejected shot attacker=" + attackerId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason;
        }
    }
}
