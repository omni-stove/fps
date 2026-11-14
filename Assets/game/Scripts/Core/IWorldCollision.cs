using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 世界の当たりへの問い合わせの差し込み口。Unity では Physics、Unity 抜きのサーバーでは自前の当たりが実装する。
    /// レイヤーのマスクは Unity の LayerMask と同じく、ビット i が立っていればレイヤー i を対象にする。
    /// トリガー (当たらない判定用の形) は対象にしない。
    /// </summary>
    public interface IWorldCollision
    {
        /// <summary>origin から direction へ maxDistance までの線分で、いちばん近くの当たりを探す。</summary>
        /// <param name="origin">始点。</param>
        /// <param name="direction">向き。長さ 1 でなくてよい (実装が正規化する)。長さ 0 なら何にも当たらない。</param>
        /// <param name="maxDistance">探す最長の距離。0 以下なら何にも当たらない。</param>
        /// <param name="layerMask">対象にするレイヤーのマスク。</param>
        /// <param name="hit">当たったときのいちばん近い当たり。当たらなければ default。</param>
        /// <returns>当たったか。</returns>
        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit);

        /// <summary>
        /// point0 と point1 を両端の球の中心とする半径 radius のカプセルに重なるものを探し、その Id を results に入れる。
        /// </summary>
        /// <param name="point0">片方の端の球の中心。</param>
        /// <param name="point1">もう片方の端の球の中心。point0 と同じなら球になる。</param>
        /// <param name="radius">半径。0 以下なら何にも重ならない。</param>
        /// <param name="layerMask">対象にするレイヤーのマスク。</param>
        /// <param name="results">
        /// 重なったものの <see cref="WorldHit.ColliderId"/> を入れる先。入りきらない分は捨てる (順序は決まっていない)。
        /// </param>
        /// <returns>results に入れた数。</returns>
        int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results);

        /// <summary>カプセルを direction へ maxDistance まで動かしたとき、最初に触れるものを探す。</summary>
        /// <param name="point0">片方の端の球の中心 (動かす前)。</param>
        /// <param name="point1">もう片方の端の球の中心 (動かす前)。</param>
        /// <param name="radius">半径。0 以下なら何にも当たらない。</param>
        /// <param name="direction">動かす向き。長さ 1 でなくてよい (実装が正規化する)。長さ 0 なら何にも当たらない。</param>
        /// <param name="maxDistance">動かす最長の距離。0 以下なら何にも当たらない。</param>
        /// <param name="layerMask">対象にするレイヤーのマスク。</param>
        /// <param name="hit">当たったときの最初の当たり。当たらなければ default。</param>
        /// <returns>当たったか。動かす前から重なっているものは当たりにしない。</returns>
        bool CapsuleCast(
            Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit);
    }
}
