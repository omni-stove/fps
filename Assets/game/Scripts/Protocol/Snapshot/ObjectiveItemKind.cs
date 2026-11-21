namespace Shooter.Protocol
{
    // 値の並びをそのまま 1 バイトで送るので、足すときは後ろに足す。
    public enum ObjectiveItemKind
    {
        Flag,

        Bomb,

        Tag,

        Cart,

        LootBox,

        SupplyCrate,

        DeathBox,

        ExtractionPoint,

        // 地面の拾い物は中身の種類 (PickupKind) ごとに分け、クライアントが中身を知らずに見た目を選べるようにする。
        // Opened は拾われて次に出るのを待っている間、Radius は拾える範囲、持ち主は落とした人
        WeaponPickup,

        AmmoPickup,

        HealthPickup,

        ArmorPickup,

        ItemPickup,

        // 弾薬箱。Opened は使ってから次に使えるまでの間、Radius は使える範囲
        AmmoCrate,
    }
}
