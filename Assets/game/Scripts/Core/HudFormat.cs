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

        /// <summary>予備弾が無限 (<see cref="GameRules.InfiniteReserve"/>) のときに予備の数の代わりに出す文字列。</summary>
        public const string InfiniteReserve = "∞";

        /// <summary>
        /// 弾数。<paramref name="infiniteReserve"/> なら予備の数の代わりに <see cref="InfiniteReserve"/> を出す
        /// (無限のときの予備の数は減らないので、数を出すと有限に見えてしまうため)。それ以外は <see cref="Ammo(int, int, bool)"/> と同じ。
        /// </summary>
        /// <example><c>Ammo(30, 90, false, true)</c> は <c>30 / ∞</c>、<c>Ammo(0, 90, true, true)</c> は <c>リロード中 / ∞</c></example>
        public static string Ammo(int magazine, int reserve, bool reloading, bool infiniteReserve)
        {
            var text = Ammo(magazine, reserve, reloading);
            if (!infiniteReserve)
            {
                return text;
            }

            return (reloading ? Reloading : magazine.ToString(CultureInfo.InvariantCulture)) + AmmoSeparator + InfiniteReserve;
        }

        /// <summary>予備を出さない HUD (<see cref="HudVisibility.ReserveAmmo"/> が false) で、武器がまだ無いときの表示。</summary>
        public const string NoMagazine = "--";

        /// <summary>
        /// 予備を出さない HUD (<see cref="HudVisibility.ReserveAmmo"/> が false) の弾数。弾倉の数だけを出し、リロード中は
        /// <see cref="Ammo(int, int, bool)"/> と同じく数の代わりに <see cref="Reloading"/> を出す。
        /// </summary>
        /// <example><c>Magazine(30, false)</c> は <c>30</c>、<c>Magazine(0, true)</c> は <c>リロード中</c></example>
        public static string Magazine(int magazine, bool reloading)
        {
            if (magazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazine), magazine, "magazine must not be negative");
            }

            return reloading ? Reloading : magazine.ToString(CultureInfo.InvariantCulture);
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

        /// <summary><see cref="RespawnMode.None"/> で倒れたとき、次の試合が始まるまで戻らないことの案内。</summary>
        public const string NoRespawnUntilNextMatch = "次の試合まで復活なし";

        /// <summary>
        /// 倒れている間の案内を、リスポーン方式に合わせて選ぶ。Individual は自分のリスポーンまで、Wave は次のウェーブまでの秒数を
        /// <see cref="Respawn"/> で、None は <see cref="NoRespawnUntilNextMatch"/> を出す (seconds は使わない)。
        /// </summary>
        /// <example><c>RespawnNotice(RespawnMode.Wave, 7)</c> は <c>リスポーンまで 7 秒</c></example>
        /// <exception cref="ArgumentOutOfRangeException">mode が定義済みの値でないか、None 以外で seconds が負。</exception>
        public static string RespawnNotice(RespawnMode mode, int seconds)
        {
            switch (mode)
            {
                case RespawnMode.Individual:
                case RespawnMode.Wave:
                    return Respawn(seconds);
                case RespawnMode.None:
                    return NoRespawnUntilNextMatch;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode is not a defined RespawnMode");
            }
        }

        /// <summary>案内が無いときの表示。空文字のときは HUD が案内の欄ごと隠す。</summary>
        public const string NoNotice = "";

        /// <summary>
        /// 蘇生の進み (0〜1) を、HUD に出す百分率の整数にする。端数は切り捨てる (100% になるのは起き上がったときだけ)。
        /// 負・NaN は 0、1 を超える値は 100。
        /// </summary>
        /// <example><c>RevivePercent(0.426f)</c> は <c>42</c></example>
        public static int RevivePercent(float progress)
        {
            if (!(progress > 0f))
            {
                return 0;
            }

            if (progress >= 1f)
            {
                return 100;
            }

            return Math.Min(99, (int)Math.Floor(progress * 100f));
        }

        /// <summary>ダウン中の人の案内。出血で倒れるまでの秒数は切り上げた整数で受ける。</summary>
        /// <example><c>Downed(12)</c> は <c>ダウン中 あと 12 秒</c></example>
        public static string Downed(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return "ダウン中 あと " + seconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }

        /// <summary>
        /// ダウン中の人の案内に、味方に起こされている進みを添える。revivePercent が 0 なら誰も起こしていないので <see cref="Downed(int)"/> と同じ。
        /// </summary>
        /// <example><c>Downed(12, 40)</c> は <c>ダウン中 あと 12 秒 (蘇生 40%)</c>、<c>Downed(12, 0)</c> は <c>ダウン中 あと 12 秒</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負か、revivePercent が 0〜100 の外。</exception>
        public static string Downed(int seconds, int revivePercent)
        {
            CheckPercent(revivePercent);
            var text = Downed(seconds);
            return revivePercent == 0 ? text : text + " (蘇生 " + revivePercent.ToString(CultureInfo.InvariantCulture) + "%)";
        }

        /// <summary>味方を起こしている人の案内。</summary>
        /// <example><c>Reviving(40)</c> は <c>蘇生中 40%</c></example>
        /// <exception cref="ArgumentOutOfRangeException">percent が 0〜100 の外。</exception>
        public static string Reviving(int percent)
        {
            CheckPercent(percent);
            return "蘇生中 " + percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>出現直後の無敵の最中の案内。無敵が切れるまでの秒数は切り上げた整数で受ける。</summary>
        /// <example><c>SpawnProtected(2)</c> は <c>保護中 あと 2 秒</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負。</exception>
        public static string SpawnProtected(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return "保護中 あと " + seconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }

        private static void CheckPercent(int percent)
        {
            if (percent < 0 || percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent), percent, "percent must be between 0 and 100");
            }
        }

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
