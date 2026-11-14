namespace Fps.Core
{
    /// <summary>
    /// 画面に出す文字の言語。設定 (<see cref="ControlSettings.Language"/>) で選び、文字は言語ごとの <see cref="StringTable"/> から引く。
    /// 並べる順と保存する名前は <see cref="GameLanguages"/>。
    /// </summary>
    public enum GameLanguage
    {
        Japanese,
        English,
    }
}
