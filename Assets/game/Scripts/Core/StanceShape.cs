namespace Shooter.Core
{
    public readonly struct StanceShape
    {
        public float Height { get; }

        public float CenterY { get; }

        public float EyeY { get; }

        public StanceShape(float height, float centerY, float eyeY)
        {
            Height = height;
            CenterY = centerY;
            EyeY = eyeY;
        }

        public float BottomY => CenterY - (Height * 0.5f);

        public float TopY => CenterY + (Height * 0.5f);
    }
}
