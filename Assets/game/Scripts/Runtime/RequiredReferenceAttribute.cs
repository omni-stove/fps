using System;

namespace Fps.Runtime
{
    /// <summary>
    /// シーン・プレハブ・アセットで必ず埋めておく参照の印。空だと動かない (仮の形や既定値に戻らない) 枠にだけ付ける。
    /// 差し替え漏れの点検 (Fps.Editor.ReplacementAudit) が、ビルドに入るシーンとプロジェクトのプレハブ・アセットで空の枠を一覧に出す。
    /// 配列やリストに付けると、空の並びと空の要素を漏れとして出す。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredReferenceAttribute : Attribute
    {
    }
}
