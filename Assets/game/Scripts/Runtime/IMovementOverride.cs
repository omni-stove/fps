using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> の通常の移動 (<see cref="PlayerMotor"/>) を横取りする動き (スライディング・はしご・乗り物の座席など)。
    /// <see cref="PlayerController.RegisterMovementOverride"/> で登録すると、<see cref="PlayerController.Tick"/> のたびに <see cref="IsActive"/> を聞かれ、
    /// 有効なもののうち <see cref="Priority"/> が最も高いもの (同じなら先に登録したもの) が、その Tick の位置・速度・向き・姿勢を <see cref="Step"/> で決める。
    /// 選ばれたときに <see cref="Begin"/>、選ばれなくなったとき (無効になった・より高いものに替わった・登録を外した・
    /// PlayerController が無効になったか初期化し直した) に <see cref="End"/> が呼ばれる。
    /// 回避・乗り越え・崖のぼりはこの口を使わないが、選ばれた時点でやめさせ、横取りの間は始めない。
    /// </summary>
    public interface IMovementOverride
    {
        /// <summary>同時に有効なものの中で、大きいほうが選ばれる。</summary>
        int Priority { get; }

        /// <summary>今、移動を横取りしたいか。Tick のたびに読む。</summary>
        bool IsActive { get; }

        /// <summary>
        /// 選ばれている間に <see cref="PlayerActionState"/> へ立てるビット。<see cref="PlayerActionReader"/> が毎フレーム読む。
        /// 何も立てないなら <see cref="PlayerActionConditions.None"/>。
        /// </summary>
        PlayerActionConditions Conditions { get; }

        /// <summary>選ばれたとき、最初の <see cref="Step"/> の前に呼ばれる。</summary>
        void Begin(PlayerController mover);

        /// <summary>
        /// 選ばれている間の Tick ごとに呼ばれ、この Tick の動きを返す。
        /// input は <see cref="PlayerController.MoveFilter"/> を通した後の入力で、PlayerMotor には渡らない。
        /// </summary>
        MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds);

        /// <summary>選ばれなくなったときに呼ばれる。<see cref="Begin"/> と必ず対になる。</summary>
        void End(PlayerController mover);
    }
}
