using Shooter.Runtime;

namespace Shooter.Editor
{
    // smoke は ExtensionPoints.CreateAll で見つけるので、実装は public な具象クラスにして引数なしのコンストラクターを持たせる。
    public interface ITpsActionSmokeStep
    {
        int Order { get; }

        string Name { get; }

        float TimeoutSeconds { get; }

        void Reset();

        void OnLog(string condition);

        bool Advance(BootstrapWorld world, out string problem);

        void Cleanup();
    }
}
