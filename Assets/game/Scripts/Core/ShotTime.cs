namespace Shooter.Core
{
    // ネットワークから受け取った値をそのまま入れられるよう、コンストラクタでは検査しない (IsValid で確かめる)。
    public readonly struct ShotTime
    {
        public ShotTime(int tick, float alpha)
        {
            Tick = tick;
            Alpha = alpha;
        }

        public int Tick { get; }

        public float Alpha { get; }

        public bool IsValid => Tick >= 0 && Alpha >= 0f && Alpha < 1f;

        public float ToTick()
        {
            return (float)(Tick + (double)Alpha);
        }
    }
}
