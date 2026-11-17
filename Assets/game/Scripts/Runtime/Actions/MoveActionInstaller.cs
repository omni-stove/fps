using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class MoveActionInstaller : IPlayerActionInstaller
    {
        // CoverActionInstaller が付けたカバーを読むので、その後に呼ぶ。
        public const int InstallOrder = 300;

        public int Order => InstallOrder;

        // PlayerController は回避と乗り越えの入力と動きを持つので付け足さない。無ければ (標的など移動しない物) どちらも無い。
        // 乗り越えと崖のぼりはカバー中に止めなくても、カバーが Jump を消すので始まらない。
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
            mover.MantleRule = rules.Actions.Mantle;
            mover.JumpRule = rules.Actions.Jump;
        }
    }
}
