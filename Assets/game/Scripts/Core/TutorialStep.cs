namespace Fps.Core
{
    /// <summary>射撃場のチュートリアルの手順。並び順は <see cref="TutorialSteps.Order"/>。</summary>
    public enum TutorialStep
    {
        /// <summary>決めた距離を歩く。</summary>
        Move,

        /// <summary>決めた時間だけ覗き込む (ADS)。</summary>
        Aim,

        /// <summary>標的に決めた回数だけ当てる。</summary>
        Shoot,

        /// <summary>リロードする。</summary>
        Reload,

        /// <summary>しゃがむ。</summary>
        Crouch,

        /// <summary>遮蔽物に張り付く。</summary>
        Cover,
    }
}
