using System.Collections.Generic;
using System.Linq;
using ImmersiveAI.Core.Voices;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class VoiceAppLocatorTests
    {
        private const string Clone = @"C:\Users\toni\Documents\GitHub\claude-voice";
        private const string Programs = @"C:\Users\toni\AppData\Local\Programs\claude-voice";
        private const string Pythonw = @"C:\Python312\pythonw.exe";

        private static System.Func<string, bool> Files(params string[] present)
        {
            var set = new HashSet<string>(present, System.StringComparer.OrdinalIgnoreCase);
            return p => set.Contains(p);
        }

        [Fact]
        public void TheRegistryWins_OverAWhereNoteNamingAnotherCopy()
        {
            var reg = new VoiceAppRecord { Source = "registry", Root = Programs };
            var where = new VoiceAppRecord { Source = "where.json", Root = Clone };
            var found = VoiceAppLocator.Choose(new[] { reg, where },
                Files(Programs + @"\voice_cli.py", Clone + @"\voice_cli.py"));
            Assert.Same(reg, found);
        }

        [Fact]
        public void AStaleRegistration_FallsThroughToTheNextSource()
        {
            // The folder was moved or deleted: the key still names it, nothing is there.
            var reg = new VoiceAppRecord { Source = "registry", Root = @"D:\gone\claude-voice" };
            var where = new VoiceAppRecord { Source = "where.json", Root = Clone };
            Assert.Same(where, VoiceAppLocator.Choose(new[] { reg, where, null }, Files(Clone + @"\voice_cli.py")));
        }

        [Fact]
        public void APackagedBuild_IsFoundByItsLauncher_WithNoScriptBesideIt()
        {
            var reg = new VoiceAppRecord { Root = Programs, Launcher = "\"" + Programs + "\\claude-voice.exe\" --engine" };
            Assert.Same(reg, VoiceAppLocator.Choose(new[] { reg }, Files(Programs + @"\claude-voice.exe")));
        }

        [Fact]
        public void A20Install_NamedByItsProgramFolder_IsFoundInItsAppFolder()
        {
            // Settings → Apps' InstallLocation and the folder guess name the PROGRAM folder; a 2.0
            // install keeps voice_cli.py in app\ and its own signed Python in runtime\.
            var apps = new VoiceAppRecord { Source = "apps", Root = Programs };
            var found = VoiceAppLocator.Choose(new[] { apps }, Files(
                Programs + @"\install.json", Programs + @"\app\voice_cli.py", Programs + @"\runtime\pythonw.exe"));
            Assert.NotNull(found);
            Assert.Equal("apps", found!.Source);
            Assert.Equal(Programs + @"\app", found.Root);
            Assert.Equal(Programs + @"\runtime\pythonw.exe", found.Python);

            // It starts on that Python, and the installer sees the same program folder the registry would give.
            var cmd = VoiceAppLocator.LaunchCommand(found, Files(Programs + @"\runtime\pythonw.exe"));
            Assert.Equal(Programs + @"\runtime\pythonw.exe", cmd!.Value.Exe);
            Assert.Equal($"\"{Programs}\\app\\voice_cli.py\" start", cmd.Value.Args);
            Assert.Equal(Programs, VoiceAppInstall.ProgramRoot(found, @"C:\elsewhere", Files(Programs + @"\install.json")));
        }

        [Fact]
        public void APreTwoInstall_InTheProgramFolder_IsStillFoundThere()
        {
            var guess = new VoiceAppRecord { Source = "folder", Root = Programs };
            Assert.Same(guess, VoiceAppLocator.Choose(new[] { guess }, Files(Programs + @"\voice_cli.py")));
        }

        [Fact]
        public void AnAppFolder_WithoutInstallJson_IsNotTakenForAnInstall()
        {
            // Somebody's folder that merely has an app\ subfolder with the script is not a 2.0 install.
            var guess = new VoiceAppRecord { Source = "folder", Root = Clone };
            Assert.Null(VoiceAppLocator.Choose(new[] { guess }, Files(Clone + @"\app\voice_cli.py")));
        }

        [Fact]
        public void NothingThere_IsNull()
        {
            Assert.Null(VoiceAppLocator.Choose(new VoiceAppRecord?[] { null, new VoiceAppRecord() }, Files()));
        }

        [Fact]
        public void TheRecordedLauncher_IsRunAsItIs()
        {
            var app = new VoiceAppRecord { Root = Clone, Launcher = $"\"{Pythonw}\" \"{Clone}\\voice_cli.py\" start", Python = @"C:\Other\python.exe" };
            var cmd = VoiceAppLocator.LaunchCommand(app, Files(Pythonw));
            Assert.NotNull(cmd);
            Assert.Equal(Pythonw, cmd!.Value.Exe);
            Assert.Equal($"\"{Clone}\\voice_cli.py\" start", cmd.Value.Args);
        }

        [Fact]
        public void WithoutALauncher_TheWhereNotesPython_RunsTheScript()
        {
            var app = new VoiceAppRecord { Root = Clone, Python = Pythonw };
            var cmd = VoiceAppLocator.LaunchCommand(app, Files(Pythonw));
            Assert.Equal(Pythonw, cmd!.Value.Exe);
            Assert.Equal($"\"{Clone}\\voice_cli.py\" start", cmd.Value.Args);
        }

        [Fact]
        public void NeverBarePython()
        {
            // A guessed folder knows no interpreter, and "python" from a game's stale PATH is the Store stub.
            var app = new VoiceAppRecord { Root = Clone, Python = "python" };
            Assert.Null(VoiceAppLocator.LaunchCommand(app, _ => true));
            Assert.Null(VoiceAppLocator.LaunchCommand(app, _ => true, fallbackPython: "pythonw"));
            var found = VoiceAppLocator.LaunchCommand(app, Files(Pythonw), fallbackPython: Pythonw);
            Assert.Equal(Pythonw, found!.Value.Exe);
        }

        [Fact]
        public void AMissingLauncherExe_FallsBackToThePython()
        {
            var app = new VoiceAppRecord { Root = Clone, Launcher = "\"C:\\gone\\pythonw.exe\" x start", Python = Pythonw };
            Assert.Equal(Pythonw, VoiceAppLocator.LaunchCommand(app, Files(Pythonw))!.Value.Exe);
        }

        [Fact]
        public void ThePort_IsTheAppsOwnWord_BeforeTheGameSetting()
        {
            var reg = new VoiceAppRecord { Root = Clone, Port = 8766 };
            Assert.Equal(8766, VoiceAppLocator.ChoosePort(reg, Enumerable.Empty<VoiceAppRecord>(), 8765, 8765));

            // An older registration with no port: the same copy's where.json says it.
            var bare = new VoiceAppRecord { Root = Clone };
            var where = new VoiceAppRecord { Root = Clone + "\\", Port = 8770 };
            var other = new VoiceAppRecord { Root = Programs, Port = 8799 };
            Assert.Equal(8770, VoiceAppLocator.ChoosePort(bare, new[] { other, where }, 8765, 8765));

            // Nobody says: the game's setting, and a nonsense setting is the default.
            Assert.Equal(9000, VoiceAppLocator.ChoosePort(bare, new[] { other }, 9000, 8765));
            Assert.Equal(8765, VoiceAppLocator.ChoosePort(null, new VoiceAppRecord?[] { null }, 0, 8765));
        }

        [Fact]
        public void ADevCopy_IsKnownByKindOrByGit()
        {
            Assert.True(VoiceAppLocator.IsDevCopy(new VoiceAppRecord { Root = Programs, Kind = "dev" }, _ => false));
            Assert.True(VoiceAppLocator.IsDevCopy(new VoiceAppRecord { Root = Clone }, p => p == Clone + @"\.git"));
            Assert.False(VoiceAppLocator.IsDevCopy(new VoiceAppRecord { Root = Programs, Kind = "installed" }, _ => false));
            Assert.False(VoiceAppLocator.IsDevCopy(null, _ => true));
        }

        [Theory]
        [InlineData("\"C:\\a b\\x.exe\" \"C:\\s.py\" start", "C:\\a b\\x.exe", "\"C:\\s.py\" start")]
        [InlineData("C:\\x.exe --engine", "C:\\x.exe", "--engine")]
        [InlineData("C:\\x.exe", "C:\\x.exe", "")]
        [InlineData("  ", "", "")]
        [InlineData("\"C:\\unclosed", "C:\\unclosed", "")]
        public void CommandLines_Split(string line, string exe, string args)
        {
            var (e, a) = VoiceAppLocator.SplitCommandLine(line);
            Assert.Equal(exe, e);
            Assert.Equal(args, a);
        }
    }
}
