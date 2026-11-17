namespace Shooter.Core
{
    /// <summary>チャットの文を受けなかった理由。</summary>
    public enum ChatRefusal
    {
        /// <summary>受けた。</summary>
        None,

        /// <summary>前後の空白と改行を落とすと何も残らない。</summary>
        Empty,

        /// <summary>前後の空白と改行を落としても <see cref="NetMatchSettings.MaxChatLength"/> を超える。</summary>
        TooLong,

        /// <summary>同じ人が前に受けた文から <see cref="NetMatchSettings.ChatIntervalSeconds"/> が経っていない。</summary>
        TooSoon,
    }
}
