namespace Sentinel.Core;

public static class ThreatScorer
{
    // Illustrative heuristic rules only. NOT a malware verdict or antivirus engine.
    public static void Apply(SecurityEvent evt)
    {
        var a = evt.Attributes;
        static string Get(Dictionary<string, string> d, string k) =>
            d.TryGetValue(k, out var value) ? value : "";
        var image = Get(a, "Image");
        var command = Get(a, "CommandLine");
        var parent = Get(a, "ParentImage");
        var target = Get(a, "TargetObject");
        var file = Get(a, "TargetFilename");
        int score = 0;
        var signals = new List<string>();
        if (evt.Category == "Process")
        {
            if (image.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase) ||
                image.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase))
            { score += 20; signals.Add("Executed from a user-writable directory"); }
            if (image.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase) &&
                (command.Contains("-EncodedCommand", StringComparison.OrdinalIgnoreCase) ||
                 command.Contains(" -enc ", StringComparison.OrdinalIgnoreCase)))
            { score += 40; signals.Add("Encoded PowerShell invocation"); }
            if ((parent.EndsWith("winword.exe", StringComparison.OrdinalIgnoreCase) ||
                 parent.EndsWith("excel.exe", StringComparison.OrdinalIgnoreCase)) &&
                (image.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                 image.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase)))
            { score += 35; signals.Add("Office spawning a command interpreter"); }
        }
        if (evt.Category == "Registry" &&
            (target.Contains(@"\CurrentVersion\Run", StringComparison.OrdinalIgnoreCase) ||
             target.Contains(@"\CurrentVersion\RunOnce", StringComparison.OrdinalIgnoreCase)))
        { score += 30; signals.Add("Autorun registry location changed"); }
        if (evt.Category == "File" &&
            file.Contains(@"\Start Menu\Programs\Startup\", StringComparison.OrdinalIgnoreCase))
        { score += 35; signals.Add("Startup folder modified"); }
        evt.RiskScore = Math.Min(100, score);
        evt.Severity = score switch
        {
            >= 75 => "Critical",
            >= 50 => "High",
            >= 25 => "Medium",
            _ => "Info"
        };
        if (signals.Count > 0)
            evt.Description = string.Join("; ", signals) + ". Review context before any action.";
    }
}
