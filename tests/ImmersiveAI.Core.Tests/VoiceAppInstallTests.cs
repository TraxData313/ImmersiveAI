using System.Collections.Generic;
using ImmersiveAI.Core.Voices;
using Xunit;

namespace ImmersiveAI.Core.Tests
{
    public class VoiceAppInstallTests
    {
        private const string Local = @"C:\Users\toni\AppData\Local";
        private const string Program = @"C:\Users\toni\AppData\Local\Programs\claude-voice";
        private const string Sha = "8649692de846c56a7189d6dae5c322ab20deb1b5908b6f39426b62a36f39415d";
        private const string Pin = "{\"version\": \"3.12.10\", \"url\": \"https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.zip\", \"sha256\": \"" + Sha + "\", \"signer\": \"Python Software Foundation\"}";

        private static System.Func<string, bool> Files(params string[] present)
        {
            var set = new HashSet<string>(present, System.StringComparer.OrdinalIgnoreCase);
            return p => set.Contains(p);
        }

        [Fact]
        public void APin_FromPythonOrg_WithAFullHash_IsTaken()
        {
            var pin = VoiceAppInstall.ParsePin(Pin);
            Assert.NotNull(pin);
            Assert.Equal("3.12.10", pin!.Version);
            Assert.Equal(Sha, pin.Sha256);
        }

        [Theory]
        [InlineData("{\"url\": \"https://example.com/python.zip\", \"sha256\": \"" + Sha + "\"}")]
        [InlineData("{\"url\": \"https://www.python.org/ftp/python/x.zip\", \"sha256\": \"abc\"}")]
        [InlineData("not json")]
        [InlineData(null)]
        public void APin_FromAnywhereElse_OrWithoutAHash_IsRefused(string? json)
        {
            Assert.Null(VoiceAppInstall.ParsePin(json));
        }

        [Fact]
        public void TheRuntime_IsReused_OnlyWhenItsMarkerNamesThePinnedHash()
        {
            var pin = VoiceAppInstall.ParsePin(Pin)!;
            Assert.True(VoiceAppInstall.RuntimeIsCurrent("{\"sha256\": \"" + Sha.ToUpperInvariant() + "\"}", pin));
            Assert.False(VoiceAppInstall.RuntimeIsCurrent("{\"sha256\": \"" + new string('0', 64) + "\"}", pin));
            Assert.False(VoiceAppInstall.RuntimeIsCurrent(null, pin));
        }

        [Fact]
        public void AnInstalledCopy_IsReinstalledWhereItIs()
        {
            var found = new VoiceAppRecord { Root = @"D:\Apps\claude-voice\app", Kind = "installed" };
            Assert.Equal(@"D:\Apps\claude-voice",
                VoiceAppInstall.ProgramRoot(found, Local, Files(@"D:\Apps\claude-voice\install.json")));
        }

        [Fact]
        public void AnythingElse_GetsTheUsualProgramFolder()
        {
            Assert.Equal(Program, VoiceAppInstall.ProgramRoot(null, Local, Files()));
            // An install from before 2.0: the code straight in the folder, no app\ and no install.json.
            var old = new VoiceAppRecord { Root = Program, Kind = "installed" };
            Assert.Equal(Program, VoiceAppInstall.ProgramRoot(old, Local, Files()));
            var noRecord = new VoiceAppRecord { Root = @"D:\x\app" };
            Assert.Equal(Program, VoiceAppInstall.ProgramRoot(noRecord, Local, Files()));
        }

        [Fact]
        public void TheInstallerIsTold_NoClaude_AndQuotedPaths()
        {
            var args = VoiceAppInstall.InstallerArgs(@"C:\t\src\installer.py", Program, @"C:\t\src", "Pocket", @"D:\voice files\", true);
            Assert.Equal("\"C:\\t\\src\\installer.py\" install --root \"" + Program + "\" --source \"C:\\t\\src\" --no-claude"
                         + " --engine pocket --big-data \"D:\\voice files\" --quiet", args);
        }

        [Theory]
        [InlineData("Doc/html/index.html", false)]
        [InlineData("../evil.dll", false)]
        [InlineData("python.exe", true)]
        [InlineData("Lib/tkinter/__init__.py", true)]
        public void TheRuntimeZip_LeavesOutTheDocs(string entry, bool wanted)
        {
            Assert.Equal(wanted, VoiceAppInstall.WantedFromRuntimeZip(entry));
        }

        [Fact]
        public void AQuietUninstall_IsRun_OnlyWhenItIsClaudeVoicesOwnPython()
        {
            var pythonw = Program + @"\runtime\pythonw.exe";
            var mine = "\"" + pythonw + "\" \"" + Program + "\\app\\installer.py\" uninstall --quiet";
            var got = VoiceAppInstall.UninstallCommand(mine, Program, Files(pythonw));
            Assert.NotNull(got);
            Assert.Equal(pythonw, got!.Value.Exe);
            Assert.EndsWith("uninstall --quiet", got.Value.Args);

            var old = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Program + "\\uninstall.ps1\" -Yes";
            Assert.Null(VoiceAppInstall.UninstallCommand(old, Program, Files("powershell.exe")));
            Assert.Null(VoiceAppInstall.UninstallCommand(mine, Program, Files()));
            // The entry names another program folder than the install the game found: not this one's to run.
            Assert.Null(VoiceAppInstall.UninstallCommand(mine, @"D:\Tools\claude-voice", Files(pythonw)));
        }

        [Fact]
        public void ABlankSigner_StillMeansThePythonSoftwareFoundation()
        {
            var pin = VoiceAppInstall.ParsePin(Pin.Replace("Python Software Foundation", " "));
            Assert.Equal("Python Software Foundation", pin!.Signer);
        }

        [Fact]
        public void ADrivesRoot_KeepsItsBackslash()
        {
            Assert.Equal("\"D:\\\\\"", VoiceAppInstall.Quote(@"D:\"));
            Assert.Equal("\"D:\\x\"", VoiceAppInstall.Quote(@"D:\x\"));
        }
    }
}
