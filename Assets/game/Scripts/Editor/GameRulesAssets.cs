using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;

namespace Shooter.Editor
{
    public static class GameRulesAssets
    {
        public static string Folder => ShooterKitRoot.OutputPath("Rules");

        public static GameRulesDefinition Load(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var definition = AssetDatabase.LoadAssetAtPath<GameRulesDefinition>(path);
            if (definition == null)
            {
                throw new InvalidOperationException($"game rules '{path}' were not found; build or import the rules first");
            }

            definition.ToRules();
            return definition;
        }

        public static void Configure(GameRulesDefinition definition, GameRules rules)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            definition.CopyFrom(rules);

            // rules は検査済みだが、アセットの形で読み直せることも確かめる。不正なら保存しない。
            definition.ToRules();
        }
    }
}
