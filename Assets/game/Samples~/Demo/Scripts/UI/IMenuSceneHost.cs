namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="MenuNavigator"/> がシーンを読み、アプリを終えるときの相手。ふだんは SceneManager と Application を使い、
    /// テストでは差し替えて、どのシーンを読もうとしたかを確かめる。
    /// </summary>
    public interface IMenuSceneHost
    {
        /// <summary>path のシーンの Build Settings での番号。入っていなければ負の値。</summary>
        int BuildIndexOf(string path);

        /// <summary>buildIndex のシーンを Single で読む。</summary>
        void Load(int buildIndex);

        /// <summary>アプリを終える (エディタでは Play Mode を止める)。</summary>
        void Quit();
    }
}
