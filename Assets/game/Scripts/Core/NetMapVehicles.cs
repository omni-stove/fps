using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    // 並びはシーンの階層順で、スナップショットと要求は乗り物をこの並びの番号で指す。サーバーとクライアントが同じ番号で数えられるようにするため。
    public sealed class NetMapVehicles
    {
        public const string Extension = ".vehicles.json";

        public const string VehiclesItem = "vehicles";
        public const string IdItem = "id";
        public const string PositionItem = "position";
        public const string RotationItem = "rotation";
        public const string MaxHealthItem = "maxHealth";
        public const string EnterRangeItem = "enterRange";
        public const string SeatsItem = "seats";
        public const string ExitsItem = "exits";
        public const string WeaponsItem = "weapons";
        public const string PartsItem = "parts";
        public const string HullItem = "hull";
        public const string DurabilityItem = "durability";
        public const string DriveItem = "drive";
        public const string CountermeasuresItem = "countermeasures";

        public const string RoleItem = "role";
        public const string PoseItem = "pose";
        public const string CameraViewItem = "cameraView";
        public const string OwnWeaponArcDegreesItem = "ownWeaponArcDegrees";
        public const string OwnWeaponSpreadMultiplierItem = "ownWeaponSpreadMultiplier";
        public const string SitItem = "sit";
        public const string SitYawDegreesItem = "sitYawDegrees";
        public const string EnterItem = "enter";

        public const string SeatItem = "seat";
        public const string YawMinDegreesItem = "yawMinDegrees";
        public const string YawMaxDegreesItem = "yawMaxDegrees";
        public const string PitchMinDegreesItem = "pitchMinDegrees";
        public const string PitchMaxDegreesItem = "pitchMaxDegrees";
        public const string TurnSpeedDegreesPerSecondItem = "turnSpeedDegreesPerSecond";
        public const string WeaponItem = "weapon";

        public const string DamageMultiplierItem = "damageMultiplier";

        public const string CenterItem = "center";
        public const string HalfExtentsItem = "halfExtents";
        public const string PartItem = "part";

        public const string SmokeHealthFractionItem = "smokeHealthFraction";
        public const string BurnHealthFractionItem = "burnHealthFraction";
        public const string BurnDamagePerSecondItem = "burnDamagePerSecond";
        public const string ExplosionMaxDamageItem = "explosionMaxDamage";
        public const string ExplosionRadiusItem = "explosionRadius";
        public const string ExplosionFullDamageRadiusItem = "explosionFullDamageRadius";
        public const string ExplosionStructureDamageItem = "explosionStructureDamage";

        public const string MaxSpeedItem = "maxSpeed";
        public const string CorrectionDistanceItem = "correctionDistance";
        public const string BurstSecondsItem = "burstSeconds";

        public const string CountItem = "count";
        public const string IntervalSecondsItem = "intervalSeconds";
        public const string LifetimeSecondsItem = "lifetimeSeconds";
        public const string DecoyConeDegreesItem = "decoyConeDegrees";
        public const string EjectSpeedItem = "ejectSpeed";
        public const string GravityMultiplierItem = "gravityMultiplier";
        public const string EjectOffsetItem = "ejectOffset";
        public const string EjectDirectionItem = "ejectDirection";

        private const string _kind = "map vehicles JSON";

        private static readonly string[] _topItems = { VehiclesItem };

        private static readonly string[] _vehicleItems =
        {
            IdItem, PositionItem, RotationItem, MaxHealthItem, EnterRangeItem, SeatsItem, ExitsItem, WeaponsItem, PartsItem, HullItem,
            DurabilityItem, DriveItem, CountermeasuresItem,
        };

        private static readonly string[] _seatItems =
        {
            RoleItem, PoseItem, CameraViewItem, OwnWeaponArcDegreesItem, OwnWeaponSpreadMultiplierItem, SitItem, SitYawDegreesItem, EnterItem, ExitsItem,
        };

        private static readonly string[] _weaponItems =
        {
            SeatItem, YawMinDegreesItem, YawMaxDegreesItem, PitchMinDegreesItem, PitchMaxDegreesItem, TurnSpeedDegreesPerSecondItem, WeaponItem,
        };

        private static readonly string[] _partItems = { IdItem, DamageMultiplierItem };
        private static readonly string[] _hullItems = { CenterItem, HalfExtentsItem, RotationItem, PartItem };

        private static readonly string[] _durabilityItems =
        {
            SmokeHealthFractionItem, BurnHealthFractionItem, BurnDamagePerSecondItem, ExplosionMaxDamageItem, ExplosionRadiusItem,
            ExplosionFullDamageRadiusItem, ExplosionStructureDamageItem,
        };

        private static readonly string[] _driveItems = { MaxSpeedItem, CorrectionDistanceItem, BurstSecondsItem };

        private static readonly string[] _countermeasureItems =
        {
            CountItem, IntervalSecondsItem, LifetimeSecondsItem, DecoyConeDegreesItem, EjectSpeedItem, GravityMultiplierItem, EjectOffsetItem,
            EjectDirectionItem,
        };

        public NetMapVehicles(IReadOnlyList<MapVehicle> vehicles)
        {
            if (vehicles == null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            var copy = new List<MapVehicle>(vehicles.Count);
            foreach (var vehicle in vehicles)
            {
                copy.Add(vehicle ?? throw new ArgumentException("vehicles must not contain null", nameof(vehicles)));
            }

            Vehicles = copy.AsReadOnly();
        }

        public static NetMapVehicles None { get; } = new NetMapVehicles(Array.Empty<MapVehicle>());

        public IReadOnlyList<MapVehicle> Vehicles { get; }

        public static NetMapVehicles Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var vehicles = new List<MapVehicle>();
            var nodes = top.Array(VehiclesItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                vehicles.Add(ReadVehicle(top.Element(nodes[i], VehiclesItem, i, _vehicleItems)));
            }

            return new NetMapVehicles(vehicles);
        }

        public static string Write(NetMapVehicles vehicles)
        {
            if (vehicles == null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            var writer = new JsonWriter();
            writer.BeginArray(VehiclesItem);
            foreach (var vehicle in vehicles.Vehicles)
            {
                WriteVehicle(writer, vehicle);
            }

            writer.EndArray();
            return writer.Finish();
        }

        private static MapVehicle ReadVehicle(ServerDataJson.Fields fields)
        {
            var seatSpecs = new List<VehicleSeatSpec>();
            var seats = new List<MapVehicleSeat>();
            var seatNodes = fields.Array(SeatsItem);
            for (var i = 0; i < seatNodes.Count; i++)
            {
                var seat = fields.Element(seatNodes[i], SeatsItem, i, _seatItems);
                seatSpecs.Add(Guard(seat, () => new VehicleSeatSpec(
                    seat.ToEnum<VehicleSeatRole>(seat.ItemPath(RoleItem), seat.Require(RoleItem)),
                    seat.ReadEnum(PoseItem, VehicleSeatSpec.DefaultPose),
                    seat.ReadEnum(CameraViewItem, VehicleSeatSpec.DefaultCameraView),
                    seat.ReadFloat(OwnWeaponArcDegreesItem, VehicleSeatSpec.DefaultOwnWeaponArcDegrees),
                    seat.ReadFloat(OwnWeaponSpreadMultiplierItem, VehicleSeatSpec.DefaultOwnWeaponSpreadMultiplier))));
                seats.Add(Guard(seat, () => new MapVehicleSeat(
                    seat.ReadVector3(SitItem), seat.ReadFloat(SitYawDegreesItem, 0f), ReadPoints(seat, EnterItem), ReadPoints(seat, ExitsItem))));
            }

            var weaponSpecs = new List<VehicleWeaponSpec>();
            var weapons = new List<WeaponSpec>();
            var weaponNodes = fields.Array(WeaponsItem);
            for (var i = 0; i < weaponNodes.Count; i++)
            {
                var weapon = fields.Element(weaponNodes[i], WeaponsItem, i, _weaponItems);
                var specFields = weapon.Object(WeaponItem, WeaponSpecJson.ItemNames)
                    ?? throw weapon.Rejected(weapon.ItemPath(WeaponItem), "is missing");
                var weaponSpec = WeaponSpecJson.Read(specFields);
                weapons.Add(weaponSpec);
                weaponSpecs.Add(Guard(weapon, () => new VehicleWeaponSpec(
                    weaponSpec.Id,
                    weapon.ToInt(weapon.ItemPath(SeatItem), weapon.Require(SeatItem)),
                    weapon.ReadFloat(YawMinDegreesItem, VehicleWeaponSpec.DefaultYawMinDegrees),
                    weapon.ReadFloat(YawMaxDegreesItem, VehicleWeaponSpec.DefaultYawMaxDegrees),
                    weapon.ReadFloat(PitchMinDegreesItem, VehicleWeaponSpec.DefaultPitchMinDegrees),
                    weapon.ReadFloat(PitchMaxDegreesItem, VehicleWeaponSpec.DefaultPitchMaxDegrees),
                    weapon.ReadFloat(TurnSpeedDegreesPerSecondItem, VehicleWeaponSpec.DefaultTurnSpeedDegreesPerSecond))));
            }

            var parts = new List<VehiclePartSpec>();
            var partNodes = fields.Array(PartsItem);
            for (var i = 0; i < partNodes.Count; i++)
            {
                var part = fields.Element(partNodes[i], PartsItem, i, _partItems);
                parts.Add(Guard(part, () => new VehiclePartSpec(
                    part.ToText(part.ItemPath(IdItem), part.Require(IdItem)),
                    part.ReadFloat(DamageMultiplierItem, 1f))));
            }

            var hull = new List<MapVehicleHullBox>();
            var hullNodes = fields.Array(HullItem);
            for (var i = 0; i < hullNodes.Count; i++)
            {
                var box = fields.Element(hullNodes[i], HullItem, i, _hullItems);
                hull.Add(Guard(box, () => new MapVehicleHullBox(
                    box.ReadVector3(CenterItem),
                    box.ReadVector3(HalfExtentsItem),
                    box.Has(RotationItem) ? ReadRotation(box, RotationItem) : Quaternion.Identity,
                    box.ReadString(PartItem, null))));
            }

            var durability = ReadDurability(fields.Object(DurabilityItem, _durabilityItems));
            var drive = ReadDrive(fields.Object(DriveItem, _driveItems));
            var countermeasures = ReadCountermeasures(fields.Object(CountermeasuresItem, _countermeasureItems));
            var spec = Guard(fields, () => new VehicleSpec(
                fields.ToText(fields.ItemPath(IdItem), fields.Require(IdItem)),
                fields.ReadInt(MaxHealthItem, VehicleSpec.DefaultMaxHealth),
                seatSpecs,
                weaponSpecs,
                parts,
                fields.ReadFloat(EnterRangeItem, VehicleSpec.DefaultEnterRange),
                durability: durability));
            return Guard(fields, () => new MapVehicle(
                spec,
                fields.ReadVector3(PositionItem),
                fields.Has(RotationItem) ? ReadRotation(fields, RotationItem) : Quaternion.Identity,
                seats,
                hull,
                ReadPoints(fields, ExitsItem),
                weapons,
                drive,
                countermeasures));
        }

        // 項目が無い乗り物はフレアを積んでいない。Unity の乗り物も VehicleCountermeasures を付けたものだけが撒くため。
        private static MapVehicleCountermeasures ReadCountermeasures(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                return null;
            }

            var spec = Guard(fields, () => new CountermeasureSpec(
                fields.ReadInt(CountItem, CountermeasureSpec.DefaultCount),
                fields.ReadFloat(IntervalSecondsItem, CountermeasureSpec.DefaultIntervalSeconds),
                fields.ReadFloat(LifetimeSecondsItem, CountermeasureSpec.DefaultLifetimeSeconds),
                fields.ReadFloat(DecoyConeDegreesItem, CountermeasureSpec.DefaultDecoyConeDegrees),
                fields.ReadFloat(EjectSpeedItem, CountermeasureSpec.DefaultEjectSpeed),
                fields.ReadFloat(GravityMultiplierItem, CountermeasureSpec.DefaultGravityMultiplier)));
            var offset = fields.Has(EjectOffsetItem) ? fields.ReadVector3(EjectOffsetItem) : MapVehicleCountermeasures.DefaultEjectOffset;
            var direction = fields.Has(EjectDirectionItem) ? fields.ReadVector3(EjectDirectionItem) : MapVehicleCountermeasures.DefaultEjectDirection;
            return Guard(fields, () => new MapVehicleCountermeasures(spec, offset, direction));
        }

        private static VehicleDurabilitySpec ReadDurability(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                return VehicleDurabilitySpec.Default;
            }

            return Guard(fields, () => new VehicleDurabilitySpec(
                fields.ReadFloat(SmokeHealthFractionItem, VehicleDurabilitySpec.DefaultSmokeHealthFraction),
                fields.ReadFloat(BurnHealthFractionItem, VehicleDurabilitySpec.DefaultBurnHealthFraction),
                fields.ReadFloat(BurnDamagePerSecondItem, VehicleDurabilitySpec.DefaultBurnDamagePerSecond),
                new ExplosionSpec(
                    new ExplosionDamage(
                        fields.ReadInt(ExplosionMaxDamageItem, VehicleDurabilitySpec.DefaultExplosionMaxDamage),
                        fields.ReadFloat(ExplosionRadiusItem, VehicleDurabilitySpec.DefaultExplosionRadius),
                        fields.ReadFloat(ExplosionFullDamageRadiusItem, VehicleDurabilitySpec.DefaultExplosionFullDamageRadius)),
                    fields.ReadInt(ExplosionStructureDamageItem, VehicleDurabilitySpec.DefaultExplosionStructureDamage))));
        }

        private static VehicleDriveSpec ReadDrive(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                return VehicleDriveSpec.Default;
            }

            return Guard(fields, () => new VehicleDriveSpec(
                fields.ReadFloat(MaxSpeedItem, VehicleDriveSpec.DefaultMaxSpeed),
                fields.ReadFloat(CorrectionDistanceItem, VehicleDriveSpec.DefaultCorrectionDistance),
                fields.ReadFloat(BurstSecondsItem, VehicleDriveSpec.DefaultBurstSeconds)));
        }

        // 型の検査の例外を、項目の場所を付けたファイルの誤りにする。引数名はどれも項目名と同じにしてある。
        private static T Guard<T>(ServerDataJson.Fields fields, Func<T> create)
        {
            try
            {
                return create();
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
            }
        }

        private static Vector3[] ReadPoints(ServerDataJson.Fields fields, string name)
        {
            var nodes = fields.Array(name);
            var points = new Vector3[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                var values = ReadNumbers(fields, fields.ElementPath(name, i), nodes[i], 3);
                points[i] = new Vector3(values[0], values[1], values[2]);
            }

            return points;
        }

        private static Quaternion ReadRotation(ServerDataJson.Fields fields, string name)
        {
            var values = ReadNumbers(fields, fields.ItemPath(name), fields.Require(name), 4);
            var rotation = new Quaternion(values[0], values[1], values[2], values[3]);
            var length = rotation.Length();
            if (!(length > 1e-6f) || !float.IsFinite(length))
            {
                throw fields.Rejected(fields.ItemPath(name), "must be a rotation (x, y, z, w) that is not zero");
            }

            return Quaternion.Normalize(rotation);
        }

        private static float[] ReadNumbers(ServerDataJson.Fields fields, string item, JsonNode node, int count)
        {
            if (node.Kind != JsonNodeKind.Array || node.Elements.Count != count)
            {
                throw fields.Invalid(
                    item, node, "must be an array of " + count.ToString(CultureInfo.InvariantCulture) + " numbers, but is " + node.Describe());
            }

            var values = new float[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = fields.ToFloat(item + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", node.Elements[i]);
            }

            return values;
        }

        private static void WriteVehicle(JsonWriter writer, MapVehicle vehicle)
        {
            var spec = vehicle.Spec;
            writer.BeginElement();
            writer.String(IdItem, spec.Id);
            writer.Vector3(PositionItem, vehicle.Position);
            WriteRotation(writer, RotationItem, vehicle.Rotation);
            writer.Int(MaxHealthItem, spec.MaxHealth);
            writer.Float(EnterRangeItem, spec.EnterRange);

            writer.BeginArray(SeatsItem);
            for (var i = 0; i < spec.Seats.Count; i++)
            {
                var seatSpec = spec.Seats[i];
                var seat = vehicle.Seats[i];
                writer.BeginElement();
                writer.Enum(RoleItem, seatSpec.Role);
                writer.Enum(PoseItem, seatSpec.Pose);
                writer.Enum(CameraViewItem, seatSpec.CameraView);
                writer.Float(OwnWeaponArcDegreesItem, seatSpec.OwnWeaponArcDegrees);
                writer.Float(OwnWeaponSpreadMultiplierItem, seatSpec.OwnWeaponSpreadMultiplier);
                writer.Vector3(SitItem, seat.Sit);
                writer.Float(SitYawDegreesItem, seat.SitYawDegrees);
                WritePoints(writer, EnterItem, seat.EnterPoints);
                WritePoints(writer, ExitsItem, seat.ExitPoints);
                writer.EndObject();
            }

            writer.EndArray();
            WritePoints(writer, ExitsItem, vehicle.ExitPoints);

            writer.BeginArray(WeaponsItem);
            for (var i = 0; i < spec.Weapons.Count; i++)
            {
                var mount = spec.Weapons[i];
                writer.BeginElement();
                writer.Int(SeatItem, mount.SeatIndex);
                writer.Float(YawMinDegreesItem, mount.YawMinDegrees);
                writer.Float(YawMaxDegreesItem, mount.YawMaxDegrees);
                writer.Float(PitchMinDegreesItem, mount.PitchMinDegrees);
                writer.Float(PitchMaxDegreesItem, mount.PitchMaxDegrees);
                writer.Float(TurnSpeedDegreesPerSecondItem, mount.TurnSpeedDegreesPerSecond);
                writer.BeginObject(WeaponItem);
                WeaponSpecJson.Write(writer, vehicle.Weapons[i]);
                writer.EndObject();
                writer.EndObject();
            }

            writer.EndArray();

            writer.BeginArray(PartsItem);
            foreach (var part in spec.Parts)
            {
                writer.BeginElement();
                writer.String(IdItem, part.Id);
                writer.Float(DamageMultiplierItem, part.DamageMultiplier);
                writer.EndObject();
            }

            writer.EndArray();

            writer.BeginArray(HullItem);
            foreach (var box in vehicle.Hull)
            {
                writer.BeginElement();
                writer.Vector3(CenterItem, box.Center);
                writer.Vector3(HalfExtentsItem, box.HalfExtents);
                WriteRotation(writer, RotationItem, box.Rotation);
                if (box.PartId != null)
                {
                    writer.String(PartItem, box.PartId);
                }

                writer.EndObject();
            }

            writer.EndArray();

            var durability = spec.Durability;
            var explosion = durability.Explosion;
            writer.BeginObject(DurabilityItem);
            writer.Float(SmokeHealthFractionItem, durability.SmokeHealthFraction);
            writer.Float(BurnHealthFractionItem, durability.BurnHealthFraction);
            writer.Float(BurnDamagePerSecondItem, durability.BurnDamagePerSecond);
            writer.Int(ExplosionMaxDamageItem, explosion.Damage.MaxDamage);
            writer.Float(ExplosionRadiusItem, explosion.Damage.Radius);
            writer.Float(ExplosionFullDamageRadiusItem, explosion.Damage.FullDamageRadius);
            writer.Int(ExplosionStructureDamageItem, explosion.StructureDamageAtCenter);
            writer.EndObject();

            writer.BeginObject(DriveItem);
            writer.Float(MaxSpeedItem, vehicle.Drive.MaxSpeed);
            writer.Float(CorrectionDistanceItem, vehicle.Drive.CorrectionDistance);
            writer.Float(BurstSecondsItem, vehicle.Drive.BurstSeconds);
            writer.EndObject();
            WriteCountermeasures(writer, vehicle.Countermeasures);
            writer.EndObject();
        }

        private static void WriteCountermeasures(JsonWriter writer, MapVehicleCountermeasures countermeasures)
        {
            if (countermeasures == null)
            {
                return;
            }

            var spec = countermeasures.Spec;
            writer.BeginObject(CountermeasuresItem);
            writer.Int(CountItem, spec.Count);
            writer.Float(IntervalSecondsItem, spec.IntervalSeconds);
            writer.Float(LifetimeSecondsItem, spec.LifetimeSeconds);
            writer.Float(DecoyConeDegreesItem, spec.DecoyConeDegrees);
            writer.Float(EjectSpeedItem, spec.EjectSpeed);
            writer.Float(GravityMultiplierItem, spec.GravityMultiplier);
            writer.Vector3(EjectOffsetItem, countermeasures.EjectOffset);
            writer.Vector3(EjectDirectionItem, countermeasures.EjectDirection);
            writer.EndObject();
        }

        private static void WriteRotation(JsonWriter writer, string name, Quaternion rotation)
        {
            writer.FloatArray(name, new[] { rotation.X, rotation.Y, rotation.Z, rotation.W });
        }

        private static void WritePoints(JsonWriter writer, string name, IReadOnlyList<Vector3> points)
        {
            var text = new StringBuilder("[");
            for (var i = 0; i < points.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                var point = points[i];
                text.Append('[')
                    .Append(JsonWriter.FloatText(point.X)).Append(", ")
                    .Append(JsonWriter.FloatText(point.Y)).Append(", ")
                    .Append(JsonWriter.FloatText(point.Z)).Append(']');
            }

            writer.Embed(name, text.Append(']').ToString());
        }
    }
}
