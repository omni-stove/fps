using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController.Tick"/> に渡された 1 フレーム分の入力を、姿勢と移動を決める前に書き換える
    /// (<see cref="PlayerController.MoveFilter"/>)。カバー中に移動を壁沿いに絞るなど、アクションが移動を変えるのに使う。
    /// しゃがみを false にしても、頭上が塞がっていれば今までどおりしゃがんだまま。
    /// </summary>
    public interface IPlayerMoveFilter
    {
        /// <param name="move">移動入力 (x が右、y が前)。</param>
        /// <param name="sprint">ダッシュするか。</param>
        /// <param name="crouch">しゃがみたいか。</param>
        /// <param name="jumpPressed">このフレームでジャンプを押したか。</param>
        void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed);
    }
}
