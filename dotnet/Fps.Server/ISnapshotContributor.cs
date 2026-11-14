using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 移動・戦闘・ルールのモジュールがそれぞれ自分の持つ部分だけを書き込めるよう、スナップショットを組み立てる側と分ける。
    /// </summary>
    internal interface ISnapshotContributor
    {
        void Contribute(SnapshotMessage snapshot, ServerPlayer receiver);
    }
}
