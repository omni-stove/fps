using System;
using System.Numerics;

namespace Shooter.Protocol
{
    // ダウン中か・乗り越えてよいかはクライアントの申告を信じないので送らない。
    public struct InputFrame
    {
        // 真上と真下を向く角 (ラジアン)。これより大きい縦の向きは届いても断る
        public const float MaxPitch = (float)(Math.PI / 2d);

        public int Tick { get; set; }

        public Vector2 Move { get; set; }

        public float Yaw { get; set; }

        // yaw と同じくラジアンで、上を向くと正、水平で 0。グラップルの縄を撃つ向きに使う
        public float Pitch { get; set; }

        public InputButtons Buttons { get; set; }

        public InputFrame(int tick, Vector2 move, float yaw, InputButtons buttons)
            : this(tick, move, yaw, buttons, 0f)
        {
        }

        public InputFrame(int tick, Vector2 move, float yaw, InputButtons buttons, float pitch)
        {
            Tick = tick;
            Move = move;
            Yaw = yaw;
            Buttons = buttons;
            Pitch = pitch;
        }

        public bool Has(InputButtons button)
        {
            return (Buttons & button) == button;
        }
    }
}
