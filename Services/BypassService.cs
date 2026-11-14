using System.IO;
using System.Text;
using WinMultiInstaller.Models;

namespace WinMultiInstaller.Services;

public static class BypassService
{
    private const string Ns =
        "xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" " +
        "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"";

    public static void ValidateEditionSelection(string root, string? editionId)
    {
        if (string.IsNullOrWhiteSpace(editionId)) return;
        if (!editionId.Equals("Professional", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Unsupported Windows edition selection: {editionId}");

        string path = Path.Combine(root, "sources", "ei.cfg");
        if (!File.Exists(path)) return;
        string? existing = ReadEditionId(path);
        if (!string.Equals(existing, editionId, StringComparison.OrdinalIgnoreCase))
            throw new IOException("The ISO already contains a conflicting sources\ei.cfg. Multi-Win will not overwrite its edition configuration.");
    }

    public static void ApplyEditionSelection(string root, string? editionId, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(editionId)) return;
        ValidateEditionSelection(root, editionId);
        string path = Path.Combine(root, "sources", "ei.cfg");
        if (File.Exists(path))
        {
            log($"Windows edition selection already configured: {editionId}.");
            return;
        }

        const string config = "[EditionID]\r\nProfessional\r\n[Channel]\r\nRetail\r\n[VL]\r\n0\r\n";
        string temp = path + ".multiwin.tmp";
        try
        {
            File.WriteAllText(temp, config, new UTF8Encoding(false));
            File.Move(temp, path);
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        log($"Windows Setup edition selection configured: {editionId}.");
    }

    private static string? ReadEditionId(string path)
    {
        bool inEditionSection = false;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inEditionSection = line.Equals("[EditionID]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (inEditionSection && line.Length > 0 && !line.StartsWith(";"))
                return line;
        }
        return null;
    }

    public static void ValidateTarget(string root, WindowsCustom c)
    {
        if (!c.HasTweaks()) return;
        string user = SanitizeUser(c.Username);
        bool hasUnattend = c.BypassTpm || c.BypassNro || user.Length > 0 ||
            c.SkipPrivacy || c.PreventBitLocker || c.HostLocale;
        if (c.EmptyAppraiser &&
            File.Exists(Path.Combine(root, "sources", "appraiserres.dll.bak")))
            throw new IOException("The ISO already contains sources\\appraiserres.dll.bak; Multi-Win refuses to overwrite that backup.");
        if (!hasUnattend) return;

        string[] answerFiles =
        {
            Path.Combine(root, "autounattend.xml"),
            Path.Combine(root, "sources", "autounattend.xml"),
            Path.Combine(root, "sources", "unattend.xml"),
            Path.Combine(root, "sources", "$OEM$", "\u0024\u0024", "Panther", "unattend.xml")
        };
        if (answerFiles.Any(File.Exists))
            throw new IOException("The ISO already contains an unattended answer file. Multi-Win refuses to overwrite or conflict with custom setup settings.");
    }

    public static void Inject(string usbRoot, string arch, WindowsCustom c, Action<string> L)
    {
        if (!c.HasTweaks()) return;

        string user = SanitizeUser(c.Username);
        bool hasUnattend = c.BypassTpm || c.BypassNro || user.Length > 0 ||
            c.SkipPrivacy || c.PreventBitLocker || c.HostLocale;
        ValidateTarget(usbRoot, c);
        if (!hasUnattend)
        {
            if (c.EmptyAppraiser) ApplyEmptyAppraiser(usbRoot, L);
            return;
        }

        string warch = arch == "x86" ? "x86" : arch == "arm64" ? "arm64" : "amd64";
        string userXml = System.Security.SecurityElement.Escape(user) ?? "";
        string tzXml;
        try { tzXml = System.Security.SecurityElement.Escape(TimeZoneInfo.Local.Id) ?? ""; }
        catch { tzXml = ""; }
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

        if (c.BypassTpm)
        {
            sb.AppendLine("  <settings pass=\"windowsPE\">");
            sb.AppendLine($"    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"{warch}\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" {Ns}>");
            sb.AppendLine("      <UserData><ProductKey><Key /></ProductKey></UserData>");
            sb.AppendLine("      <RunSynchronous>");
            int o = 1;
            foreach (var v in new[] { "BypassTPMCheck", "BypassSecureBootCheck", "BypassRAMCheck", "BypassCPUCheck" })
                sb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{o++}</Order><Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v {v} /t REG_DWORD /d 1 /f</Path></RunSynchronousCommand>");
            sb.AppendLine("      </RunSynchronous>");
            sb.AppendLine("    </component>");
            sb.AppendLine("  </settings>");
        }

        var spec = new List<string>();
        if (c.BypassNro)
            spec.Add("reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE /v BypassNRO /t REG_DWORD /d 1 /f");
        if (c.PreventBitLocker)
            spec.Add("reg add HKLM\\SYSTEM\\CurrentControlSet\\Control\\BitLocker /v PreventDeviceEncryption /t REG_DWORD /d 1 /f");
        // The local account is created in the later oobeSystem pass.
        // Do not run "net user" in specialize, before that account exists.
        if (spec.Count > 0)
        {
            sb.AppendLine("  <settings pass=\"specialize\">");
            sb.AppendLine($"    <component name=\"Microsoft-Windows-Deployment\" processorArchitecture=\"{warch}\" language=\"neutral\" {Ns} publicKeyToken=\"31bf3856ad364e35\" versionScope=\"nonSxS\">");
            sb.AppendLine("      <RunSynchronous>");
            int o = 1;
            foreach (var cmd in spec)
                sb.AppendLine($"        <RunSynchronousCommand wcm:action=\"add\"><Order>{o++}</Order><Path>{System.Security.SecurityElement.Escape(cmd)}</Path></RunSynchronousCommand>");
            sb.AppendLine("      </RunSynchronous>");
            sb.AppendLine("    </component>");
            sb.AppendLine("  </settings>");
        }

        bool oobe = c.SkipPrivacy || user.Length > 0 || c.HostLocale;
        if (oobe)
        {
            sb.AppendLine("  <settings pass=\"oobeSystem\">");
            sb.AppendLine($"    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"{warch}\" language=\"neutral\" {Ns} publicKeyToken=\"31bf3856ad364e35\" versionScope=\"nonSxS\">");
            if (c.SkipPrivacy)
                sb.AppendLine("      <OOBE><HideEULAPage>true</HideEULAPage><HideOEMRegistrationScreen>true</HideOEMRegistrationScreen><HideOnlineAccountScreens>true</HideOnlineAccountScreens><HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE><ProtectYourPC>3</ProtectYourPC></OOBE>");
            if (c.HostLocale && tzXml.Length > 0)
            {
                sb.AppendLine($"      <TimeZone>{tzXml}</TimeZone>");
            }
            if (user.Length > 0)
            {
                // The UI promises a blank password. Use an actual empty value;
                // the previous base64 string decoded to the literal password "Password".
                sb.AppendLine($"      <UserAccounts><LocalAccounts><LocalAccount wcm:action=\"add\"><Password><Value></Value><PlainText>true</PlainText></Password><DisplayName>{userXml}</DisplayName><Group>Administrators</Group><Name>{userXml}</Name></LocalAccount></LocalAccounts></UserAccounts>");
                sb.AppendLine($"      <AutoLogon><Password><Value></Value><PlainText>true</PlainText></Password><Enabled>true</Enabled><LogonCount>1</LogonCount><Username>{userXml}</Username></AutoLogon>");
            }
            sb.AppendLine("    </component>");
            sb.AppendLine("  </settings>");
        }

        sb.AppendLine("</unattend>");

        string target;
        if (c.BypassTpm)
        {
            target = Path.Combine(usbRoot, "autounattend.xml");
        }
        else
        {
            string dir = Path.Combine(usbRoot, "sources", "$OEM$", "$$", "Panther");
            Directory.CreateDirectory(dir);
            target = Path.Combine(dir, "unattend.xml");
        }
        if (File.Exists(target))
            throw new IOException($"An answer file already exists at '{target}'. Multi-Win refuses to overwrite custom unattended setup settings.");
        string tempAnswer = target + ".multiwin.tmp";
        try
        {
            File.WriteAllText(tempAnswer, sb.ToString(), new UTF8Encoding(false));
            File.Move(tempAnswer, target);
        }
        finally
        {
            try { if (File.Exists(tempAnswer)) File.Delete(tempAnswer); } catch { }
        }

        var on = new List<string>();
        if (c.BypassTpm) on.Add("TPM/SB/RAM/CPU bypass");
        if (c.BypassNro) on.Add("online account bypass");
        if (user.Length > 0) on.Add($"local user '{user}' + autologon");
        if (c.SkipPrivacy) on.Add("OOBE cleanup");
        if (c.PreventBitLocker) on.Add("BitLocker block");
        if (c.HostLocale) on.Add("host timezone");
        L($"Setup tweaks written to {Path.GetFileName(target)}: {string.Join(", ", on)}.");
        if (c.EmptyAppraiser) ApplyEmptyAppraiser(usbRoot, L);
    }

    private static void ApplyEmptyAppraiser(string root, Action<string> L)
    {
        string appraiser = Path.Combine(root, "sources", "appraiserres.dll");
        if (!File.Exists(appraiser))
        {
            L("appraiserres.dll not found, skipping.");
            return;
        }

        string backup = appraiser + ".bak";
        string temp = appraiser + ".multiwin.tmp";
        if (File.Exists(backup))
            throw new IOException("Cannot apply appraiser tweak: appraiserres.dll.bak already exists; refusing to overwrite it.");
        try
        {
            File.WriteAllBytes(temp, Array.Empty<byte>());
            File.Move(appraiser, backup);
            try { File.Move(temp, appraiser); }
            catch
            {
                if (!File.Exists(appraiser) && File.Exists(backup)) File.Move(backup, appraiser);
                throw;
            }
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
        L("appraiserres.dll emptied (original preserved as .bak).");
    }

    private static string SanitizeUser(string n)
    {
        if (string.IsNullOrWhiteSpace(n)) return "";
        if (n.Trim().Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
            n.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase)) return "";
        foreach (char bad in new[] { '\\', '/', '[', ']', '"', ':', ';', '|', '=', ',', '+', '*', '?', '<', '>', '%', '@', (char)96 })
            n = n.Replace(bad, '_');
        n = n.Trim();
        return n.Length > 20 ? n[..20] : n;
    }
}

