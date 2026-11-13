using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="VaultProbe.Evaluate"/> の結果。</summary>
    public readonly struct VaultProbeResult
    {
        public VaultProbeResult(VaultVerdict verdict, float height, float depth, Vector3 direction, VaultPath path)
        {
            Verdict = verdict;
            Height = height;
            Depth = depth;
            Direction = direction;
            Path = path;
        }

        /// <summary>前方に何も無い。</summary>
        public static VaultProbeResult None => default;

        /// <summary>乗り越えられるか、乗り越えない理由。</summary>
        public VaultVerdict Verdict { get; }

        /// <summary>乗り越えられる。</summary>
        public bool CanVault => Verdict == VaultVerdict.Vault;

        /// <summary>障害物の上端の、足元からの高さ。前方に何も無ければ 0。</summary>
        public float Height { get; }

        /// <summary>障害物の奥行き。前方に何も無ければ 0、測れる範囲より厚ければ <see cref="float.PositiveInfinity"/>。</summary>
        public float Depth { get; }

        /// <summary>乗り越える向き (手前の面に垂直で水平、長さ 1)。壁でなければ 0。</summary>
        public Vector3 Direction { get; }

        /// <summary>乗り越える軌跡。軌跡を決める前に乗り越えないと分かったときは null。</summary>
        public VaultPath Path { get; }
    }
}
