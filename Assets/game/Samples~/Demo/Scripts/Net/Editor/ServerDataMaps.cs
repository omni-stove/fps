using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    [InitializeOnLoad]
    public static class ServerDataMaps
    {
        public const string LogTag = "[Net] ServerDataMaps:";

        static ServerDataMaps()
        {
            MapCollisionExporter.UseMapScenes(Prepare);
        }

        // まだ生成していないマップがあっても書き出せるよう、無いシーンを作ってから渡す
        public static IReadOnlyList<string> Prepare()
        {
            EnsureScenes();
            return BuildSceneList.ServerScenes;
        }

        public static void EnsureScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NetSceneBuilder.ScenePath) == null)
            {
                NetSceneBuilder.Build();
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaSceneBuilder.ScenePath) == null)
            {
                ArenaSceneBuilder.BuildAt(ArenaSceneBuilder.ScenePath, ArenaSceneBuilder.NavMeshDataPath);
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NetSceneBuilder.BattleRoyaleScenePath) == null)
            {
                NetSceneBuilder.BuildBattleRoyaleAt(NetSceneBuilder.BattleRoyaleScenePath, NetSceneBuilder.BattleRoyaleNavMeshDataPath);
            }

            EnsureFlagBases();
            EnsurePayloadCarts();
            EnsureMapFeatures();
        }

        // はしご・水場・乗り物を置く前に作ったシーンは、旗の台と同じく作り直さずに置き物だけを足し、塔の足場の分だけ NavMesh を焼き直して書き出しに載せる。
        public static IReadOnlyList<string> EnsureMapFeatures()
        {
            float? captureRadius = null;
            float Radius()
            {
                captureRadius ??= NetSceneBuilder.CaptureRadius(NetSceneBuilder.LoadOrBuildMatch());
                return captureRadius.Value;
            }

            var added = new List<string>();
            if (AddMapFeaturesIfMissing(
                    NetSceneBuilder.ScenePath, () => NetSceneBuilder.LoadFeatures(Radius()), NetSceneBuilder.RequireNavigable))
            {
                added.Add(NetSceneBuilder.ScenePath);
            }

            if (AddMapFeaturesIfMissing(
                    ArenaSceneBuilder.ScenePath, () => ArenaSceneBuilder.LoadFeatures(Radius()), ArenaSceneBuilder.RequireNavigable))
            {
                added.Add(ArenaSceneBuilder.ScenePath);
            }

            if (AddMapFeaturesIfMissing(
                    NetSceneBuilder.BattleRoyaleScenePath, NetSceneBuilder.LoadBattleRoyaleFeatures, NetSceneBuilder.RequireBattleRoyaleNavigable))
            {
                added.Add(NetSceneBuilder.BattleRoyaleScenePath);
            }

            return added;
        }

        // はしご・水・乗り物が 1 つでもあれば、ゲームが置き直したものとみなして触らない。
        public static bool AddMapFeaturesIfMissing(
            string scenePath, Func<NetMapFeatureLayout> layout, Action<NetMapFeatureLayout> requireNavigable)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new FileNotFoundException(LogTag + " " + scenePath + " is missing", scenePath);
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (NetMapFeatureBuilder.HasFeatures(scene))
            {
                return false;
            }

            var features = layout();
            if (features.Count == 0)
            {
                return false;
            }

            var surface = RequireOneNavMeshSurface(scene, scenePath);
            var navMeshDataPath = AssetDatabase.GetAssetPath(surface.navMeshData);
            if (string.IsNullOrEmpty(navMeshDataPath))
            {
                throw new InvalidOperationException(LogTag + " the NavMeshSurface on '" + surface.name + "' in " + scenePath
                    + " has no saved NavMesh data; rebuild the scene");
            }

            // 塔の足場は動かない当たりなので、焼き直さないとサーバーの Bot が足場の中を歩いてしまう
            NetMapFeatureBuilder.PlaceTerrain(scene, NetMapFeatureBuilder.TerrainParent(surface), features);
            var existingData = surface.navMeshData;
            var freshData = SceneBuildHelpers.BakeNavMesh(surface);
            // 届くかの検査が通ってからアセットを書く。失敗しても、保存済みのシーンが指す NavMesh を消さない
            requireNavigable?.Invoke(features);
            // SaveNavMeshData は消して作り直すので .meta の GUID が変わる。既存のアセットへ上書きして GUID を残す
            if (existingData != null && freshData != existingData)
            {
                // CopySerialized は m_Name も写し、アセット名がファイル名と食い違うので戻す
                var existingName = existingData.name;
                EditorUtility.CopySerialized(freshData, existingData);
                existingData.name = existingName;
                surface.navMeshData = existingData;
            }

            EditorUtility.SetDirty(existingData != null ? existingData : freshData);
            EditorUtility.SetDirty(surface);
            NetMapFeatureBuilder.PlaceVehicles(scene, null, features);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException(LogTag + " failed to save " + scenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(LogTag + " added " + features.LadderTowers.Count + " ladder tower(s), " + features.WaterPools.Count + " water pool(s) and "
                + features.Vehicles.Count + " vehicle(s) to " + scenePath + " and rebaked " + navMeshDataPath + "; commit them");
            return true;
        }

        private static NavMeshSurface RequireOneNavMeshSurface(Scene scene, string scenePath)
        {
            var surfaces = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true)).ToList();
            if (surfaces.Count != 1)
            {
                throw new InvalidOperationException(LogTag + " " + scenePath + " has " + surfaces.Count
                    + " NavMeshSurfaces; adding the map features rebakes one, so rebuild the scene instead");
            }

            return surfaces[0];
        }

        // 台車を置く前に作ったシーンは、旗の台と同じく作り直さずに台車と道筋だけを足して書き出しに載せる。
        public static IReadOnlyList<string> EnsurePayloadCarts()
        {
            var added = new List<string>();
            if (AddPayloadCartIfMissing(ArenaSceneBuilder.ScenePath, ArenaSceneBuilder.PayloadRoutePoints, ArenaSceneBuilder.PayloadCheckpoints))
            {
                added.Add(ArenaSceneBuilder.ScenePath);
            }

            return added;
        }

        // 台車が 1 台でもあれば、ゲームが置き直したものとみなして触らない。
        public static bool AddPayloadCartIfMissing(string scenePath, IReadOnlyList<Vector3> route, IReadOnlyList<int> checkpoints)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new FileNotFoundException(LogTag + " " + scenePath + " is missing", scenePath);
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<PayloadCart>(true).Length > 0))
            {
                return false;
            }

            ArenaSceneBuilder.CreatePayloadCart(scene, route, checkpoints);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException(LogTag + " failed to save " + scenePath);
            }

            Debug.Log(LogTag + " added a payload cart with " + route.Count + " route point(s) to " + scenePath + "; commit it");
            return true;
        }

        // 旗の台を置く前に作ったシーンは、作り直すと NavMesh や壊れる建物まで変わってしまうので、台だけを足して書き出しに載せる。
        public static IReadOnlyList<string> EnsureFlagBases()
        {
            float? captureRadius = null;
            float Radius()
            {
                captureRadius ??= NetSceneBuilder.CaptureRadius(NetSceneBuilder.LoadOrBuildMatch());
                return captureRadius.Value;
            }

            var added = new List<string>();
            if (AddFlagBasesIfMissing(NetSceneBuilder.ScenePath, NetSceneBuilder.FlagBases, Radius))
            {
                added.Add(NetSceneBuilder.ScenePath);
            }

            if (AddFlagBasesIfMissing(ArenaSceneBuilder.ScenePath, ArenaSceneBuilder.FlagBases, () =>
                {
                    var radius = Radius();
                    var overlaps = ArenaSceneBuilder.FlagBaseOverlaps(radius);
                    if (overlaps.Count > 0)
                    {
                        throw new InvalidOperationException(LogTag + " flag base areas (capture radius " + radius + " m) overlap "
                            + ArenaSceneBuilder.ScenePath + ": " + string.Join("; ", overlaps));
                    }

                    return radius;
                }))
            {
                added.Add(ArenaSceneBuilder.ScenePath);
            }

            return added;
        }

        // 台が 1 つでもあれば、ゲームが置き直したものとみなして触らない。
        public static bool AddFlagBasesIfMissing(string scenePath, IReadOnlyList<(Side Side, Vector3 Position)> bases, Func<float> captureRadius)
        {
            if (captureRadius == null)
            {
                throw new ArgumentNullException(nameof(captureRadius));
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new FileNotFoundException(LogTag + " " + scenePath + " is missing", scenePath);
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<FlagBase>(true).Length > 0))
            {
                return false;
            }

            var radius = captureRadius();
            NetSceneBuilder.CreateFlagBases(scene, bases, radius);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException(LogTag + " failed to save " + scenePath);
            }

            Debug.Log(LogTag + " added " + bases.Count + " flag base(s) (capture radius " + radius + " m) to " + scenePath + "; commit it");
            return true;
        }

        // 箱の Id は部品に持たせたものを使う。オブジェクトの名前は複製すると重なり、付け直すと変わるので、サーバーとクライアントの間で箱を引く鍵にしない
        public static IReadOnlyList<NetMapLootContainer> ToLootContainers(string scenePath, IReadOnlyList<LootContainer> containers)
        {
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers));
            }

            var written = new List<NetMapLootContainer>(containers.Count);
            var owners = new Dictionary<string, LootContainer>(StringComparer.Ordinal);
            foreach (var container in containers)
            {
                var id = RequireId(scenePath, container, container.ContainerId, "container id", owners);
                // 遊ぶときの IsInReach と同じく、使えない距離は既定の距離に読み替える
                var reach = LootContainer.IsValidReach(container.Reach) ? container.Reach : LootContainer.DefaultReach;
                written.Add(new NetMapLootContainer(id, container.TableId, container.Position.ToNumerics(), reach));
            }

            return written;
        }

        public static IReadOnlyList<NetMapExtractionPoint> ToExtractionPoints(string scenePath, IReadOnlyList<ExtractionZone> zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            var written = new List<NetMapExtractionPoint>(zones.Count);
            var owners = new Dictionary<string, ExtractionZone>(StringComparer.Ordinal);
            foreach (var zone in zones)
            {
                var id = RequireId(scenePath, zone, zone.PointId, "point id", owners);
                // 遊ぶときの Contains と同じく、使えない半径は既定の半径に読み替える
                var radius = ExtractionZone.IsValidRadius(zone.Radius) ? zone.Radius : ExtractionZone.DefaultRadius;
                written.Add(new NetMapExtractionPoint(id, zone.transform.position.ToNumerics(), radius));
            }

            return written;
        }

        // エリアの無いシーンは null。サーバーは BR のルールでエリアの無いマップを起動のときに断る
        public static NetMapZoneCircle ToZone(string scenePath, IReadOnlyList<ShrinkingZone> zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            if (zones.Count == 0)
            {
                return null;
            }

            // サーバーは 1 つのエリアだけを縮めるので、どれを書き出すかを黙って選ばない
            if (zones.Count > 1)
            {
                throw new InvalidOperationException(LogTag + " " + scenePath + " has " + zones.Count + " ShrinkingZones; the server shrinks one, so keep one");
            }

            var zone = zones[0];
            try
            {
                return new NetMapZoneCircle(zone.transform.position.ToNumerics(), zone.InitialRadius);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(LogTag + " ShrinkingZone '" + zone.name + "' in " + scenePath + " has an invalid circle: " + e.Message, e);
            }
        }

        // パラシュートで降ろす Runner が無ければ null (地面から始める)。高さはアセットがあればそちら (遊ぶときの EffectiveDropHeight と同じ)
        public static float? ToDropHeight(string scenePath, IReadOnlyList<BattleRoyaleRunner> runners)
        {
            if (runners == null)
            {
                throw new ArgumentNullException(nameof(runners));
            }

            float? height = null;
            foreach (var runner in runners)
            {
                if (runner.StartFrom != BattleRoyaleStart.Parachute)
                {
                    continue;
                }

                var next = runner.EffectiveDropHeight;
                if (height.HasValue && !Mathf.Approximately(height.Value, next))
                {
                    throw new InvalidOperationException(
                        LogTag + " " + scenePath + " has BattleRoyaleRunners that drop from " + height.Value + " m and " + next + " m; the server drops from one height");
                }

                height = next;
            }

            return height;
        }

        private static string RequireId<T>(string scenePath, T component, string id, string label, Dictionary<string, T> owners)
            where T : Component
        {
            var kind = typeof(T).Name;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException(
                    LogTag + " " + kind + " '" + component.name + "' in " + scenePath + " has no " + label + "; set one that no other " + kind + " uses");
            }

            if (owners.TryGetValue(id, out var other))
            {
                throw new InvalidOperationException(
                    LogTag + " " + kind + "s '" + other.name + "' and '" + component.name + "' in " + scenePath + " share the " + label + " '" + id + "'");
            }

            owners.Add(id, component);
            return id;
        }
    }
}
