using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// ゲームが差し替える入口のアセット (<see cref="GameContent"/>) を作り、空の欄をプロジェクトのアセットで埋める。
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.GameContentAssets.Create [-gameContent Assets/MyGame/Resources/FpsKit/GameContent.asset]</code>
    /// </summary>
    public static class GameContentAssets
    {
        public const string MenuPath = "Fps/Create Game Content";
        public const string LogTag = "[GameContentAssets]";
        public const string ArgumentName = "-gameContent";
        public const string FileName = "GameContent.asset";

        /// <summary>Assets/ の下のアセットだけを候補にする。パッケージの同梱物はゲームが差し替える前の仮のもの。</summary>
        public const string SearchFolder = "Assets";

        private static readonly Slot[] _slots =
        {
            new Slot("Rules", typeof(GameRulesDefinition), c => c.Rules, (c, v) => c.Rules = (GameRulesDefinition)v),
            new Slot("Motor", typeof(MotorSettings), c => c.Motor, (c, v) => c.Motor = (MotorSettings)v),
            new Slot("WeaponCatalog", typeof(WeaponCatalogAsset), c => c.WeaponCatalog, (c, v) => c.WeaponCatalog = (WeaponCatalogAsset)v),
            new Slot("Loadout", typeof(LoadoutDefinition), c => c.Loadout, (c, v) => c.Loadout = (LoadoutDefinition)v),
            new Slot("Missions", typeof(MissionCatalog), c => c.Missions, (c, v) => c.Missions = (MissionCatalog)v),
            new Slot("TeamDeathmatch", typeof(TeamDeathmatchDefinition), c => c.TeamDeathmatch, (c, v) => c.TeamDeathmatch = (TeamDeathmatchDefinition)v),
            new Slot("Domination", typeof(DominationDefinition), c => c.Domination, (c, v) => c.Domination = (DominationDefinition)v),
            new Slot("Hardpoint", typeof(HardpointDefinition), c => c.Hardpoint, (c, v) => c.Hardpoint = (HardpointDefinition)v),
            new Slot("KillConfirmed", typeof(KillConfirmedDefinition), c => c.KillConfirmed, (c, v) => c.KillConfirmed = (KillConfirmedDefinition)v),
            new Slot("SearchAndDestroy", typeof(SearchAndDestroyDefinition), c => c.SearchAndDestroy, (c, v) => c.SearchAndDestroy = (SearchAndDestroyDefinition)v),
            new Slot("Horde", typeof(HordeDefinition), c => c.Horde, (c, v) => c.Horde = (HordeDefinition)v),
            new Slot("GunGameLadder", typeof(GunGameLadderDefinition), c => c.GunGameLadder, (c, v) => c.GunGameLadder = (GunGameLadderDefinition)v),
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
            new Slot("AwarenessIndicator", typeof(AwarenessIndicatorCatalog), c => c.AwarenessIndicator, (c, v) => c.AwarenessIndicator = (AwarenessIndicatorCatalog)v),
            new Slot("CameraShake", typeof(CameraShakeSettings), c => c.CameraShake, (c, v) => c.CameraShake = (CameraShakeSettings)v),
        };

        /// <summary>出力先ルート (<see cref="FpsKitRoot.Output"/>) の <c>Resources/FpsKit/GameContent.asset</c>。</summary>
        public static string DefaultPath => FpsKitRoot.OutputPath("Resources/" + GameContent.ResourceFolder + "/" + FileName);

        /// <summary>
        /// <see cref="ArgumentName"/> のパス (無ければ <see cref="DefaultPath"/>) に入口を作るか、既にあれば空の欄だけを埋める。
        /// 値が不正なら例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Create()
        {
            var path = ResolvePath(Environment.GetCommandLineArgs(), DefaultPath);
            var content = CreateOrFill(path, FindInProject);
            Selection.activeObject = content;
        }

        /// <summary>args の <see cref="ArgumentName"/> の値。無ければ fallback。値が無い・2 度あるなら例外。</summary>
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
                    throw new ArgumentException($"{ArgumentName} needs an asset path such as Assets/MyGame/Resources/FpsKit/{FileName}");
                }

                found = args[i + 1];
                i++;
            }

            return SceneBuildHelpers.ValidateAssetPath(found ?? fallback);
        }

        /// <summary>path に入口を作るか読み、空の欄を <see cref="FillEmpty"/> で埋めて保存する。</summary>
        public static GameContent CreateOrFill(string path, Func<Type, IReadOnlyList<Object>> find)
        {
            if (find == null)
            {
                throw new ArgumentNullException(nameof(find));
            }

            if (!path.Contains("/Resources/" + GameContent.ResourceFolder + "/", StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"{LogTag} '{path}' is not under a Resources/{GameContent.ResourceFolder}/ folder, so the kit will not find it;"
                    + " move it there or pass it to GameContent.Use");
            }

            var content = SceneBuildHelpers.CreateOrUpdateAsset<GameContent>(path, c => FillEmpty(c, find));
            Debug.Log($"{LogTag} saved '{path}'");
            return content;
        }

        /// <summary>
        /// content の空の欄に、find が返すその型のアセットがちょうど 1 つならそれを入れる。0 か 2 つ以上なら空のまま残す。
        /// 敵の欄が空なら、種類ごとに最初の <see cref="EnemyDefinition"/> を入れる。文言の表と調整値の JSON は埋めない (Resources のものが読まれる)。
        /// 埋めた欄と残した欄をログに出し、空のまま残した欄の名前を返す。
        /// </summary>
        public static IReadOnlyList<string> FillEmpty(GameContent content, Func<Type, IReadOnlyList<Object>> find)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (find == null)
            {
                throw new ArgumentNullException(nameof(find));
            }

            var left = new List<string>();
            foreach (var slot in _slots)
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

        /// <summary>Assets/ の下にある type のアセットを、パスの順に返す。</summary>
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
