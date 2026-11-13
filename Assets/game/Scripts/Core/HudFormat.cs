using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// HUD に出す文字列の書式。体力は数値だけ、弾数は「弾倉 / 予備」、残り時間は mm:ss。
    /// 値がまだ無いときは <see cref="NoHealth"/> などの仮の文字列を出す。
    /// </summary>
    public static class HudFormat
    {
        /// <summary>体力の源がまだ無いときの表示。</summary>
        public const string NoHealth = "--";

        /// <summary>武器がまだ無いときの表示。</summary>
        public const string NoAmmo = "-- / --";

        /// <summary>武器がまだ無いときの武器名の表示。空文字のときは HUD が武器名の欄を隠す。</summary>
        public const string NoWeaponName = "";

        /// <summary>試合がまだ無いときの表示。</summary>
        public const string NoTime = "--:--";

        /// <summary>弾倉と予備の間に置く区切り。</summary>
        public const string AmmoSeparator = " / ";

        /// <summary>mm:ss で表せる最大の秒数 (99:59)。これより長い残り時間は 99:59 に切り詰める。</summary>
        public const int MaxDisplayedSeconds = 99 * 60 + 59;

        /// <example><c>Health(75)</c> は <c>75</c></example>
        public static string Health(int current)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            return current.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>Ammo(30, 90)</c> は <c>30 / 90</c></example>
        public static string Ammo(int magazine, int reserve)
        {
            if (magazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazine), magazine, "magazine must not be negative");
            }

            if (reserve < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserve), reserve, "reserve must not be negative");
            }

            return magazine.ToString(CultureInfo.InvariantCulture) + AmmoSeparator + reserve.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 残り時間として表示する整数の秒数。端数は切り上げる (残り 0.2 秒なら 1 秒) ので、
        /// 00:00 になるのは時間切れのときだけ。負・NaN は 0、<see cref="MaxDisplayedSeconds"/> を超える値 (無限大を含む) はそこで止める。
        /// </summary>
        public static int DisplayedSeconds(float remainingSeconds)
        {
            if (!(remainingSeconds > 0f))
            {
                return 0;
            }

            if (remainingSeconds >= MaxDisplayedSeconds)
            {
                return MaxDisplayedSeconds;
            }

            return (int)Math.Ceiling(remainingSeconds);
        }

        /// <example><c>Time(65f)</c> は <c>01:05</c>、<c>Time(0.2f)</c> は <c>00:01</c>、<c>Time(0f)</c> は <c>00:00</c></example>
        public static string Time(float remainingSeconds)
        {
            var total = DisplayedSeconds(remainingSeconds);
            var minutes = total / 60;
            var seconds = total % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>試合が終わった後、次の試合が始まるまでの案内。秒数は整数 (同期されている切り上げの値) で受ける。</summary>
        /// <example><c>NextMatch(10)</c> は <c>次の試合まで 10 秒</c></example>
        public static string NextMatch(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return "次の試合まで " + seconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }

        /// <summary>リロード中に弾倉の数の代わりに出す文字列。</summary>
        public const string Reloading = "リロード中";

        /// <summary>
        /// 弾数。リロード中は弾倉の数の代わりに <see cref="Reloading"/> を出し、予備はそのまま出す
        /// (弾倉の数はリロードが終わるまで変わらず、撃てるように見えてしまうため)。
        /// </summary>
        /// <example><c>Ammo(0, 90, true)</c> は <c>リロード中 / 90</c>、<c>Ammo(30, 90, false)</c> は <c>30 / 90</c></example>
        public static string Ammo(int magazine, int reserve, bool reloading)
        {
            var text = Ammo(magazine, reserve);
            if (!reloading)
            {
                return text;
            }

            return Reloading + AmmoSeparator + reserve.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 体力が少ないと見なす割合。最大体力のこの割合以下 (0 は除く) で HUD が体力を警告の色にする。
        /// </summary>
        public const float LowHealthRatio = 0.25f;

        /// <summary>体力が警告の色で出すほど少ないか。倒れている (0) ときと最大体力を超えるときは false。</summary>
        /// <exception cref="ArgumentOutOfRangeException">current が負か、max が 1 未満。</exception>
        public static bool IsLowHealth(int current, int max)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be at least 1");
            }

            return current > 0 && current <= max * LowHealthRatio;
        }

        /// <summary>チームの点数がまだ無い (チーム戦でない) ときの表示。空文字のときは HUD が点数の欄ごと隠す。</summary>
        public const string NoScore = "";

        /// <summary>HUD に出すチーム名。</summary>
        /// <example><c>TeamName(Team.Alpha)</c> は <c>ALPHA</c></example>
        public static string TeamName(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return "ALPHA";
                case Team.Bravo:
                    return "BRAVO";
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        /// <summary>チーム名と点数。</summary>
        /// <example><c>TeamScore(Team.Bravo, 3)</c> は <c>BRAVO 3</c></example>
        public static string TeamScore(Team team, int score)
        {
            if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), score, "score must not be negative");
            }

            return TeamName(team) + " " + score.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>自分のチームの相手。点数を自分のチーム・相手の順に並べるのに使う。</summary>
        public static Team Opponent(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return Team.Bravo;
                case Team.Bravo:
                    return Team.Alpha;
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        /// <summary>試合結果がまだ無い (試合中か開始前) ときの表示。空文字のときは HUD が結果の欄ごと隠す。</summary>
        public const string NoResult = "";

        /// <summary>引き分けで終わったときの結果。</summary>
        public const string Draw = "引き分け";

        /// <summary>試合が終わったときの結果。勝ったチームか、勝者がいなければ引き分け。</summary>
        /// <example><c>Result(Team.Alpha)</c> は <c>ALPHA の勝ち</c>、<c>Result(null)</c> は <c>引き分け</c></example>
        public static string Result(Team? winner)
        {
            return winner.HasValue ? TeamName(winner.Value) + " の勝ち" : Draw;
        }

        /// <summary>倒れてからリスポーンするまでの案内。秒数は切り上げた整数で受ける。</summary>
        /// <example><c>Respawn(3)</c> は <c>リスポーンまで 3 秒</c></example>
        public static string Respawn(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return "リスポーンまで " + seconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }

        /// <summary>案内が無いときの表示。空文字のときは HUD が案内の欄ごと隠す。</summary>
        public const string NoNotice = "";

        /// <summary>キャンペーンのチェックポイントでセーブしたときの案内。</summary>
        public const string CheckpointSaved = "チェックポイントでセーブしました";

        /// <summary>キャンペーンでプレイヤーが倒れた後、チェックポイントから再開するまでの案内。秒数は切り上げた整数で受ける。</summary>
        /// <example><c>CheckpointRestart(3)</c> は <c>チェックポイントから再開まで 3 秒</c></example>
        public static string CheckpointRestart(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return "チェックポイントから再開まで " + seconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }
    }
}
