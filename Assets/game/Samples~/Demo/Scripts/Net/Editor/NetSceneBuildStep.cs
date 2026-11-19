namespace Shooter.Editor
{
    // サーバーは書き出したマップの当たりを読むので、ネットのシーンを作り直したら続けて書き出す。
    public sealed class NetSceneBuildStep : ISceneBuildStep
    {
        public string Name => "Net";

        public int Order => SceneBuildSteps.NetOrder;

        public void Build()
        {
            NetSceneBuilder.Build();
            ArenaSceneBuilder.Build();
            NetSceneBuilder.BuildBattleRoyale();
            MapCollisionExporter.Export();
        }
    }
}
