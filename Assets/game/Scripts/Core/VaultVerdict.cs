namespace Fps.Core
{
    /// <summary><see cref="VaultProbe.Evaluate"/> が乗り越えられるかを決めた結果。<see cref="Vault"/> 以外は乗り越えない理由。</summary>
    public enum VaultVerdict
    {
        /// <summary>前方に何も無い。</summary>
        NoObstacle,

        /// <summary>障害物が <see cref="VaultProbe.MaxReach"/> より遠い。</summary>
        TooFar,

        /// <summary>当たった面が壁ではない (床・天井・急な斜面)。</summary>
        NotAWall,

        /// <summary>障害物の方を向いていない。</summary>
        NotFacing,

        /// <summary>障害物が <see cref="VaultProbe.MinHeight"/> より低い (段差として歩いて上がる)。</summary>
        TooLow,

        /// <summary>障害物が <see cref="MotorSpec.VaultMaxHeight"/> より高い。</summary>
        TooHigh,

        /// <summary>障害物が <see cref="VaultProbe.MaxDepth"/> より厚い (向こう側へ降りられない)。</summary>
        TooDeep,

        /// <summary>上を越える空間か向こう側の立つ場所が塞がっている。</summary>
        Blocked,

        /// <summary>乗り越えられる。</summary>
        Vault,
    }
}
