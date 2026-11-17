namespace Shooter.Protocol
{
    // 乗れるか (距離・空き・壊れていないか) はサーバーが決めるので、クライアントは乗りたい乗り物と座席を言うだけにする。
    public sealed class VehicleRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int AnySeat = -1;

        public ushort Id => MessageIds.VehicleRequest;

        public byte Version => CurrentVersion;

        public VehicleAction Action { get; set; }

        /// <summary>乗るときの乗り物の番号。降りる・移るときは今乗っている乗り物なので読まない。</summary>
        public int Vehicle { get; set; }

        public int Seat { get; set; } = AnySeat;

        public void Write(MessageWriter writer)
        {
            if (Vehicle < 0 || Seat < AnySeat)
            {
                throw new System.InvalidOperationException("vehicle must not be negative and seat must be -1 or more");
            }

            writer.WriteByte((byte)Action);
            writer.WriteInt32(Vehicle);
            writer.WriteInt32(Seat);
        }

        public void Read(MessageReader reader)
        {
            Action = (VehicleAction)reader.ReadByteBelow(EnumRange<VehicleAction>.Count, "vehicle action");
            Vehicle = reader.ReadInt32AtLeast(0, "vehicle");
            Seat = reader.ReadInt32AtLeast(AnySeat, "vehicle seat");
        }
    }
}
