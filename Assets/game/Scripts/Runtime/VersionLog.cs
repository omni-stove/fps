using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
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
