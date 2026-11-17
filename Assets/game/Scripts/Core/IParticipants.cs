namespace Shooter.Core
{
    // 表 1 つにしないのは、Id の意味がオフライン (GameObject ごとの連番) とオンライン (プレイヤー Id) で違い、引き方も違うため。
    public interface IParticipants
    {
        bool TryGet(int id, out Participant participant);
    }
}
