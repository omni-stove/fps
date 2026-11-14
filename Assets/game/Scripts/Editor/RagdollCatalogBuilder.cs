using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    /// <summary>
    /// <see cref="CharacterRigBuilder"/> の仮リグに合わせた <see cref="RagdollCatalog"/> を作るか上書きする。
    /// コライダーの大きさはリグの見た目の部品 (<see cref="CharacterRigBuilder.BoneVolumes"/>) から取るので、部品を変えれば次の実行で追いつく。
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.RagdollCatalogBuilder.Build</code>
    /// </summary>
    public static class RagdollCatalogBuilder
    {
        public const string MenuPath = "Fps/Build Ragdoll Catalog";
        public const string LogTag = "[RagdollCatalogBuilder]";

        public static string CatalogPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + RagdollCatalog.ResourcePath + ".asset";

        // 仮リグは休みの姿勢で関節の回転が 0 で、手足は -Y、胴と頭は +Y に伸びる。肘と膝は曲がる向きが片側だけなので、
        // 左右に非対称にできるねじれ (X 軸まわり) を曲げに使い、振りはほとんど止める。
        private static readonly BoneJoint[] Joints =
        {
            new BoneJoint(CharacterRigBuilder.HipsBone, RagdollShape.Box, 12f, Vector3.up, Vector3.right, 0f, 0f, 0f, 0f),
            new BoneJoint(CharacterRigBuilder.TorsoBone, RagdollShape.Box, 20f, Vector3.up, Vector3.right, -15f, 15f, 30f, 15f),
            new BoneJoint(CharacterRigBuilder.HeadBone, RagdollShape.Sphere, 5f, Vector3.up, Vector3.right, -40f, 40f, 30f, 20f),
            new BoneJoint(CharacterRigBuilder.LeftUpperArmBone, RagdollShape.Capsule, 3f, Vector3.up, Vector3.right, -60f, 60f, 90f, 60f),
            new BoneJoint(CharacterRigBuilder.LeftForearmBone, RagdollShape.Capsule, 2f, Vector3.right, Vector3.up, -130f, 0f, 10f, 10f),
            new BoneJoint(CharacterRigBuilder.RightUpperArmBone, RagdollShape.Capsule, 3f, Vector3.up, Vector3.right, -60f, 60f, 90f, 60f),
            new BoneJoint(CharacterRigBuilder.RightForearmBone, RagdollShape.Capsule, 2f, Vector3.right, Vector3.up, -130f, 0f, 10f, 10f),
            new BoneJoint(CharacterRigBuilder.LeftThighBone, RagdollShape.Capsule, 8f, Vector3.up, Vector3.right, -30f, 30f, 70f, 40f),
            new BoneJoint(CharacterRigBuilder.LeftShinBone, RagdollShape.Capsule, 4f, Vector3.right, Vector3.up, 0f, 130f, 10f, 10f),
            new BoneJoint(CharacterRigBuilder.RightThighBone, RagdollShape.Capsule, 8f, Vector3.up, Vector3.right, -30f, 30f, 70f, 40f),
            new BoneJoint(CharacterRigBuilder.RightShinBone, RagdollShape.Capsule, 4f, Vector3.right, Vector3.up, 0f, 130f, 10f, 10f),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(CatalogPath);
        }

        /// <summary>仮リグのボーンの並び (根に近い順) で対応表の中身を返す。仮リグと表の関節がずれていれば例外を投げる。</summary>
        public static IReadOnlyList<RagdollBone> Entries()
        {
            var volumes = new Dictionary<string, Bounds>(StringComparer.Ordinal);
            foreach (var (bone, volume) in CharacterRigBuilder.BoneVolumes())
            {
                volumes.Add(bone, volume);
            }

            var joints = new Dictionary<string, BoneJoint>(StringComparer.Ordinal);
            foreach (var joint in Joints)
            {
                if (!joints.TryAdd(joint.Bone, joint))
                {
                    throw new InvalidOperationException($"ragdoll joint for bone '{joint.Bone}' is listed more than once");
                }
            }

            var entries = new List<RagdollBone>();
            foreach (var bone in CharacterRigBuilder.BoneNames)
            {
                var hasVolume = volumes.TryGetValue(bone, out var volume);
                var hasJoint = joints.TryGetValue(bone, out var joint);
                if (hasVolume != hasJoint)
                {
                    throw new InvalidOperationException(hasVolume
                        ? $"bone '{bone}' of the character rig has parts but no ragdoll joint"
                        : $"ragdoll joint for bone '{bone}' has no parts in the character rig to size its collider");
                }

                if (!hasVolume)
                {
                    continue;
                }

                joints.Remove(bone);
                entries.Add(new RagdollBone(
                    bone,
                    joint.Shape,
                    volume.center,
                    volume.size,
                    joint.Mass,
                    joint.TwistAxis,
                    joint.SwingAxis,
                    joint.LowTwist,
                    joint.HighTwist,
                    joint.Swing1,
                    joint.Swing2));
            }

            if (joints.Count > 0)
            {
                throw new InvalidOperationException(
                    $"ragdoll joints for bones the character rig does not have: {string.Join(", ", joints.Keys)}");
            }

            return entries;
        }

        public static RagdollCatalog BuildAt(string catalogPath)
        {
            catalogPath = SceneBuildHelpers.ValidateAssetPath(catalogPath);
            var entries = Entries();
            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<RagdollCatalog>(catalogPath, c =>
            {
                // 既存のアセットを書き換える前に、同じ中身の一時インスタンスで検査する (不正ならメモリ上の中身も汚さない)。
                var probe = ScriptableObject.CreateInstance<RagdollCatalog>();
                try
                {
                    probe.name = Path.GetFileNameWithoutExtension(catalogPath);
                    Configure(probe, entries);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                Configure(c, entries);
            });

            Debug.Log($"{LogTag} saved {entries.Count} bone(s) to '{catalogPath}'");
            return catalog;
        }

        private static void Configure(RagdollCatalog catalog, IReadOnlyList<RagdollBone> entries)
        {
            catalog.SetBones(entries);
            catalog.SettleSeconds = RagdollCatalog.DefaultSettleSeconds;
            catalog.BaseSpeed = RagdollCatalog.DefaultBaseSpeed;
            catalog.SpeedPerDamage = RagdollCatalog.DefaultSpeedPerDamage;
            catalog.MaxSpeed = RagdollCatalog.DefaultMaxSpeed;
        }

        private readonly struct BoneJoint
        {
            public string Bone { get; }
            public RagdollShape Shape { get; }
            public float Mass { get; }
            public Vector3 TwistAxis { get; }
            public Vector3 SwingAxis { get; }
            public float LowTwist { get; }
            public float HighTwist { get; }
            public float Swing1 { get; }
            public float Swing2 { get; }

            public BoneJoint(
                string bone, RagdollShape shape, float mass, Vector3 twistAxis, Vector3 swingAxis, float lowTwist, float highTwist, float swing1, float swing2)
            {
                Bone = bone;
                Shape = shape;
                Mass = mass;
                TwistAxis = twistAxis;
                SwingAxis = swingAxis;
                LowTwist = lowTwist;
                HighTwist = highTwist;
                Swing1 = swing1;
                Swing2 = swing2;
            }
        }
    }
}
