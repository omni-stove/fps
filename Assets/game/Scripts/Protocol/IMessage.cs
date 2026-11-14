namespace Fps.Protocol
{
    /// <summary>
    /// 通信路で送る 1 つのメッセージ。<see cref="MessageWriter.WriteMessage"/> が先頭に <see cref="Id"/> を書き、
    /// 受け手は <see cref="MessageReader.ReadMessageId"/> で Id を読んでから、その Id のメッセージの <see cref="Read"/> を呼ぶ。
    /// </summary>
    public interface IMessage
    {
        /// <summary>メッセージの種類を表す Id。種類ごとに重ならない値にする。</summary>
        ushort Id { get; }

        /// <summary>中身 (Id を除く) を書く。</summary>
        void Write(MessageWriter writer);

        /// <summary>
        /// 中身 (Id を除く) を読んで自分に入れる。足りない・壊れているなら <see cref="MessageReadException"/> を投げる。
        /// </summary>
        void Read(MessageReader reader);
    }
}
