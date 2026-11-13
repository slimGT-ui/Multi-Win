using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WinMultiInstaller.Models;

namespace WinMultiInstaller.Services;









public class HddInstallService
{
    public const string SetupDirName = "MULTIWIN-SETUP";
    private const string BcdFileName = "multiwin-bcd.txt";
    private const string RamdiskFileName = "multiwin-ramdisk.txt";

    public record HddTarget(string Root, string SetupDir, long FreeBytes);

    public record HddState(bool Installed, string? SetupDir, string? BcdId, string? RamdiskId);

    public HddState GetState()
    {
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                string dir = Path.Combine(d.RootDirectory.FullName, SetupDirName);
                if (!Directory.Exists(dir)) continue;
                string bf = Path.Combine(dir, BcdFileName);
                if (!File.Exists(bf)) continue;

                string guid = File.ReadAllText(bf).Trim();
                if (!Regex.IsMatch(guid, @"^\{[0-9a-fA-F-]{36}\}$")) continue;

                string? ramdiskId = null;
                string ramdiskFile = Path.Combine(dir, RamdiskFileName);
                if (File.Exists(ramdiskFile))
                {
                    string candidate = File.ReadAllText(ramdiskFile).Trim();
                    if (Regex.IsMatch(candidate, @"^\{[0-9a-fA-F-]{36}\}$"))
                        ramdiskId = candidate;
                }

                // Do not treat an arbitrary folder with this name as an installation.
                return new HddState(true, dir, guid, ramdiskId);
            }
            catch { }
        }
        return new HddState(false, null, null, null);
    }

    public List<HddTarget> GetTargets(long needBytes)
    {
        var list = new List<HddTarget>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                string root = d.RootDirectory.FullName.TrimEnd('\\');
                if (root.Length != 2 || root[1] != ':') continue;
                string dir = Path.Combine(root + "\\", SetupDirName);
                list.Add(new HddTarget(root, dir, d.AvailableFreeSpace));
            }
            catch { }
        }
        return list.OrderByDescending(t => t.FreeBytes).ToList();
    }

    public void Install(string isoPath, string arch, WindowsCustom custom,
        string driveRoot, bool uefi,
        IProgress<string>? log = null, CancellationToken ct = default)
    {
        void L(string m) => log?.Report(m);
        if (string.IsNullOrWhiteSpace(driveRoot) || driveRoot.Length != 2 ||
            !char.IsAsciiLetter(driveRoot[0]) || driveRoot[1] != ':')
            throw new ArgumentException("Pick an internal drive (e.g. C:).");
        driveRoot = char.ToUpperInvariant(driveRoot[0]) + ":";
        if (!File.Exists(isoPath))
            throw new FileNotFoundException("ISO file not found.", isoPath);
        long isoLen = new FileInfo(isoPath).Length;
        long need = isoLen + 512L * 1024 * 1024;
        var drive = new DriveInfo(driveRoot + "\\");
        if (drive.AvailableFreeSpace < need)
            throw new IOException(
                $"Not enough free space on {driveRoot} (need {UsbService.Fmt(need)}, free {UsbService.Fmt(drive.AvailableFreeSpace)}).");

        string setupDir = Path.Combine(driveRoot + "\\", SetupDirName);
        if (Directory.Exists(setupDir))
            throw new IOException($"{setupDir} already exists — remove the previous HDD setup first.");

        string? createdBcdId = null;
        string? createdRamdiskId = null;
        bool setupDirCreated = false;
        bool bootSequenceSet = false;

        L("Mounting ISO...");
        string isoLetter = UsbService.MountIsoImage(isoPath);
        try
        {
            ct.ThrowIfCancellationRequested();
            string bootWim = isoLetter + @"\sources\boot.wim";
            string bootSdi = isoLetter + @"\sources\boot.sdi";
            if (!File.Exists(bootWim))
                throw new IOException("This image has no sources\\boot.wim — HDD install supports Windows Setup images only.");
            if (!File.Exists(bootSdi))
                throw new IOException("This image has no sources\\boot.sdi — cannot create the ramdisk boot entry.");

            L($"Copying Setup files to {setupDir}...");
            Directory.CreateDirectory(setupDir);
            setupDirCreated = true;
            var lines = new List<string>();
            int rc = Run("robocopy", $"{isoLetter}\\ {setupDir} /E /R:2 /W:5 /MT:8",
                l => { lock (lines) lines.Add(l); L("  " + l.Trim()); }, ct);
            L($"robocopy exit code: {rc} (0-7 = success)");
            if (rc > 7) throw new IOException($"robocopy failed, exit code {rc}.");

            BypassService.Inject(setupDir + "\\", arch, custom, L);

            L("Registering one-shot boot entry...");
            string guid = BcdCopyDefault("Multi-Win Setup (auto-removable)", L, ct);
            createdBcdId = guid;
            File.WriteAllText(Path.Combine(setupDir, BcdFileName), guid);

            // Use a dedicated ramdisk-options object so existing Windows Recovery
            // entries that reference the global {ramdiskoptions} object are untouched.
            string ramdiskId = BcdCreateRamdiskOptions(L, ct);
            createdRamdiskId = ramdiskId;
            File.WriteAllText(Path.Combine(setupDir, RamdiskFileName), ramdiskId);
            string wimPath = $"[{driveRoot}]\\{SetupDirName}\\sources\\boot.wim";
            string sdiPath = $"\\{SetupDirName}\\sources\\boot.sdi";
            Bcd($"set {ramdiskId} ramdisksdidevice partition={driveRoot}", L, ct);
            Bcd($"set {ramdiskId} ramdisksdipath {sdiPath}", L, ct);
            Bcd($"set {guid} device ramdisk={wimPath},{ramdiskId}", L, ct);
            Bcd($"set {guid} osdevice ramdisk={wimPath},{ramdiskId}", L, ct);
            Bcd($"set {guid} path \\windows\\system32\\{(uefi ? "winload.efi" : "winload.exe")}", L, ct);
            Bcd($"set {guid} winpe yes", L, ct);
            Bcd($"set {guid} detecthal yes", L, ct);
            Bcd($"bootsequence {guid}", L, ct);
            bootSequenceSet = true;

            WriteCleanupScript(setupDir, guid, ramdiskId);
            L("Done. Reboot to start Windows Setup. Default boot entry unchanged — " +
              "the PC boots into Setup ONCE, then back to your system.");
            L($"After Setup finishes, delete {setupDir} and run RemoveHddSetup (or remove-hdd-setup.cmd).");
        }
        catch
        {
            // Roll back any boot configuration and files created by this attempt.
            if (bootSequenceSet)
            {
                try { Bcd("deletevalue {bootmgr} bootsequence", L, CancellationToken.None); }
                catch (Exception ex) { L("Rollback warning: could not clear one-shot boot sequence: " + ex.Message); }
            }
            bool bootEntryRemoved = createdBcdId == null;
            bool ramdiskEntryRemoved = createdRamdiskId == null;
            if (createdBcdId != null)
            {
                try
                {
                    Bcd($"delete {createdBcdId} /f", L, CancellationToken.None);
                    bootEntryRemoved = true;
                }
                catch (Exception ex)
                {
                    L("Rollback warning: could not remove temporary boot entry: " + ex.Message);
                }
            }
            if (bootEntryRemoved && createdRamdiskId != null)
            {
                try
                {
                    Bcd($"delete {createdRamdiskId} /f", L, CancellationToken.None);
                    ramdiskEntryRemoved = true;
                }
                catch (Exception ex) { L("Rollback warning: could not remove temporary ramdisk options: " + ex.Message); }
            }
            if (setupDirCreated && bootEntryRemoved && ramdiskEntryRemoved)
            {
                try { Directory.Delete(setupDir, recursive: true); }
                catch (Exception ex) { L("Rollback warning: could not remove setup files: " + ex.Message); }
            }
            else if (setupDirCreated)
                L("Keeping HDD setup files in place because BCD rollback was incomplete.");
            try { UsbService.DismountIsoImage(isoPath); } catch { }
            throw;
        }
        try { UsbService.DismountIsoImage(isoPath); } catch (Exception ex) { L("Warning: could not dismount ISO: " + ex.Message); }
    }

    public void RemoveHddSetup(Action<string>? log = null)
    {
        void L(string m) => log?.Invoke(m);
        var st = GetState();
        if (!st.Installed || st.SetupDir == null)
        {
            L("No HDD setup found, nothing to remove.");
            return;
        }
        if (st.BcdId != null)
        {
            EnsureOwnedSetupEntry(st.BcdId, L);
            if (st.RamdiskId != null) EnsureOwnedRamdiskEntry(st.RamdiskId, L);
            Bcd($"delete {st.BcdId} /f", L, CancellationToken.None);
            if (st.RamdiskId != null)
                Bcd($"delete {st.RamdiskId} /f", L, CancellationToken.None);
        }
        for (int i = 0; i < 3; i++)
        {
            try
            {
                Directory.Delete(st.SetupDir, recursive: true);
                L($"Removed {st.SetupDir}.");
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Thread.Sleep(1000); }
        }
        try
        {

            string cmd = Path.Combine(st.SetupDir, "remove-hdd-setup.cmd");
            if (!File.Exists(cmd))
                throw new IOException("Cleanup script is missing; cannot safely schedule removal.");
            int rc = Run("schtasks", $"/create /tn \"MultiWinCleanup\" /tr \"cmd.exe /c \\\"\\\"{cmd}\\\"\\\"\" /sc onstart /ru SYSTEM /f", L, CancellationToken.None);
            if (rc != 0)
                throw new IOException($"Could not schedule cleanup (schtasks exit code {rc}).");
            L($"Folder is locked, cleanup scheduled. Run {cmd} as admin after reboot.");
        }
        catch (Exception ex) { L("Remove failed: " + ex.Message); }
    }

    private static void EnsureOwnedSetupEntry(string guid, Action<string> L)
    {
        var lines = new List<string>();
        int rc = Run("bcdedit", $"/enum {guid}", lines.Add, CancellationToken.None);
        if (rc != 0)
            throw new IOException("Could not inspect the stored boot entry; refusing to remove HDD setup files.");
        string details = string.Join("\n", lines);
        if (!details.Contains("Multi-Win Setup (auto-removable)", StringComparison.OrdinalIgnoreCase) ||
            !details.Contains(SetupDirName, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Stored BCD identifier does not match a Multi-Win setup entry; refusing to delete it.");
        L("Verified the stored BCD entry belongs to Multi-Win.");
    }

    private static void EnsureOwnedRamdiskEntry(string guid, Action<string> L)
    {
        var lines = new List<string>();
        int rc = Run("bcdedit", $"/enum {guid}", lines.Add, CancellationToken.None);
        if (rc != 0)
            throw new IOException("Could not inspect the stored ramdisk options; refusing to delete it.");
        string details = string.Join("\n", lines);
        if (!details.Contains("Multi-Win Ramdisk", StringComparison.OrdinalIgnoreCase) ||
            !details.Contains(SetupDirName, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Stored ramdisk identifier does not match a Multi-Win ramdisk object.");
        L("Verified the stored ramdisk options belong to Multi-Win.");
    }

    private static void WriteCleanupScript(string setupDir, string guid, string ramdiskGuid)
    {
        string cmd = Path.Combine(setupDir, "remove-hdd-setup.cmd");
        File.WriteAllText(cmd,
            "@echo off\r\n" +
            "REM Removes the Multi-Win one-shot Setup entry and this folder. Run as admin.\r\n" +
            $"bcdedit /enum {guid} >nul 2>&1\r\n" +
            "if errorlevel 1 goto CheckRamdisk\r\n" +
            $"bcdedit /enum {guid} | findstr /i /c:\"Multi-Win Setup (auto-removable)\" >nul || exit /b 1\r\n" +
            $"bcdedit /enum {guid} | findstr /i /c:\"MULTIWIN-SETUP\" >nul || exit /b 1\r\n" +
            $"bcdedit /delete {guid} /f || exit /b 1\r\n" +
            ":CheckRamdisk\r\n" +
            $"bcdedit /enum {ramdiskGuid} >nul 2>&1\r\n" +
            "if errorlevel 1 goto RemoveFiles\r\n" +
            $"bcdedit /enum {ramdiskGuid} | findstr /i /c:\"Multi-Win Ramdisk\" >nul || exit /b 1\r\n" +
            $"bcdedit /enum {ramdiskGuid} | findstr /i /c:\"MULTIWIN-SETUP\" >nul || exit /b 1\r\n" +
            $"bcdedit /delete {ramdiskGuid} /f || exit /b 1\r\n" +
            ":RemoveFiles\r\n" +
            "schtasks /delete /tn \"MultiWinCleanup\" /f >nul 2>&1\r\n" +
            "cd /d \"%~dp0..\"\r\n" +
            $"rmdir /s /q \"{setupDir}\"\r\n",
            new UTF8Encoding(false));
    }

    private static string BcdCopyDefault(string description, Action<string> L, CancellationToken ct)
    {
        var lines = new List<string>();
        int rc = Run("bcdedit", $"/copy {{default}} /d \"{description}\"",
            l => { lock (lines) lines.Add(l); L("  " + l.Trim()); }, ct);
        string all = string.Join("\n", lines);
        var m = Regex.Match(all, @"\{[0-9a-fA-F-]{36}\}");
        if (rc != 0 || !m.Success)
        {
            if (m.Success)
            {
                try { Bcd($"delete {m.Value} /f", L, CancellationToken.None); }
                catch (Exception ex) { L("Rollback warning: could not remove partially-created BCD entry: " + ex.Message); }
            }
            throw new IOException("bcdedit /copy failed:\n" + string.Join("\n", lines.TakeLast(6)));
        }
        L($"Boot entry {m.Value} created.");
        return m.Value;
    }

    private static string BcdCreateRamdiskOptions(Action<string> L, CancellationToken ct)
    {
        var lines = new List<string>();
        int rc = Run("bcdedit", "/create /d \"Multi-Win Ramdisk\" /device",
            l => { lock (lines) lines.Add(l); L("  " + l.Trim()); }, ct);
        string all = string.Join("\n", lines);
        var match = Regex.Match(all, @"\{[0-9a-fA-F-]{36}\}");
        if (rc != 0 || !match.Success)
        {
            if (match.Success)
            {
                try { Bcd($"delete {match.Value} /f", L, CancellationToken.None); }
                catch (Exception ex) { L("Rollback warning: could not remove partially-created ramdisk options: " + ex.Message); }
            }
            throw new IOException("Could not create a dedicated ramdisk options object:\n" +
                string.Join("\n", lines.TakeLast(6)));
        }
        L($"Dedicated ramdisk options {match.Value} created.");
        return match.Value;
    }

    private static void Bcd(string args, Action<string> L, CancellationToken ct)
    {
        var lines = new List<string>();
        int rc = Run("bcdedit", "/" + args.TrimStart('/', ' '),
            l => { lock (lines) lines.Add(l); }, ct);
        if (rc != 0)
            throw new IOException("bcdedit " + args + " failed:\n" + string.Join("\n", lines.TakeLast(6)));
        foreach (var l in lines.TakeLast(3))
            if (!string.IsNullOrWhiteSpace(l)) L("  " + l.Trim());
    }

    private static int Run(string exe, string args, Action<string>? onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        ct.ThrowIfCancellationRequested();
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.Start();
        var ro = Task.Run(() => Pump(p.StandardOutput, onLine));
        var re = Task.Run(() => Pump(p.StandardError, onLine));
        try
        {
            while (!p.WaitForExit(250))
                ct.ThrowIfCancellationRequested();
            Task.WaitAll(ro, re);
            // A child that has exited completed its operation; do not turn a
            // late cancellation into an ambiguous partial-success exception.
            return p.ExitCode;
        }
        catch
        {
            try { if (!p.HasExited) { p.Kill(entireProcessTree: true); p.WaitForExit(5000); } } catch { }
            throw;
        }
    }

    private static void Pump(StreamReader r, Action<string>? onLine)
    {
        var sb = new StringBuilder();
        var buf = new char[4096];
        int n;
        while ((n = r.Read(buf, 0, buf.Length)) > 0)
        {
            for (int i = 0; i < n; i++)
            {
                char c = buf[i];
                if (c == '\r' || c == '\n')
                {
                    if (sb.Length > 0) { onLine?.Invoke(sb.ToString()); sb.Clear(); }
                }
                else sb.Append(c);
            }
        }
        if (sb.Length > 0) onLine?.Invoke(sb.ToString());
    }
}

