using System.Collections.Generic;

namespace Fps.Net
{
    /// <summary>
    /// サーバーで、各プレイヤーを最後に倒した相手の playerId を持つ。<see cref="Fps.Core.TeamDeathmatchSession.Killed"/> の
    /// (killerId, victimId) をそのまま <see cref="Record"/> に渡す。デスカムが映す相手を <see cref="NetPlayerStats.Killer"/> で配るのに使う。
    /// </summary>
    /// <remarks>
    /// 自分以外に倒されたとき (敵に倒された・FriendlyFire On で味方に倒された) はその相手を持つ。
    /// 自滅と、Reflect で自分の弾が跳ね返って倒れたとき (Session は killerId == victimId で知らせる) は、映す相手がいないので持たない。
    /// 倒した相手が後で抜けても消さない (行の受け手が、もういない相手として扱う)。
    /// </remarks>
    internal sealed class LastKillerTable
    {
        private readonly Dictionary<int, int> _killerByVictim = new Dictionary<int, int>();

        /// <summary>victimId が killerId に倒されたことを記録する。前に記録した相手は上書きする。</summary>
        public void Record(int killerId, int victimId)
        {
            if (killerId == victimId)
            {
                _killerByVictim.Remove(victimId);
                return;
            }

            _killerByVictim[victimId] = killerId;
        }

        /// <summary>victimId を最後に倒した相手。まだ倒されていないか、最後の死に自分以外の相手がいなければ null。</summary>
        public int? Get(int victimId)
        {
            return _killerByVictim.TryGetValue(victimId, out var killerId) ? killerId : (int?)null;
        }

        /// <summary>playerId が抜けたとき、その人が倒された記録を消す。</summary>
        public void Remove(int playerId)
        {
            _killerByVictim.Remove(playerId);
        }

        /// <summary>次の試合のために全員の記録を消す。</summary>
        public void Clear()
        {
            _killerByVictim.Clear();
        }
    }
}
