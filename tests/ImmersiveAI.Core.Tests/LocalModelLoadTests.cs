using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImmersiveAI.Core.Tests;

public class LocalModelLoadTests
{
    private static JObject List(string instances) => JObject.Parse(
        "{\"models\":[{\"type\":\"llm\",\"key\":\"google/gemma-4-12b\",\"loaded_instances\":" + instances + "}," +
        "{\"type\":\"llm\",\"key\":\"google/gemma-4-e4b\",\"loaded_instances\":[]}]}");

    [Fact]
    public void NotLoaded_IsLoaded()
    {
        var p = LocalModelLoad.Decide(List("[]"), "google/gemma-4-12b", 16384);
        Assert.Equal(LocalModelLoad.Step.Load, p.Step);
    }

    [Fact]
    public void LoadedAtTheModsLength_IsLeftAlone()
    {
        var p = LocalModelLoad.Decide(List("[{\"id\":\"google/gemma-4-12b\",\"config\":{\"context_length\":16384}}]"), "google/gemma-4-12b", 16384);
        Assert.Equal(LocalModelLoad.Step.AlreadyRight, p.Step);
    }

    [Fact]
    public void LoadedAtAnotherLength_IsReloaded()
    {
        var p = LocalModelLoad.Decide(List("[{\"id\":\"google/gemma-4-12b\",\"config\":{\"context_length\":8192}}]"), "google/gemma-4-12b", 16384);
        Assert.Equal(LocalModelLoad.Step.Reload, p.Step);
        Assert.Equal(new[] { "google/gemma-4-12b" }, p.Unload);
        Assert.Equal(8192, p.LoadedContext);
    }

    [Theory]
    [InlineData("qwen/qwen3-30b-a3b")]   // not on disk: the server decides
    [InlineData("")]
    public void UnknownOrBlankModel_DoesNothing(string key)
    {
        Assert.Equal(LocalModelLoad.Step.Nothing, LocalModelLoad.Decide(List("[]"), key, 16384).Step);
    }

    [Fact]
    public void NotLmStudiosShape_DoesNothing()
    {
        Assert.Equal(LocalModelLoad.Step.Nothing, LocalModelLoad.Decide(JObject.Parse("{\"data\":[]}"), "google/gemma-4-12b", 16384).Step);
    }

    [Theory]
    [InlineData("http://localhost:1234/v1/chat/completions", "http://localhost:1234")]
    [InlineData("http://127.0.0.1:11434/v1/chat/completions", "http://127.0.0.1:11434")]
    [InlineData("not a url", null)]
    public void ApiRoot_IsTheServersOrigin(string endpoint, string? expected)
    {
        Assert.Equal(expected, LocalModelLoad.ApiRoot(endpoint));
    }
}
