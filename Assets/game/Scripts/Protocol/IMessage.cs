namespace Fps.Protocol
{
    /// <summary>
    /// 通信路で送る 1 つのメッセージ。<see cref="MessageWriter.WriteMessage"/> が先頭に <see cref="Id"/> と <see cref="Version"/> を書き、
    /// 受け手は <see cref="MessageReader.ReadMessageId"/> で Id を読んでから、その Id のメッセージを
    /// <see cref="MessageReader.ReadMessageBody"/> に渡す (版が違えば読まずに断る)。</summary>
    /// <remarks>
    /// 毎 tick 届くものでも GC を起こさないよう、受け手は種類ごとに 1 つのインスタンスを使い回して <see cref="Read"/> で上書きする
    /// (<see cref="MessageTable"/>)。
    /// </remarks>
    public interface IMessage
    {
        /// <summary>メッセージの種類を表す Id。種類ごとに重ならない値にする。</summary>
        ushort Id { get; }

        /// <summary>中身の形の版。形を変えたら上げ、違う版のメッセージは読まずに断る。</summary>
        byte Version { get; }

        /// <summary>中身 (Id と版を除く) を書く。</summary>
        void Write(MessageWriter writer);

        /// <summary>
        /// 中身 (Id と版を除く) を読んで自分に入れる。足りない・壊れているなら <see cref="MessageReadException"/> を投げる。
        /// </summary>
        void Read(MessageReader reader);
    }
}
