using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class ReticleCatalogAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public const string MenuPath = "Shooter/Build Reticle Catalog";
        public const string LogTag = "[ReticleCatalogAssetStep]";

        public const string RedDotId = "redDot";
        public const string HoloId = "holo";
        public const string Scope4xId = "scope4x";
        public const string DuplexId = "duplex";
        public const string MilDotId = "mildot";

        public static readonly Color RedDotColor = new Color(1f, 0.15f, 0.1f, 1f);
        public static readonly Color HoloColor = new Color(1f, 0.3f, 0.2f, 0.9f);
        public static readonly Color ScopeLineColor = Color.black;

        public static string AssetPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + ReticleCatalog.ResourcePath + ".asset";

        public string Name => "ReticleCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(AssetPath);
        }

        [MenuItem(MenuPath)]
        public static void BuildFromMenu()
        {
            BuildAt(AssetPath);
        }

        // 画像は空にして、ゲームが差し替えるまで形をコードで描く
        public static IReadOnlyList<ReticleEntry> Entries()
        {
            return new[]
            {
                new ReticleEntry(RedDotId, null, ReticleShape.Dot, RedDotColor, 0.008f),
                new ReticleEntry(HoloId, null, ReticleShape.Circle, HoloColor, 0.06f),
                new ReticleEntry(Scope4xId, null, ReticleShape.Circle, RedDotColor, 0.25f),
                new ReticleEntry(DuplexId, null, ReticleShape.Crosshair, ScopeLineColor, 1f),
                new ReticleEntry(MilDotId, null, ReticleShape.MilDot, ScopeLineColor, 1f),
            };
        }

        public static ReticleCatalog BuildAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<ReticleCatalog>(path, c => c.SetReticles(Entries()));
            Debug.Log($"{LogTag} saved '{path}' reticles={catalog.Reticles.Count}");
            return catalog;
        }
    }
}
