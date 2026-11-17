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

        /// <summary>障害物が <see cref="MotorSpec.VaultReach"/> より遠い。</summary>
        TooFar,

        /// <summary>当たった面が壁ではない (床・天井・急な斜面)。</summary>
        NotAWall,

        /// <summary>障害物の方を向いていない。</summary>
        NotFacing,

        /// <summary>
        /// 障害物が <see cref="MotorSpec.VaultMinHeight"/> より低い (段差として歩いて上がる)。
        /// よじ登りでは <see cref="MotorSpec.VaultMaxHeight"/> 以下 (乗り越えるかジャンプで上がる)。
        /// </summary>
        TooLow,

        /// <summary>障害物が <see cref="MotorSpec.VaultMaxHeight"/> より高い。よじ登りでは <see cref="MotorSpec.MantleMaxHeight"/> より高い。</summary>
        TooHigh,

        /// <summary>
        /// 障害物が <see cref="MotorSpec.VaultMaxDepth"/> より厚い (向こう側へ降りられない) のに、上に体が立てる奥行き
        /// (<see cref="VaultProbe.MantleFootingDepth"/>) も無い。立てるなら上に乗る <see cref="Vault"/> になる。
        /// </summary>
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

        /// <summary>
        /// 手前の面が体の幅 (<see cref="VaultProbe.MinFaceWidth"/>) に足りない (壁の端や細い柱に横から当たっている)。
        /// 乗り越えでは TooHigh の後・TooDeep の前に、よじ登りでは TooHigh の後・TooNarrow の前に調べる。
        /// </summary>
        NarrowFace,
    }
}
