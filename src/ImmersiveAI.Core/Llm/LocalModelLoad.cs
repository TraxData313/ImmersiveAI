using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ImmersiveAI.Core.Llm
{
    /// <summary>
    /// LM Studio loads a model at ITS default context (8192 for google/gemma-4-12b) unless told
    /// otherwise, while the mod sizes every NPC's memory to its own "Local context length" (2026.11.15,
    /// the owner: "it always starts with 8k and I can't remember the 16384"). At game start the mod
    /// asks LM Studio to load the chosen model at the mod's length, so the two can never drift apart.
    /// This is the pure decision: from LM Studio's /api/v1/models list, what must be done.
    /// </summary>
    public static class LocalModelLoad
    {
        public enum Step
        {
            /// <summary>Not LM Studio, or the model is not on its disk: leave it to the server.</summary>
            Nothing,
            /// <summary>Already loaded at the length the mod uses.</summary>
            AlreadyRight,
            /// <summary>On disk, not loaded: load it at the mod's length.</summary>
            Load,
            /// <summary>Loaded at another length: unload those copies, then load at the mod's length.</summary>
            Reload,
        }

        public sealed class Plan
        {
            public Step Step;
            /// <summary>The loaded copies to unload first (Reload only).</summary>
            public List<string> Unload = new List<string>();
            /// <summary>The length the loaded copy holds now (0 when unknown or not loaded).</summary>
            public int LoadedContext;
        }

        /// <summary>LM Studio's own API root ("http://localhost:1234") from the chat endpoint the mod
        /// posts to ("http://localhost:1234/v1/chat/completions"). Null when it does not parse.</summary>
        public static string? ApiRoot(string? chatEndpoint)
        {
            if (string.IsNullOrWhiteSpace(chatEndpoint)) return null;
            if (!Uri.TryCreate(chatEndpoint!.Trim(), UriKind.Absolute, out var uri)) return null;
            return uri.GetLeftPart(UriPartial.Authority);
        }

        public static Plan Decide(JObject? models, string? modelKey, int wantedContext)
        {
            var plan = new Plan { Step = Step.Nothing };
            if (models == null || string.IsNullOrWhiteSpace(modelKey) || wantedContext <= 0) return plan;
            var list = models["models"] as JArray;
            if (list == null) return plan;

            var rec = list.OfType<JObject>().FirstOrDefault(m =>
                string.Equals((string?)m["key"], modelKey!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (rec == null) return plan;

            var loaded = (rec["loaded_instances"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
            if (loaded.Count == 0) { plan.Step = Step.Load; return plan; }

            foreach (var inst in loaded)
            {
                var ctx = (int?)inst.SelectToken("config.context_length") ?? 0;
                if (ctx == wantedContext) { plan.Step = Step.AlreadyRight; plan.LoadedContext = ctx; return plan; }
            }
            plan.Step = Step.Reload;
            plan.LoadedContext = (int?)loaded[0].SelectToken("config.context_length") ?? 0;
            plan.Unload = loaded.Select(i => (string?)i["id"]).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToList();
            if (plan.Unload.Count == 0) plan.Step = Step.Load;
            return plan;
        }
    }
}
