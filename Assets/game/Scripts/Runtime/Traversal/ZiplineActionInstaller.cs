using Fps.Core;
using UnityEngine;

namespace Fps.Runtime.Traversal
{
    public sealed class ZiplineActionInstaller : IPlayerActionInstaller
    {
        // 組み込みのカバー (100) を読むので、その後。回避・乗り越え (300)・グレネード (400) とは関わらない。
        public const int InstallOrder = 500;

        public int Order => InstallOrder;

        // 組み込みの並びに足さず自分で登録するのは、IPlayerActionInstaller の決まりどおり既存のファイルを変えずに足すため。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAtStartup()
        {
            PlayerActions.Register(new ZiplineActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new ZiplineActionInstaller());
        }
#endif

        // PlayerController の無い物 (標的など) は動かないので付けない。
        public void Install(GameObject player, GameRules rules)
        {
            if (player.GetComponent<PlayerController>() == null)
            {
                return;
            }

            PlayerActions.GetOrAdd<ZiplineMovement>(player).Configure(rules);
        }
    }
}
