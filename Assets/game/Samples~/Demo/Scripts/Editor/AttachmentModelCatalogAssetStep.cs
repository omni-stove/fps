using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class AttachmentModelCatalogAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = PresentationCatalogAssetStep.StepOrder + 10;

        public const string MenuPath = "Shooter/Build Attachment Model Catalog";
        public const string LogTag = "[AttachmentModelCatalogAssetStep]";

        // Tuning/inventory.json の items の id
        public const string RedDotId = "2451a5a1-24e5-45ed-bbc1-8b23276152f8";
        public const string HoloSightId = "79fb2f9e-fc61-425a-b651-8baa8d1a6b2b";
        public const string Scope4xId = "a2c5e73b-69fa-4402-b0ef-3c4297973226";
        public const string SuppressorId = "8d8c8546-59d4-4b4c-ba71-890664e5c260";
        public const string CompensatorId = "3fc98d19-5fb3-4680-bd5f-30d30d14f84e";
        public const string ForegripId = "6142e02c-15a1-4fe8-9f31-015752f1ca2e";
        public const string ExtendedMagazineId = "eb45e06b-489d-410d-ae0d-f1095df96a0b";

        public const string ModelFolderName = "AttachmentModels";
        public const string ModelPrefix = "Attachment_";

        public static readonly Color HousingColor = WeaponLook.PolymerColor;
        public static readonly Color MetalColor = WeaponLook.MetalColor;

        public static string AssetPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + AttachmentModelCatalog.ResourcePath + ".asset";

        public static string ModelFolder => PresentationAssetBuilder.PresentationFolder + "/" + ModelFolderName;

        public string Name => "AttachmentModelCatalog";

        public int Order => StepOrder;

        public void Build()
        {
            BuildAt(AssetPath, ModelFolder);
        }

        [MenuItem(MenuPath)]
        public static void BuildFromMenu()
        {
            BuildAt(AssetPath, ModelFolder);
        }

        // 箱の大きさを残すのは、ゲームがプレハブを外したときにコードで作る箱をこの大きさにするため
        public static IReadOnlyList<AttachmentModelEntry> Entries()
        {
            return new[]
            {
                new AttachmentModelEntry(RedDotId, null, AttachmentAnchor.Top, new Vector3(0.035f, 0.04f, 0.06f)),
                new AttachmentModelEntry(HoloSightId, null, AttachmentAnchor.Top, new Vector3(0.044f, 0.05f, 0.06f)),
                new AttachmentModelEntry(Scope4xId, null, AttachmentAnchor.Top, new Vector3(0.04f, 0.045f, 0.18f)),
                new AttachmentModelEntry(SuppressorId, null, AttachmentAnchor.Front, new Vector3(0.035f, 0.035f, 0.16f)),
                new AttachmentModelEntry(CompensatorId, null, AttachmentAnchor.Front, new Vector3(0.03f, 0.03f, 0.05f)),
                new AttachmentModelEntry(ForegripId, null, AttachmentAnchor.Bottom, new Vector3(0.025f, 0.08f, 0.025f)),
                new AttachmentModelEntry(ExtendedMagazineId, null, AttachmentAnchor.Bottom, new Vector3(0.03f, 0.14f, 0.05f)),
            };
        }

        public static string PrefabName(string itemId)
        {
            switch (itemId)
            {
                case RedDotId:
                    return ModelPrefix + "RedDot";
                case HoloSightId:
                    return ModelPrefix + "HoloSight";
                case Scope4xId:
                    return ModelPrefix + "Scope4x";
                case SuppressorId:
                    return ModelPrefix + "Suppressor";
                case CompensatorId:
                    return ModelPrefix + "Compensator";
                case ForegripId:
                    return ModelPrefix + "Foregrip";
                case ExtendedMagazineId:
                    return ModelPrefix + "ExtendedMagazine";
                default:
                    throw new ArgumentException($"no attachment model for item '{itemId}'", nameof(itemId));
            }
        }

        public static AttachmentShape ShapeFor(string itemId)
        {
            var parts = new List<WeaponPart>();
            switch (itemId)
            {
                case RedDotId:
                    {
                        var sight = WeaponLook.AddDotSight(parts, 0f, 0f);
                        parts.Add(new WeaponPart("DotSightEmitter", WeaponPartShape.Box, new Vector3(0f, 0.01f, -0.012f), new Vector3(0.006f, 0.004f, 0.006f), WeaponPaint.Accent));
                        return new AttachmentShape(parts, WeaponSockets.SightName, sight);
                    }

                case HoloSightId:
                    {
                        var sight = WeaponLook.AddHoloSight(parts, 0f, 0f);
                        parts.Add(new WeaponPart("HoloSightButton", WeaponPartShape.Box, new Vector3(0.026f, 0.007f, -0.015f), new Vector3(0.006f, 0.006f, 0.012f), WeaponPaint.Accent));
                        return new AttachmentShape(parts, WeaponSockets.SightName, sight);
                    }

                case Scope4xId:
                    {
                        const float diameter = 0.04f;
                        const float length = 0.18f;
                        const float mount = 0.014f;
                        var y = mount + (diameter * 0.5f);
                        parts.Add(new WeaponPart("ScopeMount_F", WeaponPartShape.Box, new Vector3(0f, mount * 0.5f, length * 0.25f), new Vector3(0.016f, mount, 0.014f), WeaponPaint.Furniture));
                        parts.Add(new WeaponPart("ScopeMount_R", WeaponPartShape.Box, new Vector3(0f, mount * 0.5f, -length * 0.25f), new Vector3(0.016f, mount, 0.014f), WeaponPaint.Furniture));
                        parts.Add(new WeaponPart("ScopeTube", WeaponPartShape.Cylinder, new Vector3(0f, y, 0f), new Vector3(diameter * 0.75f, diameter * 0.75f, length), WeaponPaint.Body));
                        parts.Add(new WeaponPart("ScopeBell_F", WeaponPartShape.Cylinder, new Vector3(0f, y, length * 0.42f), new Vector3(diameter, diameter, length * 0.16f), WeaponPaint.Body));
                        parts.Add(new WeaponPart("ScopeBell_R", WeaponPartShape.Cylinder, new Vector3(0f, y, -length * 0.42f), new Vector3(diameter * 0.9f, diameter * 0.9f, length * 0.16f), WeaponPaint.Body));
                        parts.Add(new WeaponPart("ScopeTurret_Top", WeaponPartShape.Cylinder, new Vector3(0f, y + (diameter * 0.45f), 0f), new Vector3(0.016f, 0.016f, 0.014f), WeaponPaint.Furniture, new Vector3(-90f, 0f, 0f)));
                        parts.Add(new WeaponPart("ScopeTurret_Side", WeaponPartShape.Cylinder, new Vector3(diameter * 0.45f, y, 0f), new Vector3(0.016f, 0.016f, 0.014f), WeaponPaint.Furniture, new Vector3(0f, 90f, 0f)));
                        parts.Add(new WeaponPart("ScopeLens_F", WeaponPartShape.Cylinder, new Vector3(0f, y, (length * 0.5f) + 0.001f), new Vector3(diameter * 0.85f, diameter * 0.85f, 0.002f), WeaponPaint.Glass));
                        parts.Add(new WeaponPart("ScopeLens_R", WeaponPartShape.Cylinder, new Vector3(0f, y, (-length * 0.5f) - 0.001f), new Vector3(diameter * 0.75f, diameter * 0.75f, 0.002f), WeaponPaint.Glass));
                        return new AttachmentShape(parts, WeaponSockets.SightName, new Vector3(0f, y, -length * 0.5f));
                    }

                case SuppressorId:
                    {
                        const float length = 0.16f;
                        parts.Add(new WeaponPart("SuppressorMount", WeaponPartShape.Cylinder, new Vector3(0f, 0f, 0.01f), new Vector3(0.026f, 0.026f, 0.02f), WeaponPaint.Furniture));
                        parts.Add(new WeaponPart("SuppressorBody", WeaponPartShape.Cylinder, new Vector3(0f, 0f, 0.02f + ((length - 0.02f) * 0.5f)), new Vector3(0.036f, 0.036f, length - 0.02f), WeaponPaint.Body));
                        parts.Add(new WeaponPart("SuppressorEndCap", WeaponPartShape.Cylinder, new Vector3(0f, 0f, length - 0.004f), new Vector3(0.038f, 0.038f, 0.008f), WeaponPaint.Furniture));
                        return new AttachmentShape(parts, WeaponSockets.MuzzleName, new Vector3(0f, 0f, length));
                    }

                case CompensatorId:
                    {
                        const float length = 0.05f;
                        parts.Add(new WeaponPart("CompensatorBody", WeaponPartShape.Box, new Vector3(0f, 0f, length * 0.5f), new Vector3(0.028f, 0.028f, length), WeaponPaint.Furniture));
                        for (var i = 0; i < 3; i++)
                        {
                            var z = length * (0.25f + (i * 0.25f));
                            parts.Add(new WeaponPart("CompensatorPort_" + i, WeaponPartShape.Box, new Vector3(0f, 0.0145f, z), new Vector3(0.018f, 0.002f, 0.006f), WeaponPaint.Body));
                        }

                        return new AttachmentShape(parts, WeaponSockets.MuzzleName, new Vector3(0f, 0f, length));
                    }

                case ForegripId:
                    parts.Add(new WeaponPart("ForegripClamp", WeaponPartShape.Box, new Vector3(0f, -0.006f, 0f), new Vector3(0.03f, 0.012f, 0.045f), WeaponPaint.Furniture));
                    parts.Add(new WeaponPart("ForegripHandle", WeaponPartShape.Cylinder, new Vector3(0f, -0.056f, 0f), new Vector3(0.026f, 0.026f, 0.09f), WeaponPaint.Body, new Vector3(-90f, 0f, 0f)));
                    parts.Add(new WeaponPart("ForegripCap", WeaponPartShape.Cylinder, new Vector3(0f, -0.103f, 0f), new Vector3(0.03f, 0.03f, 0.006f), WeaponPaint.Furniture, new Vector3(-90f, 0f, 0f)));
                    return new AttachmentShape(parts, null, Vector3.zero);

                case ExtendedMagazineId:
                    parts.Add(new WeaponPart("MagazineBody", WeaponPartShape.Box, new Vector3(0f, -0.065f, 0.006f), new Vector3(0.026f, 0.13f, 0.05f), WeaponPaint.Body, new Vector3(-8f, 0f, 0f)));
                    parts.Add(new WeaponPart("MagazineFloorPlate", WeaponPartShape.Box, new Vector3(0f, -0.132f, 0.016f), new Vector3(0.032f, 0.01f, 0.058f), WeaponPaint.Furniture, new Vector3(-8f, 0f, 0f)));
                    return new AttachmentShape(parts, null, Vector3.zero);

                default:
                    throw new ArgumentException($"no attachment model for item '{itemId}'", nameof(itemId));
            }
        }

        public static Color ColorOf(WeaponPaint paint)
        {
            switch (paint)
            {
                case WeaponPaint.Body:
                    return HousingColor;
                case WeaponPaint.Furniture:
                    return MetalColor;
                case WeaponPaint.Accent:
                    return ReticleCatalogAssetStep.RedDotColor;
                case WeaponPaint.Glass:
                    return WeaponLook.GlassColor;
                default:
                    throw new ArgumentOutOfRangeException(nameof(paint), paint, "unknown paint");
            }
        }

        public static AttachmentModelCatalog BuildAt(string path, string modelFolder)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            modelFolder = PresentationAssetBuilder.ValidateFolderPath(modelFolder, nameof(modelFolder));
            SceneBuildHelpers.EnsureFolder(modelFolder);

            var paint = ViewmodelAssetStep.PaintCache(modelFolder);
            var entries = new List<AttachmentModelEntry>();
            foreach (var entry in Entries())
            {
                var shape = ShapeFor(entry.ItemId);
                var prefab = PresentationAssetBuilder.SavePrefab(modelFolder, PrefabName(entry.ItemId), root => shape.Build(root.transform, paint));
                entries.Add(new AttachmentModelEntry(entry.ItemId, prefab, entry.Anchor, entry.PlaceholderSize));
            }

            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<AttachmentModelCatalog>(path, c => c.SetAttachments(entries));
            Debug.Log($"{LogTag} saved {entries.Count} model(s) to '{modelFolder}' and '{path}' attachments={catalog.Attachments.Count}");
            return catalog;
        }
    }

    public sealed class AttachmentShape
    {
        public AttachmentShape(IReadOnlyList<WeaponPart> parts, string socketName, Vector3 socketPosition)
        {
            Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            SocketName = socketName;
            SocketPosition = socketPosition;
        }

        public IReadOnlyList<WeaponPart> Parts { get; }

        public string SocketName { get; }

        public Vector3 SocketPosition { get; }

        public bool HasPart(string name)
        {
            foreach (var part in Parts)
            {
                if (string.Equals(part.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // 付けた物は銃のモデルの子として小さく映るので、影は落とさない。
        public void Build(Transform root, Func<WeaponPaint, Color, Material> paint)
        {
            WeaponLook.BuildParts(root, Parts, part => paint(part.Paint, AttachmentModelCatalogAssetStep.ColorOf(part.Paint)), castShadows: false);
            if (!string.IsNullOrEmpty(SocketName))
            {
                var socket = new GameObject(SocketName).transform;
                socket.SetParent(root, false);
                socket.localPosition = SocketPosition;
            }
        }
    }
}
