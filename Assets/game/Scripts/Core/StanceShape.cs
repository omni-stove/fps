namespace Fps.Core
{
    /// <summary>
    /// 1 つの姿勢 (立ち・しゃがみ・伏せ) でのカプセルと目の寸法。Y はすべてプレイヤー本体のローカル座標 (m)。
    /// </summary>
    public readonly struct StanceShape
    {
        /// <summary>カプセルの高さ (CharacterController.height)。</summary>
        public float Height { get; }

        /// <summary>カプセルの中心の Y (CharacterController.center.y)。</summary>
        public float CenterY { get; }

        /// <summary>目の Y。</summary>
        public float EyeY { get; }

        public StanceShape(float height, float centerY, float eyeY)
        {
            Height = height;
            CenterY = centerY;
            EyeY = eyeY;
        }

        /// <summary>カプセルの底 (足元) の Y。</summary>
        public float BottomY => CenterY - (Height * 0.5f);

        /// <summary>カプセルの頂 (頭のてっぺん) の Y。</summary>
        public float TopY => CenterY + (Height * 0.5f);
    }
}
