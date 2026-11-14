using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>乗り物の乗り降りのログの書式。どれも <see cref="Tag"/> で始まる。</summary>
    public static class VehicleLog
    {
        public const string Tag = "[Vehicle]";

        /// <summary>乗り込めなかった理由: 空いている座席が無い。</summary>
        public const string ReasonNoFreeSeat = "no_free_seat";

        /// <summary>乗り込めなかった理由: 座席の乗る位置まで遠い。</summary>
        public const string ReasonOutOfRange = "out_of_range";

        /// <summary>乗り込めなかった理由: 乗り物が壊れている。</summary>
        public const string ReasonWrecked = "wrecked";

        /// <summary>乗り込めなかった理由: もうどこかの乗り物に乗っている。</summary>
        public const string ReasonAlreadyRiding = "already_riding";

        /// <summary>乗り込めなかった理由: 乗る人が乗れる状態でない (倒れているなど)。</summary>
        public const string ReasonRiderUnavailable = "rider_unavailable";

        /// <summary>乗り込めなかった・移れなかった理由: 指定した座席が塞がっている。</summary>
        public const string ReasonSeatTaken = "seat_taken";

        /// <summary>乗り込めなかった・移れなかった理由: 指定した座席が破棄されている。</summary>
        public const string ReasonSeatRemoved = "seat_removed";

        /// <summary>降りられなかった理由: 降りる場所がすべて塞がっている。</summary>
        public const string ReasonExitBlocked = "exit_blocked";

        /// <example><c>[Vehicle] 'Jeep' board rider=Player seat=0 role=Driver</c></example>
        public static string Board(string vehicle, string rider, int seat, VehicleSeatRole role)
        {
            return $"{Tag} '{vehicle}' board rider={rider} seat={seat} role={role}";
        }

        /// <example><c>[Vehicle] 'Jeep' board refused rider=Player reason=no_free_seat</c></example>
        public static string BoardRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' board refused rider={rider} reason={reason}";
        }

        /// <example><c>[Vehicle] 'Jeep' switch rider=Player from=0 to=1 role=Gunner</c></example>
        public static string Switch(string vehicle, string rider, int from, int to, VehicleSeatRole role)
        {
            return $"{Tag} '{vehicle}' switch rider={rider} from={from} to={to} role={role}";
        }

        /// <example><c>[Vehicle] 'Jeep' switch refused rider=Player reason=no_free_seat</c></example>
        public static string SwitchRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' switch refused rider={rider} reason={reason}";
        }

        /// <summary>降りたときのログ。exitIndex は使った降りる場所の候補の番号で、塞がっていても降ろしたなら -1。</summary>
        /// <example><c>[Vehicle] 'Jeep' leave rider=Player seat=0 reason=Request exit=1</c></example>
        public static string Leave(string vehicle, string rider, int seat, VehicleLeaveReason reason, int exitIndex)
        {
            var exit = exitIndex >= 0 ? exitIndex.ToString(CultureInfo.InvariantCulture) : "-";
            return $"{Tag} '{vehicle}' leave rider={rider} seat={seat} reason={reason} exit={exit}";
        }

        /// <example><c>[Vehicle] 'Jeep' leave refused rider=Player reason=exit_blocked</c></example>
        public static string LeaveRefused(string vehicle, string rider, string reason)
        {
            return $"{Tag} '{vehicle}' leave refused rider={rider} reason={reason}";
        }

        /// <example><c>[Vehicle] 'Jeep' wrecked riders=2</c></example>
        public static string Wrecked(string vehicle, int riders)
        {
            if (riders < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(riders), riders, "riders must not be negative");
            }

            return $"{Tag} '{vehicle}' wrecked riders={riders}";
        }
    }
}
