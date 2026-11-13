using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetBootstrap"/> のうち、<c>-map</c> (<see cref="Fps.Core.NetLaunchOptions.Map"/>) で選んだマップで試合を始める処理。
    /// <list type="bullet">
    /// <item>ホスト・サーバーは、<c>-map</c> の名前のシーンが今のシーンでなければ、NetworkManager を組み立てる前にそのシーンへ移り
    /// (<see cref="MapHandOff"/>)、起動はそのシーンに置かれた NetBootstrap に任せる。マップのシーンは NetTest と同じ配線の NetBootstrap を持つ。</item>
    /// <item>名前のシーンがビルドに無ければ、起動の前 (<see cref="MapBeforeStart"/>) に理由を出して <see cref="ExitMisconfigured"/> で失敗する。</item>
    /// <item>クライアントは Netcode のシーン管理 (<see cref="NetworkSetup"/> が有効にする) でサーバーのシーンへ合わせる。
    /// 合わせ終えたことをサーバー・クライアントの両方でログに出す (<see cref="MapAfterStart"/>)。</item>
    /// </list>
    /// 指定が無ければ今のシーン (NetTest) のまま。
    /// </summary>
    public sealed partial class NetBootstrap
    {
        /// <summary>シーンのファイルの拡張子。ビルドのシーンはパスでしか引けないので、名前と突き合わせるときに外す。</summary>
        private const string SceneExtension = ".unity";

        // NetworkManager を足すとこの GameObject は DontDestroyOnLoad のシーンへ移るので、置かれていたシーンは組み立てる前に覚える
        private string _bootSceneName;
        private string _bootScenePath;

        /// <summary>
        /// オプションを読んだ後、NetworkManager や EOS のロビーを組み立てる前に呼ぶ (ロビー経由の起動では呼ばない)。
        /// </summary>
        /// <param name="handedOff">別のシーンへ移ってこの NetBootstrap を消すなら true にする。以後の起動処理は行わない。</param>
        partial void MapHandOff(ref bool handedOff);

        /// <summary>NetworkManager を組み立てた後、待ち受け・接続を始める前に呼ぶ。</summary>
        /// <param name="error">起動を止めるならその理由を入れる (<c>[Net]</c> 付きの文)。既に入っていれば触らない。</param>
        partial void MapBeforeStart(ref string error);

        /// <summary>待ち受け・接続を始められた直後に呼ぶ。</summary>
        partial void MapAfterStart();

        /// <summary>終了を求められたときに呼ぶ。</summary>
        /// <param name="allowQuit">終了を待たせるなら false にする。待たせたら、待ち終えたところで自分で終了する。</param>
        partial void MapQuitRequested(ref bool allowQuit);

        /// <summary>
        /// buildScenePaths (Build Settings の並び) から、ファイル名 (拡張子なし) が map と一致するシーンを探す。
        /// 大文字・小文字も区別する。見つからなければ path は null。
        /// </summary>
        public static bool TryFindMapScene(string map, IReadOnlyList<string> buildScenePaths, out string path)
        {
            if (buildScenePaths == null)
            {
                throw new ArgumentNullException(nameof(buildScenePaths));
            }

            path = null;
            if (string.IsNullOrEmpty(map))
            {
                return false;
            }

            foreach (var candidate in buildScenePaths)
            {
                if (string.Equals(SceneNameOf(candidate), map, StringComparison.Ordinal))
                {
                    path = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary><c>-map</c> の名前のシーンがビルドに無いときのエラーの文。ビルドにあるシーンの名前を並べる。</summary>
        public static string MapNotInBuildMessage(string map, IReadOnlyList<string> buildScenePaths)
        {
            if (buildScenePaths == null)
            {
                throw new ArgumentNullException(nameof(buildScenePaths));
            }

            var names = new List<string>();
            foreach (var path in buildScenePaths)
            {
                var name = SceneNameOf(path);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            var available = names.Count == 0 ? "none" : string.Join(", ", names);
            return "[Net] " + NetLaunchOptions.MapFlag + " " + map + " is not a scene in this build (scenes: " + available
                + "); build the map with its scene builder (e.g. Fps.Editor.ArenaSceneBuilder.Build) and rebuild";
        }

        partial void MapHandOff(ref bool handedOff)
        {
            _bootSceneName = gameObject.scene.name;
            _bootScenePath = gameObject.scene.path;

            var map = _options.Map;
            if (map == null || IsInMapScene(map))
            {
                return;
            }

            // ビルドに無ければ移らず、MapBeforeStart が理由を出して失敗させる
            if (!TryFindMapScene(map, BuildScenePaths(), out var path))
            {
                return;
            }

            Debug.Log("[Net] " + NetLaunchOptions.MapFlag + " " + map + ": loading " + path
                + "; the NetBootstrap in that scene starts " + _options);
            handedOff = true;

            // このシーンが Additive で読まれていても、移った先の NetBootstrap と並んで残らないようにする
            Destroy(gameObject);
            SceneManager.LoadScene(path, LoadSceneMode.Single);
        }

        partial void MapBeforeStart(ref string error)
        {
            if (error != null)
            {
                return;
            }

            var map = _options.Map;
            if (map == null)
            {
                return;
            }

            if (IsInMapScene(map))
            {
                Debug.Log("[Net] map " + map + " (" + (_bootScenePath ?? gameObject.scene.path) + ")");
                return;
            }

            var scenes = BuildScenePaths();
            error = TryFindMapScene(map, scenes, out var path)
                ? "[Net] " + NetLaunchOptions.MapFlag + " " + map + " is in the build (" + path
                    + ") but was not loaded before starting; the network must start from that scene's NetBootstrap"
                : MapNotInBuildMessage(map, scenes);
        }

        partial void MapAfterStart()
        {
            // NetworkSceneManager は NetworkManager と一緒に消えるので、購読は外さない (消えた後は _manager が null になり何もしない)
            if (_manager.SceneManager != null)
            {
                _manager.SceneManager.OnSceneEvent += HandleMapSceneEvent;
            }
        }

        private void HandleMapSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType != SceneEventType.SynchronizeComplete || _manager == null)
            {
                return;
            }

            var map = SceneManager.GetActiveScene().name;
            if (_manager.IsServer)
            {
                if (sceneEvent.ClientId != NetworkManager.ServerClientId)
                {
                    Debug.Log("[Net] client id=" + sceneEvent.ClientId + " synchronized to map " + map);
                }

                return;
            }

            if (sceneEvent.ClientId == _manager.LocalClientId)
            {
                Debug.Log("[Net] synchronized with the server; map " + map);
            }
        }

        /// <summary>
        /// この NetBootstrap が map の名前のシーンに置かれているか。読み込み中の Awake ではアクティブなシーンがまだ切り替わっていないことがあり、
        /// それで比べると移った先でもう一度移ろうとするので、自分のシーンで比べる。
        /// </summary>
        private bool IsInMapScene(string map)
        {
            return string.Equals(_bootSceneName ?? gameObject.scene.name, map, StringComparison.Ordinal);
        }

        /// <summary>このビルド (エディタでは Build Settings) に入っているシーンのパス。</summary>
        private static IReadOnlyList<string> BuildScenePaths()
        {
            var count = SceneManager.sceneCountInBuildSettings;
            var paths = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                paths.Add(SceneUtility.GetScenePathByBuildIndex(i));
            }

            return paths;
        }

        private static string SceneNameOf(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var normalized = path.Replace('\\', '/');
            if (!normalized.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.GetFileNameWithoutExtension(normalized);
        }
    }
}
