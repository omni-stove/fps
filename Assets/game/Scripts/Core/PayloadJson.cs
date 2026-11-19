using System.Collections.Generic;

namespace Shooter.Core
{
    public static class PayloadJson
    {
        public const string ResourcePath = "Tuning/payload";

        public const string CartSpeedItem = "cartSpeed";
        public const string MinPushersItem = "minPushers";
        public const string PusherSpeedMultipliersItem = "pusherSpeedMultipliers";
        public const string RollbackDelaySecondsItem = "rollbackDelaySeconds";
        public const string RollbackSpeedItem = "rollbackSpeed";
        public const string CheckpointBonusSecondsItem = "checkpointBonusSeconds";
        public const string TimeLimitSecondsItem = "timeLimitSeconds";

        private const string _kind = "payload JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            CartSpeedItem,
            MinPushersItem,
            PusherSpeedMultipliersItem,
            RollbackDelaySecondsItem,
            RollbackSpeedItem,
            CheckpointBonusSecondsItem,
            TimeLimitSecondsItem,
        };

        public static PayloadSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var cartSpeed = fields.ReadFloat(CartSpeedItem, PayloadSpec.DefaultCartSpeed);
                var minPushers = fields.ReadInt(MinPushersItem, PayloadSpec.DefaultMinPushers);
                var multipliers = ReadMultipliers(fields);
                var rollbackDelaySeconds = fields.ReadFloat(RollbackDelaySecondsItem, PayloadSpec.DefaultRollbackDelaySeconds);
                var rollbackSpeed = fields.ReadFloat(RollbackSpeedItem, PayloadSpec.DefaultRollbackSpeed);
                var checkpointBonusSeconds = fields.ReadFloat(CheckpointBonusSecondsItem, PayloadSpec.DefaultCheckpointBonusSeconds);
                var timeLimitSeconds = fields.ReadFloat(TimeLimitSecondsItem, PayloadSpec.DefaultTimeLimitSeconds);
                return SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new PayloadSpec(
                        cartSpeed,
                        minPushers,
                        multipliers,
                        rollbackDelaySeconds,
                        rollbackSpeed,
                        checkpointBonusSeconds,
                        timeLimitSeconds));
            });
        }

        // 書かなければ null を返し、PayloadSpec の既定の並びを使わせる。
        private static List<float> ReadMultipliers(ServerDataJson.Fields fields)
        {
            if (!fields.Has(PusherSpeedMultipliersItem))
            {
                return null;
            }

            var nodes = fields.Array(PusherSpeedMultipliersItem);
            var list = new List<float>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                list.Add(fields.ToFloat(fields.ElementPath(PusherSpeedMultipliersItem, i), nodes[i]));
            }

            return list;
        }
    }
}
