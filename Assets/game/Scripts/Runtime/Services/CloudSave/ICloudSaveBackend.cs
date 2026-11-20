using System;
using System.Collections.Generic;

namespace Shooter.Runtime
{
    // 呼び戻しは呼んだその場でも後のフレームでも返してよい。CloudSaveSync はどちらでも同じに扱う。
    public interface ICloudSaveBackend
    {
        // サインインしていない・オフラインのときは null。値が変わったら別のアカウントとして同期し直す。
        string SignedInAccount { get; }

        int MaxFileNameLength { get; }

        void Tick();

        void List(Action<IReadOnlyList<string>, string> done);

        // ファイルが無ければ (null, null) で返す。失敗は 2 つ目に理由を入れる。
        void Read(string fileName, Action<string, string> done);

        void Write(string fileName, string text, Action<string> done);
    }
}
