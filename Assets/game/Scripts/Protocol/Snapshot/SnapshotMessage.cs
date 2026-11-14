using System;
using System.Globalization;

namespace Fps.Protocol
{
    /// <summary>
    /// サーバーが毎 tick クライアントごとに送る試合の状態。Netcode の NetworkVariable・NetworkList と、毎 tick の <c>StateRpc</c>、
    /// プレイヤーとグレネードの Spawn・Despawn を 1 つにまとめたもの。並びに無い物は消えたものとして扱う。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 落ちてもよい届け方で送れるよう、差分はクライアントが受け取ったと知らせてきた (<see cref="InputMessage.AckedSnapshotTick"/>)
    /// スナップショットを基準に取る。送る側は <see cref="Baseline"/> に基準を入れて書き、変わった部分だけを載せる。
    /// 受け取る側は読んだ後に <see cref="BaselineTick"/> のスナップショットを <see cref="TryResolve"/> に渡して、載っていない部分を埋める。
    /// </para>
    /// <para>
    /// 壊れた建物の部品のように滅多に変わらず大きい状態は、毎 tick の差分に載せずに <see cref="StructureStateMessage"/> で送る。
    /// </para>
    /// </remarks>
    public sealed class SnapshotMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        /// <summary>基準が無い (全部が載っている) ことを表す <see cref="BaselineTick"/>。</summary>
        public const int NoBaseline = -1;

        public const int MaxPlayers = 64;
        public const int MaxGrenades = 64;
        public const int MaxControlPoints = 16;

        private readonly OwnerSnapshot _owner = new OwnerSnapshot();
        private readonly SnapshotList<PlayerSnapshot> _players = new SnapshotList<PlayerSnapshot>(MaxPlayers, "player");
        private readonly SnapshotList<GrenadeSnapshot> _grenades = new SnapshotList<GrenadeSnapshot>(MaxGrenades, "grenade");
        private readonly SnapshotList<ControlPointSnapshot> _controlPoints =
            new SnapshotList<ControlPointSnapshot>(MaxControlPoints, "control point");

        private MatchSnapshot _match;
        private MatchFields _matchPresent = MatchFields.All;

        public ushort Id => MessageIds.Snapshot;

        public byte Version => CurrentVersion;

        /// <summary>このスナップショットのサーバーの tick。</summary>
        public int Tick { get; set; }

        /// <summary>送る側が差分の基準にするスナップショット。null なら全部を書く。書くときにだけ使う。</summary>
        public SnapshotMessage Baseline { get; set; }

        /// <summary>読んだスナップショットの差分の基準の tick。全部が載っていたなら <see cref="NoBaseline"/>。</summary>
        public int BaselineTick { get; private set; } = NoBaseline;

        /// <summary>全部の部分が入っているか。差分を読んだ直後は false で、<see cref="TryResolve"/> で埋めると true になる。</summary>
        public bool IsComplete { get; private set; } = true;

        /// <summary>試合全体の状態。並びの要素と同じく、その場で書き換えられるよう参照で返す。</summary>
        public ref MatchSnapshot Match => ref _match;

        /// <summary>受け取るクライアントのプレイヤーが試合にいて、<see cref="Owner"/> が載っているか。</summary>
        public bool HasOwner { get; set; }

        public OwnerSnapshot Owner => _owner;

        public SnapshotList<PlayerSnapshot> Players => _players;

        public SnapshotList<GrenadeSnapshot> Grenades => _grenades;

        public SnapshotList<ControlPointSnapshot> ControlPoints => _controlPoints;

        /// <summary>送る側が中身を入れ直す前に、前の中身と基準を捨てる。確保した並びは使い回す。</summary>
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
            _owner.Grenades = default;
            _owner.EquippedWeaponIds.Clear();
            _owner.Present = OwnerFields.All;
            _players.Clear();
            _grenades.Clear();
            _controlPoints.Clear();
        }

        /// <summary>other の中身を写す (後で差分の基準に使うため取っておく)。<see cref="Baseline"/> は写さない。</summary>
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
        }

        /// <summary>
        /// 載っていなかった部分を baseline (tick が <see cref="BaselineTick"/> の、全部が入ったスナップショット) から埋める。
        /// </summary>
        /// <returns>埋められた (もう全部入っていた) なら true。baseline が違う・基準に無い物の差分があるなら false で、中身は変えない。</returns>
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
                || !_controlPoints.CanFillFrom(baseline._controlPoints))
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

            IsComplete = _matchPresent == MatchFields.All
                && (!HasOwner || _owner.Present == OwnerFields.All)
                && _players.IsComplete
                && _grenades.IsComplete
                && _controlPoints.IsComplete;
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
