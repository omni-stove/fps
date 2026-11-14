namespace Fps.Core
{
    public enum DamageKind
    {
        // 落下・縮むエリア・Inspector の Kill のように撃った物の無いダメージを、どれかの種類に寄せずに見分けられるよう分けておく。
        Other,
        Bullet,
        Explosion,
        Melee,
    }
}
