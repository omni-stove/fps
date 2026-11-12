namespace Fps.Core
{
    /// <summary>
    /// プレイヤーの視点の種類。どちらでも狙いと当たり判定はキャラの目から行い、変わるのはカメラの置き場所と自分の体の見え方だけ。
    /// </summary>
    public enum ViewMode
    {
        /// <summary>一人称。カメラを目に重ね、自分の体は影だけ描く。</summary>
        FirstPerson = 0,

        /// <summary>三人称 (肩越し)。カメラを目から離して置き、自分の体を描く。</summary>
        ThirdPerson = 1,
    }
}
