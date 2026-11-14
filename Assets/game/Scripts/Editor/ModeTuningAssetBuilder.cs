using System;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 陣取り系のモードの調整値のアセット (<see cref="Folder"/> の <see cref="DominationDefinition"/> と <see cref="HardpointDefinition"/>) を、
    /// 無ければ既定値で作る。既にあれば中身を変えずに、値が通ることだけ確かめる (ゲームが書き換えた値をシーンを組み直すたびに戻さないため)。
    /// 値が不正なら例外を投げる (batchmode では Unity が終了コード 1 で終わる)。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ModeTuningAssetBuilder.Build</code>
    /// </summary>
    public static class ModeTuningAssetBuilder
    {
        public const string MenuPath = "Fps/Build Mode Tuning Assets";
        public const string LogTag = "[ModeTuningAssetBuilder]";

        /// <summary>調整値のアセットを置く、出力先ルート (<see cref="FpsKitRoot.Output"/>) の下のフォルダ。</summary>
        public static string Folder => FpsKitRoot.OutputPath("Modes");

        public static string DominationPath => Folder + "/Domination.asset";
        public static string HardpointPath => Folder + "/Hardpoint.asset";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var domination = LoadOrCreateDomination(DominationPath);
            var hardpoint = LoadOrCreateHardpoint(HardpointPath);
            Debug.Log($"{LogTag} ready '{AssetDatabase.GetAssetPath(domination)}', '{AssetDatabase.GetAssetPath(hardpoint)}'");
        }

        /// <summary>path の <see cref="DominationDefinition"/> を返す。無ければ既定値で作って保存する。値が不正なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public static DominationDefinition LoadOrCreateDomination(string path)
        {
            return LoadOrCreate<DominationDefinition>(path, d => d.Validate());
        }

        /// <summary>path の <see cref="HardpointDefinition"/> を返す。無ければ既定値で作って保存する。値が不正なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public static HardpointDefinition LoadOrCreateHardpoint(string path)
        {
            return LoadOrCreate<HardpointDefinition>(path, d => d.Validate());
        }

        private static T LoadOrCreate<T>(string path, Action<T> validate)
            where T : ScriptableObject
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                validate(existing);
                return existing;
            }

            // 別の型のアセットが path にあれば CreateOrUpdateAsset が断る。
            return SceneBuildHelpers.CreateOrUpdateAsset(path, validate);
        }
    }
}
