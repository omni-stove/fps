using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 遮蔽物に隠れたまま狙わずに撃つブラインドファイアを受け持つ、プレイヤーのコンポーネント。
    /// <see cref="Configure"/> でルールの <see cref="ActionRules.BlindFire"/> が <see cref="BlindFire.On"/> のときだけ有効にする。
    /// 撃ち方は後の issue で足すので、今はどのルールでも射撃に手を出さない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlindFireController : MonoBehaviour
    {
        /// <summary>rules の <see cref="ActionRules.BlindFire"/> が <see cref="BlindFire.On"/> なら有効に、それ以外なら無効にする。</summary>
        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            enabled = rules.Actions.BlindFire == BlindFire.On;
        }
    }
}
