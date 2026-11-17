namespace Shooter.Core
{
    // どちらでも狙いと当たり判定はキャラの目から行い、変わるのはカメラの置き場所と自分の体の見え方だけにする。
    public enum ViewMode
    {
        FirstPerson = 0,

        ThirdPerson = 1,
    }
}
