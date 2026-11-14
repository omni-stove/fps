namespace Fps.Core
{
    /// <summary>
    /// <see cref="VaultProbe.Evaluate"/> が乗り越えられるか、<see cref="VaultProbe.EvaluateMantle"/> がよじ登れるかを決めた結果。
    /// <see cref="Vault"/> と <see cref="Mantle"/> 以外は越えない理由。値は後ろにだけ足す (前の値の番号を変えない)。
    /// </summary>
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

        /// <summary>
        /// 障害物が <see cref="VaultProbe.MinHeight"/> より低い (段差として歩いて上がる)。
        /// よじ登りでは <see cref="MotorSpec.VaultMaxHeight"/> 以下 (乗り越えるかジャンプで上がる)。
        /// </summary>
        TooLow,

        /// <summary>障害物が <see cref="MotorSpec.VaultMaxHeight"/> より高い。よじ登りでは <see cref="MotorSpec.MantleMaxHeight"/> より高い。</summary>
        TooHigh,

        /// <summary>障害物が <see cref="VaultProbe.MaxDepth"/> より厚い (向こう側へ降りられない)。</summary>
        TooDeep,

        /// <summary>上を越える空間か向こう側の立つ場所が塞がっている。よじ登りでは、上がる間の頭上か縁の上の立つ場所が塞がっている。</summary>
        Blocked,

        /// <summary>乗り越えられる。</summary>
        Vault,

        /// <summary>よじ登れる (<see cref="VaultProbe.EvaluateMantle"/> だけが返す)。</summary>
        Mantle,

        /// <summary>
        /// 縁の上の奥行きが体の立てる広さ (<see cref="VaultProbe.MantleFootingDepth"/>) に足りない
        /// (<see cref="VaultProbe.EvaluateMantle"/> だけが返す)。
        /// </summary>
        TooNarrow,
    }
}
