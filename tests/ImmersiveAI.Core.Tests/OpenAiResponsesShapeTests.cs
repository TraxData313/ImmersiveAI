using ImmersiveAI.Core.Llm;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Tests;

public sealed class OpenAiResponsesShapeTests
{
    private static readonly ToolDefinition[] Tools =
    {
        new ToolDefinition("recall_place", "Look up a place.",
            new[] { new ToolParameter("place", "Name of the place.") }),
        new ToolDefinition("move_heart", "Measure the change in feeling.",
            new[] { new ToolParameter("delta", "Change in feeling.", jsonType: "integer") },
            answerField: "heart"),
    };

    [Fact]
    public void RecallRoundReplaysReasoningCallAndOutputWithMatchingId()
    {
        var response = JObject.Parse(@"
            {""output"":[
                {""type"":""reasoning"",""id"":""rs_1"",""summary"":[],""encrypted_content"":""opaque""},
                {""type"":""function_call"",""call_id"":""call_1"",""name"":""recall_place"",""arguments"":""{\""place\"":\""Sargot\""}""}
            ]}");
        var call = Assert.Single(OpenAiResponsesShape.ParseResult(response, Tools).ToolCalls);
        var request = OpenAiResponsesShape.BuildRequest("gpt-6.2-sol", 400, new[]
        {
            ChatMessage.System("Speak in character."),
            ChatMessage.User("Who rules Sargot?"),
            ChatMessage.AssistantToolCalls("", new[] { call }),
            ChatMessage.ToolResult(call.Id, "The Vlandians."),
        }, Tools, allowToolUse: false);

        Assert.Equal("developer", (string?)request.SelectToken("input[0].role"));
        Assert.Equal("opaque", (string?)request.SelectToken("input[2].encrypted_content"));
        Assert.Equal("call_1", (string?)request.SelectToken("input[3].call_id"));
        Assert.Equal("recall_place", (string?)request.SelectToken("input[3].name"));
        Assert.Equal("call_1", (string?)request.SelectToken("input[4].call_id"));
        Assert.Equal("The Vlandians.", (string?)request.SelectToken("input[4].output"));
        Assert.Equal("speak", (string?)request.SelectToken("tool_choice.name"));
        Assert.Equal("recall_place", (string?)request.SelectToken("tools[0].name"));
        Assert.Equal(false, (bool?)request["store"]);
        Assert.Equal("low", (string?)request.SelectToken("reasoning.effort"));
    }

    [Fact]
    public void SpeakFinishesTheTurnAndHandsBackTheRelationshipChange()
    {
        var response = JObject.Parse(@"
            {""output"":[
                {""type"":""function_call"",""call_id"":""call_2"",""name"":""speak"",""arguments"":""{\""words\"":\""The Vlandians rule there.\"",\""heart\"":1}""}
            ]}");

        var result = OpenAiResponsesShape.ParseResult(response, Tools);

        Assert.Equal("The Vlandians rule there.", result.Text);
        Assert.Empty(result.ToolCalls);
        Assert.Equal("move_heart", Assert.Single(result.AnswerCalls).Name);
    }
}
