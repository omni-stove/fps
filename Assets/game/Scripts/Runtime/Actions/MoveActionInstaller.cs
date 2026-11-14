using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーの <see cref="PlayerController"/> の回避 (<see cref="PlayerController.DodgeRule"/>) と乗り越え (<see cref="PlayerController.VaultRule"/>) を
    /// ルールにし、カバー中は回避させない。
    /// </summary>
    public sealed class MoveActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。<see cref="CoverActionInstaller"/> が付けたカバーを読むので、その後に呼ぶ。</summary>
        public const int InstallOrder = 300;

        public int Order => InstallOrder;

        // PlayerController は回避と乗り越えの入力と動きを持つので付け足さない。無ければ (標的など移動しない物) どちらも無い。
        // 乗り越えはカバー中に止めなくても、カバーが Jump を消すので始まらない。
        public void Install(GameObject player, GameRules rules)
        {
            var mover = player.GetComponent<PlayerController>();
            if (mover == null)
            {
                return;
            }

            var cover = player.GetComponent<CoverController>();
            mover.DodgeRule = rules.Actions.Dodge;
            mover.DodgeBlocker = () => cover != null && cover.IsInCover;
            mover.VaultRule = rules.Actions.Vault;
        }
    }
}
