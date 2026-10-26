using FietsApp.Updates;
using NUnit.Framework;

namespace FietsApp.Tests
{
    /// <summary>De opdrachtregel waarmee de app <c>apply-update.ps1</c> start (story 13.6).</summary>
    public class UpdateApplierTests
    {
        const string Staging = @"C:\Users\Jan de Vries\AppData\Local\FietsApp\updates\staging-0.1.6";
        const string Install = @"C:\Users\Jan de Vries\AppData\Local\Programs\FietsApp";
        const string Log = @"C:\Users\Jan de Vries\AppData\Local\FietsApp\update.log";

        static string Args(bool restart) =>
            UpdateApplier.BuildArguments(Staging + @"\apply-update.ps1", 4242, Install, Staging, "FietsApp.exe", Log, restart);

        [Test]
        public void BuildArguments_QuotesPathsWithSpaces()
        {
            var args = Args(false);
            StringAssert.Contains("-InstallDir \"" + Install + "\"", args);
            StringAssert.Contains("-StagingDir \"" + Staging + "\"", args);
            StringAssert.Contains("-LogPath \"" + Log + "\"", args);
            StringAssert.Contains("-ExeName \"FietsApp.exe\"", args);
            StringAssert.Contains("-ProcessId 4242", args);
            StringAssert.StartsWith("-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ", args);
        }

        [Test]
        public void BuildArguments_WithRestart_EndsWithRestartSwitch() => StringAssert.EndsWith(" -Restart", Args(true));

        [Test]
        public void BuildArguments_WithoutRestart_HasNoRestartSwitch() => StringAssert.DoesNotContain("-Restart", Args(false));

        [Test]
        public void BuildArguments_UsesScriptFromStaging() =>
            StringAssert.Contains("-File \"" + Staging + "\\apply-update.ps1\"", Args(false));

        [Test]
        public void ScriptPath_IsInStaging() =>
            Assert.AreEqual(Staging + @"\apply-update.ps1", UpdateApplier.ScriptPath(Staging));

        [Test]
        public void StartInfo_WorkingDirectoryOutsideInstallAndStaging()
        {
            // Een werkmap in de installatiemap blokkeert het hernoemen; de staging-map wordt zelf verplaatst.
            var paths = new UpdatePaths(Install, @"C:\Users\Jan de Vries\AppData\Local\FietsApp\updates");
            var ready = new UpdateResult { Status = UpdateStatus.ReadyToApply, StagingDir = Staging };
            var info = UpdateApplier.StartInfo(ready, paths, 4242, false);
            Assert.AreEqual(paths.UpdatesRoot, info.WorkingDirectory);
            Assert.AreEqual("powershell.exe", info.FileName);
            Assert.IsFalse(info.UseShellExecute);
            Assert.IsTrue(info.CreateNoWindow);
        }
    }
}
