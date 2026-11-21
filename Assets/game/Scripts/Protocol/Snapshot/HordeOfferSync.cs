using System;
using System.Text;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 届く距離の防衛物と額・払えるかを、クライアントが防衛物の場所も部品の重なりも持たずに案内 (HordeStatus.Offer) を出せるよう、
    // サーバーが持ち主ごとに決めて送る。オフラインの HordeRunner の案内と同じく、立っている人の体と重なる部品は額に数えない。
    public readonly struct HordeOfferSync : IEquatable<HordeOfferSync>
    {
        public HordeOfferSync(int fortificationId, HordeFortificationAction action, string nameKey, int cost, HordePurchaseVerdict verdict)
        {
            if (fortificationId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fortificationId), fortificationId, "fortificationId must not be negative");
            }

            if (action != HordeFortificationAction.Build && action != HordeFortificationAction.Repair)
            {
                throw new ArgumentOutOfRangeException(nameof(action), action, "only Build and Repair are offered");
            }

            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "cost must not be negative");
            }

            if ((int)verdict < 0 || (int)verdict >= EnumRange<HordePurchaseVerdict>.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "verdict is not a defined HordePurchaseVerdict");
            }

            FortificationId = fortificationId;
            Action = action;
            NameKey = Fit(nameKey);
            Cost = cost;
            Verdict = verdict;
        }

        // 建物の番号 (StructureStateMessage.StructureId と同じ)
        public int FortificationId { get; }

        public HordeFortificationAction Action { get; }

        // 表示の鍵。表示の文はクライアントが鍵から引く
        public string NameKey { get; }

        public int Cost { get; }

        public HordePurchaseVerdict Verdict { get; }

        public bool Equals(HordeOfferSync other)
        {
            return FortificationId == other.FortificationId
                && Action == other.Action
                && string.Equals(NameKey, other.NameKey, StringComparison.Ordinal)
                && Cost == other.Cost
                && Verdict == other.Verdict;
        }

        public override bool Equals(object obj) => obj is HordeOfferSync other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(FortificationId, Action, Cost, Verdict);

        // 鍵はマップの書き出し (シーンの物の名前) から来て長さに上限が無いので、スナップショットを書けなくするより、送れる長さで切る
        public static string Fit(string nameKey)
        {
            if (string.IsNullOrEmpty(nameKey))
            {
                return string.Empty;
            }

            var length = nameKey.Length;
            while (length > 0 && Encoding.UTF8.GetByteCount(nameKey.Substring(0, length)) > MatchProtocol.MaxContentIdBytes)
            {
                length--;
            }

            // 代用対の片割れで切ると UTF-8 にできないので、その前で切る
            if (length > 0 && length < nameKey.Length && char.IsHighSurrogate(nameKey[length - 1]))
            {
                length--;
            }

            return length == nameKey.Length ? nameKey : nameKey.Substring(0, length);
        }

        public static bool operator ==(HordeOfferSync left, HordeOfferSync right) => left.Equals(right);

        public static bool operator !=(HordeOfferSync left, HordeOfferSync right) => !left.Equals(right);

        internal void Write(MessageWriter writer)
        {
            if (FortificationId < 0)
            {
                throw new InvalidOperationException("the offered fortification id must not be negative");
            }

            // default の HordeOfferSync は Action が None で、読む側が断るので書かない
            if (Action != HordeFortificationAction.Build && Action != HordeFortificationAction.Repair)
            {
                throw new InvalidOperationException("an offered fortification must be built or repaired, but is " + Action);
            }

            writer.WriteInt32(FortificationId);
            writer.WriteByte((byte)Action);
            writer.WriteString(Fit(NameKey), MatchProtocol.MaxContentIdBytes);
            writer.WriteInt32(Cost);
            writer.WriteByte((byte)Verdict);
        }

        internal static HordeOfferSync Read(MessageReader reader)
        {
            var id = reader.ReadInt32AtLeast(0, "offered fortification");
            var action = (HordeFortificationAction)reader.ReadByteBelow(EnumRange<HordeFortificationAction>.Count, "offered fortification action");
            if (action == HordeFortificationAction.None)
            {
                throw new MessageReadException("an offered fortification must be built or repaired");
            }

            var nameKey = reader.ReadString(MatchProtocol.MaxContentIdBytes, null);
            var cost = reader.ReadInt32AtLeast(0, "offered fortification cost");
            var verdict = (HordePurchaseVerdict)reader.ReadByteBelow(EnumRange<HordePurchaseVerdict>.Count, "offered fortification verdict");
            return new HordeOfferSync(id, action, nameKey, cost, verdict);
        }
    }
}
