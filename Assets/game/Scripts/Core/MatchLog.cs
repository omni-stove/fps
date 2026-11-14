using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// チームデスマッチの <c>[Match]</c> 付きログの文言。サーバーとクライアントで同じ書式にし、
    /// 2 つのログを突き合わせてスコアが合うか確かめられるようにする。
    /// ソロの試合 (<see cref="Match"/>) の状態ログ (<see cref="SoloState"/>) も同じタグで出す。
    /// </summary>
    public static class MatchLog
    {
        public const string Tag = "[Match]";

        /// <summary>引き分けで終わったときに winner= に書く値。</summary>
        public const string Draw = "draw";

        /// <example><c>[Match] score A=3 B=1</c></example>
        public static string Score(int alphaScore, int bravoScore)
        {
            return Tag + " score A=" + alphaScore.ToString(CultureInfo.InvariantCulture)
                + " B=" + bravoScore.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] ended winner=Alpha</c>、引き分けなら <c>[Match] ended winner=draw</c></example>
        public static string Ended(Team? winner)
        {
            return Tag + " ended winner=" + (winner.HasValue ? TeamName(winner.Value) : Draw);
        }

        /// <example><c>[Match] started players=2</c></example>
        public static string Started(int playerCount)
        {
            return Tag + " started players=" + playerCount.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] joined player=1 team=Bravo</c></example>
        public static string Joined(int playerId, Team team)
        {
            return Tag + " joined player=" + playerId.ToString(CultureInfo.InvariantCulture) + " team=" + TeamName(team);
        }

        /// <example><c>[Match] left player=1</c></example>
        public static string Left(int playerId)
        {
            return Tag + " left player=" + playerId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>切断した人の成績を残し始めたこと。</summary>
        /// <example><c>[Match] holding player=1 for=60s</c></example>
        public static string Held(int playerId, float seconds)
        {
            return Tag + " holding player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " for=" + seconds.ToString("0.##", CultureInfo.InvariantCulture) + "s";
        }

        /// <summary>切断した人が戻らないまま、残していた成績を捨てたこと。</summary>
        /// <example><c>[Match] hold expired player=1</c></example>
        public static string HoldExpired(int playerId)
        {
            return Tag + " hold expired player=" + playerId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>切断した人が戻ってきて、前の playerId の成績を引き継いだこと。</summary>
        /// <example><c>[Match] rejoined player=5 was=1 team=Alpha kills=2 deaths=1</c></example>
        public static string Rejoined(int playerId, HeldPlayer held)
        {
            return Tag + " rejoined player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " was=" + held.PlayerId.ToString(CultureInfo.InvariantCulture)
                + " team=" + TeamName(held.Team)
                + " kills=" + held.Kills.ToString(CultureInfo.InvariantCulture)
                + " deaths=" + held.Deaths.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>クライアントが試合に途中から入ったときに受け取った、点数・残り時間・状態。</summary>
        /// <example><c>[Match] synced A=3 B=1 remaining=120s state=Playing</c></example>
        public static string Synced(int alphaScore, int bravoScore, int remainingSeconds, GameModeState state)
        {
            return Tag + " synced A=" + alphaScore.ToString(CultureInfo.InvariantCulture)
                + " B=" + bravoScore.ToString(CultureInfo.InvariantCulture)
                + " remaining=" + remainingSeconds.ToString(CultureInfo.InvariantCulture) + "s"
                + " state=" + state;
        }

        /// <example><c>[Match] kill killer=0 victim=1</c></example>
        public static string Kill(int killerId, int victimId)
        {
            return Tag + " kill killer=" + killerId.ToString(CultureInfo.InvariantCulture)
                + " victim=" + victimId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 味方に倒された (<see cref="FriendlyFire.On"/>) ことを、点数の入る <see cref="Kill"/> と区別して書く。
        /// </summary>
        /// <example><c>[Match] teamkill killer=0 victim=2</c></example>
        public static string TeamKill(int killerId, int victimId)
        {
            return Tag + " teamkill killer=" + killerId.ToString(CultureInfo.InvariantCulture)
                + " victim=" + victimId.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] spawn player=1 at=(0.00, 0.00, 20.00)</c></example>
        public static string Spawn(int playerId, SpawnPoint point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            var p = point.Position;
            return Tag + " spawn player=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " at=(" + p.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + p.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + p.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>
        /// ソロの試合 (<see cref="Match"/>) の状態。終了後は state に勝敗 (Won / Lost) を、それまでは進行状態を書く。
        /// remaining は残り時間 (秒、小数 1 桁)。
        /// </summary>
        /// <example><c>[Match] state=Won kills=10 remaining=12.3</c></example>
        public static string SoloState(Match match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            return Tag + " state=" + SoloStateName(match)
                + " kills=" + match.Kills.ToString(CultureInfo.InvariantCulture)
                + " remaining=" + match.RemainingSeconds.ToString("F1", CultureInfo.InvariantCulture);
        }

        /// <example><c>[Match] restart</c></example>
        public static string Restart()
        {
            return Tag + " restart";
        }

        /// <summary>試合が終わった後、次の試合が始まるまでの秒数 (小数 1 桁)。</summary>
        /// <example><c>[Match] next match in=10.0s</c></example>
        public static string NextMatch(float delaySeconds)
        {
            if (!(delaySeconds >= 0f) || float.IsInfinity(delaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "delaySeconds must not be negative and must be finite");
            }

            return Tag + " next match in=" + delaySeconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        }

        /// <summary>試合で全員に配るロードアウト。name はアセット名 (空なら <c>-</c>)、switch は持ち替え秒数 (小数 2 桁)。</summary>
        /// <example><c>[Match] loadout name=DefaultLoadout slots=2 switch=0.40s</c></example>
        public static string Loadout(string name, ServerLoadout loadout)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            return Tag + " loadout name=" + (string.IsNullOrWhiteSpace(name) ? "-" : name)
                + " slots=" + loadout.Weapons.Count.ToString(CultureInfo.InvariantCulture)
                + " switch=" + Seconds(loadout.SwitchSeconds);
        }

        /// <summary>プレイヤーが自分のロードアウトに選べる武器の一覧。name はアセット名 (空なら <c>-</c>)。</summary>
        /// <example><c>[Match] loadout catalog name=WeaponCatalog weapons=2</c></example>
        public static string LoadoutCatalog(string name, int weaponCount)
        {
            return Tag + " loadout catalog name=" + (string.IsNullOrWhiteSpace(name) ? "-" : name)
                + " weapons=" + weaponCount.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 試合で使うルールの全項目。name はアセット名で、アセットが無く <see cref="GameRules.Default"/> を使うときは <c>default</c> を渡す
        /// (空なら <c>-</c>)。秒と倍率は小数 2 桁、真偽値は小文字。
        /// </summary>
        /// <example>
        /// <c>[Match] rules name=TeamDeathmatch friendlyFire=Off respawnMode=Individual waveInterval=10.00s regenDelay=4.00s
        /// regenPerSecond=20.00 deathBody=Collapse deathCamera=Killer infiniteReserve=false reserveMultiplier=1.00 destruction=Off</c> (実際は 1 行)
        /// </example>
        public static string Rules(string name, GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return Tag + " rules name=" + (string.IsNullOrWhiteSpace(name) ? "-" : name)
                + " friendlyFire=" + rules.FriendlyFire
                + " respawnMode=" + rules.RespawnMode
                + " waveInterval=" + Seconds(rules.WaveIntervalSeconds)
                + " regenDelay=" + Seconds(rules.RegenDelaySeconds)
                + " regenPerSecond=" + rules.RegenPerSecond.ToString("F2", CultureInfo.InvariantCulture)
                + " deathBody=" + rules.DeathBody
                + " deathCamera=" + rules.DeathCamera
                + " infiniteReserve=" + (rules.InfiniteReserve ? "true" : "false")
                + " reserveMultiplier=" + rules.ReserveAmmoMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " destruction=" + rules.Destruction;
        }

        /// <summary>
        /// ロードアウトの slot 番の武器と、サーバーが射撃の判定に使う値。falloff は減衰しないなら <c>none</c>、
        /// するなら <c>始まり-終わりm*最小倍率</c> (key=value を空白で区切って読めるよう、値に空白を入れない)。
        /// </summary>
        /// <example>
        /// <c>[Match] weapon slot=0 name=weapon.rifle id=6c1f3b2a-... category=AssaultRifle mode=Auto damage=25 range=100.0m
        /// falloff=20.0-60.0m*0.50 fireInterval=0.10s magazine=30 reserve=90 reload=2.00s</c> (実際は 1 行)
        /// </example>
        public static string Weapon(int slot, WeaponSpec weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            return Tag + " weapon slot=" + slot.ToString(CultureInfo.InvariantCulture)
                + " name=" + weapon.LogName
                + " id=" + weapon.Id
                + " category=" + weapon.Category
                + " mode=" + weapon.FireMode
                + " damage=" + weapon.Damage.ToString(CultureInfo.InvariantCulture)
                + " range=" + weapon.Range.ToString("F1", CultureInfo.InvariantCulture) + "m"
                + " falloff=" + Falloff(weapon.Falloff)
                + " fireInterval=" + Seconds(weapon.FireInterval)
                + " magazine=" + weapon.MagazineSize.ToString(CultureInfo.InvariantCulture)
                + " reserve=" + weapon.ReserveAmmo.ToString(CultureInfo.InvariantCulture)
                + " reload=" + Seconds(weapon.ReloadSeconds);
        }

        private static string Falloff(DamageFalloff falloff)
        {
            // DamageFalloff.None は float.MaxValue の距離を持つので、そのまま書くと読めない
            if (falloff.MinMultiplier >= 1f)
            {
                return "none";
            }

            return falloff.StartDistance.ToString("F1", CultureInfo.InvariantCulture)
                + "-" + falloff.EndDistance.ToString("F1", CultureInfo.InvariantCulture)
                + "m*" + falloff.MinMultiplier.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("F2", CultureInfo.InvariantCulture) + "s";
        }

        private static string SoloStateName(Match match)
        {
            switch (match.State)
            {
                case GameModeState.Waiting:
                    return "Waiting";
                case GameModeState.Playing:
                    return "Playing";
                case GameModeState.Ended:
                    return match.Result == MatchResult.Won ? "Won" : "Lost";
                default:
                    throw new ArgumentOutOfRangeException(nameof(match), match.State, "match has an unknown state");
            }
        }

        internal static string TeamName(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return "Alpha";
                case Team.Bravo:
                    return "Bravo";
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }
    }
}
