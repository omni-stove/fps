namespace Fps.Net
{
    /// <summary>
    /// 持ち主のクライアントが数える発射番号 (ばらつきと反動の種、<see cref="Fps.Core.SpreadPattern"/>)。
    /// サーバーの <see cref="ServerShotSequence"/> と同じ 0 から数え、撃つたびに <see cref="Take"/> で 1 つ進める。
    /// サーバーに弾かれた射撃の分だけ先に進んでずれるので、サーバーから訂正 (<see cref="Correct"/>) が届いたらサーバーの番号に合わせる。
    /// 番号は射撃と一緒に、訂正を何回受けたか (<see cref="Generation"/>) を添えて送る。
    /// </summary>
    public sealed class NetShotSequence
    {
        /// <summary>次に撃つ弾の発射番号。int の上限を超えると負へ回る (サーバーも同じ規則で数える)。</summary>
        public int NextIndex { get; private set; }

        /// <summary>受け入れた訂正の世代。サーバーの <see cref="ServerShotSequence.Generation"/> を写したもの。</summary>
        public int Generation { get; private set; }

        /// <summary>今撃つ弾の発射番号を返し、次の番号へ進める。</summary>
        public int Take()
        {
            var index = NextIndex;
            NextIndex = unchecked(index + 1);
            return index;
        }

        /// <summary>
        /// サーバーの訂正を当てる。clientIndex の弾をサーバーは serverIndex として判定したので、
        /// その後に送った (まだサーバーで数えられていない) 弾の分も含め、次の番号を同じだけずらす。
        /// 同じ世代の訂正は当て済みなので無視する。
        /// </summary>
        /// <param name="generation">訂正の世代 (<see cref="ShotNumbering.Generation"/>)。</param>
        /// <param name="clientIndex">ずれていた弾にこのクライアントが付けた番号。</param>
        /// <param name="serverIndex">サーバーがその弾に付けた番号。</param>
        /// <returns>当てたら true。</returns>
        public bool Correct(int generation, int clientIndex, int serverIndex)
        {
            if (generation == Generation)
            {
                return false;
            }

            NextIndex = unchecked(NextIndex + (serverIndex - clientIndex));
            Generation = generation;
            return true;
        }
    }
}
