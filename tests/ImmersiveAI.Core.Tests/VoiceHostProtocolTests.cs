using System.Text;
using ImmersiveAI.Core.Voices;

namespace ImmersiveAI.Core.Tests;

/// <summary>
/// The boundary with another process, which is exactly where a crash would be least forgivable:
/// the voice host can die, be replaced by a newer one, be half-written to by a killed process, or
/// simply not be the program we think it is — and none of that may cost the player their campaign.
/// So the garbage cases here are not padding; they are the feature.
/// </summary>
public class VoiceHostProtocolTests
{
    // ==================================================================
    // the exact shapes on the wire — three implementations read this file
    // ==================================================================

    [Fact]
    public void Ready_SerializesExactlyAsDocumented()
    {
        var line = new VoiceReadyEvent
        {
            Backend = "CUDA0",
            Model = "qwen-talker-1.7b-base-Q8_0.gguf",
            Rate = 24000,
        }.Serialize();

        Assert.Equal(
            @"{""event"":""ready"",""backend"":""CUDA0"",""model"":""qwen-talker-1.7b-base-Q8_0.gguf"",""rate"":24000}",
            line);
    }

    [Fact]
    public void Failed_SerializesExactlyAsDocumented()
    {
        Assert.Equal(@"{""event"":""failed"",""error"":""model not found""}",
                     new VoiceFailedEvent("model not found").Serialize());
    }

    [Fact]
    public void Pong_SerializesExactlyAsDocumented()
    {
        Assert.Equal(@"{""event"":""pong""}", new VoicePongEvent().Serialize());
    }

    [Fact]
    public void SuccessResult_SerializesExactlyAsDocumented()
    {
        var line = VoiceSynthesisResult.Success("a1b2", @"C:\cache\000.wav", 512, 51840).Serialize();
        Assert.Equal(
            @"{""id"":""a1b2"",""ok"":true,""path"":""C:\\cache\\000.wav"",""ms"":512,""samples"":51840,""rate"":24000}",
            line);
    }

    [Fact]
    public void FailureResult_SerializesExactlyAsDocumented_AndCarriesNoAudioFields()
    {
        // A refusal that pretended to have a path would be a file someone tries to play.
        var line = VoiceSynthesisResult.Failure("a1b2", "icl prompt missing").Serialize();
        Assert.Equal(@"{""id"":""a1b2"",""ok"":false,""error"":""icl prompt missing""}", line);
    }

    [Fact]
    public void Synthesize_SerializesExactlyAsDocumented()
    {
        var line = new VoiceSynthesizeRequest
        {
            Id = "a1b2",
            Text = "Well met again.",
            OutPath = @"C:\cache\a1b2\000.wav",
            Voice = VoiceSource.FromIcl(@"C:\voices\sibylla\icl-prompt.json"),
            LanguageId = -1,
        }.Serialize();

        Assert.Equal(
            @"{""op"":""synthesize"",""id"":""a1b2"",""text"":""Well met again."",""out"":""C:\\cache\\a1b2\\000.wav"","
            + @"""voice"":{""kind"":""icl"",""path"":""C:\\voices\\sibylla\\icl-prompt.json""},""languageId"":-1,""whole"":false}",
            line);
    }

    [Fact]
    public void Synthesize_CanAskForTheReplyWhole()
    {
        // "whole" is what makes Full read gapless: the host joins the pieces and writes ONE file, so
        // playback is a single sound instead of N chained on the game's tick — a frame of silence
        // inside every second of speech is heard as the voice breaking up.
        var line = new VoiceSynthesizeRequest
        {
            Id = "a1b2",
            Text = "Well met again.",
            OutPath = @"C:\cache\a1b2\000.wav",
            Voice = VoiceSource.FromEmbedding(@"C:\voices\sibylla\embedding.json"),
            Whole = true,
        }.Serialize();

        Assert.Contains(@"""whole"":true", line);

        var back = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(line));
        Assert.True(back.Whole);
    }

    [Fact]
    public void Synthesize_WholeDefaultsToFalseWhenAbsent()
    {
        var back = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            @"{""op"":""synthesize"",""id"":""x"",""text"":""hi"",""out"":""o.wav"","
            + @"""voice"":{""kind"":""embedding"",""path"":""e.json""}}"));
        Assert.False(back.Whole);
    }

    [Fact]
    public void SmallOps_SerializeExactlyAsDocumented()
    {
        Assert.Equal(@"{""op"":""cancel"",""id"":""a1b2""}", new VoiceCancelRequest("a1b2").Serialize());
        Assert.Equal(@"{""op"":""ping""}", new VoicePingRequest().Serialize());
        Assert.Equal(@"{""op"":""quit""}", new VoiceQuitRequest().Serialize());
    }

    [Fact]
    public void UnknownOpLine_IsTheDocumentedRefusal()
    {
        Assert.Equal(@"{""id"":""a1b2"",""ok"":false,""error"":""unknown op""}",
                     VoiceHostProtocol.UnknownOpLine("a1b2"));
        Assert.Equal(@"{""ok"":false,""error"":""unknown op""}", VoiceHostProtocol.UnknownOpLine(null));
    }

    [Fact]
    public void TheDocumentedExampleLines_ParseAsWritten()
    {
        // Copied from the protocol as it was handed to all three implementations. If this test ever
        // needs editing, the wire changed and two other programs need telling.
        var ready = Assert.IsType<VoiceReadyEvent>(VoiceHostProtocol.ParseMessage(
            @"{""event"":""ready"",""backend"":""CUDA0"",""model"":""qwen-talker-1.7b-base-Q8_0.gguf"",""rate"":24000}"));
        Assert.Equal("CUDA0", ready.Backend);
        Assert.Equal("qwen-talker-1.7b-base-Q8_0.gguf", ready.Model);
        Assert.Equal(24000, ready.Rate);

        Assert.IsType<VoiceFailedEvent>(VoiceHostProtocol.ParseMessage(@"{""event"":""failed"",""error"":""no cuda""}"));
        Assert.IsType<VoicePongEvent>(VoiceHostProtocol.ParseMessage(@"{""event"":""pong""}"));

        var ok = Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(
            @"{""id"":""k"",""ok"":true,""path"":""x.wav"",""ms"":512,""samples"":51840,""rate"":24000}"));
        Assert.True(ok.Ok);
        Assert.Equal(51840, ok.Samples);

        var bad = Assert.IsType<VoiceSynthesisResult>(
            VoiceHostProtocol.ParseMessage(@"{""id"":""k"",""ok"":false,""error"":""boom""}"));
        Assert.False(bad.Ok);
        Assert.Equal("boom", bad.Error);

        var syn = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            @"{""op"":""synthesize"",""id"":""k"",""text"":""hi"",""out"":""o.wav"","
            + @"""voice"":{""kind"":""icl"",""path"":""p.json"",""speaker"":""""},""languageId"":-1}"));
        Assert.Equal("k", syn.Id);
        Assert.Equal(VoiceSourceKind.Icl, syn.Voice.Kind);
        Assert.Equal("p.json", syn.Voice.Path);

        Assert.IsType<VoiceCancelRequest>(VoiceHostProtocol.ParseRequest(@"{""op"":""cancel"",""id"":""k""}"));
        Assert.IsType<VoicePingRequest>(VoiceHostProtocol.ParseRequest(@"{""op"":""ping""}"));
        Assert.IsType<VoiceQuitRequest>(VoiceHostProtocol.ParseRequest(@"{""op"":""quit""}"));
    }

    // ==================================================================
    // round trips
    // ==================================================================

    [Fact]
    public void Ready_RoundTrips()
    {
        var sent = new VoiceReadyEvent { Backend = "CPU", Model = "talker-0.6b-Q4.gguf", Rate = 16000 };
        var got = Assert.IsType<VoiceReadyEvent>(VoiceHostProtocol.ParseMessage(sent.Serialize()));

        Assert.Equal(sent.Backend, got.Backend);
        Assert.Equal(sent.Model, got.Model);
        Assert.Equal(sent.Rate, got.Rate);
    }

    [Fact]
    public void Failed_RoundTrips()
    {
        var got = Assert.IsType<VoiceFailedEvent>(
            VoiceHostProtocol.ParseMessage(new VoiceFailedEvent("GGML_ASSERT: n_tokens > 0").Serialize()));
        Assert.Equal("GGML_ASSERT: n_tokens > 0", got.Error);
    }

    [Fact]
    public void Pong_RoundTrips()
    {
        Assert.IsType<VoicePongEvent>(VoiceHostProtocol.ParseMessage(new VoicePongEvent().Serialize()));
    }

    [Fact]
    public void SuccessResult_RoundTrips()
    {
        var sent = VoiceSynthesisResult.Success("deadbeefdeadbeef", @"C:\a\b\003.wav", 1234, 8_400_000, 24000);
        var got = Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(sent.Serialize()));

        Assert.Equal(sent.Id, got.Id);
        Assert.True(got.Ok);
        Assert.Equal(sent.Path, got.Path);
        Assert.Equal(sent.Ms, got.Ms);
        Assert.Equal(sent.Samples, got.Samples);
        Assert.Equal(sent.Rate, got.Rate);
        Assert.Equal(string.Empty, got.Error);
    }

    [Fact]
    public void FailureResult_RoundTrips()
    {
        var got = Assert.IsType<VoiceSynthesisResult>(
            VoiceHostProtocol.ParseMessage(VoiceSynthesisResult.Failure("k", "engine returned -3").Serialize()));

        Assert.Equal("k", got.Id);
        Assert.False(got.Ok);
        Assert.Equal("engine returned -3", got.Error);
        Assert.Equal(string.Empty, got.Path);
    }

    [Theory]
    [InlineData(VoiceSourceKind.Default)]
    [InlineData(VoiceSourceKind.Icl)]
    [InlineData(VoiceSourceKind.Embedding)]
    [InlineData(VoiceSourceKind.Speaker)]
    public void Synthesize_RoundTripsEveryVoiceKind(VoiceSourceKind kind)
    {
        var sent = new VoiceSynthesizeRequest
        {
            Id = "k",
            Text = "The road was long.",
            OutPath = @"C:\out\000.wav",
            Voice = new VoiceSource { Kind = kind, Path = @"C:\v\x.json", Speaker = "Chelsie" },
            LanguageId = 7,
        };

        var got = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(sent.Serialize()));

        Assert.Equal(sent.Id, got.Id);
        Assert.Equal(sent.Text, got.Text);
        Assert.Equal(sent.OutPath, got.OutPath);
        Assert.Equal(sent.LanguageId, got.LanguageId);
        Assert.Equal(kind, got.Voice.Kind);
        Assert.Equal(sent.Voice.Path, got.Voice.Path);
        Assert.Equal(sent.Voice.Speaker, got.Voice.Speaker);
    }

    [Fact]
    public void Cancel_Ping_Quit_RoundTrip()
    {
        var cancel = Assert.IsType<VoiceCancelRequest>(
            VoiceHostProtocol.ParseRequest(new VoiceCancelRequest("k").Serialize()));
        Assert.Equal("k", cancel.Id);
        Assert.Equal("cancel", cancel.Op);

        var ping = Assert.IsType<VoicePingRequest>(
            VoiceHostProtocol.ParseRequest(new VoicePingRequest { Id = "beat-9" }.Serialize()));
        Assert.Equal("beat-9", ping.Id);

        Assert.IsType<VoiceQuitRequest>(VoiceHostProtocol.ParseRequest(new VoiceQuitRequest().Serialize()));
    }

    // ==================================================================
    // event vs result, request vs message — the two doors refuse each other
    // ==================================================================

    [Fact]
    public void AnEventIsNotAResult_AndAResultIsNotAnEvent()
    {
        Assert.IsType<VoiceReadyEvent>(VoiceHostProtocol.ParseMessage(@"{""event"":""ready""}"));
        Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(@"{""id"":""k"",""ok"":true}"));
    }

    [Fact]
    public void AResultWithNoIdStillParses_SoARefusalIsNeverLost()
    {
        // The host's answer to a request too broken to carry an id: not anyone's waiting line,
        // but it belongs in the log rather than in the bin.
        var got = Assert.IsType<VoiceSynthesisResult>(
            VoiceHostProtocol.ParseMessage(@"{""ok"":false,""error"":""unknown op""}"));
        Assert.Equal(string.Empty, got.Id);
        Assert.False(got.Ok);
        Assert.Equal("unknown op", got.Error);
    }

    [Fact]
    public void AResultMissingItsVerdictIsReadAsFailure_NotAsSuccess()
    {
        // The safe reading: a half-written line must never be taken for a WAV that exists.
        var got = Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(@"{""id"":""k""}"));
        Assert.False(got.Ok);
    }

    [Fact]
    public void RequestLinesAreNotMessages_AndMessageLinesAreNotRequests()
    {
        Assert.Null(VoiceHostProtocol.ParseMessage(@"{""op"":""ping""}"));
        Assert.Null(VoiceHostProtocol.ParseMessage(@"{""op"":""synthesize"",""text"":""hi""}"));

        Assert.Null(VoiceHostProtocol.ParseRequest(@"{""event"":""pong""}"));
        Assert.Null(VoiceHostProtocol.ParseRequest(@"{""id"":""k"",""ok"":true,""path"":""x.wav""}"));
    }

    [Fact]
    public void AnEventFromANewerHostIsIgnored_NotGuessedAt()
    {
        Assert.Null(VoiceHostProtocol.ParseMessage(@"{""event"":""warming"",""percent"":40}"));
    }

    [Fact]
    public void ALineWearingBothIsReadAsTheEventItDeclaresItselfToBe()
    {
        Assert.IsType<VoiceReadyEvent>(VoiceHostProtocol.ParseMessage(@"{""event"":""ready"",""id"":""k""}"));
    }

    // ==================================================================
    // garbage: none of it may ever throw
    // ==================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData(@"{""op"":""synth")]                       // truncated mid-write, a killed process
    [InlineData(@"{""op"":""ping""} {""op"":""quit""}")]   // two objects on one line
    [InlineData("[]")]
    [InlineData("[1,2,3]")]
    [InlineData(@"[{""op"":""ping""}]")]                   // the right object, wrapped wrong
    [InlineData(@"""just a string""")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData(@"{""foo"":1}")]                           // valid object, wrong shape
    [InlineData("{}")]
    [InlineData(@"{""op"":"""",""id"":""k""}")]            // an op that names nothing
    [InlineData(@"{""op"":null}")]
    [InlineData(@"{""op"":{""nested"":true}}")]
    [InlineData("\u0000\u0001\u0002")]
    [InlineData("GGML_ASSERT: a.cpp:31: n > 0")]           // the engine shouting on stderr's twin
    public void ParseRequest_RefusesGarbageWithoutThrowing(string? line)
    {
        Assert.Null(VoiceHostProtocol.ParseRequest(line));
        Assert.False(VoiceHostProtocol.TryParseRequest(line, out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData(@"{""id"":""k"",""ok"":tru")]              // truncated
    [InlineData("[]")]
    [InlineData(@"[{""id"":""k"",""ok"":true}]")]
    [InlineData(@"""just a string""")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData(@"{""foo"":1}")]
    [InlineData("{}")]
    [InlineData(@"{""event"":""""}")]
    [InlineData(@"{""event"":null}")]
    [InlineData(@"{""id"":""""}")]                          // an id naming nobody and no verdict
    [InlineData("Microsoft Windows [Version 10.0]")]        // a console banner in our stream
    public void ParseMessage_RefusesGarbageWithoutThrowing(string? line)
    {
        Assert.Null(VoiceHostProtocol.ParseMessage(line));
        Assert.False(VoiceHostProtocol.TryParseMessage(line, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void WrongTypedFieldsAreShrugsNotThrows()
    {
        var syn = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            @"{""op"":""synthesize"",""id"":{""a"":1},""text"":[1,2],""out"":null,"
            + @"""voice"":""sibylla"",""languageId"":""not a number""}"));

        Assert.Equal(string.Empty, syn.Id);
        Assert.Equal(string.Empty, syn.Text);
        Assert.Equal(string.Empty, syn.OutPath);
        Assert.Equal(VoiceSourceKind.Default, syn.Voice.Kind);
        Assert.Equal(-1, syn.LanguageId);           // the fallback, not a zero someone acts on
        Assert.False(syn.Validate(out _));
    }

    [Fact]
    public void ANumberTooLargeForALongCostsOnlyItsOwnField()
    {
        var got = Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(
            @"{""id"":""k"",""ok"":true,""path"":""x.wav"",""samples"":999999999999999999999999999999}"));

        Assert.Equal("k", got.Id);                  // the line still stands
        Assert.True(got.Ok);
        Assert.Equal("x.wav", got.Path);
        Assert.Equal(0, got.Samples);               // only the nonsense field falls back
    }

    [Fact]
    public void NumbersWrittenAsStringsAreStillNumbers()
    {
        var got = Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(
            @"{""id"":""k"",""ok"":""true"",""path"":""x.wav"",""ms"":""512"",""samples"":""51840"",""rate"":""24000""}"));

        Assert.True(got.Ok);
        Assert.Equal(512, got.Ms);
        Assert.Equal(51840, got.Samples);
        Assert.Equal(24000, got.Rate);
    }

    [Fact]
    public void UnknownFieldsAreIgnored_NotFatal()
    {
        var syn = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            @"{""op"":""synthesize"",""id"":""k"",""text"":""hi"",""out"":""o.wav"","
            + @"""voice"":{""kind"":""embedding"",""path"":""e.json"",""dimension"":2048},"
            + @"""languageId"":3,""speed"":1.2,""future"":{""deeply"":[1,{""nested"":true}]}}"));

        Assert.Equal("k", syn.Id);
        Assert.Equal(VoiceSourceKind.Embedding, syn.Voice.Kind);
        Assert.Equal(3, syn.LanguageId);
        Assert.True(syn.Validate(out _));

        var ready = Assert.IsType<VoiceReadyEvent>(VoiceHostProtocol.ParseMessage(
            @"{""event"":""ready"",""backend"":""CUDA0"",""model"":""m.gguf"",""rate"":24000,""vram"":8192}"));
        Assert.Equal("CUDA0", ready.Backend);
    }

    // ==================================================================
    // an unknown op is refused in words, never dropped
    // ==================================================================

    [Fact]
    public void AnUnknownOpParses_SoItCanBeAnswered()
    {
        var got = Assert.IsType<VoiceUnknownRequest>(
            VoiceHostProtocol.ParseRequest(@"{""op"":""transcribe"",""id"":""k"",""wav"":""in.wav""}"));

        Assert.Equal("transcribe", got.Op);
        Assert.Equal("k", got.Id);

        // …and the answer carries the id back, so the waiting side stops waiting.
        var reply = Assert.IsType<VoiceSynthesisResult>(
            VoiceHostProtocol.ParseMessage(VoiceHostProtocol.UnknownOpLine(got.Id)));
        Assert.Equal("k", reply.Id);
        Assert.False(reply.Ok);
        Assert.Equal(VoiceHostProtocol.UnknownOpError, reply.Error);
    }

    [Fact]
    public void OpsAreReadCaseInsensitively()
    {
        Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(@"{""op"":""SYNTHESIZE"",""id"":""k""}"));
        Assert.IsType<VoicePingRequest>(VoiceHostProtocol.ParseRequest(@"{""op"":""Ping""}"));
        Assert.IsType<VoiceReadyEvent>(VoiceHostProtocol.ParseMessage(@"{""event"":""Ready""}"));
    }

    // ==================================================================
    // the voice source
    // ==================================================================

    [Fact]
    public void AnAbsentOrUnknownVoiceIsTheDefaultOne_NotAnError()
    {
        var noVoice = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            @"{""op"":""synthesize"",""id"":""k"",""text"":""hi"",""out"":""o.wav""}"));
        Assert.Equal(VoiceSourceKind.Default, noVoice.Voice.Kind);
        Assert.True(noVoice.Validate(out _));      // the engine's own voice is a real answer

        var strangeKind = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            @"{""op"":""synthesize"",""id"":""k"",""text"":""hi"",""out"":""o.wav"",""voice"":{""kind"":""hologram""}}"));
        Assert.Equal(VoiceSourceKind.Default, strangeKind.Voice.Kind);

        var nullVoice = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            @"{""op"":""synthesize"",""id"":""k"",""text"":""hi"",""out"":""o.wav"",""voice"":null}"));
        Assert.Equal(VoiceSourceKind.Default, nullVoice.Voice.Kind);
    }

    [Theory]
    [InlineData(VoiceSourceKind.Default, "default")]
    [InlineData(VoiceSourceKind.Icl, "icl")]
    [InlineData(VoiceSourceKind.Embedding, "embedding")]
    [InlineData(VoiceSourceKind.Speaker, "speaker")]
    public void KindNamesRoundTripThroughTheirWireSpelling(VoiceSourceKind kind, string wire)
    {
        Assert.Equal(wire, VoiceSource.NameOf(kind));
        Assert.Equal(kind, VoiceSource.ParseKind(wire));
        Assert.Equal(kind, VoiceSource.ParseKind(wire.ToUpperInvariant()));
        Assert.Equal(kind, VoiceSource.ParseKind("  " + wire + " "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hologram")]
    public void AnUnreadableKindIsDefault(string? name)
    {
        Assert.Equal(VoiceSourceKind.Default, VoiceSource.ParseKind(name));
    }

    [Fact]
    public void EmptyVoiceFieldsAreLeftOffTheWire()
    {
        var line = new VoiceSynthesizeRequest
        {
            Id = "k",
            Text = "hi",
            OutPath = "o.wav",
            Voice = VoiceSource.FromSpeaker("Chelsie"),
        }.Serialize();

        Assert.Contains(@"""voice"":{""kind"":""speaker"",""speaker"":""Chelsie""}", line);
        Assert.DoesNotContain(@"""path""", line);
    }

    [Fact]
    public void IsNamed_KnowsWhenAVoiceNamesNothing()
    {
        Assert.True(VoiceSource.Unvoiced().IsNamed);
        Assert.True(VoiceSource.FromIcl("p.json").IsNamed);
        Assert.True(VoiceSource.FromEmbedding("e.json").IsNamed);
        Assert.True(VoiceSource.FromSpeaker("Chelsie").IsNamed);

        Assert.False(VoiceSource.FromIcl("   ").IsNamed);
        Assert.False(VoiceSource.FromEmbedding("").IsNamed);
        Assert.False(VoiceSource.FromSpeaker("").IsNamed);
    }

    // ==================================================================
    // Validate: understood but wrong is answered, not dropped
    // ==================================================================

    [Fact]
    public void Validate_PassesAWellFormedRequest()
    {
        var req = new VoiceSynthesizeRequest
        {
            Id = "k",
            Text = "Well met.",
            OutPath = @"C:\out\000.wav",
            Voice = VoiceSource.FromIcl(@"C:\v\icl-prompt.json"),
        };
        Assert.True(req.Validate(out var error));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Validate_NamesWhatIsMissing()
    {
        Assert.False(new VoiceSynthesizeRequest { Text = "hi", OutPath = "o.wav" }.Validate(out var noId));
        Assert.Contains("id", noId);

        Assert.False(new VoiceSynthesizeRequest { Id = "k", Text = "   ", OutPath = "o.wav" }.Validate(out var noText));
        Assert.Contains("text", noText);

        Assert.False(new VoiceSynthesizeRequest { Id = "k", Text = "hi" }.Validate(out var noOut));
        Assert.Contains("out", noOut);

        Assert.False(new VoiceSynthesizeRequest
        {
            Id = "k",
            Text = "hi",
            OutPath = "o.wav",
            Voice = VoiceSource.FromIcl(""),
        }.Validate(out var noPath));
        Assert.Contains("icl", noPath);

        Assert.False(new VoiceSynthesizeRequest
        {
            Id = "k",
            Text = "hi",
            OutPath = "o.wav",
            Voice = VoiceSource.FromSpeaker(" "),
        }.Validate(out var noSpeaker));
        Assert.Contains("speaker", noSpeaker);
    }

    [Fact]
    public void ABrokenRequestStillParses_SoTheHostCanAnswerItsId()
    {
        // The whole reason parsing is lenient: this line's id is a line the game is waiting on.
        var req = Assert.IsType<VoiceSynthesizeRequest>(
            VoiceHostProtocol.ParseRequest(@"{""op"":""synthesize"",""id"":""k"",""text"":""hi""}"));

        Assert.Equal("k", req.Id);
        Assert.False(req.Validate(out var why));

        var reply = Assert.IsType<VoiceSynthesisResult>(
            VoiceHostProtocol.ParseMessage(VoiceSynthesisResult.Failure(req.Id, why).Serialize()));
        Assert.Equal("k", reply.Id);
        Assert.False(reply.Ok);
    }

    // ==================================================================
    // text: the payload that must survive anything
    // ==================================================================

    [Fact]
    public void CyrillicSurvivesTheRoundTrip_AndTravelsAsPlainAscii()
    {
        const string bulgarian = "Не бях помисляла, че ще те видя отново, мъжо мой.";
        var line = new VoiceSynthesizeRequest
        {
            Id = "k",
            Text = bulgarian,
            OutPath = "o.wav",
            Voice = VoiceSource.FromIcl("p.json"),
        }.Serialize();

        // A Windows console pipe is a fine place to lose a language; escaping never asks the question.
        Assert.All(line, c => Assert.True(c < 128, "line must be pure ASCII on the wire"));

        var got = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(line));
        Assert.Equal(bulgarian, got.Text);
    }

    [Fact]
    public void RawUtf8FromAnotherImplementationIsReadJustAsWell()
    {
        // We escape; a host written in another language may not. Both must be readable.
        const string raw = @"{""op"":""synthesize"",""id"":""k"",""text"":""Здравей, друже."",""out"":""o.wav""}";
        var got = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(raw));
        Assert.Equal("Здравей, друже.", got.Text);
    }

    [Fact]
    public void EmojiAndOtherSurrogatePairsSurvive()
    {
        const string text = "Ha! 🙂 Well met 🐎 — and 𝔊othic too.";
        var got = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            new VoiceSynthesizeRequest { Id = "k", Text = text, OutPath = "o.wav" }.Serialize()));
        Assert.Equal(text, got.Text);
    }

    [Fact]
    public void QuotesBackslashesAndRealNewlinesSurvive_WithoutBreakingTheLine()
    {
        const string text = "She said \"go\" and left.\nThe path was C:\\voices\\sibylla\\icl.json\tas ever.\r\nAll of it.";

        var line = new VoiceSynthesizeRequest
        {
            Id = "k",
            Text = text,
            OutPath = @"C:\o\000.wav",
            Voice = VoiceSource.FromIcl(@"C:\v\icl-prompt.json"),
        }.Serialize();

        // The newline is the frame. A line holding one would swallow the next message whole.
        Assert.DoesNotContain("\n", line);
        Assert.DoesNotContain("\r", line);

        var got = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(line));
        Assert.Equal(text, got.Text);
        Assert.Equal(@"C:\o\000.wav", got.OutPath);
        Assert.Equal(@"C:\v\icl-prompt.json", got.Voice.Path);
    }

    [Fact]
    public void ALiteralBackslashNIsNotANewline_AndComesBackAsItself()
    {
        const string text = @"the escape \n written out, and \\ beside it";
        var got = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            new VoiceSynthesizeRequest { Id = "k", Text = text, OutPath = "o.wav" }.Serialize()));
        Assert.Equal(text, got.Text);
    }

    [Fact]
    public void AVeryLongTextRoundTripsAsOneLine()
    {
        var sb = new StringBuilder();
        while (sb.Length < 200_000)
            sb.Append("Дълъг път, и още по-дълга нощ. A long road, and a longer night. 🙂 ");
        var text = sb.ToString();

        var line = new VoiceSynthesizeRequest { Id = "k", Text = text, OutPath = "o.wav" }.Serialize();

        Assert.DoesNotContain("\n", line);
        Assert.Equal(text, Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(line)).Text);
    }

    [Fact]
    public void ControlCharactersInErrorTextCannotBreakTheFrame()
    {
        // The host's error text is whatever the engine muttered; it may hold anything at all.
        var got = Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(
            VoiceSynthesisResult.Failure("k", "GGML_ASSERT: a.cpp:31\n  n_tokens > 0\r\n\u0007").Serialize()));

        Assert.Equal("GGML_ASSERT: a.cpp:31\n  n_tokens > 0\r\n\u0007", got.Error);
    }

    [Fact]
    public void EverySerializedMessageIsExactlyOneLine()
    {
        var lines = new[]
        {
            new VoiceReadyEvent { Backend = "CUDA0", Model = "m\n.gguf" }.Serialize(),
            new VoiceFailedEvent("two\nlines").Serialize(),
            new VoicePongEvent().Serialize(),
            VoiceSynthesisResult.Success("k", "a\nb.wav", 1, 2).Serialize(),
            VoiceSynthesisResult.Failure("k", "a\r\nb").Serialize(),
            new VoiceSynthesizeRequest { Id = "k\n", Text = "a\nb", OutPath = "c\nd" }.Serialize(),
            new VoiceCancelRequest("k\n").Serialize(),
            new VoicePingRequest().Serialize(),
            new VoiceQuitRequest().Serialize(),
        };

        foreach (var line in lines)
        {
            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
            Assert.NotEqual(string.Empty, line);
        }
    }

    // ==================================================================
    // a whole session, the way the two processes actually talk
    // ==================================================================

    [Fact]
    public void AWholeSessionSurvivesBeingWrittenAndReadBackLineByLine()
    {
        var fromHost = string.Join("\n", new[]
        {
            new VoiceReadyEvent { Backend = "CUDA0", Model = "talker-1.7b.gguf" }.Serialize(),
            new VoicePongEvent().Serialize(),
            VoiceSynthesisResult.Success("aaa", @"C:\c\aaa\000.wav", 512, 51840).Serialize(),
            VoiceSynthesisResult.Failure("bbb", "icl prompt missing").Serialize(),
        });

        var read = fromHost.Split('\n');
        Assert.Equal(4, read.Length);
        Assert.IsType<VoiceReadyEvent>(VoiceHostProtocol.ParseMessage(read[0]));
        Assert.IsType<VoicePongEvent>(VoiceHostProtocol.ParseMessage(read[1]));
        Assert.True(Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(read[2])).Ok);
        Assert.False(Assert.IsType<VoiceSynthesisResult>(VoiceHostProtocol.ParseMessage(read[3])).Ok);

        var fromGame = string.Join("\n", new[]
        {
            new VoiceSynthesizeRequest
            {
                Id = VoiceCacheKey.For("sibylla", "Не бях помисляла, че ще те видя отново."),
                Text = "Не бях помисляла, че ще те видя отново.",
                OutPath = @"C:\c\aaa\000.wav",
                Voice = VoiceSource.FromIcl(@"C:\v\sibylla\icl-prompt.json"),
            }.Serialize(),
            new VoiceCancelRequest("bbb").Serialize(),
            new VoicePingRequest().Serialize(),
            new VoiceQuitRequest().Serialize(),
        });

        var back = fromGame.Split('\n');
        Assert.Equal(4, back.Length);
        var syn = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(back[0]));
        Assert.True(syn.Validate(out _));
        Assert.Equal("Не бях помисляла, че ще те видя отново.", syn.Text);
        Assert.IsType<VoiceCancelRequest>(VoiceHostProtocol.ParseRequest(back[1]));
        Assert.IsType<VoicePingRequest>(VoiceHostProtocol.ParseRequest(back[2]));
        Assert.IsType<VoiceQuitRequest>(VoiceHostProtocol.ParseRequest(back[3]));
    }

    [Fact]
    public void TheIdIsWhateverTheCacheCallsIt()
    {
        // Nothing here invents identity; the key comes from Core's own cache naming.
        var key = VoiceCacheKey.For("sibylla", "Well met again.", "talker-1.7b", -1);
        var got = Assert.IsType<VoiceSynthesizeRequest>(VoiceHostProtocol.ParseRequest(
            new VoiceSynthesizeRequest
            {
                Id = key,
                Text = "Well met again.",
                OutPath = System.IO.Path.Combine("c", key, VoiceCacheKey.ChunkFileName(0)),
            }.Serialize()));

        Assert.Equal(key, got.Id);
        Assert.EndsWith("000.wav", got.OutPath);
    }
}
