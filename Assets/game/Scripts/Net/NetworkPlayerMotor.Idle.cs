using System;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、サーバーが受け取ったリモートのクライアントの入力を、放置の判定 (<see cref="NetTeamDeathmatch"/>) に渡す処理。
    /// </summary>
    public sealed partial class NetworkPlayerMotor
    {
        /// <summary>
        /// サーバーで、リモートのオーナーから届いた入力 (<see cref="SubmitInputRpc"/>) を検査して受け入れた直後に呼ばれる。
        /// 引数はクライアントが送ってきたままの入力 (ルールで捨てる押下も残し、ダウン中も制限しない)。
        /// ホスト自身のプレイヤーの入力では呼ばない。
        /// </summary>
        internal event Action<MotorInput> ServerInputReceived;

        /// <summary>
        /// previous から current への変化を、プレイヤーが操作した印とみなすか。移動入力・向き・ダッシュ・しゃがみ・覗き込みが変わったか、
        /// ジャンプ・カバー・回避が押されていれば true。<see cref="MotorInput.Downed"/> と <see cref="MotorInput.VaultAllowed"/> は
        /// サーバーが入れる値なので見ない。
        /// </summary>
        /// <remarks>
        /// 押しっぱなしの移動 (重しを載せたキー) は変化ではないので、放置として数える。
        /// 向きは視点の yaw をそのまま送っているので、マウスを止めていれば同じ値が届く。
        /// </remarks>
        internal static bool IsActivity(MotorInput previous, MotorInput current)
        {
            return current.Jump
                || current.Cover
                || current.Dodge
                || current.Move != previous.Move
                || current.Yaw != previous.Yaw
                || current.Sprint != previous.Sprint
                || current.Crouch != previous.Crouch
                || current.Aim != previous.Aim;
        }

        private void RaiseServerInputReceived(MotorInput input)
        {
            ServerInputReceived?.Invoke(input);
        }
    }
}
