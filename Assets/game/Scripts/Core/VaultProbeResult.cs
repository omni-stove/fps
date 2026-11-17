using System.Numerics;

namespace Shooter.Core
{
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

        public static VaultProbeResult None => default;

        public VaultVerdict Verdict { get; }

        public bool CanVault => Verdict == VaultVerdict.Vault;

        public bool CanMantle => Verdict == VaultVerdict.Mantle;

        public float Height { get; }

        public float Depth { get; }

        public Vector3 Direction { get; }

        public VaultPath Path { get; }
    }
}
