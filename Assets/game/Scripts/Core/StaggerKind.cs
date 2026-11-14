namespace Fps.Core
{
    // アニメの値にそのまま出すので、並びを変えると Animator の条件がずれる。足すときは末尾に足す。
    public enum StaggerKind
    {
        None,
        Flinch,
        Kneel,
        Stumble,
        Blast,
    }
}
