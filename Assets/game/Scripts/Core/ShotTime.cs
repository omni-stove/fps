namespace Fps.Core
{
    /// <summary>
    /// 撃った側のクライアントが見ていた時点。サーバーの tick (<see cref="Tick"/>) と、次の tick までの割合 (<see cref="Alpha"/>) で表す。
    /// サーバーは <see cref="ToTick"/> の値まで <see cref="LagCompensator"/> で巻き戻して当たり判定をする。
    /// ネットワークから受け取った値をそのまま入れられるよう、コンストラクタでは検査しない (<see cref="IsValid"/> で確かめる)。
    /// </summary>
    public readonly struct ShotTime
    {
        public ShotTime(int tick, float alpha)
        {
            Tick = tick;
            Alpha = alpha;
        }

        /// <summary>サーバーの tick。</summary>
        public int Tick { get; }

        /// <summary>Tick から次の tick までの割合。0 以上 1 未満。</summary>
        public float Alpha { get; }

        /// <summary>Tick が 0 以上で、Alpha が 0 以上 1 未満か (NaN は不可)。</summary>
        public bool IsValid => Tick >= 0 && Alpha >= 0f && Alpha < 1f;

        /// <summary>Tick + Alpha。<see cref="LagCompensator.Rewind(float, int)"/> に渡す小数の tick。</summary>
        public float ToTick()
        {
            return (float)(Tick + (double)Alpha);
        }
    }
}
