using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、持ち主の射撃の入力 (Attack と射撃モード) を読み、<see cref="FireServerRpc"/> でサーバーへ送る処理。
    /// </summary>
    public sealed partial class NetPlayer
    {
        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            // 反動の戻りは撃てない間 (デスカム中など) も進める
            _recoil.Tick(Time.deltaTime);
            if (_attackAction == null || IsWatchingDeathCamera)
            {
                return;
            }

            var isPressed = _attackAction.IsPressed();
            var wasPressed = _attackAction.WasPressedThisFrame();
            var trigger = ResolveTrigger();
            if (trigger == null)
            {
                if (wasPressed)
                {
                    // 射撃モードと連射間隔が分からない間は、サーバーに弾かれない頻度が分からないので撃たない
                    WarnUnknownWeapon();
                }

                return;
            }

            trigger.Tick(Time.deltaTime);

            // 届いている弾数で撃てないと分かっている射撃は送らない (弾倉が空ならリロードを頼む)。判断はサーバーが行う。
            // ダウン中の射撃と、高い壁で端にいないブラインドファイアはサーバーが捨てるので送らない
            if (IsAlive && !IsDowned && ResolveBlindFire(out _) != BlindFireAim.Blocked
                && trigger.Pull(isPressed, wasPressed) && (_weapon == null || _weapon.PrepareFire()))
            {
                Fire();
            }
        }

        /// <summary>
        /// 装備中の武器 (<see cref="NetPlayerWeapon.WeaponId"/>) の射撃モードと連射間隔で判断する <see cref="FireTrigger"/>。
        /// 試合 (<see cref="NetTeamDeathmatch.Current"/>) の <see cref="NetTeamDeathmatch.Loadout"/>、無ければ自分で選んだ武器のある
        /// <see cref="NetTeamDeathmatch.Catalog"/> から引き (<see cref="NetTeamDeathmatch.TryFindWeaponSpec"/>)、武器か参照するアセットが変わったら作り直す。
        /// 武器がまだ届いていない・どちらにも無い・アセットが不正なら null。
        /// </summary>
        private FireTrigger ResolveTrigger()
        {
            var weaponId = _weapon != null ? _weapon.WeaponId : string.Empty;
            var match = NetTeamDeathmatch.Current;
            var loadout = match != null ? match.Loadout : null;
            var catalog = match != null ? match.Catalog : null;
            // 引けなかったときも覚えておき、毎フレーム Loadout の武器を作り直さない
            if (_triggerResolved && _triggerLoadout == loadout && _triggerCatalog == catalog
                && string.Equals(_triggerWeaponId, weaponId, StringComparison.Ordinal))
            {
                return _trigger;
            }

            _trigger = null;
            _triggerSpec = null;
            _triggerWeaponId = weaponId;
            _triggerLoadout = loadout;
            _triggerCatalog = catalog;
            _triggerError = null;
            _triggerResolved = true;
            if (string.IsNullOrEmpty(weaponId))
            {
                _triggerError = "before the server equips a weapon";
                return null;
            }

            if (!NetTeamDeathmatch.TryFindWeaponSpec(loadout, catalog, weaponId, out var spec, out var error))
            {
                _triggerError = error;
                return null;
            }

            _trigger = new FireTrigger(spec);
            _triggerSpec = spec;
            return _trigger;
        }

        /// <summary>
        /// 装備中の武器の値。<see cref="ResolveTrigger"/> と同じく試合の Loadout から引く。引けなければ null
        /// (<see cref="AimDownSights"/> は共通の初期値の視野角を使う)。
        /// </summary>
        internal WeaponSpec ResolveEquippedSpec()
        {
            ResolveTrigger();
            return _triggerSpec;
        }

        private void WarnUnknownWeapon()
        {
            Debug.LogWarning("[Net] cannot fire " + (_triggerError ?? "without a weapon"), this);
        }

        /// <summary>
        /// 1 発撃つ。目から画面中央が指す点への向き (ばらつきを掛ける前) へ、いま見ている時点 (サーバーの時刻 − 補間の遅れ)・装備中の武器の Id・
        /// 発射番号を添えてサーバーに送るだけで、武器の値 (ダメージ・射程など) は送らない。当たり判定とダメージはサーバーが今持たせている武器の値で、
        /// ばらつきはサーバーが数えた発射番号で掛けて行う。
        /// 送ったら、このクライアントで同じ種で予測したばらつきの向きへ <see cref="CombatEvents.ShotFired"/> を出し、カメラに反動を足す。
        /// </summary>
        private void Fire()
        {
            var serverTime = NetworkManager.ServerTime.Time;
            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime < 0d)
            {
                Debug.LogWarning("[Net] cannot fire before the server time is known", this);
                return;
            }

            // 装備中の武器はサーバーから届く。届く前 (参加直後) や長すぎる Id は送ってもサーバーが捨てるので送らない
            var weaponId = _weapon != null ? _weapon.WeaponId : string.Empty;
            if (string.IsNullOrEmpty(weaponId) || !NetPlayerWeapon.FitsWeaponId(weaponId))
            {
                Debug.LogWarning("[Net] cannot fire before the server equips a weapon", this);
                return;
            }

            var origin = AimOrigin();
            var aim = AimDirection();

            // ホストは他人をサーバー上の今の位置で見ている (補間しない) ので遅れは 0
            var delay = IsServer ? 0 : NetworkPlayerMotor.InterpolationDelayTicks;
            var time = FireTiming.ViewTime(serverTime, NetworkPlayerMotor.TickRate, delay);

            // Update の ResolveTrigger が同じ weaponId で引いた武器。引けていなければ Update は撃たせない
            var spec = _triggerSpec;
            var playerId = SpreadPlayerId;
            var generation = _shots.Generation;
            var shotIndex = _shots.Take();
            var cone = spec != null ? SpreadConeDegrees(spec, AimingForSpread, SpreadMoveFraction()) : 0f;
            var direction = SpreadDirection(aim, cone, playerId, shotIndex);

            // ブラインドファイアの始点と散らばりは曳光弾の予測にだけ使う。サーバーへは目の始点を送り、サーバーが自分の知るカバーの状態で決め直す
            var tracerOrigin = origin;
            var blind = spec != null && ResolveBlindFire(out var blindOrigin) == BlindFireAim.Blind;
            if (blind)
            {
                tracerOrigin = blindOrigin;
                direction = NetBlindFire.Spread(direction, spec, playerId, shotIndex);
            }

            Debug.Log("[Net] fire weapon=" + weaponId + " tick=" + time.Tick.ToString(CultureInfo.InvariantCulture)
                + " alpha=" + time.Alpha.ToString("F2", CultureInfo.InvariantCulture)
                + " index=" + shotIndex.ToString(CultureInfo.InvariantCulture)
                + " cone=" + cone.ToString("F2", CultureInfo.InvariantCulture)
                + " dir=" + NetHitLog.Direction(direction.x, direction.y, direction.z)
                + (blind ? " blind=true" : string.Empty), this);
            FireServerRpc(new FixedString64Bytes(weaponId), origin, aim, time.Tick, time.Alpha, shotIndex, generation);

            // 撃った本人の演出は返事を待たずに出す (サーバーの NetCombatFeed は本人にだけ射撃を送らない)
            CombatEvents.RaiseShotFired(gameObject, tracerOrigin, direction, weaponId);

            // この弾は今の視点の向きで撃ち、反動はカメラが次に向きを書くとき (FirstPersonLook.LateUpdate) から次の弾に効く
            if (spec != null)
            {
                _recoil.Kick(spec, playerId, shotIndex);
            }

            ReleaseSprint(SprintInput.ReasonFire);
        }

        /// <summary>
        /// 今撃つとブラインドファイアになるか (<see cref="NetBlindFire.Resolve"/>)。ルールは試合の同期値 (<see cref="NetMatchRules.BlindFireEnabled"/>)、
        /// カバーの状態はこのインスタンスの最新の値 (持ち主は予測、サーバーは確定状態)、目は今の姿勢の高さ (<see cref="EyePosition"/>)。
        /// </summary>
        internal BlindFireAim ResolveBlindFire(out Vector3 origin)
        {
            var match = NetTeamDeathmatch.Current;
            var enabled = match != null && match.SyncedRules.BlindFireEnabled;
            return NetBlindFire.Resolve(enabled, CurrentCover, EyePosition(), BodyRadius, out origin);
        }

        /// <summary>
        /// ばらつきの種にするプレイヤー Id。サーバーの <see cref="ServerPlayerId"/> はクライアント Id をそのまま int にしたもの
        /// (NetTeamDeathmatch の JoinNewPlayers) なので、サーバーに知らされていない持ち主でも同じ値を出せる。
        /// </summary>
        internal int SpreadPlayerId => unchecked((int)OwnerClientId);

        /// <summary>
        /// ばらつきに使う覗き込み。持ち主でも <see cref="AimDownSights"/> の今の値ではなく同期値を読み、サーバーと同じ値で予測する。
        /// 同期値は覗き込んでいるかだけなので、覗き込み始めから覗き込み切った扱いになる。
        /// </summary>
        internal bool AimingForSpread => _aiming.Value;

        /// <summary>
        /// ばらつきに使う移動の速さの割合 (<see cref="SpreadPattern.MoveFraction"/>)。歩きの速さで足し切る (オフラインの GameBootstrap と同じ)。
        /// 速さはサーバーでは確定状態、持ち主では最新の予測 (<see cref="NetworkPlayerMotor.HorizontalSpeed"/>)。動きを持たなければ 0。
        /// </summary>
        internal float SpreadMoveFraction()
        {
            var motor = ResolveMotor();
            if (motor == null)
            {
                return 0f;
            }

            var speed = motor.HorizontalSpeed;
            var walk = motor.WalkSpeed;
            if (float.IsNaN(speed) || float.IsInfinity(speed) || float.IsNaN(walk) || float.IsInfinity(walk))
            {
                return 0f;
            }

            return SpreadPattern.MoveFraction(speed, walk);
        }

        /// <summary>
        /// 散らばりの半角 (度)。覗き込んでいれば <see cref="WeaponSpec.AdsSpreadDegrees"/>、いなければ <see cref="WeaponSpec.HipSpreadDegrees"/> に、
        /// 移動の分を足す (<see cref="SpreadPattern.ConeDegrees"/>)。持ち主の予測とサーバーの判定で同じこの計算を通す。
        /// </summary>
        /// <exception cref="ArgumentNullException">spec が null のとき。</exception>
        internal static float SpreadConeDegrees(WeaponSpec spec, bool aiming, float moveFraction)
        {
            return SpreadPattern.ConeDegrees(spec, aiming ? 1f : 0f, moveFraction);
        }

        /// <summary>
        /// aim を、半角 coneDegrees の円錐の中で種 (playerId・shotIndex) から決めた向きにずらす (<see cref="SpreadPattern.Direction"/>)。
        /// 散らばりが 0 以下なら aim をそのまま返す。持ち主の予測とサーバーの判定で同じこの計算を通す。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">aim が有限でないか長さ 0 で、散らばりがあるとき。</exception>
        internal static Vector3 SpreadDirection(Vector3 aim, float coneDegrees, int playerId, int shotIndex)
        {
            if (!(coneDegrees > 0f))
            {
                return aim;
            }

            return NetMath.ToUnity(SpreadPattern.Direction(NetMath.ToNumerics(aim), coneDegrees, playerId, shotIndex));
        }

        /// <summary>
        /// サーバーで、この弾の番号 (持ち主が付けた clientIndex) をサーバーの番号に直したことを持ち主へ知らせる。
        /// </summary>
        internal void ServerCorrectShotIndex(ShotNumbering numbering, int clientIndex)
        {
            RequireServer();
            ShotIndexCorrectedRpc(numbering.Generation, clientIndex, numbering.Index);
        }

        /// <summary>
        /// サーバーが発射番号を直したとき、持ち主で次の番号をサーバーに合わせる (<see cref="NetShotSequence.Correct"/>)。
        /// 届くまでに撃った弾の曳光弾は古い番号で予測したままになる。
        /// </summary>
        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void ShotIndexCorrectedRpc(int generation, int clientIndex, int serverIndex)
        {
            if (_shots.Correct(generation, clientIndex, serverIndex))
            {
                Debug.Log("[Net] shot index resynced client=" + clientIndex.ToString(CultureInfo.InvariantCulture)
                    + " server=" + serverIndex.ToString(CultureInfo.InvariantCulture)
                    + " next=" + _shots.NextIndex.ToString(CultureInfo.InvariantCulture)
                    + " generation=" + generation.ToString(CultureInfo.InvariantCulture), this);
            }
        }

        /// <summary>サーバーで、受け入れた弾 1 発に発射番号を付ける (<see cref="ServerShotSequence.Assign"/>)。</summary>
        internal ShotNumbering ServerAssignShotIndex(int clientIndex, int clientGeneration)
        {
            RequireServer();
            return _serverShots.Assign(clientIndex, clientGeneration);
        }

        /// <summary>
        /// 射撃をサーバーへ送る。weaponId は撃った武器の Id で、武器の値は送らない (サーバーが今持たせている武器の値を使う)。
        /// 試合のロードアウトに無い・サーバーが今持たせている武器と違う Id の射撃は捨てられる。
        /// clientTick + interpolationAlpha は撃った側が他人を見ていた時点 (サーバーの tick)。
        /// サーバーはその時点まで巻き戻して当たり判定をする (<see cref="NetTeamDeathmatch.ServerFire"/>)。
        /// direction はばらつきを掛ける前の向き。shotIndex と shotGeneration は持ち主が予測に使った発射番号と訂正の世代で、
        /// サーバーは自分の番号と突き合わせるだけで、ばらつきには自分の番号を使う。
        /// </summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void FireServerRpc(
            FixedString64Bytes weaponId,
            Vector3 origin,
            Vector3 direction,
            int clientTick,
            float interpolationAlpha,
            int shotIndex,
            int shotGeneration,
            RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored shot from client " + rpcParams.Receive.SenderClientId
                    + " for player owned by " + OwnerClientId);
                return;
            }

            var match = NetTeamDeathmatch.Current;
            if (match != null)
            {
                match.ServerFire(
                    this, weaponId.ToString(), origin, direction, new ShotTime(clientTick, interpolationAlpha), shotIndex, shotGeneration);
            }
        }

        private void ResolveAttackAction()
        {
            if (Application.isBatchMode)
            {
                // 画面なしでは入力が無い
                return;
            }

            var actions = InputSystem.actions;
            var attack = actions != null ? actions.FindAction(AttackActionPath) : null;
            if (attack == null)
            {
                Debug.LogWarning("[Net] input action '" + AttackActionPath + "' was not found; firing is disabled");
                return;
            }

            _attackAction = attack;
            if (!_attackAction.enabled)
            {
                _attackAction.Enable();
                _enabledAttackAction = true;
            }
        }

        private void ReleaseAttackAction()
        {
            if (_enabledAttackAction && _attackAction != null)
            {
                _attackAction.Disable();
            }

            _enabledAttackAction = false;
            _attackAction = null;
        }
    }
}
