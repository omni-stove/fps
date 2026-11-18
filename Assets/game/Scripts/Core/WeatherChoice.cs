using System;

namespace Shooter.Core
{
    public readonly struct WeatherChoice : IEquatable<WeatherChoice>
    {
        public const float HorizonHours = 6f;

        public const float DegreesPerHour = 360f / WeatherPreset.HoursPerDay;

        public WeatherChoice(WeatherPreset weather, WeatherPreset timeOfDay, bool weatherFromRules, bool timeOfDayFromRules)
        {
            Weather = weather ?? throw new ArgumentNullException(nameof(weather));
            TimeOfDay = timeOfDay ?? throw new ArgumentNullException(nameof(timeOfDay));
            WeatherFromRules = weatherFromRules;
            TimeOfDayFromRules = timeOfDayFromRules;
        }

        public WeatherPreset Weather { get; }

        public WeatherPreset TimeOfDay { get; }

        public bool WeatherFromRules { get; }

        public bool TimeOfDayFromRules { get; }

        public float TimeOfDayHours => TimeOfDay.TimeOfDayHours;

        public float SunIntensity => TimeOfDay.SunIntensity;

        public float FogDensity => Weather.FogDensity;

        public float RainIntensity => Weather.RainIntensity;

        public float WindStrength => Weather.WindStrength;

        public bool IsNight => !IsSunUp(TimeOfDayHours);

        public float LightPitchDegrees => LightPitch(TimeOfDayHours);

        // ルールの Id が一覧に無ければ試合は止めずにマップの既定へ、それも無ければ一覧の既定へ下がる。
        // 時刻を書かなければ天候の組の時刻を使うので、天候の Id だけでも 1 つの空になる。
        public static WeatherChoice Choose(WeatherSpec spec, string weatherId, string timeOfDayId, string mapWeatherId = null, string mapTimeOfDayId = null)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var ruleWeather = spec.Find(Trimmed(weatherId));
            var weather = ruleWeather ?? spec.Find(Trimmed(mapWeatherId)) ?? spec.DefaultPreset;
            var ruleTime = spec.Find(Trimmed(timeOfDayId));
            var time = ruleTime ?? spec.Find(Trimmed(mapTimeOfDayId)) ?? weather;
            return new WeatherChoice(weather, time, ruleWeather != null, ruleTime != null);
        }

        public static bool operator ==(WeatherChoice left, WeatherChoice right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeatherChoice left, WeatherChoice right)
        {
            return !left.Equals(right);
        }

        public static bool IsSunUp(float hours)
        {
            var pitch = SunPitch(hours);
            return pitch > 0f && pitch < 180f;
        }

        public static float SunPitch(float hours)
        {
            var pitch = (hours - HorizonHours) * DegreesPerHour;
            pitch %= 360f;
            return pitch < 0f ? pitch + 360f : pitch;
        }

        // 地平の下から照らすと地面の裏しか明るくならないので、夜は太陽と反対側にある月の向きで照らす。
        public static float LightPitch(float hours)
        {
            var pitch = SunPitch(hours);
            return pitch <= 180f ? pitch : pitch - 180f;
        }

        public static bool IsUnset(string id)
        {
            return string.IsNullOrWhiteSpace(id);
        }

        public bool Equals(WeatherChoice other)
        {
            return ReferenceEquals(Weather, other.Weather)
                && ReferenceEquals(TimeOfDay, other.TimeOfDay)
                && WeatherFromRules == other.WeatherFromRules
                && TimeOfDayFromRules == other.TimeOfDayFromRules;
        }

        public override bool Equals(object obj)
        {
            return obj is WeatherChoice other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Weather == null ? 0 : Weather.GetHashCode();
                hash = (hash * 397) ^ (TimeOfDay == null ? 0 : TimeOfDay.GetHashCode());
                return (hash * 397) ^ ((WeatherFromRules ? 1 : 0) | (TimeOfDayFromRules ? 2 : 0));
            }
        }

        private static string Trimmed(string id)
        {
            return IsUnset(id) ? null : id.Trim();
        }
    }
}
