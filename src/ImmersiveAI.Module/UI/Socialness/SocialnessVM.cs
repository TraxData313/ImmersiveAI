using System;
using TaleWorlds.Library;

namespace ImmersiveAI.UI.Socialness
{
    /// <summary>
    /// The little always-there hand on how social the player feels right now: a 0–24 stepper on the
    /// campaign map that edits <see cref="ModConfig.DailyInitiationRate"/> live and saves it, so
    /// "leave me be" and "glad of company every hour" are one click away instead of a config-file
    /// trip. 0 silences the reaching-out entirely; 24 means someone near is moved every hour (the
    /// player's openness overriding faint bonds — the s² blend in InitiationScorer). Letters follow
    /// the same number at half weight, capped by MaxLettersInFlight so a social morning cannot bury
    /// a busy evening. A hover on the label unfolds the explanation.
    /// </summary>
    public class SocialnessVM : ViewModel
    {
        private readonly ModConfig _config;
        private bool _isHintShown;

        public SocialnessVM(ModConfig config)
        {
            _config = config;
        }

        // Steps of a quarter (Anton's ask, 2026.07.10 — "1 is too coarse, I want 0.7-ish"): the
        // value snaps to the 0.25 grid first, so a hand-edited 0.3 becomes 0.25/0.5, never 0.55.
        private const double Step = 0.25;

        public void ExecuteIncrease() =>
            SetRate(Math.Min(24.0, Snap(_config.DailyInitiationRate) + Step));

        public void ExecuteDecrease() =>
            SetRate(Math.Max(0.0, Snap(_config.DailyInitiationRate) - Step));

        // Jump straight to "leave me be" or "glad of company every hour" (Anton's ask, 2026.07.11) —
        // the [0] and [24] rails flanking the stepper, one click instead of many.
        public void ExecuteMin() => SetRate(0.0);

        public void ExecuteMax() => SetRate(24.0);

        private static double Snap(double value) => Math.Round(value / Step) * Step;

        private void SetRate(double value)
        {
            if (Math.Abs(value - _config.DailyInitiationRate) < 0.0001) return;
            _config.DailyInitiationRate = value;
            _config.Save();
            OnPropertyChanged("ValueText");
        }

        // The explanation unfolds on a CLICK of the label (the reliable road — hover events proved
        // shy on a no-focus map layer in the 2026.07.10 playtest) and, where hover does fire, on
        // hover too; the two agree because hover-end only hides where hover-begin also works.
        public void ExecuteToggleHint() => IsHintShown = !_isHintShown;

        public void ExecuteBeginHint() => IsHintShown = true;

        public void ExecuteEndHint() => IsHintShown = false;

        [DataSourceProperty]
        public string LabelText => "Socialness (?)";

        [DataSourceProperty]
        public string ValueText
        {
            get
            {
                double r = _config.DailyInitiationRate;
                return r == Math.Floor(r) ? ((int)r).ToString() : r.ToString("0.##");
            }
        }

        [DataSourceProperty]
        public string HintText =>
            "How open you feel to company right now.\n\n" +
            "It sets how often those near you — your party, and folk in the same settlement — are " +
            "moved to come up and speak: the expected visits per day across everyone together, " +
            "weighted by how close each bond is.\n\n" +
            "0 — you are left in peace.  24 — someone seeks you out every hour, however slight the " +
            "bonds.  In between, low numbers let the bonds decide and high numbers mean your own " +
            "openness carries the day.\n\n" +
            "Friends too far to walk over write letters at half this openness, and at most " +
            $"{_config.MaxLettersInFlight} letters may be on the road to you at once — letters take " +
            "days to arrive, so a social morning is not allowed to bury a busy evening.\n\n" +
            "(Saved as DailyInitiationRate in config.json. Click the label again to close this.)";

        [DataSourceProperty]
        public bool IsHintShown
        {
            get => _isHintShown;
            set { if (value != _isHintShown) { _isHintShown = value; OnPropertyChangedWithValue(value, "IsHintShown"); } }
        }
    }
}
