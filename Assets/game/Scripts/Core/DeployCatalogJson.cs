using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class DeployCatalogJson
    {
        public const string TypesItem = "types";

        public const string IdItem = "id";
        public const string RoleItem = "role";
        public const string MaxActiveItem = "maxActive";
        public const string OverflowItem = "overflow";
        public const string MaxHealthItem = "maxHealth";
        public const string MaxRangeItem = "maxRange";
        public const string MaxSlopeDegreesItem = "maxSlopeDegrees";
        public const string MaxStepHeightItem = "maxStepHeight";
        public const string WidthItem = "width";
        public const string HeightItem = "height";
        public const string DepthItem = "depth";
        public const string MaxChargesItem = "maxCharges";
        public const string RechargeSecondsItem = "rechargeSeconds";
        public const string CooldownSecondsItem = "cooldownSeconds";
        public const string TurretItem = "turret";
        public const string DecoyItem = "decoy";
        public const string BeaconItem = "beacon";
        public const string DroneItem = "drone";

        public const string WeaponIdItem = "weaponId";
        public const string RoundsItem = "rounds";
        public const string LifetimeSecondsItem = "lifetimeSeconds";
        public const string SightRangeItem = "sightRange";
        public const string TurnDegreesPerSecondItem = "turnDegreesPerSecond";
        public const string AimToleranceDegreesItem = "aimToleranceDegrees";

        public const string CallIntervalSecondsItem = "callIntervalSeconds";
        public const string CallRadiusItem = "callRadius";

        public const string UsesItem = "uses";
        public const string SpawnDistanceItem = "spawnDistance";

        public const string SpeedItem = "speed";
        public const string ControlRangeItem = "controlRange";
        public const string BatterySecondsItem = "batterySeconds";
        public const string SpotRadiusItem = "spotRadius";
        public const string SpotIntervalSecondsItem = "spotIntervalSeconds";
        public const string RevealSecondsItem = "revealSeconds";

        private const string _kind = "deployable JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { TypesItem };

        // 項目名はコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName から示せるようにする。
        public static IReadOnlyList<string> TypeItemNames { get; } = new[]
        {
            IdItem,
            RoleItem,
            MaxActiveItem,
            OverflowItem,
            MaxHealthItem,
            MaxRangeItem,
            MaxSlopeDegreesItem,
            MaxStepHeightItem,
            WidthItem,
            HeightItem,
            DepthItem,
            MaxChargesItem,
            RechargeSecondsItem,
            CooldownSecondsItem,
            TurretItem,
            DecoyItem,
            BeaconItem,
            DroneItem,
        };

        public static IReadOnlyList<string> TurretItemNames { get; } = new[]
        {
            WeaponIdItem,
            RoundsItem,
            LifetimeSecondsItem,
            SightRangeItem,
            TurnDegreesPerSecondItem,
            AimToleranceDegreesItem,
        };

        public static IReadOnlyList<string> DecoyItemNames { get; } = new[] { LifetimeSecondsItem, CallIntervalSecondsItem, CallRadiusItem };

        public static IReadOnlyList<string> BeaconItemNames { get; } = new[] { UsesItem, SpawnDistanceItem };

        public static IReadOnlyList<string> DroneItemNames { get; } = new[]
        {
            SpeedItem,
            ControlRangeItem,
            BatterySecondsItem,
            SpotRadiusItem,
            SpotIntervalSecondsItem,
            RevealSecondsItem,
        };

        public static DeployCatalog Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var nodes = fields.Array(TypesItem);
                var types = new List<DeployType>(nodes.Count);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < nodes.Count; i++)
                {
                    var entry = fields.Element(nodes[i], TypesItem, i, TypeItemNames);
                    var type = ReadType(entry);
                    if (!ids.Add(type.Id))
                    {
                        throw entry.Rejected(entry.ItemPath(IdItem), $"id '{type.Id}' is listed twice");
                    }

                    types.Add(type);
                }

                return new DeployCatalog(types);
            }
            catch (ServerDataJsonException e)
            {
                // サーバーは TuningJsonException だけを「調整値が読めない」として起動を止める。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static DeployType ReadType(ServerDataJson.Fields entry)
        {
            var id = entry.ToText(entry.ItemPath(IdItem), entry.Require(IdItem));
            var role = entry.ToEnum<DeployRole>(entry.ItemPath(RoleItem), entry.Require(RoleItem));
            var deploy = ReadDeploy(entry, DeployType.DefaultDeploy(role));
            var ability = ReadAbility(entry);

            var turretFields = entry.Object(TurretItem, TurretItemNames);
            var decoyFields = entry.Object(DecoyItem, DecoyItemNames);
            var beaconFields = entry.Object(BeaconItem, BeaconItemNames);
            var droneFields = entry.Object(DroneItem, DroneItemNames);
            if (role == DeployRole.Turret && turretFields == null)
            {
                throw entry.Rejected(entry.ItemPath(TurretItem), "a Turret needs a turret object with weaponId");
            }

            TurretSpec turret = null;
            string weaponId = null;
            if (turretFields != null)
            {
                weaponId = turretFields.ToText(turretFields.ItemPath(WeaponIdItem), turretFields.Require(WeaponIdItem));
                turret = ReadTurret(turretFields);
            }

            DecoySpec decoy = null;
            if (decoyFields != null)
            {
                decoy = ReadDecoy(decoyFields);
            }
            else if (role == DeployRole.Decoy)
            {
                // タレットの撃つ武器と違い、囮の値はどれも既定があるので、decoy を書かなければ既定の囮にする。
                decoy = DecoySpec.Default;
            }

            BeaconSpec beacon = null;
            if (beaconFields != null)
            {
                beacon = ReadBeacon(beaconFields);
            }
            else if (role == DeployRole.Beacon)
            {
                // 囮と同じく、ビーコンの値はどれも既定があるので、beacon を書かなければ既定のビーコンにする。
                beacon = BeaconSpec.Default;
            }

            DroneSpec drone = null;
            if (droneFields != null)
            {
                drone = ReadDrone(droneFields);
            }
            else if (role == DeployRole.Drone)
            {
                // 囮と同じく、ドローンの値はどれも既定があるので、drone を書かなければ既定のドローンにする。
                drone = DroneSpec.Default;
            }

            try
            {
                return new DeployType(id, role, deploy, turret, weaponId, decoy, beacon, drone, ability);
            }
            catch (ArgumentException e)
            {
                throw Rejected(entry, TypeItemNames, e);
            }
        }

        private static DeploySpec ReadDeploy(ServerDataJson.Fields entry, DeploySpec defaults)
        {
            var maxActive = entry.ReadInt(MaxActiveItem, defaults.MaxActive);
            var overflow = entry.ReadEnum(OverflowItem, defaults.Overflow);
            var maxHealth = entry.ReadInt(MaxHealthItem, defaults.MaxHealth);
            var maxRange = entry.ReadFloat(MaxRangeItem, defaults.MaxRange);
            var maxSlopeDegrees = entry.ReadFloat(MaxSlopeDegreesItem, defaults.MaxSlopeDegrees);
            var maxStepHeight = entry.ReadFloat(MaxStepHeightItem, defaults.MaxStepHeight);
            var width = entry.ReadFloat(WidthItem, defaults.Width);
            var height = entry.ReadFloat(HeightItem, defaults.Height);
            var depth = entry.ReadFloat(DepthItem, defaults.Depth);
            try
            {
                return new DeploySpec(maxActive, overflow, maxHealth, maxRange, maxSlopeDegrees, maxStepHeight, width, height, depth);
            }
            catch (ArgumentException e)
            {
                throw Rejected(entry, TypeItemNames, e);
            }
        }

        // 置くまでの溜め (windupSeconds) は読まない。置く地点はクライアントが溜めの後に送るので、サーバーが受けるかには効かない
        private static AbilitySpec ReadAbility(ServerDataJson.Fields entry)
        {
            var maxCharges = entry.ReadInt(MaxChargesItem, AbilitySpec.DefaultMaxCharges);
            var rechargeSeconds = entry.ReadFloat(RechargeSecondsItem, AbilitySpec.DefaultRechargeSeconds);
            var cooldownSeconds = entry.ReadFloat(CooldownSecondsItem, AbilitySpec.DefaultCooldownSeconds);
            try
            {
                return new AbilitySpec(maxCharges, rechargeSeconds, cooldownSeconds, AbilitySpec.DefaultWindupSeconds);
            }
            catch (ArgumentException e)
            {
                throw Rejected(entry, TypeItemNames, e);
            }
        }

        private static TurretSpec ReadTurret(ServerDataJson.Fields fields)
        {
            var rounds = fields.ReadInt(RoundsItem, TurretSpec.DefaultRounds);
            var lifetimeSeconds = fields.ReadFloat(LifetimeSecondsItem, TurretSpec.DefaultLifetimeSeconds);
            var sightRange = fields.ReadFloat(SightRangeItem, TurretSpec.DefaultSightRange);
            var turnDegreesPerSecond = fields.ReadFloat(TurnDegreesPerSecondItem, TurretSpec.DefaultTurnDegreesPerSecond);
            var aimToleranceDegrees = fields.ReadFloat(AimToleranceDegreesItem, TurretSpec.DefaultAimToleranceDegrees);
            try
            {
                return new TurretSpec(rounds, lifetimeSeconds, sightRange, turnDegreesPerSecond, aimToleranceDegrees);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, TurretItemNames, e);
            }
        }

        private static DecoySpec ReadDecoy(ServerDataJson.Fields fields)
        {
            var lifetimeSeconds = fields.ReadFloat(LifetimeSecondsItem, DecoySpec.DefaultLifetimeSeconds);
            var callIntervalSeconds = fields.ReadFloat(CallIntervalSecondsItem, DecoySpec.DefaultCallIntervalSeconds);
            var callRadius = fields.ReadFloat(CallRadiusItem, DecoySpec.DefaultCallRadius);
            try
            {
                return new DecoySpec(lifetimeSeconds, callIntervalSeconds, callRadius);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, DecoyItemNames, e);
            }
        }

        private static BeaconSpec ReadBeacon(ServerDataJson.Fields fields)
        {
            var uses = fields.ReadInt(UsesItem, BeaconSpec.DefaultUses);
            var spawnDistance = fields.ReadFloat(SpawnDistanceItem, BeaconSpec.DefaultSpawnDistance);
            try
            {
                return new BeaconSpec(uses, spawnDistance);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, BeaconItemNames, e);
            }
        }

        private static DroneSpec ReadDrone(ServerDataJson.Fields fields)
        {
            var speed = fields.ReadFloat(SpeedItem, DroneSpec.DefaultSpeed);
            var controlRange = fields.ReadFloat(ControlRangeItem, DroneSpec.DefaultControlRange);
            var batterySeconds = fields.ReadFloat(BatterySecondsItem, DroneSpec.DefaultBatterySeconds);
            var spotRadius = fields.ReadFloat(SpotRadiusItem, DroneSpec.DefaultSpotRadius);
            var spotIntervalSeconds = fields.ReadFloat(SpotIntervalSecondsItem, DroneSpec.DefaultSpotIntervalSeconds);
            var revealSeconds = fields.ReadFloat(RevealSecondsItem, DroneSpec.DefaultRevealSeconds);
            try
            {
                return new DroneSpec(speed, controlRange, batterySeconds, spotRadius, spotIntervalSeconds, revealSeconds);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, DroneItemNames, e);
            }
        }

        private static ServerDataJsonException Rejected(ServerDataJson.Fields fields, IReadOnlyList<string> known, ArgumentException e)
        {
            foreach (var name in known)
            {
                if (string.Equals(name, e.ParamName, StringComparison.Ordinal))
                {
                    return fields.Rejected(fields.ItemPath(name), e.Message, e);
                }
            }

            return fields.Rejected(fields.Path, e.Message, e);
        }
    }
}
