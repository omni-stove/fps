using System;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、サーバーが動かす Bot (<see cref="NetPlayer.IsBot"/>) の入力を受ける処理。
    /// </summary>
    /// <remarks>
    /// Bot はサーバーの上で <see cref="NetworkPlayerRole.ServerSimulated"/> として動かし、人のクライアントから
    /// <see cref="SubmitInputRpc"/> で届いた入力と同じ <see cref="ApplyAuthoritative"/> を通す (ルールで使えない動き・ダウン中の制限も同じ)。
    /// tick はサーバーが 1 つずつ数える。他のクライアントは人と同じく確定状態を補間して表示する。
    /// </remarks>
    public sealed partial class NetworkPlayerMotor
    {
        private bool _isBot;

        /// <summary>サーバーで Bot として入力を受けているか (<see cref="ServerApplyBotInput"/> を呼べるか)。</summary>
        internal bool AcceptsBotInput => _isBot && _role == NetworkPlayerRole.ServerSimulated;

        /// <summary>
        /// サーバーで、Bot の 1 tick 分の入力で動かす。呼ぶ側が <see cref="TickSeconds"/> ごとに 1 回呼ぶ。
        /// ダウンと乗り越えてよいかは入力の値ではなくサーバーの知る状態で決め、カバー・覗き込み・回避は使わない。
        /// </summary>
        /// <exception cref="InvalidOperationException">Bot として生成されていない (<see cref="AcceptsBotInput"/> が false)。</exception>
        /// <exception cref="ArgumentException">移動か向きが有限でない。</exception>
        internal void ServerApplyBotInput(MotorInput input)
        {
            if (!AcceptsBotInput)
            {
                throw new InvalidOperationException(name + " is not a bot driven by the server (role=" + _role + ")");
            }

            if (!MotorInput.IsValid(input.Move, input.Yaw))
            {
                throw new ArgumentException("bot input must be finite", nameof(input));
            }

            var tick = unchecked(_lastProcessedTick + 1);
            ApplyAuthoritative(tick, new MotorInput(input.Move, input.Yaw, input.Sprint, input.Jump, input.Crouch, _downed, false, false, false, false));
        }
    }
}
