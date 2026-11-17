using System;
using System.Globalization;

namespace Shooter.Protocol
{
    // 落ちてもよい届け方で送れるよう、差分はクライアントが受け取ったと知らせてきた (InputMessage.AckedSnapshotTick)
    // スナップショットを基準に取る。
    // 壊れた建物の部品のように滅多に変わらず大きい状態は、毎 tick の差分に載せずに StructureStateMessage で送る。
    public sealed class SnapshotMessage : IMessage
    {
        public const byte CurrentVersion = 13;

        public const int NoBaseline = -1;

        public const int MaxPlayers = 64;
        public const int MaxGrenades = 64;
        public const int MaxControlPoints = 16;
        public const int MaxProjectiles = 128;
        public const int MaxVehicles = 64;

        private readonly OwnerSnapshot _owner = new OwnerSnapshot();
        private readonly SnapshotList<PlayerSnapshot> _players = new SnapshotList<PlayerSnapshot>(MaxPlayers, "player");
        private readonly SnapshotList<GrenadeSnapshot> _grenades = new SnapshotList<GrenadeSnapshot>(MaxGrenades, "grenade");
        private readonly SnapshotList<ControlPointSnapshot> _controlPoints =
            new SnapshotList<ControlPointSnapshot>(MaxControlPoints, "control point");
        private readonly SnapshotList<ProjectileSnapshot> _projectiles =
            new SnapshotList<ProjectileSnapshot>(MaxProjectiles, "projectile");
        private readonly SnapshotList<VehicleSnapshot> _vehicles = new SnapshotList<VehicleSnapshot>(MaxVehicles, "vehicle");

        private MatchSnapshot _match;
        private MatchFields _matchPresent = MatchFields.All;

        public ushort Id => MessageIds.Snapshot;

        public byte Version => CurrentVersion;

        public int Tick { get; set; }

        public SnapshotMessage Baseline { get; set; }

        public int BaselineTick { get; private set; } = NoBaseline;

        public bool IsComplete { get; private set; } = true;

        // 並びの要素と同じく、その場で書き換えられるよう参照で返す。
        public ref MatchSnapshot Match => ref _match;

        public bool HasOwner { get; set; }

        public OwnerSnapshot Owner => _owner;

        public SnapshotList<PlayerSnapshot> Players => _players;

        public SnapshotList<GrenadeSnapshot> Grenades => _grenades;

        public SnapshotList<ControlPointSnapshot> ControlPoints => _controlPoints;

        public SnapshotList<ProjectileSnapshot> Projectiles => _projectiles;

        public SnapshotList<VehicleSnapshot> Vehicles => _vehicles;

        public void Clear()
        {
            Tick = 0;
            Baseline = null;
            BaselineTick = NoBaseline;
            IsComplete = true;
            Match = default;
            _matchPresent = MatchFields.All;
            HasOwner = false;
            _owner.LastProcessedInputTick = -1;
            _owner.Weapon = default;
            _owner.Knockback = default;
            _owner.Grenades.Clear();
            _owner.EquippedWeaponIds.Clear();
            _owner.Present = OwnerFields.All;
            _players.Clear();
            _grenades.Clear();
            _controlPoints.Clear();
            _projectiles.Clear();
            _vehicles.Clear();
        }

        public void CopyFrom(SnapshotMessage other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Tick = other.Tick;
            Baseline = null;
            BaselineTick = other.BaselineTick;
            IsComplete = other.IsComplete;
            Match = other.Match;
            _matchPresent = other._matchPresent;
            HasOwner = other.HasOwner;
            _owner.CopyFrom(other._owner);
            _players.CopyFrom(other._players);
            _grenades.CopyFrom(other._grenades);
            _controlPoints.CopyFrom(other._controlPoints);
            _projectiles.CopyFrom(other._projectiles);
            _vehicles.CopyFrom(other._vehicles);
        }

        public bool TryResolve(SnapshotMessage baseline)
        {
            if (IsComplete)
            {
                return true;
            }

            if (baseline == null || baseline == this || !baseline.IsComplete || baseline.Tick != BaselineTick)
            {
                return false;
            }

            if (HasOwner && _owner.Present != OwnerFields.All && !baseline.HasOwner)
            {
                return false;
            }

            if (!_players.CanFillFrom(baseline._players)
                || !_grenades.CanFillFrom(baseline._grenades)
                || !_controlPoints.CanFillFrom(baseline._controlPoints)
                || !_projectiles.CanFillFrom(baseline._projectiles)
                || !_vehicles.CanFillFrom(baseline._vehicles))
            {
                return false;
            }

            Match.FillFrom(in baseline.Match, _matchPresent);
            _matchPresent = MatchFields.All;
            if (HasOwner)
            {
                _owner.FillFrom(baseline._owner);
            }

            _players.FillFrom(baseline._players);
            _grenades.FillFrom(baseline._grenades);
            _controlPoints.FillFrom(baseline._controlPoints);
            _projectiles.FillFrom(baseline._projectiles);
            _vehicles.FillFrom(baseline._vehicles);
            IsComplete = true;
            return true;
        }

        public void Write(MessageWriter writer)
        {
            var baseline = Baseline;
            if (Tick < 0)
            {
                throw new InvalidOperationException("snapshot tick must not be negative");
            }

            if (baseline != null && (baseline == this || baseline.Tick >= Tick))
            {
                throw new InvalidOperationException(
                    "the baseline of snapshot " + Text(Tick) + " must be an older snapshot (got tick " + Text(baseline.Tick) + ")");
            }

            writer.WriteInt32(Tick);
            writer.WriteInt32(baseline != null ? baseline.Tick : NoBaseline);

            var matchFields = baseline != null ? Match.ChangedFields(in baseline.Match) : MatchFields.All;
            writer.WriteByte((byte)matchFields);
            Match.Write(writer, matchFields);

            writer.WriteBool(HasOwner);
            if (HasOwner)
            {
                var ownerFields = baseline != null && baseline.HasOwner ? _owner.ChangedFields(baseline._owner) : OwnerFields.All;
                writer.WriteByte((byte)ownerFields);
                _owner.Write(writer, ownerFields);
            }

            _players.Write(writer, baseline?._players);
            _grenades.Write(writer, baseline?._grenades);
            _controlPoints.Write(writer, baseline?._controlPoints);
            _projectiles.Write(writer, baseline?._projectiles);
            _vehicles.Write(writer, baseline?._vehicles);
        }

        public void Read(MessageReader reader)
        {
            Baseline = null;
            Tick = reader.ReadInt32AtLeast(0, "snapshot tick");
            BaselineTick = reader.ReadInt32AtLeast(NoBaseline, "baseline tick");
            if (BaselineTick >= Tick)
            {
                throw new MessageReadException(
                    "snapshot " + Text(Tick) + " is a diff against a newer snapshot " + Text(BaselineTick));
            }

            var hasBaseline = BaselineTick != NoBaseline;
            IsComplete = false;

            _matchPresent = (MatchFields)reader.ReadByteBelow((int)MatchFields.All + 1, "match parts");
            ExpectAll(hasBaseline, (int)_matchPresent, (int)MatchFields.All, "match");
            Match.Read(reader, _matchPresent);

            HasOwner = reader.ReadBool();
            if (HasOwner)
            {
                var ownerFields = (OwnerFields)reader.ReadByteBelow((int)OwnerFields.All + 1, "owner parts");
                ExpectAll(hasBaseline, (int)ownerFields, (int)OwnerFields.All, "owner");
                _owner.Read(reader, ownerFields);
            }

            _players.Read(reader, hasBaseline);
            _grenades.Read(reader, hasBaseline);
            _controlPoints.Read(reader, hasBaseline);
            _projectiles.Read(reader, hasBaseline);
            _vehicles.Read(reader, hasBaseline);

            IsComplete = _matchPresent == MatchFields.All
                && (!HasOwner || _owner.Present == OwnerFields.All)
                && _players.IsComplete
                && _grenades.IsComplete
                && _controlPoints.IsComplete
                && _projectiles.IsComplete
                && _vehicles.IsComplete;
        }

        private static void ExpectAll(bool hasBaseline, int present, int all, string what)
        {
            if (!hasBaseline && present != all)
            {
                throw new MessageReadException("a snapshot without a baseline must carry every " + what + " part");
            }
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
