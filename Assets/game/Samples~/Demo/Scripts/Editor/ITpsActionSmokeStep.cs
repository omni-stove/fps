using Fps.Runtime;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場の smoke (<see cref="TpsRangeSmokeRun"/>) に足す手順 1 つ分。public な具象クラスで引数なしのコンストラクターを持てば、
    /// smoke が <see cref="ExtensionPoints.CreateAll{T}"/> で見つけ、組み込みの手順をすべて終えた後に <see cref="Order"/> の順に 1 つずつ進める。
    /// 射撃場に置く物 (<see cref="ITpsRangeFixture"/>) と組にして 1 ファイルずつ足せば、新しいアクションを既存のファイルを変えずに確かめられる。
    /// どのメソッドも Editor の更新 (Play 中のフレーム) から呼ぶ。
    /// </summary>
    public interface ITpsActionSmokeStep
    {
        /// <summary>進める順。小さいほど先。同じ値なら型の完全名の順。</summary>
        int Order { get; }

        /// <summary>失敗や時間切れの知らせに出す名前。</summary>
        string Name { get; }

        /// <summary>この手順を始めてから終えるまでに待つ秒数の上限。0 より大きい。</summary>
        float TimeoutSeconds { get; }

        /// <summary>smoke を始めるときに 1 度呼ぶ。前の実行で覚えた状態を捨てる。</summary>
        void Reset();

        /// <summary>
        /// Play 中に出たエラー以外のログを、手順の順番に関わらず受ける。手順が待つログはここで覚える
        /// (エラー・例外・Assert のログは smoke がまとめて失敗にする)。
        /// </summary>
        void OnLog(string condition);

        /// <summary>
        /// 自分の番のあいだ毎フレーム呼ぶ。終えたら true を返し、次のフレームも続けるなら false を返す。
        /// 失敗したら problem に理由を入れる (返り値は見ない)。続けるか終えたときは problem を null にする。
        /// </summary>
        /// <param name="world">射撃場の GameBootstrap が作ったもの。プレイヤーと武器は初期化済み。</param>
        /// <param name="problem">失敗の理由。失敗していなければ null。</param>
        bool Advance(BootstrapWorld world, out string problem);

        /// <summary>smoke を終えるときに、成否や自分の番が来たかに関わらず 1 度呼ぶ。プレイヤーに差し込んだもの (移動の差し込みなど) を外す。</summary>
        void Cleanup();
    }
}
