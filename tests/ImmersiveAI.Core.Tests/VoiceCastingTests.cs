using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Voices;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class VoiceCastingTests
    {
        private static VoicePreset Voice(string id, VoiceGender gender, string culture = "")
            => new VoicePreset
            {
                Id = id,
                Name = id,
                Gender = gender,
                Culture = culture,
                SpeakerName = "built-in",     // enough to be speakable without touching disk
            };

        private static List<VoicePreset> Shelf() => new List<VoicePreset>
        {
            Voice("gwen", VoiceGender.Female, "battania"),
            Voice("bree", VoiceGender.Female, "battania"),
            Voice("livia", VoiceGender.Female, "empire"),
            Voice("sibylla", VoiceGender.Female),
            Voice("cadoc", VoiceGender.Male, "battania"),
            Voice("marcus", VoiceGender.Male, "empire"),
        };

        [Fact]
        public void Picks_a_voice_of_their_own_people_and_sex()
        {
            var pick = VoiceCasting.Pick(new VoiceAssignments(), Shelf(), "lord_1_1", isFemale: true, culture: "battania");
            Assert.Contains(pick, new[] { "gwen", "bree" });
        }

        [Fact]
        public void The_same_soul_always_gets_the_same_voice()
        {
            var shelf = Shelf();
            var first = VoiceCasting.Pick(new VoiceAssignments(), shelf, "lord_1_1", true, "battania");
            for (var i = 0; i < 20; i++)
                Assert.Equal(first, VoiceCasting.Pick(new VoiceAssignments(), shelf, "lord_1_1", true, "battania"));
        }

        [Fact]
        public void Shelf_order_never_changes_the_answer()
        {
            var forward = Shelf();
            var backward = Enumerable.Reverse(Shelf()).ToList();
            foreach (var id in new[] { "lord_1_1", "lord_2_7", "companion_x" })
                Assert.Equal(
                    VoiceCasting.Pick(new VoiceAssignments(), forward, id, true, "battania"),
                    VoiceCasting.Pick(new VoiceAssignments(), backward, id, true, "battania"));
        }

        [Fact]
        public void Different_souls_spread_across_the_voices_available()
        {
            var shelf = Shelf();
            var chosen = new HashSet<string>();
            for (var i = 0; i < 60; i++)
                chosen.Add(VoiceCasting.Pick(new VoiceAssignments(), shelf, "lord_" + i, true, "battania"));

            Assert.Equal(2, chosen.Count);    // both Battanian women get used
        }

        /// <summary>The whole reason this is rendezvous hashing and not hash-modulo-count: a shelf
        /// that grows in an update must not recast everyone who already had a voice.</summary>
        [Fact]
        public void Adding_a_voice_moves_only_a_few_souls()
        {
            var before = Shelf();
            var after = Shelf();
            after.Add(Voice("nesta", VoiceGender.Female, "battania"));

            var souls = Enumerable.Range(0, 300).Select(i => "lord_" + i).ToList();
            var moved = souls.Count(id =>
                VoiceCasting.Pick(new VoiceAssignments(), before, id, true, "battania")
                != VoiceCasting.Pick(new VoiceAssignments(), after, id, true, "battania"));

            // A third would move under a perfect split; modulo would move nearly all 300.
            Assert.InRange(moved, 1, 150);
        }

        [Fact]
        public void A_casting_by_hand_always_wins()
        {
            var sheet = new VoiceAssignments();
            sheet.Cast("lord_1_1", "marcus");
            Assert.Equal("marcus", VoiceCasting.Pick(sheet, Shelf(), "lord_1_1", true, "battania"));
        }

        /// <summary>The all-women / all-men slots were retired 2026.08.15 — they outranked the
        /// per-people casting and could not be undone. An old sheet still carrying one must not
        /// resurrect that behaviour.</summary>
        [Fact]
        public void The_retired_all_women_slot_no_longer_overrides_anything()
        {
            var sheet = new VoiceAssignments { DefaultFemale = "sibylla", DefaultMale = "marcus" };
            var pick = VoiceCasting.Pick(sheet, Shelf(), "lord_1_1", true, "battania");
            Assert.Contains(pick, new[] { "gwen", "bree" });
        }

        [Fact]
        public void ClearDeadDefaults_empties_the_retired_slots_and_leaves_castings_alone()
        {
            var sheet = new VoiceAssignments { DefaultFemale = "sibylla", DefaultMale = "marcus" };
            sheet.Cast("lord_1_1", "gwen");

            Assert.True(sheet.ClearDeadDefaults());
            Assert.Equal(string.Empty, sheet.DefaultFemale);
            Assert.Equal(string.Empty, sheet.DefaultMale);
            Assert.Equal("gwen", sheet.ByNpc["lord_1_1"]);
            Assert.False(sheet.ClearDeadDefaults());     // nothing left to clear
        }

        /// <summary>Hosted voices are billed by the minute; handing one out to a soul nobody cast
        /// would put the whole world on the meter without anyone choosing it.</summary>
        [Fact]
        public void A_hosted_voice_is_never_given_out_automatically()
        {
            var hosted = new VoicePreset
            {
                Id = "alloy", Name = "Alloy", Gender = VoiceGender.Female,
                Culture = "battania", Backend = VoiceBackend.Remote, RemoteVoiceId = "alloy",
            };
            var shelf = new List<VoicePreset> { hosted };

            Assert.Equal(string.Empty, VoiceCasting.Pick(new VoiceAssignments(), shelf, "lord_1_1", true, "battania"));

            // ...but the player may still put one on somebody by hand.
            var sheet = new VoiceAssignments();
            sheet.Cast("lord_1_1", "alloy");
            Assert.Equal("alloy", VoiceCasting.Pick(sheet, shelf, "lord_1_1", true, "battania"));
        }

        [Fact]
        public void Falls_back_to_the_unaffiliated_voices_then_to_anyone_of_that_sex()
        {
            // Nobody Khuzait on the shelf, so the voice belonging to no people answers.
            Assert.Equal("sibylla",
                VoiceCasting.Pick(new VoiceAssignments(), Shelf(), "lord_1_1", true, "khuzait"));

            // No unaffiliated man either, so any man of the right sex will do.
            var pick = VoiceCasting.Pick(new VoiceAssignments(), Shelf(), "lord_1_1", false, "khuzait");
            Assert.Contains(pick, new[] { "cadoc", "marcus" });
        }

        [Fact]
        public void Never_crosses_the_sexes()
        {
            for (var i = 0; i < 50; i++)
            {
                var pick = VoiceCasting.Pick(new VoiceAssignments(), Shelf(), "lord_" + i, false, "battania");
                Assert.DoesNotContain(pick, new[] { "gwen", "bree", "livia", "sibylla" });
            }
        }

        [Fact]
        public void Turning_it_off_leaves_only_what_the_player_cast()
        {
            Assert.Equal(string.Empty,
                VoiceCasting.Pick(new VoiceAssignments(), Shelf(), "lord_1_1", true, "battania", autoCast: false));
        }

        [Fact]
        public void An_empty_shelf_is_silence_not_a_crash()
        {
            Assert.Equal(string.Empty,
                VoiceCasting.Pick(new VoiceAssignments(), new List<VoicePreset>(), "lord_1_1", true, "battania"));
            Assert.Equal(string.Empty, VoiceCasting.Pick(null, null, "lord_1_1", true, "battania"));
            Assert.Equal(string.Empty, VoiceCasting.Pick(new VoiceAssignments(), Shelf(), null, true, "battania"));
        }

        [Fact]
        public void Culture_matching_ignores_case_and_padding()
        {
            var shelf = new List<VoicePreset> { Voice("gwen", VoiceGender.Female, "Battania") };
            Assert.Equal("gwen", VoiceCasting.Pick(new VoiceAssignments(), shelf, "lord_1_1", true, "  battania "));
        }

        [Fact]
        public void Label_reads_like_the_folders()
        {
            var gwen = Voice("gwen", VoiceGender.Female, "battania");
            gwen.Name = "Gwen";                              // the folder is the id, the Name is hers
            Assert.Equal("female/battania/Gwen", VoiceCasting.Label(gwen));

            var sibylla = Voice("sibylla", VoiceGender.Female);
            sibylla.Name = "Sibylla";
            Assert.Equal("female/Sibylla", VoiceCasting.Label(sibylla));

            // No sex, no people: just whatever it is called.
            Assert.Equal("Marcus", VoiceCasting.Label(new VoicePreset { Id = "marcus", Name = "Marcus" }));
        }
    }
}
