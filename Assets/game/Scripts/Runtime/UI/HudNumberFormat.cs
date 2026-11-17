using System;
using System.Globalization;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 書式を言語や画面ごとに変えられるよう、<see cref="HudModel"/> は数で持ち、文字列にするのは UI のここで行う。
    /// </summary>
    public static class HudNumberFormat
    {
        public const string NoHealth = "--";

        public const string NoAmmo = "-- / --";

        public const string NoMagazine = "--";

        public const string NoTime = "--:--";

        public const string AmmoSeparator = " / ";

        /// <summary>無限のときの予備の数は減らないので、数を出すと有限に見えてしまうため、数の代わりにこれを出す。</summary>
        public const string InfiniteReserve = "∞";

        /// <summary>弾倉の数はリロードが終わるまで変わらず撃てるように見えてしまうため、リロード中は数の代わりにこれを出す。</summary>
        public static LocalizedText Reloading => new LocalizedText("hud.reloading");

        /// <example><c>Health(75)</c> は <c>75</c>、<c>Health(null)</c> は <c>--</c></example>
        /// <exception cref="ArgumentOutOfRangeException">current が負。</exception>
        public static string Health(int? current)
        {
            if (!current.HasValue)
            {
                return NoHealth;
            }

            if (current.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            return current.Value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 弾数。magazine が null なら仮の表示。showsReserve が false なら弾倉の数だけを出す。リロード中は弾倉の数の代わりに <see cref="Reloading"/>、
        /// infiniteReserve なら予備の数の代わりに <see cref="InfiniteReserve"/> を出す。
        /// </summary>
        /// <example>
        /// <c>Ammo(30, 90, false, false, true)</c> は <c>30 / 90</c>、<c>Ammo(0, 90, true, false, true)</c> は <c>リロード中 / 90</c>、
        /// <c>Ammo(30, 90, false, true, true)</c> は <c>30 / ∞</c>、<c>Ammo(30, 90, false, false, false)</c> は <c>30</c>
        /// </example>
        /// <exception cref="ArgumentOutOfRangeException">magazine か reserve が負。</exception>
        public static string Ammo(int? magazine, int? reserve, bool reloading, bool infiniteReserve, bool showsReserve)
        {
            if (!magazine.HasValue)
            {
                return showsReserve ? NoAmmo : NoMagazine;
            }

            if (magazine.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazine), magazine, "magazine must not be negative");
            }

            var reserveCount = reserve ?? 0;
            if (reserveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserve), reserve, "reserve must not be negative");
            }

            var magazineText = reloading ? GameText.Resolve(Reloading) : magazine.Value.ToString(CultureInfo.InvariantCulture);
            if (!showsReserve)
            {
                return magazineText;
            }

            var reserveText = infiniteReserve ? InfiniteReserve : reserveCount.ToString(CultureInfo.InvariantCulture);
            return magazineText + AmmoSeparator + reserveText;
        }

        /// <example><c>Time(65)</c> は <c>01:05</c>、<c>Time(0)</c> は <c>00:00</c>、<c>Time(null)</c> は <c>--:--</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負。</exception>
        public static string Time(int? seconds)
        {
            if (!seconds.HasValue)
            {
                return NoTime;
            }

            if (seconds.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            var minutes = seconds.Value / 60;
            var rest = seconds.Value % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + rest.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>残り時間を <see cref="HudFormat.DisplayedSeconds"/> の秒数にして <see cref="Time(int?)"/> の書式で出す。</summary>
        /// <example><c>RemainingTime(65f)</c> は <c>01:05</c>、<c>RemainingTime(0.2f)</c> は <c>00:01</c>、<c>RemainingTime(float.PositiveInfinity)</c> は <c>99:59</c></example>
        public static string RemainingTime(float remainingSeconds)
        {
            return Time(HudFormat.DisplayedSeconds(remainingSeconds));
        }

        public static string Health(HudModel model)
        {
            return Health(model.CurrentHealth);
        }

        public static string Ammo(HudModel model)
        {
            return Ammo(model.MagazineAmmo, model.ReserveAmmo, model.IsReloading, model.IsReserveInfinite, model.ShowsReserveAmmo);
        }

        public static string Time(HudModel model)
        {
            return Time(model.RemainingSeconds);
        }
    }
}
