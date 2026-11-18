using System.Globalization;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、smoke とテストがログの行を同じ関数で作って突き合わせるため。
    public static class WeatherLog
    {
        public const string LogTag = "[Weather]";

        public const string RulesSource = "rules";

        public const string MapSource = "map";

        public static string Applied(string origin, WeatherChoice choice, string weatherId, string timeOfDayId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} applied from={1} weather={2} timeOfDay={3} hours={4} fog={5} rain={6} wind={7} sun={8} requested={9}/{10}",
                LogTag,
                origin,
                choice.Weather.Id,
                choice.TimeOfDay.Id,
                choice.TimeOfDayHours,
                choice.FogDensity,
                choice.RainIntensity,
                choice.WindStrength,
                choice.SunIntensity,
                Requested(weatherId),
                Requested(timeOfDayId));
        }

        // 一覧に無い Id は既定へ下がって試合は続くので、書き間違いに気づけるよう 1 度だけ知らせる。
        public static string UnknownId(string item, string id, string fallback)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} '{2}' is not in the weather presets; using '{3}' instead",
                LogTag, item, id, fallback);
        }

        public static string NoController(string weatherId, string timeOfDayId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} the rules chose {1}/{2} but the scene has no WeatherController; the sky stays as the scene made it",
                LogTag, Requested(weatherId), Requested(timeOfDayId));
        }

        private static string Requested(string id)
        {
            return WeatherChoice.IsUnset(id) ? "-" : id.Trim();
        }
    }
}
