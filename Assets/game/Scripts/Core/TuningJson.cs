using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public static class TuningJson
    {
        public const string FuseSecondsItem = "fuseSeconds";
        public const string ThrowSecondsItem = "throwSeconds";
        public const string ThrowSpeedItem = "throwSpeed";
        public const string ThrowLiftSpeedItem = "throwLiftSpeed";
        public const string MaxDamageItem = "maxDamage";
        public const string DamageRadiusItem = "damageRadius";
        public const string FullDamageRadiusItem = "fullDamageRadius";
        public const string StructureDamageAtCenterItem = "structureDamageAtCenter";
        public const string GrenadeIdItem = "id";
        public const string GrenadeNameKeyItem = "nameKey";
        public const string GrenadeImpactItem = "impact";
        public const string StickinessItem = "stickiness";
        public const string RollDampingItem = "rollDamping";
        public const string BodyRadiusItem = "bodyRadius";
        public const string MassItem = "mass";
        public const string BouncinessItem = "bounciness";
        public const string FrictionItem = "friction";
        public const string AngularDampingItem = "angularDamping";
        public const string ReleaseDistanceItem = "releaseDistance";
        public const string GrenadeTypesItem = "types";
        public const string CarriedItem = "carried";

        public const string LifetimeSecondsItem = "lifetimeSeconds";
        public const string MaxMarkersItem = "maxMarkers";
        public const string MaxDistanceItem = "maxDistance";
        public const string EnemyAimToleranceDegreesItem = "enemyAimToleranceDegrees";
        public const string MinIntervalSecondsItem = "minIntervalSeconds";

        public const string ExplosionSpeedItem = "explosionSpeed";
        public const string ExplosionUpwardRatioItem = "explosionUpwardRatio";
        public const string ExplosionWeightFactorItem = "explosionWeightFactor";
        public const string MeleeSpeedItem = "meleeSpeed";
        public const string MeleeUpwardRatioItem = "meleeUpwardRatio";
        public const string MeleeWeightFactorItem = "meleeWeightFactor";
        public const string ChargeSpeedItem = "chargeSpeed";
        public const string ChargeUpwardRatioItem = "chargeUpwardRatio";
        public const string ChargeWeightFactorItem = "chargeWeightFactor";
        public const string MinSpeedItem = "minSpeed";
        public const string MinAirSecondsItem = "minAirSeconds";
        public const string MaxAirSecondsItem = "maxAirSeconds";
        public const string AirControlItem = "airControl";
        public const string GravityItem = "gravity";
        public const string RecoverSecondsItem = "recoverSeconds";
        public const string LandingSnapRadiusItem = "landingSnapRadius";
        public const string PlayerWeightItem = "playerWeight";
        public const string EnemyWeightItem = "enemyWeight";

        public const string SightRangeItem = "sightRange";
        public const string AttackRangeItem = "attackRange";
        public const string LoseSightSecondsItem = "loseSightSeconds";
        public const string SearchSecondsItem = "searchSeconds";
        public const string ReactionSecondsItem = "reactionSeconds";
        public const string FireRangeItem = "fireRange";
        public const string FacingToleranceDegreesItem = "facingToleranceDegrees";
        public const string RangeSpreadDegreesItem = "rangeSpreadDegrees";
        public const string OpeningSpreadDegreesItem = "openingSpreadDegrees";
        public const string SettledSpreadDegreesItem = "settledSpreadDegrees";
        public const string OpeningShotsItem = "openingShots";
        public const string CoverHideSecondsItem = "coverHideSeconds";
        public const string CoverPeekSecondsItem = "coverPeekSeconds";
        public const string CoverHealthFractionItem = "coverHealthFraction";
        public const string ArchetypeItem = "archetype";
        public const string StaggerResistanceItem = "staggerResistance";
        public const string SprintRatioItem = "sprintRatio";
        public const string CrouchRatioItem = "crouchRatio";
        public const string RecoilControlItem = "recoilControl";
        public const string MaxTurnDegreesPerSecondItem = "maxTurnDegreesPerSecond";
        public const string AimErrorPerMeterItem = "aimErrorPerMeter";
        public const string AimErrorRefreshSecondsItem = "aimErrorRefreshSeconds";
        public const string FireToleranceDegreesItem = "fireToleranceDegrees";
        public const string ArrivalDistanceItem = "arrivalDistance";
        public const string SprintDistanceItem = "sprintDistance";
        public const string SprintMaxAngleDegreesItem = "sprintMaxAngleDegrees";
        public const string CrouchWhenHoldingItem = "crouchWhenHolding";
        public const string NavSampleRadiusItem = "navSampleRadius";
        public const string RepathSecondsItem = "repathSeconds";
        public const string RepathDistanceItem = "repathDistance";
        public const string StuckSecondsItem = "stuckSeconds";
        public const string StuckProgressItem = "stuckProgress";
        public const string RallyRouteRadiusFractionItem = "rallyRouteRadiusFraction";
        public const string RallyRouteAngleStepDegreesItem = "rallyRouteAngleStepDegrees";
        public const string RallyMoveRadiusFractionItem = "rallyMoveRadiusFraction";

        private const string _grenadeKind = "grenade JSON";
        private const string _pingKind = "ping JSON";
        private const string _knockbackKind = "knockback JSON";
        private const string _botsKind = "bots JSON";

        public static IReadOnlyList<string> GrenadeItemNames { get; } = new[]
        {
            GrenadeIdItem,
            GrenadeNameKeyItem,
            FuseSecondsItem,
            ThrowSecondsItem,
            ThrowSpeedItem,
            ThrowLiftSpeedItem,
            MaxDamageItem,
            DamageRadiusItem,
            FullDamageRadiusItem,
            StructureDamageAtCenterItem,
            GrenadeImpactItem,
            StickinessItem,
            RollDampingItem,
            BodyRadiusItem,
            MassItem,
            BouncinessItem,
            FrictionItem,
            AngularDampingItem,
            ReleaseDistanceItem,
        };

        public static IReadOnlyList<string> GrenadeCatalogItemNames { get; } = new[] { GrenadeTypesItem, CarriedItem };

        // 爆発を impact に書いた種類に平らな爆発の値も書くと、どちらを使うか読み手が迷う
        private static readonly string[] _flatExplosionItems = { MaxDamageItem, DamageRadiusItem, FullDamageRadiusItem, StructureDamageAtCenterItem };

        public static IReadOnlyList<string> PingItemNames { get; } = new[]
        {
            LifetimeSecondsItem,
            MaxMarkersItem,
            MaxDistanceItem,
            EnemyAimToleranceDegreesItem,
            MinIntervalSecondsItem,
        };

        public static IReadOnlyList<string> KnockbackItemNames { get; } = new[]
        {
            ExplosionSpeedItem,
            ExplosionUpwardRatioItem,
            ExplosionWeightFactorItem,
            MeleeSpeedItem,
            MeleeUpwardRatioItem,
            MeleeWeightFactorItem,
            ChargeSpeedItem,
            ChargeUpwardRatioItem,
            ChargeWeightFactorItem,
            MinSpeedItem,
            MinAirSecondsItem,
            MaxAirSecondsItem,
            AirControlItem,
            GravityItem,
            RecoverSecondsItem,
            LandingSnapRadiusItem,
            PlayerWeightItem,
            EnemyWeightItem,
        };

        // 項目名は EnemySpec・BotInputSpec・NetBotSpec のコンストラクターの引数名にそろえ、値の検査で断られた項目を名前で示せるようにする。
        // EnemySpec の fireToleranceDegrees だけは BotInputSpec の同じ名前と分けるため facingToleranceDegrees と呼ぶ
        public static IReadOnlyList<string> BotItemNames { get; } = new[]
        {
            SightRangeItem,
            AttackRangeItem,
            LoseSightSecondsItem,
            SearchSecondsItem,
            ReactionSecondsItem,
            OpeningSpreadDegreesItem,
            SettledSpreadDegreesItem,
            OpeningShotsItem,
            CoverHideSecondsItem,
            CoverPeekSecondsItem,
            CoverHealthFractionItem,
            ArchetypeItem,
            StaggerResistanceItem,
            FireRangeItem,
            FacingToleranceDegreesItem,
            RangeSpreadDegreesItem,
            SprintRatioItem,
            CrouchRatioItem,
            RecoilControlItem,
            MaxTurnDegreesPerSecondItem,
            AimErrorPerMeterItem,
            AimErrorRefreshSecondsItem,
            FireToleranceDegreesItem,
            ArrivalDistanceItem,
            SprintDistanceItem,
            SprintMaxAngleDegreesItem,
            CrouchWhenHoldingItem,
            NavSampleRadiusItem,
            RepathSecondsItem,
            RepathDistanceItem,
            StuckSecondsItem,
            StuckProgressItem,
            RallyRouteRadiusFractionItem,
            RallyRouteAngleStepDegreesItem,
            RallyMoveRadiusFractionItem,
        };

        // GrenadeTypesItem も CarriedItem も無いオブジェクトは、種類が 1 つだった頃の形として、その 1 種類だけを持つ並びに読む。
        public static GrenadeCatalog ParseGrenades(string text, string sourceName)
        {
            var source = SourceOf(sourceName, _grenadeKind);
            return Translated(() =>
            {
                var root = ServerDataJson.ParseDocument(text, source, _grenadeKind);
                if (root.Kind == GameRulesJson.NodeKind.Object && !HasMember(root, GrenadeTypesItem) && !HasMember(root, CarriedItem))
                {
                    return GrenadeCatalog.Of(ReadGrenade(new ServerDataJson.Fields(source, _grenadeKind, root, string.Empty, GrenadeItemNames)));
                }

                var fields = new ServerDataJson.Fields(source, _grenadeKind, root, string.Empty, GrenadeCatalogItemNames);
                var types = new List<GrenadeSpec>();
                var nodes = fields.Array(GrenadeTypesItem);
                for (var i = 0; i < nodes.Count; i++)
                {
                    types.Add(ReadGrenade(fields.Element(nodes[i], GrenadeTypesItem, i, GrenadeItemNames)));
                }

                List<string> carried = null;
                if (fields.Has(CarriedItem))
                {
                    carried = new List<string>();
                    var carriedNodes = fields.Array(CarriedItem);
                    for (var i = 0; i < carriedNodes.Count; i++)
                    {
                        carried.Add(fields.ToText(fields.ElementPath(CarriedItem, i), carriedNodes[i]));
                    }
                }

                try
                {
                    return new GrenadeCatalog(types, carried);
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(e.ParamName == "carriedIds" ? CarriedItem : GrenadeTypesItem, e.Message, e);
                }
            });
        }

        public static GrenadeSpec ParseGrenade(string text, string sourceName)
        {
            var source = SourceOf(sourceName, _grenadeKind);
            return Translated(() => ReadGrenade(
                new ServerDataJson.Fields(source, _grenadeKind, ServerDataJson.ParseDocument(text, source, _grenadeKind), string.Empty, GrenadeItemNames)));
        }

        public static PingSpec ParsePing(string text, string sourceName)
        {
            var source = SourceOf(sourceName, _pingKind);
            var fields = Fields.Read(text, source, _pingKind, PingItemNames);
            var defaults = PingSpec.Default;
            try
            {
                return new PingSpec(
                    fields.ReadFloat(LifetimeSecondsItem, defaults.LifetimeSeconds),
                    fields.ReadInt(MaxMarkersItem, defaults.MaxMarkers),
                    fields.ReadFloat(MaxDistanceItem, defaults.MaxDistance),
                    fields.ReadFloat(EnemyAimToleranceDegreesItem, defaults.EnemyAimToleranceDegrees),
                    fields.ReadFloat(MinIntervalSecondsItem, defaults.MinIntervalSeconds));
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, _pingKind, PingItemNames, e);
            }
        }

        public static KnockbackSpec ParseKnockback(string text, string sourceName)
        {
            var source = SourceOf(sourceName, _knockbackKind);
            var fields = Fields.Read(text, source, _knockbackKind, KnockbackItemNames);
            var defaults = KnockbackSpec.Default;
            var explosion = ReadForce(fields, source, defaults.Explosion, ExplosionSpeedItem, ExplosionUpwardRatioItem, ExplosionWeightFactorItem);
            var melee = ReadForce(fields, source, defaults.Melee, MeleeSpeedItem, MeleeUpwardRatioItem, MeleeWeightFactorItem);
            var charge = ReadForce(fields, source, defaults.Charge, ChargeSpeedItem, ChargeUpwardRatioItem, ChargeWeightFactorItem);
            try
            {
                return new KnockbackSpec(
                    explosion,
                    melee,
                    charge,
                    fields.ReadFloat(MinSpeedItem, defaults.MinSpeed),
                    fields.ReadFloat(MinAirSecondsItem, defaults.MinAirSeconds),
                    fields.ReadFloat(MaxAirSecondsItem, defaults.MaxAirSeconds),
                    fields.ReadFloat(AirControlItem, defaults.AirControl),
                    fields.ReadFloat(GravityItem, defaults.Gravity),
                    fields.ReadFloat(RecoverSecondsItem, defaults.RecoverSeconds),
                    fields.ReadFloat(LandingSnapRadiusItem, defaults.LandingSnapRadius),
                    fields.ReadFloat(PlayerWeightItem, defaults.PlayerWeight),
                    fields.ReadFloat(EnemyWeightItem, defaults.EnemyWeight));
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, _knockbackKind, KnockbackItemNames, e);
            }
        }

        public static NetBotSpec ParseBots(string text, string sourceName)
        {
            var source = SourceOf(sourceName, _botsKind);
            var fields = Fields.Read(text, source, _botsKind, BotItemNames);
            var defaults = NetBotSpec.Default;
            var brain = defaults.Brain;
            var input = defaults.Input;

            // 道の角で止まる距離と行き先に着いたとみなす距離が違うと、手前で止まった bot がいつまでも着かずに行き詰まるので、1 つの項目で両方を決める
            var arrivalDistance = fields.ReadFloat(ArrivalDistanceItem, input.ArrivalDistance);
            var archetype = fields.ReadEnum(ArchetypeItem, brain.Archetype);
            float? staggerResistance = fields.Has(StaggerResistanceItem) ? fields.ReadFloat(StaggerResistanceItem, brain.StaggerResistance) : (float?)null;
            EnemySpec readBrain;
            try
            {
                readBrain = new EnemySpec(
                    fields.ReadFloat(SightRangeItem, brain.SightRange),
                    fields.ReadFloat(AttackRangeItem, brain.AttackRange),
                    fields.ReadFloat(LoseSightSecondsItem, brain.LoseSightSeconds),
                    fields.ReadFloat(SearchSecondsItem, brain.SearchSeconds),
                    fields.ReadFloat(ReactionSecondsItem, brain.ReactionSeconds),
                    fields.ReadFloat(OpeningSpreadDegreesItem, brain.OpeningSpreadDegrees),
                    fields.ReadFloat(SettledSpreadDegreesItem, brain.SettledSpreadDegrees),
                    fields.ReadInt(OpeningShotsItem, brain.OpeningShots),
                    fields.ReadFloat(CoverHideSecondsItem, brain.CoverHideSeconds),
                    fields.ReadFloat(CoverPeekSecondsItem, brain.CoverPeekSeconds),
                    fields.ReadFloat(CoverHealthFractionItem, brain.CoverHealthFraction),
                    archetype,
                    staggerResistance: staggerResistance,
                    // 省いたときは 0 を渡し、EnemySpec に読んだ攻撃距離へそろえさせる (既定の FireRange を使うと攻撃距離だけ変えたデータで食い違う)
                    fireRange: fields.ReadFloat(FireRangeItem, 0f),
                    fireToleranceDegrees: fields.ReadFloat(FacingToleranceDegreesItem, brain.FireToleranceDegrees),
                    rangeSpreadDegrees: fields.ReadFloat(RangeSpreadDegreesItem, brain.RangeSpreadDegrees),
                    arrivalDistance: arrivalDistance,
                    sprintRatio: fields.ReadFloat(SprintRatioItem, brain.SprintRatio),
                    crouchRatio: fields.ReadFloat(CrouchRatioItem, brain.CrouchRatio),
                    recoilControl: fields.ReadFloat(RecoilControlItem, brain.RecoilControl));
            }
            catch (ArgumentException e) when (e.ParamName == FireToleranceDegreesItem)
            {
                throw RejectedItem(source, _botsKind, FacingToleranceDegreesItem, e);
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, _botsKind, BotItemNames, e);
            }

            try
            {
                return new NetBotSpec(
                    readBrain,
                    new BotInputSpec(
                        fields.ReadFloat(MaxTurnDegreesPerSecondItem, input.MaxTurnDegreesPerSecond),
                        fields.ReadFloat(AimErrorPerMeterItem, input.AimErrorPerMeter),
                        fields.ReadFloat(AimErrorRefreshSecondsItem, input.AimErrorRefreshSeconds),
                        fields.ReadFloat(FireToleranceDegreesItem, input.FireToleranceDegrees),
                        arrivalDistance,
                        fields.ReadFloat(SprintDistanceItem, input.SprintDistance),
                        fields.ReadFloat(SprintMaxAngleDegreesItem, input.SprintMaxAngleDegrees),
                        fields.ReadBool(CrouchWhenHoldingItem, input.CrouchWhenHolding)),
                    fields.ReadFloat(NavSampleRadiusItem, defaults.NavSampleRadius),
                    fields.ReadFloat(RepathSecondsItem, defaults.RepathSeconds),
                    fields.ReadFloat(RepathDistanceItem, defaults.RepathDistance),
                    fields.ReadFloat(StuckSecondsItem, defaults.StuckSeconds),
                    fields.ReadFloat(StuckProgressItem, defaults.StuckProgress),
                    fields.ReadFloat(RallyRouteRadiusFractionItem, defaults.RallyRouteRadiusFraction),
                    fields.ReadFloat(RallyRouteAngleStepDegreesItem, defaults.RallyRouteAngleStepDegrees),
                    fields.ReadFloat(RallyMoveRadiusFractionItem, defaults.RallyMoveRadiusFraction));
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, _botsKind, BotItemNames, e);
            }
        }

        private static GrenadeSpec ReadGrenade(ServerDataJson.Fields fields)
        {
            var defaults = GrenadeSpec.Default;
            var id = fields.ReadString(GrenadeIdItem, defaults.Id);
            var nameKey = fields.ReadString(GrenadeNameKeyItem, defaults.NameKey);
            var fuseSeconds = fields.ReadFloat(FuseSecondsItem, defaults.FuseSeconds);
            var throwSeconds = fields.ReadFloat(ThrowSecondsItem, defaults.ThrowSeconds);
            var throwSpeed = fields.ReadFloat(ThrowSpeedItem, defaults.ThrowSpeed);
            var throwLiftSpeed = fields.ReadFloat(ThrowLiftSpeedItem, defaults.ThrowLiftSpeed);
            var stickiness = fields.ReadEnum(StickinessItem, defaults.Stickiness);
            var rollDamping = fields.ReadFloat(RollDampingItem, defaults.RollDamping);
            var bodyRadius = fields.ReadFloat(BodyRadiusItem, defaults.BodyRadius);
            var mass = fields.ReadFloat(MassItem, defaults.Mass);
            var bounciness = fields.ReadFloat(BouncinessItem, defaults.Bounciness);
            var friction = fields.ReadFloat(FrictionItem, defaults.Friction);
            var angularDamping = fields.ReadFloat(AngularDampingItem, defaults.AngularDamping);
            var releaseDistance = fields.ReadFloat(ReleaseDistanceItem, defaults.ReleaseDistance);
            var impactFields = fields.Object(GrenadeImpactItem, WeaponSpecJson.GrenadeImpactItemNames);
            try
            {
                if (impactFields == null)
                {
                    return new GrenadeSpec(
                        fuseSeconds,
                        throwSeconds,
                        throwSpeed,
                        throwLiftSpeed,
                        fields.ReadInt(MaxDamageItem, defaults.MaxDamage),
                        fields.ReadFloat(DamageRadiusItem, defaults.DamageRadius),
                        fields.ReadFloat(FullDamageRadiusItem, defaults.FullDamageRadius),
                        fields.ReadInt(StructureDamageAtCenterItem, defaults.StructureDamageAtCenter),
                        id,
                        nameKey,
                        stickiness,
                        rollDamping,
                        bodyRadius,
                        mass,
                        bounciness,
                        friction,
                        angularDamping,
                        releaseDistance);
                }

                foreach (var flat in _flatExplosionItems)
                {
                    if (fields.Has(flat))
                    {
                        throw fields.Rejected(
                            fields.ItemPath(flat), $"is also given by '{fields.ItemPath(GrenadeImpactItem)}'; write the explosion in '{GrenadeImpactItem}.{WeaponSpecJson.ExplosionItem}'");
                    }
                }

                return new GrenadeSpec(
                    id,
                    nameKey,
                    WeaponSpecJson.ReadImpact(impactFields),
                    fuseSeconds,
                    throwSeconds,
                    throwSpeed,
                    throwLiftSpeed,
                    stickiness,
                    rollDamping,
                    bodyRadius,
                    mass,
                    bounciness,
                    friction,
                    angularDamping,
                    releaseDistance);
            }
            catch (ArgumentException e)
            {
                var item = e.ParamName != null && Contains(GrenadeItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : null;
                throw fields.Rejected(item ?? (fields.Path.Length == 0 ? null : fields.Path), e.Message, e);
            }
        }

        // 種類の並びは入れ子を持つので ServerDataJson で読み、呼ぶ側には今までどおり TuningJsonException で知らせる。
        // 値の検査で断ったときは、元の ArgumentException を中に残す。
        private static T Translated<T>(Func<T> read)
        {
            try
            {
                return read();
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static bool HasMember(GameRulesJson.Node node, string name)
        {
            foreach (var member in node.Members)
            {
                if (string.Equals(member.Key, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // KnockbackForce の引数名 (speed など) は種類を持たないので、どの種類の項目かをここで添える。
        private static KnockbackForce ReadForce(
            Fields fields, string source, KnockbackForce defaults, string speedItem, string upwardRatioItem, string weightFactorItem)
        {
            try
            {
                return new KnockbackForce(
                    fields.ReadFloat(speedItem, defaults.Speed),
                    fields.ReadFloat(upwardRatioItem, defaults.UpwardRatio),
                    fields.ReadFloat(weightFactorItem, defaults.WeightFactor));
            }
            catch (ArgumentException e)
            {
                string item;
                switch (e.ParamName)
                {
                    case "speed":
                        item = speedItem;
                        break;
                    case "upwardRatio":
                        item = upwardRatioItem;
                        break;
                    case "weightFactor":
                        item = weightFactorItem;
                        break;
                    default:
                        item = null;
                        break;
                }

                var where = item == null ? string.Empty : $" item '{item}'";
                throw new TuningJsonException(source, item, $"{_knockbackKind} '{source}'{where} is invalid: {e.Message}", e);
            }
        }

        internal static string SourceOf(string sourceName, string kind)
        {
            return string.IsNullOrWhiteSpace(sourceName) ? "(unnamed " + kind + ")" : sourceName;
        }

        // Spec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        // 内側の ExplosionDamage など、項目に無い名前で投げられたときは項目に結び付けない。
        internal static TuningJsonException Rejected(string source, string kind, IReadOnlyList<string> known, ArgumentException e)
        {
            string item = null;
            foreach (var name in known)
            {
                if (string.Equals(name, e.ParamName, StringComparison.Ordinal))
                {
                    item = name;
                    break;
                }
            }

            return RejectedItem(source, kind, item, e);
        }

        private static TuningJsonException RejectedItem(string source, string kind, string item, ArgumentException e)
        {
            var where = item == null ? string.Empty : $" item '{item}'";
            return new TuningJsonException(source, item, $"{kind} '{source}'{where} is invalid: {e.Message}", e);
        }

        internal sealed class Fields
        {
            private static readonly char[] _nonIntegerMarks = { '.', 'e', 'E' };

            private readonly string _source;
            private readonly string _kind;
            private readonly Dictionary<string, GameRulesJson.Node> _values =
                new Dictionary<string, GameRulesJson.Node>(StringComparer.Ordinal);

            private Fields(string source, string kind)
            {
                _source = source;
                _kind = kind;
            }

            public static Fields Read(string text, string source, string kind, IReadOnlyList<string> known)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                GameRulesJson.Node root;
                try
                {
                    root = new GameRulesJson.Parser(text).ParseDocument();
                }
                catch (FormatException e)
                {
                    throw new TuningJsonException(source, null, $"{kind} '{source}' is not valid JSON: {e.Message}", e);
                }

                if (root.Kind != GameRulesJson.NodeKind.Object)
                {
                    throw new TuningJsonException(
                        source, null, $"{kind} '{source}': the top level must be an object, but is {root.Describe()} ({root.Position})");
                }

                var fields = new Fields(source, kind);
                var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
                foreach (var member in root.Members)
                {
                    if (!knownSet.Contains(member.Key))
                    {
                        throw new TuningJsonException(
                            source,
                            member.Key,
                            $"{kind} '{source}': unknown item '{member.Key}' ({member.Value.Position}); "
                            + $"known items are {string.Join(", ", known)}");
                    }

                    fields._values.Add(member.Key, member.Value);
                }

                return fields;
            }

            public float ReadFloat(string name, float fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                Require(node, name, "a number");

                // GameRulesJson と同じく double を経ずに float へ直す。無限大になる大きすぎる値は Spec の検査が項目名付きで断る。
                try
                {
                    return float.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                catch (OverflowException e)
                {
                    throw Invalid(name, node, "is too large for a 32-bit float", e);
                }
            }

            public int ReadInt(string name, int fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                Require(node, name, "a whole number");
                if (node.Text.IndexOfAny(_nonIntegerMarks) >= 0)
                {
                    throw Invalid(name, node, "must be a whole number without a fraction or exponent");
                }

                if (!int.TryParse(node.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                {
                    throw Invalid(name, node, "is out of the 32-bit integer range");
                }

                return value;
            }

            public bool Has(string name)
            {
                return _values.ContainsKey(name);
            }

            public T ReadEnum<T>(string name, T fallback)
                where T : struct, Enum
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                if (node.Kind != GameRulesJson.NodeKind.String)
                {
                    throw Invalid(name, node, $"must be a string, but is {node.Describe()}");
                }

                // 数を名前として書いたもの ("1") を Enum.TryParse は通すので、名前だけを受け付ける。
                foreach (var value in Enum.GetNames(typeof(T)))
                {
                    if (string.Equals(value, node.Text, StringComparison.Ordinal))
                    {
                        return Enum.Parse<T>(value);
                    }
                }

                throw Invalid(name, node, $"must be one of {string.Join(", ", Enum.GetNames(typeof(T)))}, but is \"{node.Text}\"");
            }

            public bool ReadBool(string name, bool fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                if (node.Kind != GameRulesJson.NodeKind.True && node.Kind != GameRulesJson.NodeKind.False)
                {
                    throw Invalid(name, node, $"must be true or false, but is {node.Describe()}");
                }

                return node.Kind == GameRulesJson.NodeKind.True;
            }

            private GameRulesJson.Node Find(string name)
            {
                if (!_values.TryGetValue(name, out var node))
                {
                    return null;
                }

                if (node.Kind == GameRulesJson.NodeKind.Null)
                {
                    throw Invalid(name, node, "must not be null; leave the item out to use the default");
                }

                return node;
            }

            private void Require(GameRulesJson.Node node, string name, string expected)
            {
                if (node.Kind != GameRulesJson.NodeKind.Number)
                {
                    throw Invalid(name, node, $"must be {expected}, but is {node.Describe()}");
                }
            }

            private TuningJsonException Invalid(string name, GameRulesJson.Node node, string problem, Exception inner = null)
            {
                return new TuningJsonException(_source, name, $"{_kind} '{_source}' item '{name}' {problem} ({node.Position})", inner);
            }
        }
    }

    public sealed class TuningJsonException : Exception
    {
        public TuningJsonException()
        {
        }

        public TuningJsonException(string message)
            : base(message)
        {
        }

        public TuningJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public TuningJsonException(string sourceName, string item, string message, Exception innerException = null)
            : base(message, innerException)
        {
            SourceName = sourceName;
            Item = item;
        }

        public string SourceName { get; }

        public string Item { get; }
    }
}
