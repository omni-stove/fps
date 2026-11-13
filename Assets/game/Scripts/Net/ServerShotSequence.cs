namespace Fps.Net
{
    /// <summary>
    /// サーバーが 1 人のプレイヤーについて数える発射番号。検査を通って弾を消費した射撃にだけ <see cref="Assign"/> で番号を付け、
    /// ばらつきの種にはクライアントが送ってきた番号ではなくこの番号を使う (ずれたときはサーバーの値が正)。
    /// <para>
    /// クライアントの番号と食い違ったら訂正の世代 (<see cref="Generation"/>) を進め、持ち主へ訂正を送らせる。
    /// 訂正が届くまでに古い世代で送られてくる弾は同じだけずれているはずで、持ち主は訂正でまとめて合わせるので、
    /// 世代が今と違う弾では訂正を重ねない (重ねるとずれを二重に直してしまう)。
    /// </para>
    /// </summary>
    public sealed class ServerShotSequence
    {
        /// <summary>次に受け入れる弾に付ける発射番号。int の上限を超えると負へ回る。</summary>
        public int NextIndex { get; private set; }

        /// <summary>送った訂正の世代。0 から始まり、訂正を送るたびに 1 進む。</summary>
        public int Generation { get; private set; }

        /// <summary>
        /// 受け入れた弾 1 発に番号を付ける。
        /// </summary>
        /// <param name="clientIndex">クライアントがその弾に付けてきた番号。</param>
        /// <param name="clientGeneration">クライアントが受け入れていた訂正の世代。</param>
        public ShotNumbering Assign(int clientIndex, int clientGeneration)
        {
            var index = NextIndex;
            NextIndex = unchecked(index + 1);
            if (clientIndex == index || clientGeneration != Generation)
            {
                return new ShotNumbering(index, false, Generation);
            }

            Generation = unchecked(Generation + 1);
            return new ShotNumbering(index, true, Generation);
        }
    }
}
