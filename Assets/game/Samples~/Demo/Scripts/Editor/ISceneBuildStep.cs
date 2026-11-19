namespace Shooter.Editor
{
    // SceneBuildSteps.BuildAll がリフレクションで集めるので、実装は引数の無い公開コンストラクタを持つ公開の具象クラスにする。
    // ほかの手順の生成物に頼るなら、それより大きい Order にする。
    public interface ISceneBuildStep
    {
        string Name { get; }

        int Order { get; }

        void Build();
    }
}
