using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class TeamDeathmatchHudFormatter : ModeHudFormatterBase
    {
        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ModeHudFormatters.Register<ModeSession>(new TeamDeathmatchHudFormatter());
        }
    }
}
