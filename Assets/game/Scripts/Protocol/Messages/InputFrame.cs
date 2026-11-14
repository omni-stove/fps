using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>
    /// 1 tick の移動の入力 (Netcode の <c>SubmitInputRpc</c> の中身)。ダウン中か・乗り越えてよいかはクライアントの申告を信じないので送らない。
    /// </summary>
    public struct InputFrame
    {
        public int Tick { get; set; }

        /// <summary>移動入力。X が右、Y が前。長さはサーバーが 1 以下に絞る。</summary>
        public Vector2 Move { get; set; }

        /// <summary>向き (Y 軸まわり、ラジアン)。</summary>
        public float Yaw { get; set; }

        public InputButtons Buttons { get; set; }

        public InputFrame(int tick, Vector2 move, float yaw, InputButtons buttons)
        {
            Tick = tick;
            Move = move;
            Yaw = yaw;
            Buttons = buttons;
        }

        public bool Has(InputButtons button)
        {
            return (Buttons & button) == button;
        }
    }
}
