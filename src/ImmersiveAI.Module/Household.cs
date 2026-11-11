using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ImmersiveAI
{
    /// <summary>
    /// HOW A MAN HOLDS MORE THAN ONE WIFE IN A GAME THAT HAS ROOM FOR ONE (2026.11.11).
    ///
    /// THE SHAPE ("R1"): the first wife keeps the game's own spouse slot, both ways — his
    /// <c>Spouse</c> is her and hers is him, exactly as vanilla left them. Every further wife's own
    /// slot points at him as well, ONE-SIDED: her <c>Spouse</c> is him, his stays the first wife's.
    /// Nobody is moved to ExSpouses and nobody's slot is ever empty. The shape describes itself: a
    /// living hero whose spouse's spouse is someone else IS a further spouse, so nothing extra is
    /// saved and the index here is rebuilt from the world at every load.
    ///
    /// WHY NOT MARRY ANYONE'S SHAPE ("R2": the newest wife in the slot, older ones parked with an
    /// empty slot and each other in ExSpouses): a parked wife reads to vanilla as UNWED — the NPC
    /// marriage lottery may marry her to a lord, the encyclopedia lists her as a former spouse, and
    /// her own pregnancy roll has no father. R1 keeps every wife "married" in vanilla's eyes.
    ///
    /// THE CASCADE LAW: <c>Hero.Spouse</c>'s SETTER moves the old spouse into ExSpouses and nulls
    /// the other side, recursively. Never set Spouse through the setter while anyone involved holds
    /// a slot — the private field is written instead, on exactly the hero meant. Traced over every
    /// vanilla writer (1.4.8): <c>MarriageAction</c> refuses anyone with a Spouse (so it can never
    /// reach a wife), <c>Hero.AfterLoad</c>'s spouse fix runs only for saves older than 1.2.8, the
    /// XML read is for a new game's templates, and the ONLY live writer is <c>KillCharacterAction</c>
    /// (<c>victim.Spouse = null</c>, right after it raises HeroKilled). For a further wife that
    /// would cascade into HIS slot and dissolve the first marriage, so <see cref="OnHeroKilled"/>
    /// settles every death in this household by field first, and the vanilla line then finds an
    /// empty slot and does nothing.
    /// </summary>
    internal static class Household
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo? SpouseField = typeof(Hero).GetField("_spouse", Private);
        private static readonly FieldInfo? ExSpousesField = typeof(Hero).GetField("_exSpouses", Private);

        /// <summary>Whether the slots can be written without the setter. False means a game update
        /// moved the fields: no further wedding is performed, and every guard stands aside.</summary>
        internal static bool CanWriteSlots => SpouseField != null && ExSpousesField != null;

        // holder -> the further spouses whose slot points at them. A cache for the hot path
        // (SpousesOf rides every sheet); every read re-checks the live fields, so a stale entry is
        // harmless and the guards below never trust it alone.
        private static readonly Dictionary<string, List<Hero>> _further =
            new Dictionary<string, List<Hero>>(StringComparer.Ordinal);

        // ------------------------------ the field writes ------------------------------

        /// <summary>Writes one hero's slot, and nothing else in the world.</summary>
        internal static void SetSlot(Hero hero, Hero? to)
        {
            if (hero == null) return;
            if (SpouseField == null) throw new InvalidOperationException("Hero._spouse was not found");
            SpouseField.SetValue(hero, to);
        }

        private static void AddFormer(Hero hero, Hero former)
        {
            if (hero == null || former == null || hero == former || ExSpousesField == null) return;
            if (ExSpousesField.GetValue(hero) is MBList<Hero> list && !list.Contains(former)) list.Add(former);
        }

        // ------------------------------ the index ------------------------------

        /// <summary>Reads the whole world for one-sided slots. At load, and after anything that
        /// changes a household; a few hundred field reads.</summary>
        internal static void Rebuild()
        {
            try
            {
                _further.Clear();
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    var holder = hero?.Spouse;
                    if (holder == null || holder == hero || holder.Spouse == hero) continue;
                    Track(holder, hero!);
                }
                int count = _further.Values.Sum(l => l.Count);
                if (count > 0) ModLog.Info($"household: {count} further spouse(s) found in this world.");
            }
            catch (Exception ex) { ModLog.Error("reading the households", ex); }
        }

        private static void Track(Hero holder, Hero further)
        {
            if (!_further.TryGetValue(holder.StringId, out var list))
                _further[holder.StringId] = list = new List<Hero>();
            if (!list.Contains(further)) list.Add(further);
        }

        /// <summary>The living further spouses of this holder — every one whose own slot points at
        /// them while theirs holds someone else. Empty for an ordinary marriage.</summary>
        internal static List<Hero> FurtherSpousesOf(Hero holder)
        {
            var found = new List<Hero>();
            try
            {
                if (holder != null && holder == _lentBy && _lentFrom != null && _lentFrom.IsAlive)
                    found.Add(_lentFrom);
                if (holder == null || !_further.TryGetValue(holder.StringId, out var list)) return found;
                foreach (var h in list)
                    if (h != null && h.IsAlive && h.Spouse == holder && holder.Spouse != h && !found.Contains(h))
                        found.Add(h);
            }
            catch { }
            return found;
        }

        /// <summary>Whether this hero holds only a one-sided slot — a further spouse.</summary>
        internal static bool IsFurtherSpouse(Hero hero)
        {
            try { return hero?.Spouse != null && hero.Spouse != hero && hero.Spouse.Spouse != hero; }
            catch { return false; }
        }

        // ------------------------------ the law, as if he were free ------------------------------

        /// <summary>
        /// Vanilla's (or the installed model's) whole marriage law over the pair — kinship within
        /// three generations, courtships elsewhere, clans, age, station — asked AS IF the holder
        /// were free. The model refuses anyone whose slot is full, so the slot is emptied for the
        /// length of the question by field (no cascade), and put back whatever happens. The bride's
        /// own marriage is never lent away: a married bride is refused outright.
        /// </summary>
        internal static bool LawAllows(Hero holder, Hero bride)
        {
            try
            {
                var model = Campaign.Current?.Models?.MarriageModel;
                if (model == null || holder == null || bride == null) return false;
                if (bride.Spouse != null) return false;
                var held = holder.Spouse;
                if (held == null) return model.IsCoupleSuitableForMarriage(holder, bride);
                if (!CanWriteSlots) return false;
                SetSlot(holder, null);
                try { return model.IsCoupleSuitableForMarriage(holder, bride); }
                finally { SetSlot(holder, held); }
            }
            catch (Exception ex)
            {
                ModLog.Error("asking the marriage law", ex);
                return false;
            }
        }

        // ------------------------------ the wedding's own bond ------------------------------

        /// <summary>The bond itself: her slot points at him, his keeps the first. Indexed at once.</summary>
        internal static void BindFurther(Hero holder, Hero further)
        {
            SetSlot(further, holder);
            Track(holder, further);
        }

        /// <summary>
        /// For the length of <paramref name="act"/> only, the holder's slot shows the new spouse —
        /// so a wedding listener reading <c>groom.Spouse</c> (vanilla's wedding scene does exactly
        /// that) sees the bride of THIS day. Put back whatever happens. The first wife's own slot
        /// still points at him throughout, so she is never unwed even for that instant.
        /// </summary>
        internal static void WithSlotLent(Hero holder, Hero lentTo, Action act)
        {
            var held = holder.Spouse;
            SetSlot(holder, lentTo);
            _lentBy = holder;
            _lentFrom = held;
            try { act(); }
            finally
            {
                _lentBy = null;
                _lentFrom = null;
                try { SetSlot(holder, held); }
                catch (Exception ex) { ModLog.Error("giving the first spouse's slot back", ex); }
            }
        }

        // While a slot is lent, the spouse it was lent AWAY from is still a spouse — anyone asking
        // inside the wedding's own listeners (the chronicle reads who he is already wed to) must
        // still find her.
        private static Hero? _lentBy;
        private static Hero? _lentFrom;

        // ------------------------------ the deaths ------------------------------

        /// <summary>
        /// THE DEATH GUARD. Runs on HeroKilled, which vanilla raises immediately BEFORE
        /// <c>victim.Spouse = null</c>. Every case where that setter would reach a second marriage
        /// is settled here by field, the way vanilla would have widowed an ordinary pair (each in the
        /// other's ExSpouses); vanilla's own line then finds the slot already empty and does nothing.
        /// An ordinary marriage — one wife, no further spouses — is left entirely to vanilla.
        /// <list type="bullet">
        /// <item>He dies: every further wife is widowed here; the first is widowed by vanilla.</item>
        /// <item>A further wife dies: she alone is widowed; his slot and the first wife's are
        /// untouched (the setter would have dissolved them).</item>
        /// <item>The first wife dies while a further one lives: she is widowed here and the next
        /// wife steps into his slot, so the household never shows him unwed.</item>
        /// </list>
        /// </summary>
        internal static void OnHeroKilled(Hero victim)
        {
            try
            {
                if (victim == null || !CanWriteSlots) return;
                bool touched = false;

                // 1. Every further spouse whose slot points at the one who died.
                var pointing = Hero.AllAliveHeroes
                    .Where(h => h != null && h != victim && h.Spouse == victim && victim.Spouse != h)
                    .ToList();
                foreach (var further in pointing)
                {
                    SetSlot(further, null);
                    AddFormer(further, victim);
                    AddFormer(victim, further);
                    touched = true;
                }

                var partner = victim.Spouse;
                if (partner != null && partner != victim)
                {
                    if (partner.Spouse != victim)
                    {
                        // 2. The one who died was a further spouse: widow her alone.
                        SetSlot(victim, null);
                        AddFormer(victim, partner);
                        AddFormer(partner, victim);
                        touched = true;
                    }
                    else
                    {
                        // 3. The one who died held the slot, and someone else is wed to the same
                        //    holder: widow her here, and the next spouse takes the slot.
                        var next = Hero.AllAliveHeroes
                            .Where(h => h != null && h != victim && h.Spouse == partner)
                            .OrderBy(h => h.StringId, StringComparer.Ordinal)
                            .FirstOrDefault();
                        if (next != null)
                        {
                            SetSlot(victim, null);
                            AddFormer(victim, partner);
                            AddFormer(partner, victim);
                            SetSlot(partner, next);
                            touched = true;
                            ModLog.Info($"household: {victim.Name} died; {next.Name} now holds {partner.Name}'s spouse slot.");
                        }
                    }
                }

                if (touched) Rebuild();
            }
            catch (Exception ex) { ModLog.Error("settling a death in a household of more than one spouse", ex); }
        }
    }
}
