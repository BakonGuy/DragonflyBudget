namespace Dragonfly.Services;

public class SemVer : IComparable<SemVer>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }

    public SemVer(int major, int minor, int patch, string? preRelease = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public static SemVer? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var s = tag.TrimStart('v', 'V');
        string? pre = null;

        // Strip build metadata after '+'
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];

        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            pre = s[(dash + 1)..];
            s = s[..dash];
        }

        var parts = s.Split('.');
        if (parts.Length < 1) return null;
        if (!int.TryParse(parts[0], out var major)) return null;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
        return new SemVer(major, minor, patch, pre);
    }

    public bool IsNewerThan(SemVer other)
    {
        if (Major != other.Major) return Major > other.Major;
        if (Minor != other.Minor) return Minor > other.Minor;
        if (Patch != other.Patch) return Patch > other.Patch;
        // Same core version: a release (no prerelease) is newer than a prerelease
        if (PreRelease == null && other.PreRelease != null) return true;
        if (PreRelease != null && other.PreRelease == null) return false;
        return false; // same version + same prerelease status
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null) return 1;
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        if (Patch != other.Patch) return Patch.CompareTo(other.Patch);
        if (PreRelease == null && other.PreRelease != null) return 1;
        if (PreRelease != null && other.PreRelease == null) return -1;
        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public override string ToString() =>
        PreRelease != null ? $"{Major}.{Minor}.{Patch}-{PreRelease}" : $"{Major}.{Minor}.{Patch}";
}
