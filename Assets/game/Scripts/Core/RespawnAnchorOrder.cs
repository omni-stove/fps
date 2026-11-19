using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class RespawnAnchorOrder
    {
        // 拠点は出現点が無くても倒れた場所で戻せて必ず選べるので、tryAnchor には尋ねずにそこで止める。
        // 選べた所で止めるのは、ビーコンのように試すと回数が減るものを、後ろの順のために使ってしまわないため。
        public static RespawnAnchor Choose(IReadOnlyList<RespawnAnchor> anchors, Func<RespawnAnchor, bool> tryAnchor)
        {
            if (anchors == null)
            {
                throw new ArgumentNullException(nameof(anchors));
            }

            if (tryAnchor == null)
            {
                throw new ArgumentNullException(nameof(tryAnchor));
            }

            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == RespawnAnchor.Base)
                {
                    return RespawnAnchor.Base;
                }

                if (tryAnchor(anchor))
                {
                    return anchor;
                }
            }

            return RespawnAnchor.Base;
        }
    }
}
