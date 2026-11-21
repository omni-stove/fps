using Shooter.Core;

namespace Shooter.Server
{
    // オフラインの EnemyMelee と同じく、判断に MeleeEnemyBehavior を足して、手の届く相手にはその場で振らせる。
    // 振りの時間は近接のモジュールが人と同じ決まりで進めるので、その振り (MeleeSwing) を判断に読ませる
    internal sealed partial class ServerBotAgent
    {
        private MeleeEnemyBehavior _melee;

        public bool WantsSwing => _melee != null && _melee.WantsSwing;

        public void UseMelee(MeleeSwing swing)
        {
            if (_melee != null && ReferenceEquals(_melee.Swing, swing))
            {
                return;
            }

            if (_melee != null)
            {
                Brain.RemoveBehavior(_melee);
                _melee = null;
            }

            if (swing == null)
            {
                return;
            }

            _melee = new MeleeEnemyBehavior(swing) { Enabled = false };
            Brain.AddBehavior(_melee);
        }

        // ルールの近接が途中で切り替わっても追えるよう、判断の前に毎 tick 決め直す
        public void EnableMelee(bool rulesAllow)
        {
            if (_melee != null)
            {
                _melee.Enabled = rulesAllow && _spec.Tactics.MeleeAttacks && AllowsMelee;
            }
        }

        internal MeleeEnemyBehavior MeleeBehavior => _melee;
    }
}
