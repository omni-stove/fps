namespace Fps.Runtime
{
    public interface IPhotoCapture
    {
        // ScreenCapture はフレームの終わりに書くので、返すのは保存し終えたパスではなく保存する先のパス
        string Capture();
    }
}
