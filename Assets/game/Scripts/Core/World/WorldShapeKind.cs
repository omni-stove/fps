namespace Fps.Core
{
    // 値は .fpsmap にそのまま書くので、変えると読めなくなる
    public enum WorldShapeKind : byte
    {
        Mesh = 1,
        Box = 2,
        Capsule = 3,
        Sphere = 4,
    }
}
