using System;

namespace Shooter.Core
{
    // 書き込みや送信の失敗は例外にせず false とログで返す。クラッシュを書いている最中に投げると、記録係が次のクラッシュの元になるため。
    // Dispose は残っている行を送らずに捨てる。同意を外したあとに送らないためで、送り切るときは先に Flush(true) を呼ぶ。
    public interface ITelemetrySink : IDisposable
    {
        string Target { get; }

        bool Write(string line);

        void Flush(bool wait);
    }
}
