using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class DamageNumberCatalogAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = 100;

        public const string MenuPath = "Shooter/Build Damage Number Catalog";
        public const string LogTag = "[DamageNumberCatalogAssetStep]";

        public static string AssetPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + DamageNumberCatalog.ResourcePath + ".asset";

        public string Name => "DamageNumberCatalog";

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

        // 仮素材の規約 (2 回走らせても差分が出ない) に合わせ、手で変えた値も既定値へ戻す。
        // キットの既定は出さない (Enabled = false) だが、デモは当てた量を確かめながら調整するために出す。
        public static DamageNumberCatalog BuildAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var catalog = SceneBuildHelpers.CreateOrUpdateAsset<DamageNumberCatalog>(path, c =>
            {
                // 既存のアセットを書き換える前に既定値を検査し、不正ならメモリ上の中身も汚さない。
                var probe = ScriptableObject.CreateInstance<DamageNumberCatalog>();
                try
                {
                    Configure(probe);
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                Configure(c);
            });

            Debug.Log($"{LogTag} saved '{path}'");
            return catalog;
        }

        private static void Configure(DamageNumberCatalog catalog)
        {
            catalog.ResetToDefaults();
            catalog.Enabled = true;
        }
    }
}
