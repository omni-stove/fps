using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerActions.Attach"/> が呼ぶ、アクション 1 つ分の取り付け。プレイヤーに自分のコンポーネントを付け、ルールで有効・無効を決める。
    /// 新しいアクションは、これを実装した型を 1 つ足して <see cref="PlayerActions.Register"/> で登録すれば、既存のファイルを変えずに付けられる。
    /// 登録は実装した型に置いた <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]</c> の静的メソッドから行い、
    /// EditMode のテストでも効かせたいときは <c>[UnityEditor.InitializeOnLoadMethod]</c> (<c>#if UNITY_EDITOR</c> で囲む) も付ける。
    /// </summary>
    public interface IPlayerActionInstaller
    {
        /// <summary>
        /// 呼ぶ順。小さいほど先に呼ぶ。同じ値なら型の完全名の順。ほかのアクションのコンポーネントを読むなら、それより大きくする
        /// (組み込みは <see cref="CoverActionInstaller.InstallOrder"/> などを参照)。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// player に自分のコンポーネントを (無ければ) 付け、rules に従って設定する。何度呼んでもコンポーネントは増やさず、最後に渡した rules に従う。
        /// rules で使わないことになっていても、前に有効にしたものを無効に戻すために呼ばれる。
        /// </summary>
        /// <param name="player">プレイヤーの GameObject。null ではない。</param>
        /// <param name="rules">今のルール。null ではない。</param>
        void Install(GameObject player, GameRules rules);
    }
}
