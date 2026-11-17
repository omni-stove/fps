namespace Shooter.Protocol
{
    // 既定値 (default) を乗っていないことにするため、番号に 1 を足して持つ。
    public struct VehicleSeatSync
    {
        private readonly int _vehiclePlusOne;

        public const int NoVehicle = -1;

        public static VehicleSeatSync None => default;

        public VehicleSeatSync(int vehicle, int seat)
        {
            _vehiclePlusOne = vehicle + 1;
            Seat = vehicle == NoVehicle ? 0 : seat;
        }

        public int Vehicle => _vehiclePlusOne - 1;

        public int Seat { get; }

        public bool IsRiding => Vehicle != NoVehicle;

        public bool SameAs(in VehicleSeatSync other)
        {
            return Vehicle == other.Vehicle && Seat == other.Seat;
        }

        internal void Write(MessageWriter writer)
        {
            if (Vehicle < NoVehicle || Seat < 0 || Seat > byte.MaxValue)
            {
                throw new System.InvalidOperationException("vehicle must be -1 or more and seat 0 to 255");
            }

            writer.WriteInt32(Vehicle);
            writer.WriteByte((byte)Seat);
        }

        internal static VehicleSeatSync Read(MessageReader reader)
        {
            var vehicle = reader.ReadInt32AtLeast(NoVehicle, "riding vehicle");
            var seat = reader.ReadByte();
            if (vehicle == NoVehicle && seat != 0)
            {
                throw new MessageReadException("a player who is not riding has seat " + seat);
            }

            return new VehicleSeatSync(vehicle, seat);
        }
    }
}
