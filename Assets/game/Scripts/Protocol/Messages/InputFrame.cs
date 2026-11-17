using System.Numerics;

namespace Shooter.Protocol
{
    // ダウン中か・乗り越えてよいかはクライアントの申告を信じないので送らない。
    public struct InputFrame
    {
        public int Tick { get; set; }

        public Vector2 Move { get; set; }

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
