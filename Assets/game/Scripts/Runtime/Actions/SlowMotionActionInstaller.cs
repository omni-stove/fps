using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 持ち主をプレイヤーにするのは、プレイヤーが消える (シーンを離れる) とルールを Off に戻すため。
    public sealed class SlowMotionActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントは読まないので、並びは問わない。
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        // 時間の速さはプレイヤーではなく全体のものなので、プレイヤーにはコンポーネントを付けない。
        public void Install(GameObject player, GameRules rules)
        {
            TimeScaleArbiter.ApplyRules(rules.Actions.SlowMotion, player);
        }
    }
}
