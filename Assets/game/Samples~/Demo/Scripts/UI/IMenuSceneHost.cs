namespace Shooter.Runtime
{
    public interface IMenuSceneHost
    {
        int BuildIndexOf(string path);

        void Load(int buildIndex);

        void Quit();
    }
}
