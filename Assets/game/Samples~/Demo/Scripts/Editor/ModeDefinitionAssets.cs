using System;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// モードの調整値のアセット (<see cref="Folder"/> の <c>&lt;モード名&gt;.asset</c>) を、シーンの生成スクリプトが Runner に配線するために用意する。
    /// 既にあるアセットは中身を書き換えずにそのまま返す。
    /// </summary>
    /// <remarks>
    /// ゲームがアセットで変えた値を、シーンを組み直すたびに既定値へ戻さないため、GameRulesAssetBuilder のように毎回上書きはしない。
    /// </remarks>
    public static class ModeDefinitionAssets
    {
        public const string LogTag = "[ModeDefinitionAssets]";

        /// <summary>モードの調整値のアセットを置く、出力先ルート (<see cref="FpsKitRoot.Output"/>) の下のフォルダ。</summary>
        public static string Folder => FpsKitRoot.OutputPath("Modes");

        /// <summary><see cref="Folder"/> の下の <c>&lt;modeName&gt;.asset</c>。</summary>
        public static string PathOf(string modeName)
        {
            if (string.IsNullOrWhiteSpace(modeName))
            {
                throw new ArgumentException("mode name must not be empty", nameof(modeName));
            }

            return Folder + "/" + modeName + ".asset";
        }

        /// <summary>
        /// path にある型 T のアセットを返す。無ければ configureNew で値を入れて作り、保存して返す。
        /// path に別の型のアセットがあれば <see cref="InvalidOperationException"/>。
        /// </summary>
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
