namespace Fps.Editor
{
    /// <summary>
    /// 演出の仮素材を作る手順 1 つ。<see cref="PresentationAssetSteps.BuildAll"/> がリフレクションで集めて <see cref="Order"/> の順に実行する。
    /// 演出を足すときは、この型を実装した公開の具象クラス (引数の無い公開コンストラクタを持つもの) を足すだけでよい。
    /// </summary>
    /// <remarks>
    /// 手順は自分のカタログを <c>Resources/Presentation/&lt;名前&gt;Catalog.asset</c> に置き (<see cref="Fps.Runtime.PresentationCatalog"/> の規約)、
    /// 既存のアセットは GUID を保って中身だけ更新する (2 回走らせても差分が出ない)。ほかの手順の生成物に頼るなら、それより大きい <see cref="Order"/> にする。
    /// </remarks>
    public interface IPresentationAssetStep
    {
        /// <summary>ログに出す名前。</summary>
        string Name { get; }

        /// <summary>実行の順。小さいものから実行し、同じなら型の完全名の順。</summary>
        int Order { get; }

        /// <summary>素材を作るか上書きして保存する。失敗したら例外を投げる (残りの手順は実行しない)。</summary>
        void Build();
    }
}
