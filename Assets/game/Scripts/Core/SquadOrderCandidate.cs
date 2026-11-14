using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="SquadOrderRecipients.Select"/> に渡す、指示を受けられるかもしれない bot 1 人。</summary>
    public readonly struct SquadOrderCandidate
    {
        public SquadOrderCandidate(Side side, Vector3 position, bool isAlive)
        {
            Side = side;
            Position = position;
            IsAlive = isAlive;
        }

        public Side Side { get; }

        /// <summary>足元。</summary>
        public Vector3 Position { get; }

        public bool IsAlive { get; }
    }
}
