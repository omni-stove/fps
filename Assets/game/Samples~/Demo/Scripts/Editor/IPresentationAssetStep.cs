namespace Shooter.Editor
{
    // PresentationAssetSteps.BuildAll がリフレクションで集めるので、実装は引数の無い公開コンストラクタを持つ公開の具象クラスにする。
    // 既存のアセットは GUID を保って中身だけ更新し、2 回走らせても差分が出ないようにする。ほかの手順の生成物に頼るなら、それより大きい Order にする。
    public interface IPresentationAssetStep
    {
        string Name { get; }

        int Order { get; }

        void Build();
    }
}
