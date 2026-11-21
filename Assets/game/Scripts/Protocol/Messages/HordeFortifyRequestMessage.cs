namespace Shooter.Protocol
{
    // Horde の防衛物を直す・置く要求。どの防衛物かも額も送らないのは、オフラインの HordeRunner.TryInteract と同じく
    // サーバーが自分の体の位置からいちばん近い防衛物を選び、通貨と額を決めて、クライアントの言い値を信じないため。
    public sealed class HordeFortifyRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.HordeFortifyRequest;

        public byte Version => CurrentVersion;

        public void Write(MessageWriter writer)
        {
        }

        public void Read(MessageReader reader)
        {
        }
    }
}
