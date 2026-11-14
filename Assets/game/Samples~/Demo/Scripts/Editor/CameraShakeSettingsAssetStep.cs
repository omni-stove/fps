using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    public sealed class CameraShakeSettingsAssetStep : IPresentationAssetStep
    {
        public const int StepOrder = 100;

        public const string MenuPath = "Fps/Build Camera Shake Settings";
        public const string LogTag = "[CameraShakeSettingsAssetStep]";

        public static string AssetPath => PresentationAssetBuilder.PresentationFolder + "/Resources/" + CameraShakeSettings.ResourcePath + ".asset";

        public string Name => "CameraShakeSettings";

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

        // 仮素材の規約 (2 回走らせても差分が出ない) に合わせ、手で変えた値も既定値へ戻す。調整した値を残すならこの手順を走らせない。
        public static CameraShakeSettings BuildAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            var settings = SceneBuildHelpers.CreateOrUpdateAsset<CameraShakeSettings>(path, s =>
            {
                // 既存のアセットを書き換える前に既定値を検査し、不正ならメモリ上の中身も汚さない。
                var probe = ScriptableObject.CreateInstance<CameraShakeSettings>();
                try
                {
                    probe.ResetToDefaults();
                    probe.Validate();
                }
                finally
                {
                    Object.DestroyImmediate(probe);
                }

                s.ResetToDefaults();
            });

            Debug.Log($"{LogTag} saved '{path}'");
            return settings;
        }
    }
}
