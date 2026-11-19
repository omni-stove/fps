using System.Numerics;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class MatchMessageRoundTripTests
    {
        // 重さと倍率の数え方 (1 + 1 バイト)、遅くならない重さ・1 kg ごとの減り・重さの倍率の下限 (4 バイトずつ)、動き (1 バイト)
        private const int _carryLoadBytes = 1 + 1 + (3 * 4) + 1;

        // 承認の末尾に並ぶ吹き飛ばしの値 (力 3 種類 × 3 個と、そのほか 9 個)
        private const int _knockbackBytes = 18 * 4;

        // 伏せの値の後ろに並ぶリーン 3 個・空中で跳べる回数 (4 バイト)・二段目の速さ・ジャンプの猶予・カバーへ走る速さ・水泳 3 個・壁走りの入切 (1 バイト)・壁走り 4 個
        private const int _traversalBytes = (3 * 4) + 4 + 4 + 4 + 4 + (3 * 4) + 1 + (4 * 4);

        private readonly MessageTable _server = MatchMessages.CreateServerTable();
        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void ConnectRequest()
        {
            var sent = new ConnectRequestMessage { ClientVersion = "1.2.3", RejoinKey = "0123abcd", DisplayName = "ぷれいやー" };

            var got = ToServer<ConnectRequestMessage>(sent);

            Assert.That(got.ProtocolVersion, Is.EqualTo(MatchProtocol.Version));
            Assert.That(got.ClientVersion, Is.EqualTo("1.2.3"));
            Assert.That(got.RejoinKey, Is.EqualTo("0123abcd"));
            Assert.That(got.DisplayName, Is.EqualTo("ぷれいやー"));
            Assert.That(got.PartyId, Is.Empty);
            Assert.That(got.JoinCode, Is.Empty);
            Assert.That(got.SkillRating, Is.Null);
            Assert.That(got.ProductUserId, Is.Empty);
        }

        [Test]
        public void AConnectRequestWithoutExtrasKeepsTheOldLayout()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectRequestMessage { ClientVersion = "1.2.3", RejoinKey = "0123abcd", DisplayName = "ぷれいやー" });

            Assert.That(writer.Length, Is.EqualTo(2 + 1 + 2 + 7 + 10 + 17));
            Assert.That(ConnectRequestMessage.CurrentVersion, Is.EqualTo(1));
        }

        [Test]
        public void ConnectRequestWithExtras()
        {
            var sent = new ConnectRequestMessage
            {
                ClientVersion = "1.2.3",
                RejoinKey = "0123abcd",
                DisplayName = "p",
                PartyId = "party-1",
                JoinCode = "AB12",
                SkillRating = -25,
                ProductUserId = "0123456789abcdef0123456789abcdef",
            };

            var got = ToServer<ConnectRequestMessage>(sent);

            Assert.That(got.HasExtras, Is.True);
            Assert.That(got.DisplayName, Is.EqualTo("p"));
            Assert.That(got.PartyId, Is.EqualTo("party-1"));
            Assert.That(got.JoinCode, Is.EqualTo("AB12"));
            Assert.That(got.SkillRating, Is.EqualTo(-25));
            Assert.That(got.ProductUserId, Is.EqualTo("0123456789abcdef0123456789abcdef"));
        }

        [Test]
        public void AConnectRequestWithOnlyASkillRatingCarriesIt()
        {
            var got = ToServer<ConnectRequestMessage>(new ConnectRequestMessage { ClientVersion = "1.2.3", RejoinKey = "k", SkillRating = 0 });

            Assert.That(got.SkillRating, Is.EqualTo(0));
            Assert.That(got.PartyId, Is.Empty);
            Assert.That(got.JoinCode, Is.Empty);
            Assert.That(got.ProductUserId, Is.Empty);
        }

        [Test]
        public void AConnectRequestWithoutExtrasClearsTheExtrasReadBefore()
        {
            ToServer<ConnectRequestMessage>(
                new ConnectRequestMessage { ClientVersion = "1.2.3", RejoinKey = "k", PartyId = "party", JoinCode = "CODE", SkillRating = 9, ProductUserId = "u" });

            var got = ToServer<ConnectRequestMessage>(new ConnectRequestMessage { ClientVersion = "1.2.3", RejoinKey = "k" });

            Assert.That(got.HasExtras, Is.False);
            Assert.That(got.PartyId, Is.Empty);
            Assert.That(got.JoinCode, Is.Empty);
            Assert.That(got.SkillRating, Is.Null);
            Assert.That(got.ProductUserId, Is.Empty);
        }

        [Test]
        public void AConnectRequestRefusesToWriteAnOverlongExtra()
        {
            var writer = new MessageWriter();
            var request = new ConnectRequestMessage
            {
                ClientVersion = "1.2.3",
                RejoinKey = "k",
                JoinCode = new string('c', ConnectionExtras.MaxJoinCodeLength + 1),
            };

            Assert.Throws<System.ArgumentException>(() => writer.WriteMessage(request));
            Assert.That(writer.Length, Is.Zero);
        }

        [Test]
        public void AConnectRequestWithCutOffExtrasIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectRequestMessage { ClientVersion = "1.2.3", RejoinKey = "k", PartyId = "party" });
            var whole = writer.ToArray();

            Assert.Throws<MessageReadException>(() => _server.Read(new System.ArraySegment<byte>(whole, 0, whole.Length - 1)));
        }

        [Test]
        public void ConnectAccepted()
        {
            var got = ToClient<ConnectAcceptedMessage>(
                new ConnectAcceptedMessage
                {
                    PlayerId = 7,
                    ServerVersion = "1.2.3",
                    TickRate = 60,
                    ServerTick = 1234,
                    Map = "NetArena01",
                    MaxHealth = 150,
                });

            Assert.That(got.PlayerId, Is.EqualTo(7));
            Assert.That(got.ServerVersion, Is.EqualTo("1.2.3"));
            Assert.That(got.TickRate, Is.EqualTo(60));
            Assert.That(got.ServerTick, Is.EqualTo(1234));
            Assert.That(got.Map, Is.EqualTo("NetArena01"));
            Assert.That(got.MaxHealth, Is.EqualTo(150));
        }

        [Test]
        public void ConnectAcceptedCarriesTheServersMotorSettings()
        {
            var motor = new NetMotorSettings(
                new MotorSpec(
                    7f, 11f, 1.5f, 18f, 0.2f, 3f, 13f, 26f, 3.5f, 0.4f, 1.1f, 2.1f, 3.5f,
                    new MoveDirectionScale(1f, 0.75f, 0.9f), new MoveDirectionScale(1.1f, 0.5f, 0.6f),
                    new CarryLoadSpec(CarrySource.Carried, CarrySource.None, 4f, 0.05f, 0.6f, CarryGaits.Sprint | CarryGaits.Prone)),
                new StanceGeometry(1.8f, 0.1f, 0.65f, 1.1f, 0.9f, 0.4f),
                new CapsuleMoverSpec(40f, 0.25f, 0.06f, 5),
                new ProneSpec(0.9f, 1.4f, 25f, 0.4f, 2f, 0.6f));

            var got = ToClient<ConnectAcceptedMessage>(
                new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100, Motor = motor, KillPlaneY = -30f });

            Assert.That(got.KillPlaneY, Is.EqualTo(-30f));
            Assert.That(got.Motor.Motor.WalkSpeed, Is.EqualTo(7f));
            Assert.That(got.Motor.Motor.MantleSpeed, Is.EqualTo(3.5f));
            Assert.That(got.Motor.Motor.WalkDirection.Backward, Is.EqualTo(0.75f));
            Assert.That(got.Motor.Motor.WalkDirection.Side, Is.EqualTo(0.9f));
            Assert.That(got.Motor.Motor.SprintDirection.Forward, Is.EqualTo(1.1f));
            Assert.That(got.Motor.Motor.SprintDirection.Backward, Is.EqualTo(0.5f));
            Assert.That(got.Motor.Motor.SprintDirection.Side, Is.EqualTo(0.6f));
            Assert.That(got.Motor.Motor.CarryLoad.WeightSource, Is.EqualTo(CarrySource.Carried));
            Assert.That(got.Motor.Motor.CarryLoad.MultiplierSource, Is.EqualTo(CarrySource.None));
            Assert.That(got.Motor.Motor.CarryLoad.FreeWeight, Is.EqualTo(4f));
            Assert.That(got.Motor.Motor.CarryLoad.SpeedLossPerKg, Is.EqualTo(0.05f));
            Assert.That(got.Motor.Motor.CarryLoad.MinWeightMultiplier, Is.EqualTo(0.6f));
            Assert.That(got.Motor.Motor.CarryLoad.Gaits, Is.EqualTo(CarryGaits.Sprint | CarryGaits.Prone));
            Assert.That(got.Motor.Stance.Standing.Height, Is.EqualTo(1.8f));
            Assert.That(got.Motor.Stance.Standing.EyeY, Is.EqualTo(0.65f));
            Assert.That(got.Motor.Stance.Crouched.Height, Is.EqualTo(1.1f));
            Assert.That(got.Motor.Stance.Prone.Height, Is.EqualTo(0.9f));
            Assert.That(got.Motor.Stance.Radius, Is.EqualTo(0.4f));
            Assert.That(got.Motor.Mover.SlopeLimitDegrees, Is.EqualTo(40f));
            Assert.That(got.Motor.Mover.LayerMask, Is.EqualTo(5));
            Assert.That(got.Motor.Prone.Speed, Is.EqualTo(1.4f));
            Assert.That(got.Motor.Prone.DiveSeconds, Is.EqualTo(0.6f));
        }

        [Test]
        public void ConnectAcceptedCarriesTheLeanAirJumpSwimAndWallRunValues()
        {
            var motor = new NetMotorSettings(
                new MotorSpec(
                    5f, 8f, 1.2f, 20f, 0.1f, 2.5f,
                    leanSideOffset: 0.55f, leanAngleDegrees: 20f, leanSpeed: 4f, airJumps: 2, airJumpSpeed: 5.5f, jumpGraceSeconds: 0.12f),
                NetMotorSettings.Default.Stance,
                CapsuleMoverSpec.Default,
                ProneSpec.Default,
                new SwimTuning(2.5f, 9f, 20f),
                new WallRunTuning(true, 2f, 6f, 0.3f, 8f));

            var got = ToClient<ConnectAcceptedMessage>(
                new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100, Motor = motor, KillPlaneY = -30f });

            Assert.That(got.Motor.Motor.LeanSideOffset, Is.EqualTo(0.55f));
            Assert.That(got.Motor.Motor.LeanAngleDegrees, Is.EqualTo(20f));
            Assert.That(got.Motor.Motor.LeanSpeed, Is.EqualTo(4f));
            Assert.That(got.Motor.Motor.AirJumps, Is.EqualTo(2));
            Assert.That(got.Motor.Motor.AirJumpSpeed, Is.EqualTo(5.5f));
            Assert.That(got.Motor.Motor.JumpGraceSeconds, Is.EqualTo(0.12f));
            Assert.That(got.Motor.Swim.Speed, Is.EqualTo(2.5f));
            Assert.That(got.Motor.Swim.Buoyancy, Is.EqualTo(9f));
            Assert.That(got.Motor.Swim.BreathSeconds, Is.EqualTo(20f));
            Assert.That(got.Motor.WallRun.Enabled, Is.True);
            Assert.That(got.Motor.WallRun.MaxSeconds, Is.EqualTo(2f));
            Assert.That(got.Motor.WallRun.MinSpeed, Is.EqualTo(6f));
            Assert.That(got.Motor.WallRun.GravityScale, Is.EqualTo(0.3f));
            Assert.That(got.Motor.WallRun.JumpOffSpeed, Is.EqualTo(8f));
            Assert.That(got.KillPlaneY, Is.EqualTo(-30f), "the values after the new ones still line up");
        }

        [Test]
        public void ConnectAcceptedWithANonPositiveBreathIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 });
            var bytes = writer.AsSegment().ToArray();

            // 息の秒数は水泳の 3 番目で、後ろに壁走りの入切 (1 バイト) と 4 個、落ちる高さ、吹き飛ばしの値が続く
            var breathSeconds = bytes.Length - _knockbackBytes - 4 - (4 * 4) - 1 - 4;
            Assert.That(System.BitConverter.ToSingle(bytes, breathSeconds), Is.EqualTo(SwimTuning.DefaultBreathSeconds), "the offset points at the breath");
            System.BitConverter.TryWriteBytes(new System.Span<byte>(bytes, breathSeconds, 4), 0f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ConnectAcceptedWithANegativeJumpGraceIsUnreadable()
        {
            var writer = new MessageWriter();
            var motor = new NetMotorSettings(
                new MotorSpec(5f, 8f, 1.2f, 20f, 0.1f, 2.5f, jumpGraceSeconds: 0.12f),
                NetMotorSettings.Default.Stance,
                CapsuleMoverSpec.Default,
                ProneSpec.Default,
                SwimTuning.Default,
                WallRunTuning.Default);
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100, Motor = motor });
            var bytes = writer.AsSegment().ToArray();

            // ジャンプの猶予はカバーへ走る速さと水泳 3 個の前で、後ろに壁走りの入切 (1 バイト) と 4 個、落ちる高さ、吹き飛ばしの値が続く
            var jumpGrace = bytes.Length - _knockbackBytes - 4 - (4 * 4) - 1 - (3 * 4) - 4 - 4;
            Assert.That(System.BitConverter.ToSingle(bytes, jumpGrace), Is.EqualTo(0.12f), "the offset points at the jump grace");
            System.BitConverter.TryWriteBytes(new System.Span<byte>(bytes, jumpGrace, 4), -0.1f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ConnectAcceptedWithMotorSettingsOutOfRangeIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 });
            var bytes = writer.AsSegment().ToArray();

            // 歩く速さ (移動の値の先頭) を負にする。最後の 4 バイトは落ちる高さ、その前に移動の値が並ぶ (19 + 6 個、持ち物の値、6 + 3 + 1 + 6 個)
            var walkSpeed = bytes.Length - _knockbackBytes - 4 - _traversalBytes - ((19 + 6 + 6 + 3 + 1 + 6) * 4) - _carryLoadBytes;
            System.BitConverter.TryWriteBytes(new System.Span<byte>(bytes, walkSpeed, 4), -1f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ConnectAcceptedWithACarryMinMultiplierAboveOneIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 });
            var bytes = writer.AsSegment().ToArray();

            // 重さの倍率の下限は持ち物の値の 4 番目 (重さと倍率の数え方 2 バイト、遅くならない重さ、1 kg ごとの減り) で、後ろに動きの 1 バイトが続く
            var minWeightMultiplier = bytes.Length - _knockbackBytes - 4 - _traversalBytes - ((6 + 3 + 1 + 6) * 4) - 1 - 4;
            System.BitConverter.TryWriteBytes(new System.Span<byte>(bytes, minWeightMultiplier, 4), 1.5f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ConnectAcceptedWithAnUndefinedCarrySourceIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 });
            var bytes = writer.AsSegment().ToArray();

            var weightSource = bytes.Length - _knockbackBytes - 4 - _traversalBytes - ((6 + 3 + 1 + 6) * 4) - _carryLoadBytes;
            bytes[weightSource] = 9;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ConnectAcceptedWithANegativeDirectionScaleIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = "NetTest", MaxHealth = 100 });
            var bytes = writer.AsSegment().ToArray();

            // 歩きの前の倍率 (向きの倍率の先頭) を負にする。その後ろに向きの倍率 6 個と持ち物の値、体・カプセル・伏せの値 (6 + 3 + 1 + 6 個)、落ちる高さが並ぶ
            var walkForward = bytes.Length - _knockbackBytes - 4 - _traversalBytes - ((6 + 6 + 3 + 1 + 6) * 4) - _carryLoadBytes;
            System.BitConverter.TryWriteBytes(new System.Span<byte>(bytes, walkForward, 4), -1f);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new System.ArraySegment<byte>(bytes)));
        }

        [Test]
        public void TheSyncedRulesCarryProneAndLadder()
        {
            var rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithProne(Prone.On).WithLadder(Ladder.On)));

            Assert.That(rules.Prone, Is.EqualTo(Prone.On));
            Assert.That(rules.Ladder, Is.EqualTo(Ladder.On));
            Assert.That(rules.SameAs(MatchRulesSync.From(GameRules.Default)), Is.False);
        }

        [Test]
        public void TheSyncedRulesCarryTheJumpAndAllowItBeforeAnyArrive()
        {
            var rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithJump(Jump.Off)));

            Assert.That(default(MatchRulesSync).Jump, Is.EqualTo(Jump.On));
            Assert.That(MatchRulesSync.From(GameRules.Default).Jump, Is.EqualTo(Jump.On));
            Assert.That(rules.Jump, Is.EqualTo(Jump.Off));
            Assert.That(rules.SameAs(MatchRulesSync.From(GameRules.Default)), Is.False);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MatchRulesSync { Jump = (Jump)99 });
        }

        [Test]
        public void TheSyncedRulesCarryTheReloadMode()
        {
            var rules = MatchRulesSync.From(GameRules.Default.WithReloadMode(ReloadMode.Manual));

            Assert.That(MatchRulesSync.From(GameRules.Default).ReloadMode, Is.EqualTo(ReloadMode.Auto));
            Assert.That(rules.ReloadMode, Is.EqualTo(ReloadMode.Manual));
            Assert.That(rules.SameAs(MatchRulesSync.From(GameRules.Default)), Is.False);
        }

        [Test]
        public void TheSyncedRulesCarryTextChatSpectatingAndAbilities()
        {
            var defaults = MatchRulesSync.From(GameRules.Default);

            Assert.That(defaults.TextChat, Is.EqualTo(TextChat.On));
            Assert.That(defaults.Spectating, Is.EqualTo(Spectating.On));
            Assert.That(defaults.Abilities, Is.EqualTo(Abilities.Off));
            Assert.That(MatchRulesSync.From(GameRules.Default.WithTextChat(TextChat.Off)).SameAs(defaults), Is.False);
            Assert.That(MatchRulesSync.From(GameRules.Default.WithSpectating(Spectating.Off)).SameAs(defaults), Is.False);
            Assert.That(MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On))).SameAs(defaults), Is.False);
        }

        [TestCase(TextChat.Off, Spectating.Off, Abilities.On)]
        [TestCase(TextChat.On, Spectating.On, Abilities.Off)]
        [TestCase(TextChat.Off, Spectating.On, Abilities.On)]
        public void TextChatSpectatingAndAbilitiesRoundTripInTheSnapshot(TextChat textChat, Spectating spectating, Abilities abilities)
        {
            var rules = GameRules.Default
                .WithTextChat(textChat)
                .WithSpectating(spectating)
                .WithActions(ActionRules.Default.WithAbilities(abilities));
            var sent = new SnapshotMessage();
            sent.Clear();
            sent.Tick = 10;
            sent.Match.State = GameModeState.Playing;
            sent.Match.Rules = MatchRulesSync.From(rules);

            var got = ToClient<SnapshotMessage>(sent);

            Assert.That(got.Match.Rules.TextChat, Is.EqualTo(textChat));
            Assert.That(got.Match.Rules.Spectating, Is.EqualTo(spectating));
            Assert.That(got.Match.Rules.Abilities, Is.EqualTo(abilities));
            Assert.That(got.Match.Rules.SameAs(sent.Match.Rules), Is.True);
        }

        [TestCase("")]
        [TestCase("../NetTest")]
        public void ConnectAcceptedWithAMapThatIsNotASceneNameIsUnreadable(string map)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60, Map = map, MaxHealth = 100 });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void ConnectRejectedAndServerDisconnectingCarryTheNoticeReason()
        {
            var rejected = ToClient<ConnectRejectedMessage>(
                new ConnectRejectedMessage { Reason = DisconnectNotice.VersionMismatchReason("1.2.3") });
            var stopping = ToClient<ServerDisconnectingMessage>(
                new ServerDisconnectingMessage { Reason = DisconnectNotice.ServerStoppedReason });

            Assert.That(rejected.Reason, Is.EqualTo(DisconnectNotice.VersionMismatchReason("1.2.3")));
            Assert.That(stopping.Reason, Is.EqualTo(DisconnectNotice.ServerStoppedReason));
        }

        [Test]
        public void NetStats()
        {
            Assert.That(ToServer<NetStatsProbeMessage>(new NetStatsProbeMessage { Sequence = 65000 }).Sequence, Is.EqualTo(65000));
            var echo = ToClient<NetStatsEchoMessage>(new NetStatsEchoMessage { Sequence = 3, ServerTick = 99 });
            Assert.That(echo.Sequence, Is.EqualTo(3));
            Assert.That(echo.ServerTick, Is.EqualTo(99));
        }

        [Test]
        public void Input()
        {
            var sent = new InputMessage { AckedSnapshotTick = 40 };
            sent.Add(new InputFrame(50, new Vector2(0.5f, 1f), 1.25f, InputButtons.Sprint | InputButtons.Jump));
            sent.Add(new InputFrame(51, new Vector2(-1f, 0f), -3f, InputButtons.All));

            var got = ToServer<InputMessage>(sent);

            Assert.That(got.AckedSnapshotTick, Is.EqualTo(40));
            Assert.That(got.Count, Is.EqualTo(2));
            Assert.That(got[0].Tick, Is.EqualTo(50));
            Assert.That(got[0].Move, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(got[0].Yaw, Is.EqualTo(1.25f));
            Assert.That(got[0].Buttons, Is.EqualTo(InputButtons.Sprint | InputButtons.Jump));
            Assert.That(got[1].Tick, Is.EqualTo(51));
            Assert.That(got[1].Has(InputButtons.Dodge), Is.True);
            Assert.That(got[1].Buttons, Is.EqualTo(InputButtons.All));
        }

        [Test]
        public void InputCarriesTheLeanButtons()
        {
            var sent = new InputMessage();
            sent.Add(new InputFrame(7, Vector2.Zero, 0f, InputButtons.LeanLeft | InputButtons.Aim));
            sent.Add(new InputFrame(8, Vector2.Zero, 0f, InputButtons.LeanRight));

            var got = ToServer<InputMessage>(sent);

            Assert.That(got[0].Buttons, Is.EqualTo(InputButtons.LeanLeft | InputButtons.Aim));
            Assert.That(got[0].Has(InputButtons.LeanRight), Is.False);
            Assert.That(got[1].Buttons, Is.EqualTo(InputButtons.LeanRight));
        }

        [Test]
        public void ChatRequestAndChat()
        {
            var request = ToServer<ChatRequestMessage>(new ChatRequestMessage { Channel = ChatChannel.Team, Text = "右から来る" });
            Assert.That(request.Channel, Is.EqualTo(ChatChannel.Team));
            Assert.That(request.Text, Is.EqualTo("右から来る"));

            var chat = ToClient<ChatMessage>(new ChatMessage { SenderId = 3, Channel = ChatChannel.All, Text = "gg" });
            Assert.That(chat.SenderId, Is.EqualTo(3));
            Assert.That(chat.Channel, Is.EqualTo(ChatChannel.All));
            Assert.That(chat.Text, Is.EqualTo("gg"));
        }

        [Test]
        public void EmptyOrTooLongChatIsNotWritten()
        {
            Assert.Throws<System.InvalidOperationException>(() => new MessageWriter().WriteMessage(new ChatRequestMessage { Text = "" }));
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new ChatMessage { SenderId = 1, Text = null }));
            Assert.Throws<System.ArgumentException>(
                () => new MessageWriter().WriteMessage(new ChatRequestMessage { Text = new string('a', MatchProtocol.MaxChatBytes + 1) }));
        }

        [Test]
        public void ChatWithoutTextOrWithAnUnknownChannelIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.ChatRequest);
            writer.WriteByte(ChatRequestMessage.CurrentVersion);
            writer.WriteByte((byte)ChatChannel.All);
            writer.WriteString("");
            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));

            writer.Reset();
            writer.WriteUInt16(MessageIds.Chat);
            writer.WriteByte(ChatMessage.CurrentVersion);
            writer.WriteInt32(1);
            writer.WriteByte(2);
            writer.WriteString("hi");
            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void MapVoteRequestAndState()
        {
            Assert.That(ToServer<MapVoteRequestMessage>(new MapVoteRequestMessage { MapIndex = 2 }).MapIndex, Is.EqualTo(2));

            var sent = new MapVoteStateMessage { RemainingSeconds = 12.5f };
            sent.AddCandidate("NetArena01", 3);
            sent.AddCandidate("NetHarbor", 0);
            sent.AddCandidate("NetDesert", 1);
            var got = ToClient<MapVoteStateMessage>(sent);

            Assert.That(got.CandidateCount, Is.EqualTo(3));
            Assert.That(got.MapId(0), Is.EqualTo("NetArena01"));
            Assert.That(got.Votes(0), Is.EqualTo(3));
            Assert.That(got.MapId(1), Is.EqualTo("NetHarbor"));
            Assert.That(got.Votes(1), Is.Zero);
            Assert.That(got.MapId(2), Is.EqualTo("NetDesert"));
            Assert.That(got.Votes(2), Is.EqualTo(1));
            Assert.That(got.RemainingSeconds, Is.EqualTo(12.5f));
            Assert.That(got.ChosenIndex, Is.EqualTo(MapVoteStateMessage.NotChosen));

            sent.RemainingSeconds = 0f;
            sent.ChosenIndex = 0;
            got = ToClient<MapVoteStateMessage>(sent);
            Assert.That(got.ChosenIndex, Is.Zero);
        }

        [Test]
        public void AFewerCandidateMapVoteStateDropsTheOldOnes()
        {
            var many = new MapVoteStateMessage();
            many.AddCandidate("a", 1);
            many.AddCandidate("b", 2);
            ToClient<MapVoteStateMessage>(many);
            var one = new MapVoteStateMessage();
            one.AddCandidate("c", 0);

            var got = ToClient<MapVoteStateMessage>(one);

            Assert.That(got.CandidateCount, Is.EqualTo(1));
            Assert.That(got.MapId(0), Is.EqualTo("c"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => got.MapId(1));
        }

        [Test]
        public void MapVoteStateRefusesAChoiceOutsideTheCandidates()
        {
            var state = new MapVoteStateMessage { ChosenIndex = 1 };
            state.AddCandidate("a", 0);

            Assert.Throws<System.InvalidOperationException>(() => new MessageWriter().WriteMessage(state));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => state.AddCandidate("b", -1));
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new MapVoteRequestMessage { MapIndex = -1 }));
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new MapVoteRequestMessage { MapIndex = MapVoteStateMessage.MaxCandidates }));
        }

        [Test]
        public void MapVoteStateWithAChoiceOutsideTheCandidatesIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.MapVoteState);
            writer.WriteByte(MapVoteStateMessage.CurrentVersion);
            writer.WriteByte(1);
            writer.WriteString("a");
            writer.WriteInt32(0);
            writer.WriteSingle(1f);
            writer.WriteByte(2);
            writer.WriteString("");

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void MapVoteStateCarriesTheNextPlaylistEntry()
        {
            var sent = new MapVoteStateMessage { RemainingSeconds = 3f, NextPlaylistEntryName = "playlist.hardpoint-night" };
            sent.AddCandidate("NetArena01", 0);

            var got = ToClient<MapVoteStateMessage>(sent);

            Assert.That(MapVoteStateMessage.CurrentVersion, Is.EqualTo(2));
            Assert.That(got.NextPlaylistEntryName, Is.EqualTo("playlist.hardpoint-night"));
            Assert.That(got.MapId(0), Is.EqualTo("NetArena01"));
        }

        [Test]
        public void MapVoteStateWithoutAPlaylistRoundTripsAnEmptyName()
        {
            ToClient<MapVoteStateMessage>(new MapVoteStateMessage { NextPlaylistEntryName = "playlist.old" });
            var writer = new MessageWriter();
            writer.WriteMessage(new MapVoteStateMessage { NextPlaylistEntryName = null });

            var got = (MapVoteStateMessage)_client.Read(writer.AsSegment());

            // Id・版 (2 + 1)、候補の数 (1)、残り秒 (4)、選ばれた番号 (1)、次の項目の名前 (2)
            Assert.That(writer.Length, Is.EqualTo(3 + 1 + 4 + 1 + 2));
            Assert.That(got.NextPlaylistEntryName, Is.Empty);
            Assert.That(new MapVoteStateMessage().NextPlaylistEntryName, Is.Empty);
        }

        [Test]
        public void ANextPlaylistEntryLongerThanAContentIdIsNotWritten()
        {
            var sent = new MapVoteStateMessage { NextPlaylistEntryName = new string('a', MatchProtocol.MaxContentIdBytes + 1) };

            Assert.Throws<System.ArgumentException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void VoteKickRequest()
        {
            var yes = ToServer<VoteKickRequestMessage>(new VoteKickRequestMessage { TargetPlayerId = 7, InFavor = true });
            Assert.That(yes.TargetPlayerId, Is.EqualTo(7));
            Assert.That(yes.InFavor, Is.True);

            var no = ToServer<VoteKickRequestMessage>(new VoteKickRequestMessage { TargetPlayerId = 0, InFavor = false });
            Assert.That(no.TargetPlayerId, Is.Zero);
            Assert.That(no.InFavor, Is.False);
        }

        [Test]
        public void ADefaultVoteKickRequestRoundTrips()
        {
            var got = ToServer<VoteKickRequestMessage>(new VoteKickRequestMessage());

            Assert.That(got.TargetPlayerId, Is.Zero);
            Assert.That(got.InFavor, Is.False);
        }

        [Test]
        public void AVoteKickRequestWithANegativeTargetIsNotWritten()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new VoteKickRequestMessage { TargetPlayerId = -1 }));
        }

        [Test]
        public void AVoteKickRequestWithANegativeTargetIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.VoteKickRequest);
            writer.WriteByte(VoteKickRequestMessage.CurrentVersion);
            writer.WriteInt32(-1);
            writer.WriteBool(true);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [TestCase(VoteKickOutcome.Undecided)]
        [TestCase(VoteKickOutcome.Passed)]
        [TestCase(VoteKickOutcome.Failed)]
        public void VoteKickState(VoteKickOutcome outcome)
        {
            var sent = new VoteKickStateMessage
            {
                TargetPlayerId = 4,
                CallerPlayerId = 2,
                YesVotes = 3,
                RequiredVotes = 5,
                RemainingSeconds = 17.5f,
                Outcome = outcome,
            };

            var got = ToClient<VoteKickStateMessage>(sent);

            Assert.That(got.TargetPlayerId, Is.EqualTo(4));
            Assert.That(got.CallerPlayerId, Is.EqualTo(2));
            Assert.That(got.YesVotes, Is.EqualTo(3));
            Assert.That(got.RequiredVotes, Is.EqualTo(5));
            Assert.That(got.RemainingSeconds, Is.EqualTo(17.5f));
            Assert.That(got.Outcome, Is.EqualTo(outcome));
        }

        [Test]
        public void ADefaultVoteKickStateRoundTrips()
        {
            var got = ToClient<VoteKickStateMessage>(new VoteKickStateMessage());

            Assert.That(got.TargetPlayerId, Is.Zero);
            Assert.That(got.CallerPlayerId, Is.Zero);
            Assert.That(got.YesVotes, Is.Zero);
            Assert.That(got.RequiredVotes, Is.EqualTo(1));
            Assert.That(got.RemainingSeconds, Is.Zero);
            Assert.That(got.Outcome, Is.EqualTo(VoteKickOutcome.Undecided));
        }

        [TestCase(-1, 0, 0, 1, 0f)]
        [TestCase(0, -1, 0, 1, 0f)]
        [TestCase(0, 0, -1, 1, 0f)]
        [TestCase(0, 0, 0, 0, 0f)]
        [TestCase(0, 0, 0, 1, -1f)]
        [TestCase(0, 0, 0, 1, float.NaN)]
        public void AnImpossibleVoteKickStateIsNotWritten(int target, int caller, int yes, int required, float seconds)
        {
            var sent = new VoteKickStateMessage
            {
                TargetPlayerId = target,
                CallerPlayerId = caller,
                YesVotes = yes,
                RequiredVotes = required,
                RemainingSeconds = seconds,
            };

            Assert.Throws<System.InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AVoteKickStateWithAnUnknownOutcomeIsNotWritten()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new VoteKickStateMessage { Outcome = (VoteKickOutcome)3 }));
        }

        [TestCase(-1, 2, 0, 1, 1f, 0)]
        [TestCase(1, -1, 0, 1, 1f, 0)]
        [TestCase(1, 2, -1, 1, 1f, 0)]
        [TestCase(1, 2, 0, 0, 1f, 0)]
        [TestCase(1, 2, 0, 1, -1f, 0)]
        [TestCase(1, 2, 0, 1, float.PositiveInfinity, 0)]
        [TestCase(1, 2, 0, 1, 1f, 3)]
        public void AnImpossibleVoteKickStateIsUnreadable(int target, int caller, int yes, int required, float seconds, int outcome)
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.VoteKickState);
            writer.WriteByte(VoteKickStateMessage.CurrentVersion);
            writer.WriteInt32(target);
            writer.WriteInt32(caller);
            writer.WriteInt32(yes);
            writer.WriteInt32(required);
            writer.WriteSingle(seconds);
            writer.WriteByte((byte)outcome);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void BuyRequest()
        {
            var got = ToServer<BuyRequestMessage>(new BuyRequestMessage { ItemId = "armor-light" });

            Assert.That(got.ItemId, Is.EqualTo("armor-light"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void ABuyRequestWithoutAnItemIsNotWritten(string itemId)
        {
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new BuyRequestMessage { ItemId = itemId }));
        }

        [Test]
        public void ABuyRequestWithoutAnItemIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.BuyRequest);
            writer.WriteByte(BuyRequestMessage.CurrentVersion);
            writer.WriteString("");

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void ABuyRequestLongerThanAContentIdIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.BuyRequest);
            writer.WriteByte(BuyRequestMessage.CurrentVersion);
            writer.WriteString(new string('a', MatchProtocol.MaxContentIdBytes + 1));

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void MapVoteRequestOutsideTheCandidateLimitIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.MapVoteRequest);
            writer.WriteByte(MapVoteRequestMessage.CurrentVersion);
            writer.WriteByte(MapVoteStateMessage.MaxCandidates);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [TestCase(SpectateStep.Next)]
        [TestCase(SpectateStep.Previous)]
        public void SpectateRequest(SpectateStep step)
        {
            Assert.That(ToServer<SpectateRequestMessage>(new SpectateRequestMessage { Step = step }).Step, Is.EqualTo(step));
        }

        [Test]
        public void SpectateRequestWithAnUnknownStepIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.SpectateRequest);
            writer.WriteByte(SpectateRequestMessage.CurrentVersion);
            writer.WriteByte(2);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void FireRequest()
        {
            var got = ToServer<FireRequestMessage>(new FireRequestMessage
            {
                WeaponId = "rifle",
                Origin = new Vector3(1f, 2f, 3f),
                Direction = new Vector3(0f, 0f, 1f),
                ClientTick = 77,
                InterpolationAlpha = 0.25f,
                ShotIndex = 4,
                ShotGeneration = 2,
            });

            Assert.That(got.WeaponId, Is.EqualTo("rifle"));
            Assert.That(got.Origin, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(got.Direction, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            Assert.That(got.ClientTick, Is.EqualTo(77));
            Assert.That(got.InterpolationAlpha, Is.EqualTo(0.25f));
            Assert.That(got.ShotIndex, Is.EqualTo(4));
            Assert.That(got.ShotGeneration, Is.EqualTo(2));
            Assert.That(got.ChargeFraction, Is.Zero);
            Assert.That(got.AmmoTypeIndex, Is.Zero);
            Assert.That(got.HasLockTarget, Is.False);
            Assert.That(got.Trigger, Is.EqualTo(TriggerEdge.None));
        }

        [Test]
        public void FireRequestCarriesChargeAmmoTypeLockAndTrigger()
        {
            var got = ToServer<FireRequestMessage>(new FireRequestMessage
            {
                WeaponId = "railgun",
                Direction = new Vector3(0f, 0f, 1f),
                ChargeFraction = 0.75f,
                AmmoTypeIndex = 3,
                HasLockTarget = true,
                LockTargetId = 9,
                Trigger = TriggerEdge.Released,
            });

            Assert.That(got.ChargeFraction, Is.EqualTo(0.75f));
            Assert.That(got.AmmoTypeIndex, Is.EqualTo(3));
            Assert.That(got.HasLockTarget, Is.True);
            Assert.That(got.LockTargetId, Is.EqualTo(9));
            Assert.That(got.Trigger, Is.EqualTo(TriggerEdge.Released));
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void FireRequestWithAChargeOutsideZeroToOneIsUnreadable(float charge)
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new FireRequestMessage { WeaponId = "railgun", ChargeFraction = charge });

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void FireRequestRejectsANegativeLockTarget()
        {
            var request = new FireRequestMessage { HasLockTarget = true, LockTargetId = -1 };

            Assert.Throws<System.InvalidOperationException>(() => new MessageWriter().WriteMessage(request));
        }

        [Test]
        public void WeaponRequests()
        {
            Assert.That(
                ToServer<WeaponActionRequestMessage>(new WeaponActionRequestMessage { Action = WeaponAction.ActiveReload }).Action,
                Is.EqualTo(WeaponAction.ActiveReload));
            Assert.That(
                ToServer<WeaponActionRequestMessage>(new WeaponActionRequestMessage { Action = WeaponAction.SwitchAmmoType }).Action,
                Is.EqualTo(WeaponAction.SwitchAmmoType));
            Assert.That(
                ToServer<SwitchWeaponRequestMessage>(new SwitchWeaponRequestMessage { WeaponId = "pistol" }).WeaponId,
                Is.EqualTo("pistol"));

            var loadout = new LoadoutRequestMessage();
            loadout.WeaponIds.Add("rifle");
            loadout.WeaponIds.Add("pistol");
            var got = ToServer<LoadoutRequestMessage>(loadout);
            Assert.That(got.WeaponIds.Count, Is.EqualTo(2));
            Assert.That(got.WeaponIds[0], Is.EqualTo("rifle"));
            Assert.That(got.WeaponIds[1], Is.EqualTo("pistol"));
        }

        [Test]
        public void GrenadePingAndReviveRequests()
        {
            var thrown = ToServer<ThrowGrenadeRequestMessage>(
                new ThrowGrenadeRequestMessage { Direction = new Vector3(0f, 0.5f, 1f), GrenadeTypeId = "smoke" });
            Assert.That(thrown.Direction, Is.EqualTo(new Vector3(0f, 0.5f, 1f)));
            Assert.That(thrown.GrenadeTypeId, Is.EqualTo("smoke"));
            Assert.That(
                ToServer<PingRequestMessage>(new PingRequestMessage { Direction = new Vector3(1f, 0f, 0f) }).Direction,
                Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(ToServer<ReviveRequestMessage>(new ReviveRequestMessage { TargetPlayerId = 12 }).TargetPlayerId, Is.EqualTo(12));
        }

        [Test]
        public void StructureState()
        {
            var sent = new StructureStateMessage { StructureId = 3 };
            sent.SetWords(new ulong[] { 1UL, ulong.MaxValue, 0x8000000000000000UL });

            var got = ToClient<StructureStateMessage>(sent);
            var words = new ulong[got.WordCount];
            got.CopyWordsTo(words);

            Assert.That(got.StructureId, Is.EqualTo(3));
            Assert.That(words, Is.EqualTo(new ulong[] { 1UL, ulong.MaxValue, 0x8000000000000000UL }));
        }

        [Test]
        public void ShotWithACharacterImpact()
        {
            var got = ToClient<ShotMessage>(new ShotMessage
            {
                HasShooter = true,
                ShooterId = 5,
                WeaponId = "rifle",
                Origin = new Vector3(1f, 1f, 1f),
                Direction = new Vector3(0f, 0f, 1f),
                HasImpact = true,
                ImpactPoint = new Vector3(1f, 1f, 9f),
                ImpactNormal = new Vector3(0f, 0f, -1f),
                ImpactMaterialId = "flesh",
                ImpactIsCharacter = true,
                HasImpactZone = true,
                ImpactZone = HitZone.Head,
            });

            Assert.That(got.HasShooter, Is.True);
            Assert.That(got.ShooterId, Is.EqualTo(5));
            Assert.That(got.WeaponId, Is.EqualTo("rifle"));
            Assert.That(got.Origin, Is.EqualTo(new Vector3(1f, 1f, 1f)));
            Assert.That(got.Direction, Is.EqualTo(new Vector3(0f, 0f, 1f)));
            Assert.That(got.HasImpact, Is.True);
            Assert.That(got.ImpactPoint, Is.EqualTo(new Vector3(1f, 1f, 9f)));
            Assert.That(got.ImpactNormal, Is.EqualTo(new Vector3(0f, 0f, -1f)));
            Assert.That(got.ImpactMaterialId, Is.EqualTo("flesh"));
            Assert.That(got.ImpactIsCharacter, Is.True);
            Assert.That(got.HasImpactZone, Is.True);
            Assert.That(got.ImpactZone, Is.EqualTo(HitZone.Head));
            Assert.That(got.SegmentCount, Is.EqualTo(1));
            Assert.That(got.Segment(0).ImpactMaterialId, Is.EqualTo("flesh"));
        }

        [Test]
        public void ShotCarriesEverySegmentOfOneFiring()
        {
            var sent = new ShotMessage { HasShooter = true, ShooterId = 2, WeaponId = "shotgun", Direction = new Vector3(0f, 0f, 1f) };
            ref var pellet = ref sent.AddSegment();
            pellet.Origin = new Vector3(0f, 1f, 0f);
            pellet.Direction = new Vector3(0.1f, 0f, 1f);
            pellet.HasImpact = true;
            pellet.ImpactPoint = new Vector3(1f, 1f, 10f);
            pellet.ImpactNormal = new Vector3(0f, 0f, -1f);
            pellet.ImpactMaterialId = "flesh";
            pellet.ImpactIsCharacter = true;
            pellet.HasImpactZone = true;
            pellet.ImpactZone = HitZone.Limb;
            ref var ricochet = ref sent.AddSegment();
            ricochet.Origin = new Vector3(1f, 1f, 10f);
            ricochet.Direction = new Vector3(0f, 1f, 0f);
            ricochet.HasImpact = true;
            ricochet.ImpactPoint = new Vector3(1f, 4f, 10f);
            ricochet.ImpactNormal = new Vector3(0f, -1f, 0f);
            ricochet.ImpactMaterialId = "metal";

            var got = ToClient<ShotMessage>(sent);

            Assert.That(got.SegmentCount, Is.EqualTo(3));
            Assert.That(got.HasImpact, Is.False);
            Assert.That(got.Segment(1).Direction, Is.EqualTo(new Vector3(0.1f, 0f, 1f)));
            Assert.That(got.Segment(1).ImpactIsCharacter, Is.True);
            Assert.That(got.Segment(1).ImpactZone, Is.EqualTo(HitZone.Limb));
            Assert.That(got.Segment(2).Origin, Is.EqualTo(new Vector3(1f, 1f, 10f)));
            Assert.That(got.Segment(2).ImpactPoint, Is.EqualTo(new Vector3(1f, 4f, 10f)));
            Assert.That(got.Segment(2).ImpactMaterialId, Is.EqualTo("metal"));
            Assert.That(got.Segment(2).ImpactIsCharacter, Is.False);
        }

        [Test]
        public void ReadingAOneSegmentShotAfterAManySegmentShotLeavesOneSegment()
        {
            var many = new ShotMessage { WeaponId = "shotgun" };
            many.AddSegment();
            ToClient<ShotMessage>(many);

            var got = ToClient<ShotMessage>(new ShotMessage { WeaponId = "rifle" });

            Assert.That(got.SegmentCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetSegmentsLeavesOneEmptySegment()
        {
            var shot = new ShotMessage { HasImpact = true };
            shot.AddSegment();

            shot.ResetSegments();

            Assert.That(shot.SegmentCount, Is.EqualTo(1));
            Assert.That(shot.HasImpact, Is.False);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => shot.Segment(1));
        }

        [Test]
        public void ShotHoldsAtMostMaxSegments()
        {
            var shot = new ShotMessage();
            for (var i = 1; i < ShotMessage.MaxSegments; i++)
            {
                shot.AddSegment();
            }

            Assert.Throws<System.InvalidOperationException>(() => shot.AddSegment());
        }

        [Test]
        public void ShotWithoutSegmentsIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.Shot);
            writer.WriteByte(ShotMessage.CurrentVersion);
            writer.WriteInt32(PlayerIds.None);
            writer.WriteString("rifle");
            writer.WriteByte(0);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void ShotWithoutShooterOrImpact()
        {
            var got = ToClient<ShotMessage>(new ShotMessage { WeaponId = "rifle", Direction = new Vector3(0f, 0f, 1f) });

            Assert.That(got.HasShooter, Is.False);
            Assert.That(got.HasImpact, Is.False);
            Assert.That(got.HasImpactZone, Is.False);
        }

        [Test]
        public void ShotRejectsAZoneOnANonCharacterImpact()
        {
            var shot = new ShotMessage { HasImpact = true, ImpactIsCharacter = false, HasImpactZone = true };

            Assert.Throws<System.InvalidOperationException>(() => new MessageWriter().WriteMessage(shot));
        }

        [Test]
        public void HitConfirmAndDamageTaken()
        {
            var hit = ToClient<HitConfirmMessage>(new HitConfirmMessage { Damage = 25, Zone = HitZone.Limb, Killed = true, Downed = true });
            Assert.That(hit.Damage, Is.EqualTo(25));
            Assert.That(hit.Zone, Is.EqualTo(HitZone.Limb));
            Assert.That(hit.Killed, Is.True);
            Assert.That(hit.Downed, Is.True);

            var taken = ToClient<DamageTakenMessage>(
                new DamageTakenMessage { HasSource = true, SourcePosition = new Vector3(4f, 5f, 6f), Amount = 30 });
            Assert.That(taken.HasSource, Is.True);
            Assert.That(taken.SourcePosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));
            Assert.That(taken.Amount, Is.EqualTo(30));

            var unknown = ToClient<DamageTakenMessage>(new DamageTakenMessage { Amount = 10 });
            Assert.That(unknown.HasSource, Is.False);
            Assert.That(unknown.SourcePosition, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void KilledAndExplosion()
        {
            var killed = ToClient<KilledMessage>(
                new KilledMessage { KillerId = 1, VictimId = 2, WeaponId = "rifle", Headshot = true, Friendly = true });
            Assert.That(killed.KillerId, Is.EqualTo(1));
            Assert.That(killed.VictimId, Is.EqualTo(2));
            Assert.That(killed.WeaponId, Is.EqualTo("rifle"));
            Assert.That(killed.Headshot, Is.True);
            Assert.That(killed.Friendly, Is.True);
            Assert.That(killed.AssisterCount, Is.Zero);

            var explosion = ToClient<ExplosionMessage>(
                new ExplosionMessage { Center = new Vector3(1f, 0f, 2f), HasThrower = true, ThrowerId = 4, WeaponId = "frag" });
            Assert.That(explosion.Center, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(explosion.HasThrower, Is.True);
            Assert.That(explosion.ThrowerId, Is.EqualTo(4));
            Assert.That(explosion.WeaponId, Is.EqualTo("frag"));
        }

        [Test]
        public void KilledCarriesTheAssisters()
        {
            var sent = new KilledMessage { KillerId = 1, VictimId = 2, WeaponId = "rifle" };
            sent.AddAssister(3);
            sent.AddAssister(5);

            var got = ToClient<KilledMessage>(sent);

            Assert.That(got.AssisterCount, Is.EqualTo(2));
            Assert.That(got.AssisterId(0), Is.EqualTo(3));
            Assert.That(got.AssisterId(1), Is.EqualTo(5));

            sent.ClearAssisters();
            got = ToClient<KilledMessage>(sent);
            Assert.That(got.AssisterCount, Is.Zero);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => got.AssisterId(0));
        }

        [Test]
        public void KilledHoldsAtMostMaxAssisters()
        {
            var killed = new KilledMessage();
            for (var i = 0; i < KilledMessage.MaxAssisters; i++)
            {
                killed.AddAssister(i);
            }

            Assert.Throws<System.InvalidOperationException>(() => killed.AddAssister(99));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new KilledMessage().AddAssister(-1));
        }

        [Test]
        public void KilledWithTooManyOrNegativeAssistersIsUnreadable()
        {
            var writer = new MessageWriter();
            WriteKilledHead(writer);
            writer.WriteByte(KilledMessage.MaxAssisters + 1);
            for (var i = 0; i <= KilledMessage.MaxAssisters; i++)
            {
                writer.WriteInt32(i);
            }

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));

            writer.Reset();
            WriteKilledHead(writer);
            writer.WriteByte(1);
            writer.WriteInt32(-1);
            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void Pings()
        {
            var placed = ToClient<PingPlacedMessage>(new PingPlacedMessage
            {
                PingId = 9,
                Kind = PingKind.Enemy,
                Position = new Vector3(1f, 2f, 3f),
                Tracking = true,
                RemainingSeconds = 4.5f,
            });
            Assert.That(placed.PingId, Is.EqualTo(9));
            Assert.That(placed.Kind, Is.EqualTo(PingKind.Enemy));
            Assert.That(placed.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(placed.Tracking, Is.True);
            Assert.That(placed.RemainingSeconds, Is.EqualTo(4.5f));

            var moved = ToClient<PingMovedMessage>(new PingMovedMessage { PingId = 9, Position = new Vector3(2f, 2f, 2f) });
            Assert.That(moved.PingId, Is.EqualTo(9));
            Assert.That(moved.Position, Is.EqualTo(new Vector3(2f, 2f, 2f)));

            var lost = ToClient<PingLostMessage>(new PingLostMessage { PingId = 9, Position = new Vector3(3f, 2f, 2f) });
            Assert.That(lost.PingId, Is.EqualTo(9));
            Assert.That(lost.Position, Is.EqualTo(new Vector3(3f, 2f, 2f)));

            var removed = ToClient<PingRemovedMessage>(new PingRemovedMessage { PingId = 9, Reason = PingRemoval.TargetGone });
            Assert.That(removed.PingId, Is.EqualTo(9));
            Assert.That(removed.Reason, Is.EqualTo(PingRemoval.TargetGone));
        }

        [Test]
        public void OwnerNotices()
        {
            var teleported = ToClient<TeleportedMessage>(
                new TeleportedMessage { PlayerId = 3, Tick = 500, Position = new Vector3(5f, 0f, 5f), Yaw = 1.5f });
            Assert.That(teleported.PlayerId, Is.EqualTo(3));
            Assert.That(teleported.Tick, Is.EqualTo(500));
            Assert.That(teleported.Position, Is.EqualTo(new Vector3(5f, 0f, 5f)));
            Assert.That(teleported.Yaw, Is.EqualTo(1.5f));

            var corrected = ToClient<ShotIndexCorrectedMessage>(
                new ShotIndexCorrectedMessage { Generation = 2, ClientIndex = 5, ServerIndex = 4 });
            Assert.That(corrected.Generation, Is.EqualTo(2));
            Assert.That(corrected.ClientIndex, Is.EqualTo(5));
            Assert.That(corrected.ServerIndex, Is.EqualTo(4));

            var idle = ToClient<IdleWarningMessage>(new IdleWarningMessage { Active = true, RemainingSeconds = 10f });
            Assert.That(idle.Active, Is.True);
            Assert.That(idle.RemainingSeconds, Is.EqualTo(10f));

            var answer = ToClient<LoadoutAnswerMessage>(new LoadoutAnswerMessage { Accepted = false, Reason = "unknown weapon" });
            Assert.That(answer.Accepted, Is.False);
            Assert.That(answer.Reason, Is.EqualTo("unknown weapon"));
        }

        [Test]
        public void ProjectileSpawnedAndEnded()
        {
            var spawned = ToClient<ProjectileSpawnedMessage>(new ProjectileSpawnedMessage
            {
                ProjectileId = 12,
                WeaponId = "crossbow",
                HasShooter = true,
                ShooterId = 3,
                Position = new Vector3(1f, 2f, 3f),
                Velocity = new Vector3(0f, 0f, 60f),
            });
            Assert.That(spawned.ProjectileId, Is.EqualTo(12));
            Assert.That(spawned.WeaponId, Is.EqualTo("crossbow"));
            Assert.That(spawned.HasShooter, Is.True);
            Assert.That(spawned.ShooterId, Is.EqualTo(3));
            Assert.That(spawned.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(spawned.Velocity, Is.EqualTo(new Vector3(0f, 0f, 60f)));

            var orphan = ToClient<ProjectileSpawnedMessage>(new ProjectileSpawnedMessage { ProjectileId = 13, WeaponId = "rocket" });
            Assert.That(orphan.HasShooter, Is.False);

            var ended = ToClient<ProjectileEndedMessage>(new ProjectileEndedMessage
            {
                ProjectileId = 12,
                Reason = ProjectileEndReason.HitCharacter,
                Position = new Vector3(1f, 2f, 30f),
            });
            Assert.That(ended.ProjectileId, Is.EqualTo(12));
            Assert.That(ended.Reason, Is.EqualTo(ProjectileEndReason.HitCharacter));
            Assert.That(ended.Position, Is.EqualTo(new Vector3(1f, 2f, 30f)));
        }

        [Test]
        public void ProjectileEndedWithAnUnknownReasonIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.ProjectileEnded);
            writer.WriteByte(ProjectileEndedMessage.CurrentVersion);
            writer.WriteInt32(1);
            writer.WriteByte(200);
            writer.WriteVector3(Vector3.Zero);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void BeamState()
        {
            var got = ToClient<BeamStateMessage>(new BeamStateMessage
            {
                ShooterId = 4,
                WeaponId = "laser",
                Start = new Vector3(0f, 1.5f, 0f),
                End = new Vector3(0f, 1.5f, 25f),
                Active = true,
            });

            Assert.That(got.ShooterId, Is.EqualTo(4));
            Assert.That(got.WeaponId, Is.EqualTo("laser"));
            Assert.That(got.Start, Is.EqualTo(new Vector3(0f, 1.5f, 0f)));
            Assert.That(got.End, Is.EqualTo(new Vector3(0f, 1.5f, 25f)));
            Assert.That(got.Active, Is.True);
            Assert.That(ToClient<BeamStateMessage>(new BeamStateMessage { ShooterId = 4 }).Active, Is.False);
        }

        [Test]
        public void StatusAndAreaEffects()
        {
            var status = ToClient<StatusEffectMessage>(new StatusEffectMessage { TargetId = 6, KindId = "burn", RemainingSeconds = 3.5f });
            Assert.That(status.TargetId, Is.EqualTo(6));
            Assert.That(status.KindId, Is.EqualTo("burn"));
            Assert.That(status.RemainingSeconds, Is.EqualTo(3.5f));

            var area = ToClient<AreaEffectMessage>(new AreaEffectMessage
            {
                AreaId = 2,
                KindId = "smoke",
                Center = new Vector3(4f, 0f, 4f),
                Radius = 5f,
                RemainingSeconds = 12f,
            });
            Assert.That(area.AreaId, Is.EqualTo(2));
            Assert.That(area.KindId, Is.EqualTo("smoke"));
            Assert.That(area.Center, Is.EqualTo(new Vector3(4f, 0f, 4f)));
            Assert.That(area.Radius, Is.EqualTo(5f));
            Assert.That(area.RemainingSeconds, Is.EqualTo(12f));
        }

        [Test]
        public void EffectsWithNegativeTimeOrRadiusAreUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new StatusEffectMessage { TargetId = 1, KindId = "burn", RemainingSeconds = -1f });
            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));

            writer.Reset();
            writer.WriteMessage(new AreaEffectMessage { AreaId = 1, KindId = "smoke", Radius = -2f, RemainingSeconds = 1f });
            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void TheNewEventsAreOnlyReadByTheClient()
        {
            foreach (var id in new[]
            {
                MessageIds.ProjectileSpawned, MessageIds.ProjectileEnded, MessageIds.BeamState, MessageIds.StatusEffect, MessageIds.AreaEffect,
            })
            {
                Assert.That(_client.TryGet(id, out _), Is.True, "client reads " + id);
                Assert.That(_server.TryGet(id, out _), Is.False, "server refuses " + id);
            }
        }

        private static void WriteKilledHead(MessageWriter writer)
        {
            writer.WriteUInt16(MessageIds.Killed);
            writer.WriteByte(KilledMessage.CurrentVersion);
            writer.WriteInt32(1);
            writer.WriteInt32(2);
            writer.WriteString("rifle");
            writer.WriteBool(false);
            writer.WriteBool(false);
        }

        [Test]
        public void InteractRequest()
        {
            var got = ToServer<InteractRequestMessage>(new InteractRequestMessage { ItemId = 17 });

            Assert.That(got.ItemId, Is.EqualTo(17));
            Assert.That(ToServer<InteractRequestMessage>(new InteractRequestMessage()).ItemId, Is.Zero);
        }

        [Test]
        public void AnInteractRequestWithANegativeItemIsNotWritten()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new InteractRequestMessage { ItemId = -1 }));
        }

        [Test]
        public void AnInteractRequestWithANegativeItemIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.InteractRequest);
            writer.WriteByte(InteractRequestMessage.CurrentVersion);
            writer.WriteInt32(-1);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [TestCase(ExtractionOutcome.None)]
        [TestCase(ExtractionOutcome.Extracted)]
        [TestCase(ExtractionOutcome.Killed)]
        [TestCase(ExtractionOutcome.TimedOut)]
        public void StashState(ExtractionOutcome outcome)
        {
            var sent = new StashStateMessage { Outcome = outcome };
            sent.Stash.Add("ammo-rifle", 240);
            sent.Stash.Add("armor-light", 1);
            sent.Carried.Add("medkit", 3);
            sent.Carried.Add("gold-bar", ushort.MaxValue);
            sent.Carried.Add("rifle", 1);
            sent.BroughtIn.Add("rifle", 1);

            var got = ToClient<StashStateMessage>(sent);

            Assert.That(got.Outcome, Is.EqualTo(outcome));
            Assert.That(got.Stash.Count, Is.EqualTo(2));
            Assert.That(got.Stash.IdAt(0), Is.EqualTo("ammo-rifle"));
            Assert.That(got.Stash.CountAt(0), Is.EqualTo(240));
            Assert.That(got.Stash.IdAt(1), Is.EqualTo("armor-light"));
            Assert.That(got.Stash.CountAt(1), Is.EqualTo(1));
            Assert.That(got.Carried.Count, Is.EqualTo(3));
            Assert.That(got.Carried.IdAt(0), Is.EqualTo("medkit"));
            Assert.That(got.Carried.CountAt(0), Is.EqualTo(3));
            Assert.That(got.Carried.IdAt(1), Is.EqualTo("gold-bar"));
            Assert.That(got.Carried.CountAt(1), Is.EqualTo(ushort.MaxValue));
            Assert.That(got.Carried.IdAt(2), Is.EqualTo("rifle"));
            Assert.That(got.BroughtIn.Count, Is.EqualTo(1));
            Assert.That(got.BroughtIn.IdAt(0), Is.EqualTo("rifle"));
            Assert.That(got.BroughtIn.CountAt(0), Is.EqualTo(1));
        }

        [Test]
        public void AnEmptyStashStateRoundTrips()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new StashStateMessage());

            var got = (StashStateMessage)_client.Read(writer.AsSegment());

            // Id・版 (2 + 1)、3 つの並びの数 (1 ずつ)、脱出の結果 (1)
            Assert.That(writer.Length, Is.EqualTo(3 + 3 + 1));
            Assert.That(got.Stash.Count, Is.Zero);
            Assert.That(got.Carried.Count, Is.Zero);
            Assert.That(got.BroughtIn.Count, Is.Zero);
            Assert.That(got.Outcome, Is.EqualTo(ExtractionOutcome.None));
        }

        [Test]
        public void AStashStateReadAgainReplacesTheOldLists()
        {
            var full = new StashStateMessage { Outcome = ExtractionOutcome.Killed };
            full.Carried.Add("medkit", 2);
            full.Carried.Add("rifle", 1);
            ToClient<StashStateMessage>(full);
            var fewer = new StashStateMessage();
            fewer.Carried.Add("rifle", 1);

            var got = ToClient<StashStateMessage>(fewer);

            Assert.That(got.Carried.Count, Is.EqualTo(1));
            Assert.That(got.Carried.IdAt(0), Is.EqualTo("rifle"));
            Assert.That(got.Outcome, Is.EqualTo(ExtractionOutcome.None));
        }

        [Test]
        public void ClearingAndCopyingAStashKeepTheItems()
        {
            var sent = new StashStateMessage { Outcome = ExtractionOutcome.Extracted };
            sent.Stash.Add("medkit", 4);
            var copy = new StashItemList("copy");

            copy.CopyFrom(sent.Stash);
            sent.Clear();

            Assert.That(copy.Count, Is.EqualTo(1));
            Assert.That(copy.IdAt(0), Is.EqualTo("medkit"));
            Assert.That(copy.CountAt(0), Is.EqualTo(4));
            Assert.That(sent.Stash.Count, Is.Zero);
            Assert.That(sent.Outcome, Is.EqualTo(ExtractionOutcome.None));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => sent.Stash.IdAt(0));
        }

        [TestCase(null, 1)]
        [TestCase("", 1)]
        [TestCase("medkit", 0)]
        [TestCase("medkit", -1)]
        [TestCase("medkit", ushort.MaxValue + 1)]
        public void AnImpossibleStashItemIsNotAdded(string itemId, int count)
        {
            var list = new StashStateMessage().Stash;

            Assert.Throws(Is.InstanceOf<System.ArgumentException>(), () => list.Add(itemId, count));
            Assert.That(list.Count, Is.Zero);
        }

        [Test]
        public void AStashListStopsAtItsLimit()
        {
            var list = new StashStateMessage().Carried;
            for (var i = 0; i < StashItemList.MaxCount; i++)
            {
                list.Add("item-" + i, 1);
            }

            Assert.Throws<System.InvalidOperationException>(() => list.Add("one-more", 1));
            Assert.That(list.Count, Is.EqualTo(StashItemList.MaxCount));
        }

        [Test]
        public void AStashStateWithAnUnknownOutcomeIsNotWritten()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => new MessageWriter().WriteMessage(new StashStateMessage { Outcome = (ExtractionOutcome)4 }));
        }

        [Test]
        public void AStashItemLongerThanAContentIdIsNotWritten()
        {
            var sent = new StashStateMessage();
            sent.Stash.Add(new string('a', MatchProtocol.MaxContentIdBytes + 1), 1);

            Assert.Throws<System.ArgumentException>(() => new MessageWriter().WriteMessage(sent));
        }

        [TestCase("medkit", 1, 4)]
        [TestCase("medkit", 0, 0)]
        [TestCase("", 1, 0)]
        public void AnImpossibleStashStateIsUnreadable(string itemId, int count, int outcome)
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.StashState);
            writer.WriteByte(StashStateMessage.CurrentVersion);
            writer.WriteByte(1);
            writer.WriteString(itemId);
            writer.WriteUInt16((ushort)count);
            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteByte((byte)outcome);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void AStashStateThatEndsEarlyIsUnreadable()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.StashState);
            writer.WriteByte(StashStateMessage.CurrentVersion);
            writer.WriteByte(2);
            writer.WriteString("medkit");
            writer.WriteUInt16(1);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        private T ToServer<T>(IMessage message)
            where T : class, IMessage
        {
            return RoundTrip<T>(message, _server);
        }

        private T ToClient<T>(IMessage message)
            where T : class, IMessage
        {
            return RoundTrip<T>(message, _client);
        }

        private static T RoundTrip<T>(IMessage message, MessageTable table)
            where T : class, IMessage
        {
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            var read = table.Read(writer.AsSegment());
            Assert.That(read, Is.InstanceOf<T>());
            Assert.That(read, Is.Not.SameAs(message));
            return (T)read;
        }
    }
}
