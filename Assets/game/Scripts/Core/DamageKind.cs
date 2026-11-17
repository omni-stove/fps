namespace Shooter.Core
{
    public enum DamageKind
    {
        // 落下・縮むエリア・Inspector の Kill のように撃った物の無いダメージを、どれかの種類に寄せずに見分けられるよう分けておく。
        Other,
        Bullet,
        Explosion,
        Melee,

        // 倒れてきた建物の破片のように、撃った物でなく周りの物が当たったダメージ。帰属は壊した人に付けられる。
        Environment,
        Charge,

        // 燃焼などの状態異常のダメージ。Other に寄せないのは、撃った位置を持たなくても帰属 (かけた人) のあるダメージだと見分けられるようにするため。
        StatusEffect,

        // 乗り物が人や物にぶつかったダメージ。Environment に寄せないのは、運転していた人に帰属し、轢かれたと見分けられるようにするため。
        Vehicle,

        // 息が切れたダメージ。Other に寄せないのは、Inspector の Kill や落下と分けて、溺れたと見分けられるようにするため。
        Drowning,
    }
}
