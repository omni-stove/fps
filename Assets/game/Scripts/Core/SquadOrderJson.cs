using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class SquadOrderJson
    {
        public const string HoldSecondsItem = "holdSeconds";
        public const string MaxDistanceItem = "maxDistance";
        public const string EnemyAimToleranceDegreesItem = "enemyAimToleranceDegrees";
        public const string NearbyRadiusItem = "nearbyRadius";
        public const string FollowDistanceItem = "followDistance";
        public const string LeashDistanceItem = "leashDistance";

        private const string _kind = "squad JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            HoldSecondsItem,
            MaxDistanceItem,
            EnemyAimToleranceDegreesItem,
            NearbyRadiusItem,
            FollowDistanceItem,
            LeashDistanceItem,
        };

        public static SquadOrderSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var defaults = SquadOrderSpec.Default;
                try
                {
                    return new SquadOrderSpec(
                        fields.ReadFloat(HoldSecondsItem, defaults.HoldSeconds),
                        fields.ReadFloat(MaxDistanceItem, defaults.MaxDistance),
                        fields.ReadFloat(EnemyAimToleranceDegreesItem, defaults.EnemyAimToleranceDegrees),
                        fields.ReadFloat(NearbyRadiusItem, defaults.NearbyRadius),
                        fields.ReadFloat(FollowDistanceItem, defaults.FollowDistance),
                        fields.ReadFloat(LeashDistanceItem, defaults.LeashDistance));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(e.ParamName) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        // SquadOrderSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static bool Known(string name)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
