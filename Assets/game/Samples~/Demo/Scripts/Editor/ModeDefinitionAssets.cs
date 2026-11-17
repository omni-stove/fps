using System;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // ゲームがアセットで変えた値を、シーンを組み直すたびに既定値へ戻さないため、GameRulesAssetBuilder のように毎回上書きはしない。
    public static class ModeDefinitionAssets
    {
        public const string LogTag = "[ModeDefinitionAssets]";

        public static string Folder => ShooterKitRoot.OutputPath("Modes");

        public static string PathOf(string modeName)
        {
            if (string.IsNullOrWhiteSpace(modeName))
            {
                throw new ArgumentException("mode name must not be empty", nameof(modeName));
            }

            return Folder + "/" + modeName + ".asset";
        }

        public static T LoadOrCreate<T>(string path, Action<T> configureNew)
            where T : ScriptableObject
        {
            if (configureNew == null)
            {
                throw new ArgumentNullException(nameof(configureNew));
            }

            path = SceneBuildHelpers.ValidateAssetPath(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Debug.Log($"{LogTag} using the existing '{path}'");
                return existing;
            }

            var created = SceneBuildHelpers.CreateOrUpdateAsset(path, configureNew);
            Debug.Log($"{LogTag} created '{path}' with the default values");
            return created;
        }
    }
}
