using System.Collections.Generic;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    public sealed class AttachmentModelCatalogAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public const string MenuPath = "Fps/Build Attachment Model Catalog";
        public const string LogTag = "[AttachmentModelCatalogAssetStep]";

        // Tuning/inventory.json の items の id
        public const string RedDotId = "2451a5a1-24e5-45ed-bbc1-8b23276152f8";
        public const string Scope4xId = "a2c5e73b-69fa-4402-b0ef-3c4297973226";
        public const string SuppressorId = "8d8c8546-59d4-4b4c-ba71-890664e5c260";
        public const string CompensatorId = "3fc98d19-5fb3-4680-bd5f-30d30d14f84e";
        public const string ForegripId = "6142e02c-15a1-4fe8-9f31-015752f1ca2e";
        public const string ExtendedMagazineId = "eb45e06b-489d-410d-ae0d-f1095df96a0b";

        public static string AssetPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + AttachmentModelCatalog.ResourcePath + ".asset";

        public string Name => "AttachmentModelCatalog";

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

        // プレハブは空にして、ゲームが差し替えるまでコードで作る箱を出す
        public static IReadOnlyList<AttachmentModelEntry> Entries()
        {
            return new[]
            {
                new AttachmentModelEntry(RedDotId, null, AttachmentAnchor.Top, new Vector3(0.035f, 0.04f, 0.06f)),
                new AttachmentModelEntry(Scope4xId, null, AttachmentAnchor.Top, new Vector3(0.04f, 0.045f, 0.18f)),
                new AttachmentModelEntry(SuppressorId, null, AttachmentAnchor.Front, new Vector3(0.035f, 0.035f, 0.16f)),
                new AttachmentModelEntry(CompensatorId, null, AttachmentAnchor.Front, new Vector3(0.03f, 0.03f, 0.05f)),
                new AttachmentModelEntry(ForegripId, null, AttachmentAnchor.Bottom, new Vector3(0.025f, 0.08f, 0.025f)),
                new AttachmentModelEntry(ExtendedMagazineId, null, AttachmentAnchor.Bottom, new Vector3(0.03f, 0.14f, 0.05f)),
            };
        }

        public static AttachmentModelCatalog BuildAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<AttachmentModelCatalog>(path, c => c.SetAttachments(Entries()));
            Debug.Log($"{LogTag} saved '{path}' attachments={catalog.Attachments.Count}");
            return catalog;
        }
    }
}
