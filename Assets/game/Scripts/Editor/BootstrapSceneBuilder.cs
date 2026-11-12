using System;
using System.Collections.Generic;
using System.IO;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// Bootstrap シーンを生成する。シーンには <see cref="GameBootstrap"/> を置いた GameObject だけを入れ、
    /// 残りは実行時に GameBootstrap がコードで作る。保存したシーンは Build Settings の先頭に登録する。
    /// Editor を閉じた状態で次のように呼べる (保存先は -bootstrapScenePath で変えられる):
    /// <code>unity -batchmode -quit -executeMethod Fps.Editor.BootstrapSceneBuilder.Build</code>
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public const string DefaultScenePath = "Assets/game/Scenes/Bootstrap.unity";
        public const string ScenePathArgument = "-bootstrapScenePath";
        public const string BootstrapObjectName = "GameBootstrap";
        public const string MenuPath = "Fps/Build Bootstrap Scene";

        /// <summary>
        /// コマンドライン引数で決まる場所に Bootstrap シーンを生成し、Build Settings の先頭に登録する。
        /// 失敗したら例外を投げる (batchmode では Unity が終了コード 1 で終わる)。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Build()
        {
            // メニューから呼ばれたとき、開いているシーンの未保存の変更を黙って捨てない。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[BootstrapSceneBuilder] cancelled");
                return;
            }

            var path = ResolveScenePath(Environment.GetCommandLineArgs());
            BuildAt(path);
        }

        /// <summary>path に Bootstrap シーンを生成・保存し、Build Settings の先頭に登録する。</summary>
        public static void BuildAt(string path)
        {
            path = ValidateScenePath(path);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateScene(scene);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException($"Failed to save the bootstrap scene to '{path}'");
            }

            EditorBuildSettings.scenes = WithSceneFirst(EditorBuildSettings.scenes, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BootstrapSceneBuilder] saved '{path}' and registered it first in Build Settings");
        }

        /// <summary>
        /// 空のシーンに GameBootstrap を置いた GameObject を 1 つだけ入れる。参照は実行時にコードで解決するので配線はしない。
        /// </summary>
        public static GameObject PopulateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a valid, loaded scene", nameof(scene));
            }

            if (scene.rootCount != 0)
            {
                throw new InvalidOperationException(
                    $"scene '{scene.name}' already has {scene.rootCount} root object(s); the bootstrap scene must contain only {BootstrapObjectName}");
            }

            var bootstrap = new GameObject(BootstrapObjectName, typeof(GameBootstrap));
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return bootstrap;
        }

        /// <summary>
        /// Build Settings のシーン一覧の先頭に path を有効な状態で置く。既にあるものは先頭へ移し、
        /// 他のシーンは順番と有効/無効をそのまま残す。
        /// </summary>
        public static EditorBuildSettingsScene[] WithSceneFirst(IReadOnlyList<EditorBuildSettingsScene> current, string path)
        {
            path = ValidateScenePath(path);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            if (current != null)
            {
                foreach (var scene in current)
                {
                    if (scene == null || string.IsNullOrEmpty(scene.path) || SamePath(scene.path, path))
                    {
                        continue;
                    }

                    scenes.Add(scene);
                }
            }

            return scenes.ToArray();
        }

        /// <summary>
        /// コマンドライン引数から保存先を決める。<see cref="ScenePathArgument"/> があればその次の値、無ければ既定の場所。
        /// </summary>
        public static string ResolveScenePath(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                return DefaultScenePath;
            }

            string found = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], ScenePathArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{ScenePathArgument} needs a path such as {DefaultScenePath}", nameof(args));
                }

                if (found != null)
                {
                    throw new ArgumentException($"{ScenePathArgument} is given more than once", nameof(args));
                }

                found = args[i + 1];
                i++;
            }

            return found == null ? DefaultScenePath : ValidateScenePath(found);
        }

        /// <summary>Assets/ 以下の .unity であることを確かめ、区切りを / に揃えて返す。</summary>
        public static string ValidateScenePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("scene path must not be empty", nameof(path));
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException($"scene path '{path}' must be under Assets/", nameof(path));
            }

            if (!normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"scene path '{path}' must end with .unity", nameof(path));
            }

            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException($"scene path '{path}' must not contain empty, '.' or '..' segments", nameof(path));
                }
            }

            return normalized;
        }

        private static bool SamePath(string a, string b)
        {
            return string.Equals(a.Replace('\\', '/'), b, StringComparison.Ordinal);
        }
    }
}
