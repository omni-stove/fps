using System;
using Fps.Core;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// 試合の勝敗を何で決めているか (<see cref="GameRules.Objective"/>) と、拠点で決めるときの両チームのポイント (切り捨てた整数)。
    /// 目標とポイントが別々に届いて HUD が食い違わないよう、1 つの NetworkVariable で送る。
    /// </summary>
    public struct NetObjectiveStatus : INetworkSerializable, IEquatable<NetObjectiveStatus>
    {
        public MatchObjective Objective;
        public int AlphaPoints;
        public int BravoPoints;

        /// <summary>拠点なし (<see cref="MatchObjective.None"/>)、ポイント 0。</summary>
        public static NetObjectiveStatus None => default;

        public NetObjectiveStatus(MatchObjective objective, int alphaPoints, int bravoPoints)
        {
            Objective = objective;
            AlphaPoints = alphaPoints;
            BravoPoints = bravoPoints;
        }

        /// <summary>拠点のポイントで勝敗を決める試合なら true。</summary>
        public bool IsDomination => Objective == MatchObjective.Domination;

        /// <summary>team のポイント。0 未満は 0 にする。</summary>
        public int PointsOf(Team team) => Math.Max(0, team == Team.Bravo ? BravoPoints : AlphaPoints);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Objective);
            serializer.SerializeValue(ref AlphaPoints);
            serializer.SerializeValue(ref BravoPoints);
        }

        public bool Equals(NetObjectiveStatus other)
        {
            return Objective == other.Objective && AlphaPoints == other.AlphaPoints && BravoPoints == other.BravoPoints;
        }

        public override bool Equals(object obj)
        {
            return obj is NetObjectiveStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Objective, AlphaPoints, BravoPoints);
        }
    }
}
