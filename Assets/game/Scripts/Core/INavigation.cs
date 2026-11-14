using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 経路の問い合わせの差し込み口。Unity では NavMesh、Unity 抜きのサーバーでは自前の経路のデータが実装する。
    /// エリアのマスクは Unity の NavMesh のエリアのマスクと同じく、ビット i が立っていればエリア i を通れる。
    /// </summary>
    public interface INavigation
    {
        /// <summary>position から maxDistance 以内で、歩ける面のいちばん近い点を探す。</summary>
        /// <param name="position">探す中心。</param>
        /// <param name="maxDistance">探す最長の距離。0 以下なら見つからない。</param>
        /// <param name="areaMask">対象にするエリアのマスク。</param>
        /// <param name="nearest">見つかったときの面の上の点。見つからなければ position のまま。</param>
        /// <returns>見つかったか。</returns>
        bool SamplePosition(Vector3 position, float maxDistance, int areaMask, out Vector3 nearest);

        /// <summary>
        /// from から to までの経路を求めて path に入れる。path の前の中身は捨てる。
        /// はしごなどのリンクを渡る区間は、渡り始める点の <see cref="NavPathPoint.LinkId"/> で示す。
        /// </summary>
        /// <param name="from">出発点。歩ける面の上でなければ経路は求まらない。</param>
        /// <param name="to">目的地。届かなければいちばん近づける所までの経路 (<see cref="NavPathStatus.Partial"/>) になる。</param>
        /// <param name="areaMask">通れるエリアのマスク。</param>
        /// <param name="path">結果を入れる先。</param>
        /// <returns>経路が求まったか (<see cref="NavPathStatus.Invalid"/> でないか)。</returns>
        bool CalculatePath(Vector3 from, Vector3 to, int areaMask, NavPath path);
    }
}
