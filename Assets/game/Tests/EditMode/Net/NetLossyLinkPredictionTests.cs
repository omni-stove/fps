using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// 悪い回線 (片道 150 ms・ロス 5%、ゆらぎあり・なし) での予測と照合。<see cref="NetworkPlayerMotor"/> と同じ送り方
    /// (入力は信頼性あり・順序ありで送り直し、確定状態は信頼性なしで最新だけを送る、ダウンは信頼性ありで届く) を
    /// 乱数の種を固定した回線の模型で再現し、予測とサーバーを別々の <see cref="NetMotorSimulation"/> で動かす。
    /// 確かめること:
    /// <list type="bullet">
    /// <item>遅れ・ロス・並びの入れ替わりだけでは巻き戻さない (サーバーと同じ入力を同じ順に通すので答えが同じになる)。</item>
    /// <item>サーバーだけが知っているダウンで食い違っても巻き戻し、照合した後の予測は、まだ届かない tick も含めて
    /// サーバーが同じ tick で出す位置から <see cref="NetworkPlayerMotor.ReconcileTolerance"/> 以内に収まる。</item>
    /// <item>回線が落ち着いた後の最新の予測はサーバーと一致する。</item>
    /// </list>
    /// サーバーが入力の受け付けを抑える上限 (送りすぎの不正対策) はここでは真似ない。照合の正しさだけを見る。
    /// </summary>
    public sealed class NetLossyLinkPredictionTests
    {
        // NetSceneBuilder のプレイヤーと同じ寸法
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private const float _crouchHeight = 1.2f;
        private const float _eyeHeight = 0.6f;

        private const int _latencyMs = 150;
        private const int _lossPercent = 5;

        // クライアントが入力を作る tick の数。_idleFromTick からは止まって、最後の照合が落ち着くのを待つ
        private const int _clientTicks = 420;
        private const int _idleFromTick = 360;

        // 最後の入力が送り直しを重ねても届き、最後の確定状態が返るまで回す余り
        private const int _drainFrames = 240;

        // サーバーがこのフレームでダウンさせる。クライアントは届くまで知らずに動き続ける
        private const int _downAtServerFrame = 200;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void ReconciledPredictionStaysWithinToleranceOnABadLink([Values(0, 30)] int jitterMs, [Values(1, 2, 3)] int seed)
        {
            CreateGround();
            var run = Run(new LossyLink(seed, _latencyMs, _lossPercent, jitterMs));
            var context = " (latency=" + _latencyMs + " loss=" + _lossPercent + " jitter=" + jitterMs + " seed=" + seed + ")";

            Assert.That(run.Link.DroppedUnreliable, Is.GreaterThan(0), "the link must drop some state packets" + context);
            Assert.That(run.Link.ResentReliable, Is.GreaterThan(0), "the link must resend some inputs" + context);
            Assert.That(run.ServerStates.Count, Is.EqualTo(_clientTicks), "every reliable input reaches the server" + context);
            Assert.That(run.FirstDownedServerTick, Is.GreaterThan(0), "the server downs the player during the run" + context);

            var corrected = 0;
            var checkedAfterReconcile = 0;
            foreach (var record in run.Reconciles)
            {
                if (record.Result.Outcome == ReconcileOutcome.Ignored)
                {
                    continue;
                }

                if (record.Result.Outcome == ReconcileOutcome.Corrected)
                {
                    corrected++;
                    Assert.That(record.Result.Tick, Is.GreaterThanOrEqualTo(run.FirstDownedServerTick),
                        "delay and loss alone must not rewind; rewound at tick " + record.Result.Tick + " error=" + record.Result.Error + context);
                }

                // まだ届いていない tick の入力でダウンの知り方が食い違っていれば、そこから先は次の照合で直るので比べない
                if (!run.AgreesOnDowned(record.Result.Tick + 1, record.CurrentTick))
                {
                    continue;
                }

                checkedAfterReconcile++;
                var server = run.ServerStates[record.CurrentTick - 1];
                Assert.That(record.Current.IsCrouched, Is.EqualTo(server.IsCrouched),
                    "stance after reconciling tick " + record.Result.Tick + " at tick " + record.CurrentTick + context);
                Assert.That(Vector3.Distance(record.Current.Position, server.Position), Is.LessThanOrEqualTo(NetworkPlayerMotor.ReconcileTolerance),
                    "position after reconciling tick " + record.Result.Tick + " at tick " + record.CurrentTick + context);
            }

            Assert.That(corrected, Is.GreaterThan(0), "the downed mismatch must be reconciled" + context);
            Assert.That(checkedAfterReconcile, Is.GreaterThan(_clientTicks / 4), "enough reconciles must be compared" + context);

            var last = run.ServerStates[_clientTicks - 1];
            Assert.That(run.FinalPrediction.IsCrouched, Is.True, "the downed player ends crouched" + context);
            Assert.That(MotorState.PositionError(run.FinalPrediction, last), Is.LessThanOrEqualTo(NetworkPlayerMotor.ReconcileTolerance),
                "the final prediction matches the server" + context);
        }

        [Test]
        public void TheLinkModelKeepsReliableMessagesInOrder()
        {
            var link = new LossyLink(7, _latencyMs, 50, 100);
            var last = 0.0;
            var ordered = 0.0;
            for (var i = 0; i < 200; i++)
            {
                var sent = i * 10.0;
                var arrive = link.SendReliable(sent, ref ordered);
                Assert.That(arrive, Is.GreaterThanOrEqualTo(sent + _latencyMs), "a message cannot arrive before the latency");
                Assert.That(arrive, Is.GreaterThanOrEqualTo(last), "reliable messages arrive in the order they were sent");
                last = arrive;
            }

            Assert.That(link.ResentReliable, Is.GreaterThan(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LossyLink(1, _latencyMs, 100, 0));
        }

        private RunResult Run(LossyLink link)
        {
            var predictedBody = CreatePlayer("Predicted", new Vector3(-30f, 1.1f, 0f));
            var serverBody = CreatePlayer("Server", new Vector3(-30f, 1.1f, 0f));

            // NetworkPlayerMotor と同じくプレイヤー同士は衝突させない
            Physics.IgnoreCollision(predictedBody, serverBody, true);
            Physics.SyncTransforms();
            var predicted = CreateSimulation(predictedBody);
            var serverSimulation = CreateSimulation(serverBody);

            var initial = new MotorState(predictedBody.transform.position, Vector3.zero, false, false);
            var prediction = new PredictionBuffer<MotorInput, MotorState>(
                predicted.Step, MotorState.PositionError, NetworkPlayerMotor.ReconcileTolerance);
            prediction.Reset(0, initial);

            var tickMs = 1000.0 / NetworkPlayerMotor.TickRate;
            var result = new RunResult(link);

            // クライアント → サーバー (入力)
            var inputsInFlight = new Queue<InFlight<MotorInput>>();
            var inputOrder = 0.0;

            // サーバー → クライアント (確定状態とダウンの知らせ)
            var statesInFlight = new List<InFlight<MotorState>>();
            var downedArrival = double.PositiveInfinity;
            var downedOrder = 0.0;

            var clientDowned = false;
            var serverDowned = false;
            var serverState = initial;

            for (var frame = 1; frame <= _clientTicks + _drainFrames; frame++)
            {
                var now = frame * tickMs;

                if (!clientDowned && downedArrival <= now)
                {
                    clientDowned = true;
                }

                // 信頼性なしなので、遅れのゆらぎで追い越したものも届いた順に渡す (古い tick は PredictionBuffer が捨てる)
                statesInFlight.Sort((a, b) => a.ArriveMs.CompareTo(b.ArriveMs));
                while (statesInFlight.Count > 0 && statesInFlight[0].ArriveMs <= now)
                {
                    var packet = statesInFlight[0];
                    statesInFlight.RemoveAt(0);
                    var reconcile = prediction.Reconcile(packet.Tick, packet.Payload);
                    result.Reconciles.Add(new ReconcileRecord(reconcile, prediction.CurrentTick, prediction.Current));
                }

                if (frame <= _clientTicks)
                {
                    var input = SampleInput(frame, clientDowned);
                    prediction.Predict(frame, input);
                    result.ClientDowned.Add(clientDowned);
                    inputsInFlight.Enqueue(new InFlight<MotorInput>(frame, input, link.SendReliable(now, ref inputOrder)));
                }

                if (frame == _downAtServerFrame)
                {
                    serverDowned = true;
                    downedArrival = link.SendReliable(now, ref downedOrder);
                }

                var processed = false;
                while (inputsInFlight.Count > 0 && inputsInFlight.Peek().ArriveMs <= now)
                {
                    var packet = inputsInFlight.Dequeue();
                    var sent = packet.Payload;

                    // SubmitInputRpc と同じく、ダウンはクライアントの申告ではなくサーバーの知る状態で入れる
                    var input = new MotorInput(sent.Move, sent.Yaw, sent.Sprint, sent.Jump, sent.Crouch, serverDowned);
                    serverState = serverSimulation.Step(serverState, input);
                    result.ServerStates.Add(serverState);
                    result.ServerDowned.Add(serverDowned);
                    if (serverDowned && result.FirstDownedServerTick == 0)
                    {
                        result.FirstDownedServerTick = packet.Tick;
                    }

                    processed = true;
                }

                // NetworkPlayerMotor.Update と同じく、1 フレームに何 tick 進めても送るのは最新だけ
                if (processed && link.TrySendUnreliable(now, out var arrive))
                {
                    statesInFlight.Add(new InFlight<MotorState>(result.ServerStates.Count, serverState, arrive));
                }
            }

            result.FinalPrediction = prediction.Current;
            return result;
        }

        // 向きをゆっくり振りながら前へ進み、ダッシュ・ジャンプ・しゃがみを混ぜる。予測とサーバーで同じ入力になるよう tick だけから決める
        private static MotorInput SampleInput(int tick, bool downed)
        {
            if (tick >= _idleFromTick)
            {
                return new MotorInput(Vector2.zero, Mathf.PI / 2f, false, false, false, downed);
            }

            var yaw = (Mathf.PI / 2f) + (0.3f * Mathf.Sin(tick * 0.05f));
            var sprint = (tick / 60) % 2 == 1;
            var jump = tick % 45 == 0;
            var crouch = !sprint && tick % 100 >= 70 && tick % 100 < 85;
            return new MotorInput(new Vector2(0.2f * Mathf.Cos(tick * 0.1f), 1f), yaw, sprint, jump, crouch, downed);
        }

        private NetMotorSimulation CreateSimulation(CharacterController controller)
        {
            // NetworkPlayerMotor の既定値と同じ
            return NetTestWorld.CreateSimulation(controller, new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, PlayerController.DefaultCrouchSpeed), _crouchHeight, _created);
        }

        private CharacterController CreatePlayer(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _created.Add(go);
            var controller = go.AddComponent<CharacterController>();
            controller.height = _height;
            controller.radius = _radius;
            controller.center = Vector3.zero;

            var eye = new GameObject(NetPlayer.ViewName).transform;
            eye.SetParent(go.transform, false);
            eye.localPosition = new Vector3(0f, _eyeHeight, 0f);

            Physics.SyncTransforms();
            return controller;
        }

        // 走り切っても端に届かない広さ (ダッシュで 1 秒おきに 6 秒走っても 40m ほど)
        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(120f, 1f, 120f);
            _created.Add(ground);
            Physics.SyncTransforms();
        }

        private readonly struct InFlight<T>
        {
            public InFlight(int tick, T payload, double arriveMs)
            {
                Tick = tick;
                Payload = payload;
                ArriveMs = arriveMs;
            }

            public int Tick { get; }

            public T Payload { get; }

            public double ArriveMs { get; }
        }

        private readonly struct ReconcileRecord
        {
            public ReconcileRecord(ReconcileResult result, int currentTick, MotorState current)
            {
                Result = result;
                CurrentTick = currentTick;
                Current = current;
            }

            public ReconcileResult Result { get; }

            /// <summary>照合した直後の最新の予測の tick。</summary>
            public int CurrentTick { get; }

            /// <summary>照合した直後の最新の予測。</summary>
            public MotorState Current { get; }
        }

        private sealed class RunResult
        {
            public RunResult(LossyLink link)
            {
                Link = link;
            }

            public LossyLink Link { get; }

            /// <summary>i 番目が tick i + 1 のサーバーの確定状態。</summary>
            public List<MotorState> ServerStates { get; } = new List<MotorState>();

            /// <summary>i 番目が tick i + 1 をサーバーがダウンとして動かしたか。</summary>
            public List<bool> ServerDowned { get; } = new List<bool>();

            /// <summary>i 番目が tick i + 1 をクライアントがダウンとして予測したか。</summary>
            public List<bool> ClientDowned { get; } = new List<bool>();

            public List<ReconcileRecord> Reconciles { get; } = new List<ReconcileRecord>();

            /// <summary>サーバーが初めてダウンとして動かした tick。まだなら 0。</summary>
            public int FirstDownedServerTick { get; set; }

            public MotorState FinalPrediction { get; set; }

            /// <summary>tick from から to まで、予測とサーバーがダウンを同じに扱ったか。範囲が空なら true。</summary>
            public bool AgreesOnDowned(int from, int to)
            {
                for (var tick = from; tick <= to; tick++)
                {
                    if (ClientDowned[tick - 1] != ServerDowned[tick - 1])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Unity Transport のシミュレーターに似せた片方向の回線の模型。どの送信も遅れ (+ 0〜ゆらぎ) の後に届き、ロスの割合で落ちる。
        /// 信頼性ありの送信は落ちたら往復ぶん待って送り直し、前に送ったものより先には渡さない (順序ありの配送)。
        /// </summary>
        private sealed class LossyLink
        {
            private readonly System.Random _random;
            private readonly double _latencyMs;
            private readonly double _jitterMs;
            private readonly double _lossPercent;

            public LossyLink(int seed, int latencyMs, int lossPercent, int jitterMs)
            {
                if (lossPercent < 0 || lossPercent >= 100)
                {
                    // 100% では信頼性ありの送り直しが終わらない
                    throw new ArgumentOutOfRangeException(nameof(lossPercent), lossPercent, "lossPercent must be 0 to 99");
                }

                _random = new System.Random(seed);
                _latencyMs = latencyMs;
                _jitterMs = jitterMs;
                _lossPercent = lossPercent;
            }

            public int DroppedUnreliable { get; private set; }

            public int ResentReliable { get; private set; }

            public bool TrySendUnreliable(double sentMs, out double arriveMs)
            {
                if (IsLost())
                {
                    DroppedUnreliable++;
                    arriveMs = double.PositiveInfinity;
                    return false;
                }

                arriveMs = sentMs + Delay();
                return true;
            }

            /// <param name="sentMs">送った時刻。</param>
            /// <param name="lastArriveMs">この向きで前に送った信頼性ありのものが届く時刻。届く時刻に更新する。</param>
            public double SendReliable(double sentMs, ref double lastArriveMs)
            {
                var attempt = sentMs;
                while (IsLost())
                {
                    ResentReliable++;
                    attempt += 2.0 * (_latencyMs + _jitterMs);
                }

                lastArriveMs = Math.Max(attempt + Delay(), lastArriveMs);
                return lastArriveMs;
            }

            private double Delay()
            {
                return _latencyMs + (_random.NextDouble() * _jitterMs);
            }

            private bool IsLost()
            {
                return _random.NextDouble() * 100.0 < _lossPercent;
            }
        }
    }
}
