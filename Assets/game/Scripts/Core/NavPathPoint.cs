using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="NavPath"/> の角の 1 つと、そこから次の角までの渡り方。</summary>
    public readonly struct NavPathPoint
    {
        /// <summary>次の角まで面の上を歩くことを表す <see cref="LinkId"/>。</summary>
        public const int NoLink = -1;

        public NavPathPoint(Vector3 position, int linkId)
        {
            Position = position;
            LinkId = linkId < 0 ? NoLink : linkId;
        }

        /// <summary>角の位置。</summary>
        public Vector3 Position { get; }

        /// <summary>
        /// 次の角まではしごなどのリンクを渡るなら、そのリンクを実装の中で引く Id (0 以上)。歩くなら <see cref="NoLink"/>。
        /// 最後の角では意味を持たない。
        /// </summary>
        public int LinkId { get; }

        /// <summary>次の角までリンクを渡るか。</summary>
        public bool StartsLink => LinkId != NoLink;
    }
}
