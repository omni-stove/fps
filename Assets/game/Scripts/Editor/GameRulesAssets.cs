using System;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;

namespace Fps.Editor
{
    /// <summary>
    /// ルールのアセット (<see cref="GameRulesDefinition"/>) に値を入れ、読み直して確かめる。
    /// どのルールを作るかは持たず、JSON の取り込みやゲームのビルダーがここを通して保存する。
    /// </summary>
    public static class GameRulesAssets
    {
        /// <summary>ルールのアセットを置く、出力先ルート (<see cref="FpsKitRoot.Output"/>) の下のフォルダ。</summary>
        public static string Folder => FpsKitRoot.OutputPath("Rules");

        /// <summary>path の <see cref="GameRulesDefinition"/> を読み、<see cref="GameRulesDefinition.ToRules"/> に通ることを確かめて返す。</summary>
        /// <exception cref="InvalidOperationException">path にルールが無い。</exception>
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

        /// <summary>definition の中身を rules の値にし、<see cref="GameRulesDefinition.ToRules"/> に通ることを確かめる。</summary>
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
