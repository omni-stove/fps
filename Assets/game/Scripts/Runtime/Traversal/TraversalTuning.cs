using Shooter.Core;

namespace Shooter.Runtime
{
    public static class TraversalTuning
    {
        // スライディング・パラシュート・ジップラインの取り付けが同じ規則で読むよう、引き先を 1 か所にまとめる。
        public static TraversalSpec Current =>
            KitTuning.Load(c => c.TraversalTuning, TraversalJson.ResourcePath, TraversalJson.Parse, TraversalSpec.Default);
    }
}
