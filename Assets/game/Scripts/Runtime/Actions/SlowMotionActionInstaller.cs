using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ルールの <see cref="SlowMotion"/> を <see cref="TimeScaleArbiter.ApplyRules"/> に入れる。持ち主はプレイヤーにするので、
    /// プレイヤーが消える (シーンを離れる) とルールは Off に戻る。<see cref="PlayerActions.Attach"/> はオフラインでだけ呼ばれる。
    /// </summary>
    public sealed class SlowMotionActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションのコンポーネントは読まない。</summary>
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        // 時間の速さはプレイヤーではなく全体のものなので、プレイヤーにはコンポーネントを付けない。
        public void Install(GameObject player, GameRules rules)
        {
            TimeScaleArbiter.ApplyRules(rules.Actions.SlowMotion, player);
        }
    }
}
