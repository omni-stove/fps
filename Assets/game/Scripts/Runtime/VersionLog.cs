using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 起動時にビルドの版 (Application.version、ビルド時に <c>Fps.Editor.BuildVersionStamper</c> が入れたもの) を
    /// <c>[Version] 0.2.0</c> の形でログに出す。クライアントと専用サーバーのどちらのビルドにも入り、どのシーンから始めても出る。
    /// </summary>
    public static class VersionLog
    {
        // シーンの読み込みより前に出し、最初のシーンの Awake が出すログや終了より先にログの頭へ来るようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LogOnStartup()
        {
            Debug.Log(BuildVersion.LogLine(Application.version));
        }
    }
}
