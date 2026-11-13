using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>サーバー権威のヒットスキャンが出す <c>[Net]</c> 付きログの文言。</summary>
    public static class NetHitLog
    {
        public const string Tag = "[Net]";

        /// <example><c>[Net] hit attacker=0 victim=1 rewindTicks=6.40 zone=Body dmg=25 hp=75 outcome=Damaged</c></example>
        /// <param name="attackerId">撃った人のプレイヤー id。</param>
        /// <param name="victimId">当たった人のプレイヤー id。</param>
        /// <param name="rewindTicks">巻き戻した tick 数 (サーバーの今の tick − 判定に使った tick)。</param>
        /// <param name="zone">当たった部位。</param>
        /// <param name="damage">計算したダメージ。</param>
        /// <param name="healthAfter">ダメージ後の体力。</param>
        /// <param name="outcome">ダメージを与えた結果。</param>
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

        /// <example><c>[Net] hit attacker=0 victim=1 rewindTicks=6.40 zone=Head dmg=50 hp=50 outcome=Damaged victimCrouched=true</c></example>
        /// <param name="attackerId">撃った人のプレイヤー id。</param>
        /// <param name="victimId">当たった人のプレイヤー id。</param>
        /// <param name="rewindTicks">巻き戻した tick 数 (サーバーの今の tick − 判定に使った tick)。</param>
        /// <param name="zone">当たった部位。</param>
        /// <param name="damage">計算したダメージ。</param>
        /// <param name="healthAfter">ダメージ後の体力。</param>
        /// <param name="outcome">ダメージを与えた結果。</param>
        /// <param name="victimCrouched">判定に使った (巻き戻した時点の) 当たった人のしゃがみ。</param>
        public static string Hit(
            int attackerId, int victimId, float rewindTicks, HitZone zone, int damage, int healthAfter, DamageOutcome outcome, bool victimCrouched)
        {
            return Hit(attackerId, victimId, rewindTicks, zone, damage, healthAfter, outcome)
                + " victimCrouched=" + (victimCrouched ? "true" : "false");
        }

        /// <summary>
        /// サーバーがばらつきを掛けた後の判定の向き。持ち主の <c>[Net] fire</c> のログの index と dir と突き合わせる。
        /// </summary>
        /// <example><c>[Net] shot attacker=1 index=5 cone=2.50 dir=(0.0123,-0.0456,0.9989)</c></example>
        /// <param name="attackerId">撃った人のプレイヤー id。</param>
        /// <param name="shotIndex">サーバーが付けた発射番号。</param>
        /// <param name="coneDegrees">掛けた散らばりの半角 (度)。</param>
        /// <param name="x">判定の向きの x。</param>
        /// <param name="y">判定の向きの y。</param>
        /// <param name="z">判定の向きの z。</param>
        public static string Shot(int attackerId, int shotIndex, float coneDegrees, float x, float y, float z)
        {
            return Tag + " shot attacker=" + attackerId.ToString(CultureInfo.InvariantCulture)
                + " index=" + shotIndex.ToString(CultureInfo.InvariantCulture)
                + " cone=" + coneDegrees.ToString("F2", CultureInfo.InvariantCulture)
                + " dir=" + Direction(x, y, z);
        }

        /// <summary>サーバーが発射番号を自分の値に直したとき。</summary>
        /// <example><c>[Net] shot index corrected attacker=1 client=7 server=5 generation=1</c></example>
        public static string ShotIndexCorrected(int attackerId, int clientIndex, int serverIndex, int generation)
        {
            return Tag + " shot index corrected attacker=" + attackerId.ToString(CultureInfo.InvariantCulture)
                + " client=" + clientIndex.ToString(CultureInfo.InvariantCulture)
                + " server=" + serverIndex.ToString(CultureInfo.InvariantCulture)
                + " generation=" + generation.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>ログの向きの書式。小数 4 桁で、クライアントとサーバーのログで同じ書式にする。</summary>
        /// <example><c>(0.0123,-0.0456,0.9989)</c></example>
        public static string Direction(float x, float y, float z)
        {
            return "(" + x.ToString("F4", CultureInfo.InvariantCulture)
                + "," + y.ToString("F4", CultureInfo.InvariantCulture)
                + "," + z.ToString("F4", CultureInfo.InvariantCulture) + ")";
        }

        /// <example><c>[Net] rejected shot attacker=0 reason=OriginTooFar</c></example>
        public static string Rejected(int attackerId, ShotRejection reason)
        {
            return Tag + " rejected shot attacker=" + attackerId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason;
        }
    }
}
