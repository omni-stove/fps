using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class AircraftHudText
    {
        public const string SpeedKey = "aircraft.speed";

        public const string AltitudeKey = "aircraft.altitude";

        public const string ThrottleKey = "aircraft.throttle";

        public const string AirBrakeKey = "aircraft.airBrake";

        public const string AngleOfAttackKey = "aircraft.angleOfAttack";

        public const string LoadFactorKey = "aircraft.loadFactor";

        public const string StallKey = "aircraft.stall";

        public const float KilometersPerHourPerMeterPerSecond = 3.6f;

        public static LocalizedText Speed(AircraftStatus status)
        {
            return new LocalizedText(SpeedKey, Round(status.Speed * KilometersPerHourPerMeterPerSecond));
        }

        public static LocalizedText Altitude(AircraftStatus status)
        {
            return new LocalizedText(AltitudeKey, Round(status.Altitude));
        }

        // 負のスロットルを別の文にするのは、エアブレーキを開いているのに「スロットル -50%」と出ると、何が起きているかが読み取れないため。
        public static LocalizedText Throttle(AircraftStatus status)
        {
            return status.Throttle < 0f
                ? new LocalizedText(AirBrakeKey, Round(-status.Throttle * 100f))
                : new LocalizedText(ThrottleKey, Round(status.Throttle * 100f));
        }

        public static LocalizedText AngleOfAttack(AircraftStatus status)
        {
            return new LocalizedText(AngleOfAttackKey, Round(status.AngleOfAttackDegrees));
        }

        // 表の差し込みは書式を持てないので、小数 1 桁の文字にしてから渡す。
        public static LocalizedText LoadFactor(AircraftStatus status)
        {
            return new LocalizedText(LoadFactorKey, status.LoadFactor.ToString("0.0", CultureInfo.InvariantCulture));
        }

        public static bool ShowsWarning(AircraftStatus status)
        {
            return status.StallWarning;
        }

        public static LocalizedText Warning(AircraftStatus status)
        {
            return status.StallWarning ? new LocalizedText(StallKey) : default;
        }

        public static List<LocalizedText> Readings(AircraftStatus status)
        {
            return new List<LocalizedText>
            {
                Speed(status),
                Altitude(status),
                Throttle(status),
                AngleOfAttack(status),
                LoadFactor(status),
            };
        }

        private static int Round(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
