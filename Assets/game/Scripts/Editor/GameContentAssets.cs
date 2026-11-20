using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    public static class GameContentAssets
    {
        public const string MenuPath = "Shooter/Create Game Content";
        public const string LogTag = "[GameContentAssets]";
        public const string ArgumentName = "-gameContent";
        public const string FileName = "GameContent.asset";

        // パッケージの同梱物はゲームが差し替える前の仮のものなので、Assets/ の下のアセットだけを候補にする。
        public const string SearchFolder = "Assets";

        public const string TuningFolder = "Tuning";

        private static readonly Slot[] _slots =
        {
            new Slot("Rules", typeof(GameRulesDefinition), c => c.Rules, (c, v) => c.Rules = (GameRulesDefinition)v),
            new Slot("Motor", typeof(MotorSettings), c => c.Motor, (c, v) => c.Motor = (MotorSettings)v),
            new Slot("WeaponCatalog", typeof(WeaponCatalogAsset), c => c.WeaponCatalog, (c, v) => c.WeaponCatalog = (WeaponCatalogAsset)v),
            new Slot("Loadout", typeof(LoadoutDefinition), c => c.Loadout, (c, v) => c.Loadout = (LoadoutDefinition)v),
            new Slot("AbilityCatalog", typeof(AbilityCatalogAsset), c => c.AbilityCatalog, (c, v) => c.AbilityCatalog = (AbilityCatalogAsset)v),
            new Slot("TeamDeathmatch", typeof(TeamDeathmatchDefinition), c => c.TeamDeathmatch, (c, v) => c.TeamDeathmatch = (TeamDeathmatchDefinition)v),
            new Slot("Domination", typeof(DominationDefinition), c => c.Domination, (c, v) => c.Domination = (DominationDefinition)v),
            new Slot("Hardpoint", typeof(HardpointDefinition), c => c.Hardpoint, (c, v) => c.Hardpoint = (HardpointDefinition)v),
            new Slot("KillConfirmed", typeof(KillConfirmedDefinition), c => c.KillConfirmed, (c, v) => c.KillConfirmed = (KillConfirmedDefinition)v),
            new Slot("SearchAndDestroy", typeof(SearchAndDestroyDefinition), c => c.SearchAndDestroy, (c, v) => c.SearchAndDestroy = (SearchAndDestroyDefinition)v),
            new Slot("CaptureTheFlag", typeof(CaptureTheFlagDefinition), c => c.CaptureTheFlag, (c, v) => c.CaptureTheFlag = (CaptureTheFlagDefinition)v),
            new Slot("Horde", typeof(HordeDefinition), c => c.Horde, (c, v) => c.Horde = (HordeDefinition)v),
            new Slot("GunGameLadder", typeof(GunGameLadderDefinition), c => c.GunGameLadder, (c, v) => c.GunGameLadder = (GunGameLadderDefinition)v),
            new Slot("FreeForAll", typeof(FreeForAllDefinition), c => c.FreeForAll, (c, v) => c.FreeForAll = (FreeForAllDefinition)v),
            new Slot("Guardian", typeof(GuardianDefinition), c => c.Guardian, (c, v) => c.Guardian = (GuardianDefinition)v),
            new Slot("Execution", typeof(ExecutionDefinition), c => c.Execution, (c, v) => c.Execution = (ExecutionDefinition)v),
            new Slot("BattleRoyale", typeof(BattleRoyaleDefinition), c => c.BattleRoyale, (c, v) => c.BattleRoyale = (BattleRoyaleDefinition)v),
            new Slot("MenuScenes", typeof(MenuSceneSettings), c => c.MenuScenes, (c, v) => c.MenuScenes = (MenuSceneSettings)v),
            new Slot("Presentation", typeof(PresentationCatalog), c => c.Presentation, (c, v) => c.Presentation = (PresentationCatalog)v),
            new Slot("Viewmodels", typeof(ViewmodelCatalog), c => c.Viewmodels, (c, v) => c.Viewmodels = (ViewmodelCatalog)v),
            new Slot("WeaponExtras", typeof(WeaponExtrasCatalog), c => c.WeaponExtras, (c, v) => c.WeaponExtras = (WeaponExtrasCatalog)v),
            new Slot("HitFeedback", typeof(HitFeedbackCatalog), c => c.HitFeedback, (c, v) => c.HitFeedback = (HitFeedbackCatalog)v),
            new Slot("DamageFeedback", typeof(DamageFeedbackCatalog), c => c.DamageFeedback, (c, v) => c.DamageFeedback = (DamageFeedbackCatalog)v),
            new Slot("SurfaceImpacts", typeof(SurfaceImpactCatalog), c => c.SurfaceImpacts, (c, v) => c.SurfaceImpacts = (SurfaceImpactCatalog)v),
            new Slot("Footsteps", typeof(FootstepSurfaceCatalog), c => c.Footsteps, (c, v) => c.Footsteps = (FootstepSurfaceCatalog)v),
            new Slot("Ragdoll", typeof(RagdollCatalog), c => c.Ragdoll, (c, v) => c.Ragdoll = (RagdollCatalog)v),
            new Slot("Suppression", typeof(SuppressionCatalog), c => c.Suppression, (c, v) => c.Suppression = (SuppressionCatalog)v),
            new Slot("CameraShake", typeof(CameraShakeSettings), c => c.CameraShake, (c, v) => c.CameraShake = (CameraShakeSettings)v),
            new Slot("Reticles", typeof(ReticleCatalog), c => c.Reticles, (c, v) => c.Reticles = (ReticleCatalog)v),
            new Slot("StatusEffectVisuals", typeof(StatusEffectVisualCatalog), c => c.StatusEffectVisuals, (c, v) => c.StatusEffectVisuals = (StatusEffectVisualCatalog)v),
            new Slot("FlashOverlay", typeof(FlashOverlayCatalog), c => c.FlashOverlay, (c, v) => c.FlashOverlay = (FlashOverlayCatalog)v),
            new Slot("CosmeticVisuals", typeof(CosmeticVisualCatalog), c => c.CosmeticVisuals, (c, v) => c.CosmeticVisuals = (CosmeticVisualCatalog)v),
            new Slot("StreakIcons", typeof(StreakIconCatalog), c => c.StreakIcons, (c, v) => c.StreakIcons = (StreakIconCatalog)v),
            new Slot("EmoteAnimations", typeof(EmoteCatalog), c => c.EmoteAnimations, (c, v) => c.EmoteAnimations = (EmoteCatalog)v),
            new Slot("VisionVisuals", typeof(VisionVisualCatalog), c => c.VisionVisuals, (c, v) => c.VisionVisuals = (VisionVisualCatalog)v),
        };

        // 調整値の JSON は型が TextAsset で見分けられないので、キットが Resources から読むときのファイル名で選ぶ。
        private static readonly TuningSlot[] _tuningSlots =
        {
            new TuningSlot("GrenadeTuning", KitTuning.GrenadeResourcePath, c => c.GrenadeTuning, (c, v) => c.GrenadeTuning = v),
            new TuningSlot("PingTuning", KitTuning.PingResourcePath, c => c.PingTuning, (c, v) => c.PingTuning = v),
            new TuningSlot("KnockbackTuning", KitTuning.KnockbackResourcePath, c => c.KnockbackTuning, (c, v) => c.KnockbackTuning = v),
            new TuningSlot("InventoryTuning", KitTuning.InventoryResourcePath, c => c.InventoryTuning, (c, v) => c.InventoryTuning = v),
            new TuningSlot("SuppressionTuning", KitTuning.SuppressionResourcePath, c => c.SuppressionTuning, (c, v) => c.SuppressionTuning = v),
            new TuningSlot("SquadTuning", SquadCommandActionInstaller.TuningResourcePath, c => c.SquadTuning, (c, v) => c.SquadTuning = v),
            new TuningSlot("StaggerTuning", StaggerActionInstaller.TuningResourcePath, c => c.StaggerTuning, (c, v) => c.StaggerTuning = v),
            new TuningSlot("MeleeTuning", MeleeActionInstaller.TuningResourcePath, c => c.MeleeTuning, (c, v) => c.MeleeTuning = v),
            new TuningSlot("TraversalTuning", TraversalJson.ResourcePath, c => c.TraversalTuning, (c, v) => c.TraversalTuning = v),
            new TuningSlot("StealthTuning", KitTuning.StealthResourcePath, c => c.StealthTuning, (c, v) => c.StealthTuning = v),
            new TuningSlot("BotsTuning", KitTuning.BotsResourcePath, c => c.BotsTuning, (c, v) => c.BotsTuning = v),
            new TuningSlot("StreaksTuning", StreakJson.ResourcePath, c => c.StreaksTuning, (c, v) => c.StreaksTuning = v),
            new TuningSlot("AimAssistTuning", AimAssist.TuningResourcePath, c => c.AimAssistTuning, (c, v) => c.AimAssistTuning = v),
            new TuningSlot("ProgressionTuning", KitTuning.ProgressionResourcePath, c => c.ProgressionTuning, (c, v) => c.ProgressionTuning = v),
            new TuningSlot("SkillTuning", KitTuning.SkillResourcePath, c => c.SkillTuning, (c, v) => c.SkillTuning = v),
            new TuningSlot("MinimapTuning", MinimapJson.ResourcePath, c => c.MinimapTuning, (c, v) => c.MinimapTuning = v),
            new TuningSlot("AssistTuning", KitTuning.AssistResourcePath, c => c.AssistTuning, (c, v) => c.AssistTuning = v),
            new TuningSlot("RumbleTuning", GamepadRumble.TuningResourcePath, c => c.RumbleTuning, (c, v) => c.RumbleTuning = v),
            new TuningSlot("ChatWordsTuning", ChatWordFilterJson.ResourcePath, c => c.ChatWordsTuning, (c, v) => c.ChatWordsTuning = v),
            new TuningSlot("AchievementsTuning", KitTuning.AchievementsResourcePath, c => c.AchievementsTuning, (c, v) => c.AchievementsTuning = v),
            new TuningSlot("LeaderboardsTuning", LeaderboardJson.ResourcePath, c => c.LeaderboardsTuning, (c, v) => c.LeaderboardsTuning = v),
            new TuningSlot("CosmeticsTuning", CosmeticJson.ResourcePath, c => c.CosmeticsTuning, (c, v) => c.CosmeticsTuning = v),
            new TuningSlot("EmotesTuning", EmoteJson.ResourcePath, c => c.EmotesTuning, (c, v) => c.EmotesTuning = v),
            new TuningSlot("WeatherTuning", WeatherJson.ResourcePath, c => c.WeatherTuning, (c, v) => c.WeatherTuning = v),
            new TuningSlot("TelemetryTuning", TelemetryJson.ResourcePath, c => c.TelemetryTuning, (c, v) => c.TelemetryTuning = v),
            new TuningSlot("InfectedTuning", InfectedJson.ResourcePath, c => c.InfectedTuning, (c, v) => c.InfectedTuning = v),
            new TuningSlot("LootTuning", LootJson.ResourcePath, c => c.LootTuning, (c, v) => c.LootTuning = v),
            new TuningSlot("StashTuning", StashJson.ResourcePath, c => c.StashTuning, (c, v) => c.StashTuning = v),
            new TuningSlot("ExtractionTuning", ExtractionJson.ResourcePath, c => c.ExtractionTuning, (c, v) => c.ExtractionTuning = v),
            new TuningSlot("ReplayTuning", ReplayJson.ResourcePath, c => c.ReplayTuning, (c, v) => c.ReplayTuning = v),
            new TuningSlot("PayloadTuning", KitTuning.PayloadResourcePath, c => c.PayloadTuning, (c, v) => c.PayloadTuning = v),
            new TuningSlot("EconomyTuning", KitTuning.EconomyResourcePath, c => c.EconomyTuning, (c, v) => c.EconomyTuning = v),
            new TuningSlot("SupplyDropsTuning", KitTuning.SupplyDropsResourcePath, c => c.SupplyDropsTuning, (c, v) => c.SupplyDropsTuning = v),
            new TuningSlot("RedeployTuning", KitTuning.RedeployResourcePath, c => c.RedeployTuning, (c, v) => c.RedeployTuning = v),
            new TuningSlot("MutatorsTuning", KitTuning.MutatorsResourcePath, c => c.MutatorsTuning, (c, v) => c.MutatorsTuning = v),
            new TuningSlot("PlaylistsTuning", KitTuning.PlaylistsResourcePath, c => c.PlaylistsTuning, (c, v) => c.PlaylistsTuning = v),
            new TuningSlot("KeySharingTuning", KitTuning.KeySharingResourcePath, c => c.KeySharingTuning, (c, v) => c.KeySharingTuning = v),
            new TuningSlot("NameplatesTuning", KitTuning.NameplatesResourcePath, c => c.NameplatesTuning, (c, v) => c.NameplatesTuning = v),
        };

        public static string DefaultPath => ShooterKitRoot.OutputPath("Resources/" + GameContent.ResourceFolder + "/" + FileName);

        [MenuItem(MenuPath)]
        public static void Create()
        {
            var path = ResolvePath(Environment.GetCommandLineArgs(), DefaultPath);
            var content = CreateOrFill(path, FindInProject, FindTuningInProject);
            Selection.activeObject = content;
        }

        public static string ResolvePath(IReadOnlyList<string> args, string fallback)
        {
            string found = null;
            for (var i = 0; args != null && i < args.Count; i++)
            {
                if (!string.Equals(args[i], ArgumentName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found != null)
                {
                    throw new ArgumentException($"{ArgumentName} is given more than once");
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{ArgumentName} needs an asset path such as Assets/MyGame/Resources/ShooterKit/{FileName}");
                }

                found = args[i + 1];
                i++;
            }

            return SceneBuildHelpers.ValidateAssetPath(found ?? fallback);
        }

        public static GameContent CreateOrFill(string path, Func<Type, IReadOnlyList<Object>> find, Func<string, IReadOnlyList<TextAsset>> findTuning)
        {
            if (find == null)
            {
                throw new ArgumentNullException(nameof(find));
            }

            if (findTuning == null)
            {
                throw new ArgumentNullException(nameof(findTuning));
            }

            if (!path.Contains("/Resources/" + GameContent.ResourceFolder + "/", StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"{LogTag} '{path}' is not under a Resources/{GameContent.ResourceFolder}/ folder, so the kit will not find it;"
                    + " move it there or pass it to GameContent.Use");
            }

            var content = SceneBuildHelpers.CreateOrUpdateAsset<GameContent>(path, c => FillEmpty(c, find, findTuning));
            Debug.Log($"{LogTag} saved '{path}'");
            return content;
        }

        // 文言の表は空でも Resources のものが読まれるので埋めない。
        public static IReadOnlyList<string> FillEmpty(GameContent content, Func<Type, IReadOnlyList<Object>> find, Func<string, IReadOnlyList<TextAsset>> findTuning)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (find == null)
            {
                throw new ArgumentNullException(nameof(find));
            }

            if (findTuning == null)
            {
                throw new ArgumentNullException(nameof(findTuning));
            }

            var left = new List<string>();
            foreach (var slot in AllSlots())
            {
                if (slot.Get(content) != null)
                {
                    continue;
                }

                var candidates = (find(slot.Type) ?? Array.Empty<Object>()).Where(c => c != null && c != content).ToList();
                if (candidates.Count == 1)
                {
                    slot.Set(content, candidates[0]);
                    Debug.Log($"{LogTag} {slot.Name} = '{candidates[0].name}'");
                    continue;
                }

                left.Add(slot.Name);
                Debug.Log(candidates.Count == 0
                    ? $"{LogTag} {slot.Name} left empty: no {slot.Type.Name} in the project"
                    : $"{LogTag} {slot.Name} left empty: {candidates.Count} {slot.Type.Name} assets ({string.Join(", ", candidates.Select(c => c.name))}); choose one in the Inspector");
            }

            foreach (var slot in _tuningSlots)
            {
                if (slot.Get(content) != null)
                {
                    continue;
                }

                var candidates = (findTuning(slot.FileName) ?? Array.Empty<TextAsset>()).Where(c => c != null).ToList();
                if (candidates.Count == 1)
                {
                    slot.Set(content, candidates[0]);
                    Debug.Log($"{LogTag} {slot.Name} = '{candidates[0].name}'");
                    continue;
                }

                // 見つからない欄は、空のままでもキットの Resources/Tuning/<名前> が読まれるので、埋められなかった欄に数えない。
                if (candidates.Count == 0)
                {
                    Debug.Log($"{LogTag} {slot.Name} left empty: no {slot.FileName} in the project, so Resources/{slot.ResourcePath} is read");
                    continue;
                }

                left.Add(slot.Name);
                Debug.Log($"{LogTag} {slot.Name} left empty: {candidates.Count} {slot.FileName} files; choose one in the Inspector");
            }

            if (content.Enemies.Count == 0)
            {
                var enemies = (find(typeof(EnemyDefinition)) ?? Array.Empty<Object>())
                    .OfType<EnemyDefinition>()
                    .GroupBy(e => e.Archetype)
                    .Select(g => g.First())
                    .ToList();
                content.SetEnemies(enemies);
                if (enemies.Count == 0)
                {
                    left.Add("Enemies");
                }

                Debug.Log($"{LogTag} Enemies = [{string.Join(", ", enemies.Select(e => $"{e.Archetype}: '{e.name}'"))}]");
            }

            return left;
        }

        public static IReadOnlyList<Object> FindInProject(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return AssetDatabase.FindAssets("t:" + type.Name, new[] { SearchFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => AssetDatabase.LoadAssetAtPath(p, type))
                .Where(a => a != null)
                .ToList();
        }

        // キットの Resources/Tuning/ にある同梱のものは欄が空でも読まれ、C# のスクリプトも TextAsset なので、どちらも候補にしない。
        public static IReadOnlyList<TextAsset> FindTuningInProject(string fileName)
        {
            var bundled = ShooterKitRoot.BundledPath("Resources/" + TuningFolder);
            return AssetDatabase.FindAssets("t:" + nameof(TextAsset), new[] { SearchFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.Ordinal)
                .Where(p => IsTuningCandidate(p, fileName, bundled))
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => AssetDatabase.LoadAssetAtPath<TextAsset>(p))
                .Where(a => a != null && !(a is MonoScript))
                .ToList();
        }

        public static bool IsTuningCandidate(string assetPath, string fileName, string bundledTuningFolder)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var normalized = assetPath.Replace('\\', '/');
            if (!string.Equals(System.IO.Path.GetFileNameWithoutExtension(normalized), fileName, StringComparison.Ordinal))
            {
                return false;
            }

            // The Demo sample ships text tables with the same file names (minimap.json etc.) under Resources/Localization.
            if (normalized.Contains("/Resources/Localization/", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(bundledTuningFolder))
            {
                return true;
            }

            var folder = bundledTuningFolder.Replace('\\', '/').TrimEnd('/') + "/";
            return !normalized.StartsWith(folder, StringComparison.Ordinal);
        }

        // 登録された枠と組み込みの枠で名前が重なったら、組み込みの欄を二度埋めないように組み込みのほうを使う。
        private static IEnumerable<Slot> AllSlots()
        {
            var builtIn = new HashSet<string>(_slots.Select(s => s.Name), StringComparer.Ordinal);
            return _slots.Concat(GameContentSlots.All
                .Where(s => !builtIn.Contains(s.Name))
                .Select(s => new Slot(s.Name, s.AssetType, s.Get, s.Set)));
        }

        private sealed class TuningSlot
        {
            public TuningSlot(string name, string resourcePath, Func<GameContent, TextAsset> get, Action<GameContent, TextAsset> set)
            {
                Name = name;
                ResourcePath = resourcePath;
                FileName = resourcePath.Substring(resourcePath.LastIndexOf('/') + 1);
                Get = c =>
                {
                    var value = get(c);
                    return value != null ? value : null;
                };
                Set = set;
            }

            public string Name { get; }

            public string ResourcePath { get; }

            public string FileName { get; }

            public Func<GameContent, TextAsset> Get { get; }

            public Action<GameContent, TextAsset> Set { get; }
        }

        private sealed class Slot
        {
            public Slot(string name, Type type, Func<GameContent, Object> get, Action<GameContent, Object> set)
            {
                Name = name;
                Type = type;
                Get = c =>
                {
                    var value = get(c);
                    return value != null ? value : null;
                };
                Set = set;
            }

            public string Name { get; }

            public Type Type { get; }

            public Func<GameContent, Object> Get { get; }

            public Action<GameContent, Object> Set { get; }
        }
    }
}
