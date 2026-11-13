using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary><see cref="DropOnDeath.DecideDrops"/> に渡す、1 回倒れたときの情報。</summary>
    public readonly struct DropContext
    {
        public DropContext(DropOnDeath source, Damageable victim, GameObject killer, Side? victimSide, Vector3 position)
        {
            Source = source;
            Victim = victim;
            Killer = killer;
            VictimSide = victimSide;
            Position = position;
        }

        /// <summary>落とす部品。</summary>
        public DropOnDeath Source { get; }

        /// <summary>倒れた人。</summary>
        public Damageable Victim { get; }

        /// <summary>倒した人 (<see cref="CombatEvents.CurrentAttribution"/>)。分からないか自滅なら null。</summary>
        public GameObject Killer { get; }

        /// <summary>倒れた人の陣営 (<see cref="SideMember"/>)。持たなければ null。</summary>
        public Side? VictimSide { get; }

        /// <summary>落とす位置 (倒れた人の足元)。</summary>
        public Vector3 Position { get; }
    }
}
