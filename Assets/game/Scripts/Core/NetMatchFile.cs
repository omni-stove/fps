using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// Unity 抜きのサーバーが <c>-rules &lt;名前&gt;</c> で読む、1 つの試合のルールのファイル (<c>server-data/rules/&lt;名前&gt;.json</c>)。
    /// Unity の Editor が NetMatch.prefab とルールのアセットから書き出す (<see cref="Write"/>) ので、サーバーの試合は Unity の専用サーバーと同じ値で動く。
    /// </summary>
    public sealed class NetMatchFile
    {
        public const string Extension = ".json";

        public const string NameItem = "name";
        public const string RulesItem = "rules";
        public const string MatchItem = "match";
        public const string LoadoutItem = "loadout";
        public const string CatalogItem = "catalog";
        public const string WeaponsItem = "weapons";
        public const string SwitchSecondsItem = "switchSeconds";
        public const string SlotsItem = "slots";

        public const string ScoreLimitItem = "scoreLimit";
        public const string TimeLimitSecondsItem = "timeLimitSeconds";
        public const string RespawnSecondsItem = "respawnSeconds";
        public const string RestartDelaySecondsItem = "restartDelaySeconds";
        public const string MaxHealthItem = "maxHealth";
        public const string MinEnemySpawnDistanceItem = "minEnemySpawnDistance";
        public const string ReconnectGraceSecondsItem = "reconnectGraceSeconds";
        public const string MaxShotOriginOffsetItem = "maxShotOriginOffset";
        public const string DominationScoreLimitItem = "dominationScoreLimit";
        public const string CaptureSecondsItem = "captureSeconds";
        public const string PointsPerSecondItem = "pointsPerSecond";
        public const string MaxCapturersItem = "maxCapturers";

        public const string MotorItem = "motor";
        public const string WalkSpeedItem = "walkSpeed";
        public const string SprintSpeedItem = "sprintSpeed";
        public const string JumpHeightItem = "jumpHeight";
        public const string GravityItem = "gravity";
        public const string AirControlItem = "airControl";
        public const string CrouchSpeedItem = "crouchSpeed";
        public const string SafeFallSpeedItem = "safeFallSpeed";
        public const string LethalFallSpeedItem = "lethalFallSpeed";
        public const string DodgeDistanceItem = "dodgeDistance";
        public const string DodgeSecondsItem = "dodgeSeconds";
        public const string VaultMaxHeightItem = "vaultMaxHeight";
        public const string MantleMaxHeightItem = "mantleMaxHeight";
        public const string MantleSpeedItem = "mantleSpeed";
        public const string StandingHeightItem = "standingHeight";
        public const string StandingCenterYItem = "standingCenterY";
        public const string StandingEyeYItem = "standingEyeY";
        public const string CrouchHeightItem = "crouchHeight";
        public const string RadiusItem = "radius";
        public const string SlopeLimitDegreesItem = "slopeLimitDegrees";
        public const string StepOffsetItem = "stepOffset";
        public const string SkinWidthItem = "skinWidth";

        private const string _kind = "match rules JSON";

        private static readonly string[] _topItems = { NameItem, RulesItem, MatchItem, MotorItem, LoadoutItem, CatalogItem, WeaponsItem };

        private static readonly string[] _motorItems =
        {
            WalkSpeedItem, SprintSpeedItem, JumpHeightItem, GravityItem, AirControlItem, CrouchSpeedItem, SafeFallSpeedItem, LethalFallSpeedItem,
            DodgeDistanceItem, DodgeSecondsItem, VaultMaxHeightItem, MantleMaxHeightItem, MantleSpeedItem, StandingHeightItem, StandingCenterYItem,
            StandingEyeYItem, CrouchHeightItem, RadiusItem, SlopeLimitDegreesItem, StepOffsetItem, SkinWidthItem,
        };
        private static readonly string[] _loadoutItems = { NameItem, SwitchSecondsItem, SlotsItem, WeaponsItem };
        private static readonly string[] _catalogItems = { NameItem, WeaponsItem };

        private static readonly string[] _matchItems =
        {
            ScoreLimitItem, TimeLimitSecondsItem, RespawnSecondsItem, RestartDelaySecondsItem, MaxHealthItem, MinEnemySpawnDistanceItem,
            ReconnectGraceSecondsItem, MaxShotOriginOffsetItem, DominationScoreLimitItem, CaptureSecondsItem, PointsPerSecondItem,
            MaxCapturersItem,
        };

        public NetMatchFile(
            string name,
            GameRules rules,
            NetMatchSettings settings,
            string loadoutName,
            LoadoutRule loadoutRule,
            IReadOnlyList<WeaponSpec> loadoutWeapons,
            float switchSeconds,
            string catalogName,
            WeaponCatalog catalog,
            NetMotorSettings motor = null)
        {
            Motor = motor ?? NetMotorSettings.Default;
            Name = name ?? string.Empty;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            LoadoutName = loadoutName ?? string.Empty;
            LoadoutRule = loadoutRule ?? throw new ArgumentNullException(nameof(loadoutRule));
            LoadoutWeapons = new List<WeaponSpec>(loadoutWeapons ?? throw new ArgumentNullException(nameof(loadoutWeapons))).AsReadOnly();
            SwitchSeconds = switchSeconds;
            CatalogName = catalog == null ? null : catalogName ?? string.Empty;
            Catalog = catalog;

            // 作れないロードアウトを試合の開始まで持ち越さないよう、ここで一度組んで確かめる
            CreateLoadout();
        }

        public string Name { get; }

        public GameRules Rules { get; }

        public NetMatchSettings Settings { get; }

        public NetMotorSettings Motor { get; }

        public string LoadoutName { get; }

        public LoadoutRule LoadoutRule { get; }

        public IReadOnlyList<WeaponSpec> LoadoutWeapons { get; }

        public float SwitchSeconds { get; }

        public string CatalogName { get; }

        public WeaponCatalog Catalog { get; }

        /// <summary>試合ごとに作り直すのは、<see cref="ServerLoadout"/> がプレイヤーごとの選んだ並びと連射の制限を持つため。</summary>
        public ServerLoadout CreateLoadout()
        {
            return new ServerLoadout(LoadoutRule, LoadoutWeapons, Catalog, SwitchSeconds);
        }

        public static NetMatchFile Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            var root = ServerDataJson.ParseDocument(text, source, _kind);
            var top = new ServerDataJson.Fields(source, _kind, root, string.Empty, _topItems);

            var rulesNode = top.Find(RulesItem);
            var rules = rulesNode == null ? GameRules.Default : GameRulesJson.FromNode(rulesNode, source, RulesItem + ".");
            var settings = ReadSettings(top.Object(MatchItem, _matchItems));
            var motor = ReadMotor(top.Object(MotorItem, _motorItems));
            var weapons = ReadWeapons(top);

            var loadout = top.Object(LoadoutItem, _loadoutItems)
                ?? throw new ServerDataJsonException(source, LoadoutItem, $"{_kind} '{source}' has no item '{LoadoutItem}'");
            var rule = ReadRule(loadout);
            var loadoutWeapons = Pick(loadout, weapons);
            var switchSeconds = loadout.ReadFloat(SwitchSecondsItem, 0f);

            var catalogFields = top.Object(CatalogItem, _catalogItems);
            WeaponCatalog catalog = null;
            if (catalogFields != null)
            {
                try
                {
                    catalog = new WeaponCatalog(Pick(catalogFields, weapons));
                }
                catch (ArgumentException e)
                {
                    throw catalogFields.Rejected(CatalogItem, e.Message, e);
                }
            }

            try
            {
                return new NetMatchFile(
                    top.ReadString(NameItem, string.Empty),
                    rules,
                    settings,
                    loadout.ReadString(NameItem, string.Empty),
                    rule,
                    loadoutWeapons,
                    switchSeconds,
                    catalogFields?.ReadString(NameItem, string.Empty),
                    catalog,
                    motor);
            }
            catch (ArgumentException e)
            {
                throw loadout.Rejected(LoadoutItem, e.Message, e);
            }
        }

        public static string Write(NetMatchFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var writer = new ServerDataJson.Writer();
            writer.String(NameItem, file.Name);
            writer.Embed(RulesItem, GameRulesJson.Write(file.Rules));

            var s = file.Settings;
            writer.BeginObject(MatchItem);
            writer.Int(ScoreLimitItem, s.ScoreLimit);
            writer.Float(TimeLimitSecondsItem, s.TimeLimitSeconds);
            writer.Float(RespawnSecondsItem, s.RespawnSeconds);
            writer.Float(RestartDelaySecondsItem, s.RestartDelaySeconds);
            writer.Int(MaxHealthItem, s.MaxHealth);
            writer.Float(MinEnemySpawnDistanceItem, s.MinEnemySpawnDistance);
            writer.Float(ReconnectGraceSecondsItem, s.ReconnectGraceSeconds);
            writer.Float(MaxShotOriginOffsetItem, s.MaxShotOriginOffset);
            writer.Int(DominationScoreLimitItem, s.DominationScoreLimit);
            writer.Float(CaptureSecondsItem, s.CaptureSeconds);
            writer.Float(PointsPerSecondItem, s.PointsPerSecond);
            writer.Int(MaxCapturersItem, s.MaxCapturers);
            writer.EndObject();

            var motor = file.Motor.Motor;
            var stance = file.Motor.Stance;
            var mover = file.Motor.Mover;
            writer.BeginObject(MotorItem);
            writer.Float(WalkSpeedItem, motor.WalkSpeed);
            writer.Float(SprintSpeedItem, motor.SprintSpeed);
            writer.Float(JumpHeightItem, motor.JumpHeight);
            writer.Float(GravityItem, motor.Gravity);
            writer.Float(AirControlItem, motor.AirControl);
            writer.Float(CrouchSpeedItem, motor.CrouchSpeed);
            writer.Float(SafeFallSpeedItem, motor.SafeFallSpeed);
            writer.Float(LethalFallSpeedItem, motor.LethalFallSpeed);
            writer.Float(DodgeDistanceItem, motor.DodgeDistance);
            writer.Float(DodgeSecondsItem, motor.DodgeSeconds);
            writer.Float(VaultMaxHeightItem, motor.VaultMaxHeight);
            writer.Float(MantleMaxHeightItem, motor.MantleMaxHeight);
            writer.Float(MantleSpeedItem, motor.MantleSpeed);
            writer.Float(StandingHeightItem, stance.Standing.Height);
            writer.Float(StandingCenterYItem, stance.Standing.CenterY);
            writer.Float(StandingEyeYItem, stance.Standing.EyeY);
            writer.Float(CrouchHeightItem, stance.Crouched.Height);
            writer.Float(RadiusItem, stance.Radius);
            writer.Float(SlopeLimitDegreesItem, mover.SlopeLimitDegrees);
            writer.Float(StepOffsetItem, mover.StepOffset);
            writer.Float(SkinWidthItem, mover.SkinWidth);
            writer.EndObject();

            writer.BeginObject(LoadoutItem);
            writer.String(NameItem, file.LoadoutName);
            writer.Float(SwitchSecondsItem, file.SwitchSeconds);
            writer.BeginArray(SlotsItem);
            foreach (var slot in file.LoadoutRule.Slots)
            {
                var names = new List<string>();
                foreach (var category in slot)
                {
                    names.Add(ServerDataJson.Writer.Quote(category.ToString()));
                }

                writer.Element("[" + string.Join(", ", names) + "]");
            }

            writer.EndArray();
            writer.StringArray(WeaponsItem, Ids(file.LoadoutWeapons));
            writer.EndObject();

            var all = new List<WeaponSpec>(file.LoadoutWeapons);
            if (file.Catalog != null)
            {
                writer.BeginObject(CatalogItem);
                writer.String(NameItem, file.CatalogName);
                writer.StringArray(WeaponsItem, Ids(file.Catalog.All));
                writer.EndObject();
                foreach (var weapon in file.Catalog.All)
                {
                    if (!ContainsId(all, weapon.Id))
                    {
                        all.Add(weapon);
                    }
                }
            }

            writer.BeginArray(WeaponsItem);
            foreach (var weapon in all)
            {
                writer.BeginElement();
                WeaponSpecJson.Write(writer, weapon);
                writer.EndObject();
            }

            writer.EndArray();
            return writer.Finish();
        }

        private static NetMatchSettings ReadSettings(ServerDataJson.Fields match)
        {
            if (match == null)
            {
                return NetMatchSettings.Default;
            }

            var d = NetMatchSettings.Default;
            try
            {
                return new NetMatchSettings(
                    match.ReadInt(ScoreLimitItem, d.ScoreLimit),
                    match.ReadFloat(TimeLimitSecondsItem, d.TimeLimitSeconds),
                    match.ReadFloat(RespawnSecondsItem, d.RespawnSeconds),
                    match.ReadFloat(RestartDelaySecondsItem, d.RestartDelaySeconds),
                    match.ReadInt(MaxHealthItem, d.MaxHealth),
                    match.ReadFloat(MinEnemySpawnDistanceItem, d.MinEnemySpawnDistance),
                    match.ReadFloat(ReconnectGraceSecondsItem, d.ReconnectGraceSeconds),
                    match.ReadFloat(MaxShotOriginOffsetItem, d.MaxShotOriginOffset),
                    match.ReadInt(DominationScoreLimitItem, d.DominationScoreLimit),
                    match.ReadFloat(CaptureSecondsItem, d.CaptureSeconds),
                    match.ReadFloat(PointsPerSecondItem, d.PointsPerSecond),
                    match.ReadInt(MaxCapturersItem, d.MaxCapturers));
            }
            catch (ArgumentException e)
            {
                // NetMatchSettings の引数名は項目名と同じ
                throw match.Rejected(e.ParamName == null ? match.Path : match.ItemPath(e.ParamName), e.Message, e);
            }
        }

        private static NetMotorSettings ReadMotor(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                return NetMotorSettings.Default;
            }

            var d = NetMotorSettings.Default;
            var dm = d.Motor;
            var ds = d.Stance;
            var dc = d.Mover;
            try
            {
                return new NetMotorSettings(
                    new MotorSpec(
                        fields.ReadFloat(WalkSpeedItem, dm.WalkSpeed),
                        fields.ReadFloat(SprintSpeedItem, dm.SprintSpeed),
                        fields.ReadFloat(JumpHeightItem, dm.JumpHeight),
                        fields.ReadFloat(GravityItem, dm.Gravity),
                        fields.ReadFloat(AirControlItem, dm.AirControl),
                        fields.ReadFloat(CrouchSpeedItem, dm.CrouchSpeed),
                        fields.ReadFloat(SafeFallSpeedItem, dm.SafeFallSpeed),
                        fields.ReadFloat(LethalFallSpeedItem, dm.LethalFallSpeed),
                        fields.ReadFloat(DodgeDistanceItem, dm.DodgeDistance),
                        fields.ReadFloat(DodgeSecondsItem, dm.DodgeSeconds),
                        fields.ReadFloat(VaultMaxHeightItem, dm.VaultMaxHeight),
                        fields.ReadFloat(MantleMaxHeightItem, dm.MantleMaxHeight),
                        fields.ReadFloat(MantleSpeedItem, dm.MantleSpeed)),
                    new StanceGeometry(
                        fields.ReadFloat(StandingHeightItem, ds.Standing.Height),
                        fields.ReadFloat(StandingCenterYItem, ds.Standing.CenterY),
                        fields.ReadFloat(StandingEyeYItem, ds.Standing.EyeY),
                        fields.ReadFloat(CrouchHeightItem, ds.Crouched.Height),
                        fields.ReadFloat(RadiusItem, ds.Radius)),
                    new CapsuleMoverSpec(
                        fields.ReadFloat(SlopeLimitDegreesItem, dc.SlopeLimitDegrees),
                        fields.ReadFloat(StepOffsetItem, dc.StepOffset),
                        fields.ReadFloat(SkinWidthItem, dc.SkinWidth),
                        dc.LayerMask));
            }
            catch (ArgumentException e)
            {
                // MotorSpec・StanceGeometry・CapsuleMoverSpec の引数名は項目名と同じ
                throw fields.Rejected(e.ParamName == null ? fields.Path : fields.ItemPath(e.ParamName), e.Message, e);
            }
        }

        private static Dictionary<string, WeaponSpec> ReadWeapons(ServerDataJson.Fields top)
        {
            var weapons = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
            var nodes = top.Array(WeaponsItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var fields = top.Element(nodes[i], WeaponsItem, i, WeaponSpecJson.ItemNames);
                var spec = WeaponSpecJson.Read(fields);
                if (weapons.ContainsKey(spec.Id))
                {
                    throw fields.Rejected(fields.ItemPath(WeaponSpecJson.IdItem), "weapon id " + spec.Id + " is listed more than once");
                }

                weapons.Add(spec.Id, spec);
            }

            return weapons;
        }

        private static LoadoutRule ReadRule(ServerDataJson.Fields loadout)
        {
            var slots = new List<List<WeaponCategory>>();
            var nodes = loadout.Array(SlotsItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var item = loadout.ElementPath(SlotsItem, i);
                var node = nodes[i];
                if (node.Kind != GameRulesJson.NodeKind.Array)
                {
                    throw loadout.Invalid(item, node, "must be an array of weapon categories, but is " + node.Describe());
                }

                var slot = new List<WeaponCategory>();
                for (var j = 0; j < node.Elements.Count; j++)
                {
                    slot.Add(loadout.ToEnum<WeaponCategory>(item + "[" + j + "]", node.Elements[j]));
                }

                slots.Add(slot);
            }

            try
            {
                return new LoadoutRule(slots);
            }
            catch (ArgumentException e)
            {
                throw loadout.Rejected(loadout.ItemPath(SlotsItem), e.Message, e);
            }
        }

        private static List<WeaponSpec> Pick(ServerDataJson.Fields owner, Dictionary<string, WeaponSpec> weapons)
        {
            var picked = new List<WeaponSpec>();
            var nodes = owner.Array(WeaponsItem);
            for (var i = 0; i < nodes.Count; i++)
            {
                var item = owner.ElementPath(WeaponsItem, i);
                var id = owner.ToText(item, nodes[i]);
                if (!weapons.TryGetValue(id, out var spec))
                {
                    throw owner.Invalid(item, nodes[i], "names weapon " + id + ", which is not in the top-level '" + WeaponsItem + "'");
                }

                picked.Add(spec);
            }

            return picked;
        }

        private static IEnumerable<string> Ids(IEnumerable<WeaponSpec> weapons)
        {
            foreach (var weapon in weapons)
            {
                yield return weapon.Id;
            }
        }

        private static bool ContainsId(List<WeaponSpec> weapons, string id)
        {
            foreach (var weapon in weapons)
            {
                if (string.Equals(weapon.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
