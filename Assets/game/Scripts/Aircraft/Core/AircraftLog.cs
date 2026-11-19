using System.Globalization;

namespace Shooter.Core
{
    // タグを乗り物と揃えるのは、飛行機の行も乗り物のログとして同じ絞り込みで読めるようにするため。
    public static class AircraftLog
    {
        public const string Tag = VehicleLog.Tag;

        public static string PartBroken(string vehicle, string part, AircraftDamageEffect effect, AircraftCondition condition)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' part broken part={2} effect={3} lift={4:0.00}/{5:0.00} control={6:0.00} thrust={7:0.00}",
                Tag,
                vehicle,
                part,
                effect,
                condition.LeftLift,
                condition.RightLift,
                condition.Control,
                condition.Thrust);
        }

        public static string NetCondition(string vehicle, AircraftCondition condition)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} '{1}' net condition lift={2:0.00}/{3:0.00} control={4:0.00} thrust={5:0.00}",
                Tag,
                vehicle,
                condition.LeftLift,
                condition.RightLift,
                condition.Control,
                condition.Thrust);
        }

        public static string InvalidAircraftDamage(string vehicle, string error)
        {
            return $"{Tag} '{vehicle}' aircraft damage settings are invalid; parts take no flight damage: {error}";
        }

        public static string EffectsMissingMotor(string vehicle)
        {
            return $"{Tag} '{vehicle}' has no SimpleAircraftMotor; the aircraft effects show nothing";
        }
    }
}
