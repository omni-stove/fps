namespace Fps.Protocol
{
    /// <summary>メッセージでプレイヤー (人と Bot) を指す、サーバーが振る Id の決まり。</summary>
    public static class PlayerIds
    {
        /// <summary>誰も指さない Id (撃った人が消えた・キラーがいない)。振る Id はこれより大きくする。</summary>
        public const int None = -1;
    }
}
