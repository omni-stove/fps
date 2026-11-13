using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 遮蔽物に張り付いて身を隠すカバーを受け持つ、プレイヤーのコンポーネント。
    /// <see cref="Configure"/> でルールの <see cref="ActionRules.Cover"/> が <see cref="Cover.On"/> のときだけ有効にする。
    /// 張り付き・覗き込みの動作は後の issue で足すので、今はどのルールでも隠れず、移動にも射撃にも手を出さない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoverController : MonoBehaviour
    {
        /// <summary>遮蔽物に張り付いているか。</summary>
        public bool IsInCover => false;

        /// <summary>張り付いたまま身を乗り出して狙っているか。</summary>
        public bool IsPeeking => false;

        /// <summary>張り付いている遮蔽物の上端の高さ (ワールドの y、m)。<see cref="IsInCover"/> が false の間は意味を持たない。</summary>
        public float CoverTopY => 0f;

        /// <summary>張り付いている遮蔽物の面の、こちら向きの法線 (ワールド)。<see cref="IsInCover"/> が false の間は意味を持たない。</summary>
        public Vector3 CoverNormal => Vector3.zero;

        /// <summary>rules の <see cref="ActionRules.Cover"/> が <see cref="Cover.On"/> なら有効に、それ以外なら無効にする。</summary>
        public void Configure(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            enabled = rules.Actions.Cover == Cover.On;
        }
    }
}
