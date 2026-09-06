using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeskBox.Services;

/// <summary>
/// Hides and restores the Windows desktop system icons (Recycle Bin, This
/// PC, ...) through the HideDesktopIcons registry values. These icons are
/// shell namespace objects rather than files, so quick organization hides
/// them via the registry and undo restores the previous visibility.
/// </summary>
public sealed class DesktopSystemIconService
{
    internal const string NewStartPanelKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
    internal const string ClassicStartPanelKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartPanel";

    /// <summary>Desktop system icons hidden by quick organization.</summary>
    public static readonly IReadOnlyList<string> DesktopSystemIconClsids =
    [
        "{645FF040-5081-101B-9F08-00AA002F954E}", // Recycle Bin
        "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", // This PC
        "{59031A47-3F72-44A7-89C5-5595FE6B30EE}", // User's Files
        "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", // Network
        "{5399E694-6CE7-4B44-91B1-73AC7A97BF02}" // Control Panel
    ];

    /// <summary>
    /// Hides every currently visible desktop system icon and returns the
    /// CLSIDs whose visibility this call changed. Icons that were already
    /// hidden are not returned, so undo never reveals them.
    /// </summary>
    public List<string> HideVisibleDesktopSystemIcons()
    {
        List<string> toHide = ComputeClsidsToHide(IsHidden);
        foreach (string clsid in toHide)
        {
            WriteHidden(clsid, hidden: true);
        }

        if (toHide.Count > 0)
        {
            NotifyShellChanged();
        }

        return toHide;
    }

    /// <summary>
    /// Restores the system icons a previous organization run hid. Icons the
    /// user re-hid manually afterwards are left hidden.
    /// </summary>
    public void RestoreDesktopSystemIcons(IEnumerable<string> clsids)
    {
        List<string> toRestore = ComputeClsidsToRestore(clsids, IsHidden);
        foreach (string clsid in toRestore)
        {
            WriteHidden(clsid, hidden: false);
        }

        if (toRestore.Count > 0)
        {
            NotifyShellChanged();
        }
    }

    internal static List<string> ComputeClsidsToHide(Func<string, bool> isHidden) =>
        DesktopSystemIconClsids.Where(clsid => !isHidden(clsid)).ToList();

    internal static List<string> ComputeClsidsToRestore(
        IEnumerable<string> hiddenClsids,
        Func<string, bool> isHidden) =>
        hiddenClsids.Where(isHidden).ToList();

    private static bool IsHidden(string clsid) =>
        ReadInt(NewStartPanelKeyPath, clsid) == 1 ||
        ReadInt(ClassicStartPanelKeyPath, clsid) == 1;

    private static void WriteHidden(string clsid, bool hidden)
    {
        WriteInt(NewStartPanelKeyPath, clsid, hidden ? 1 : 0);
        WriteInt(ClassicStartPanelKeyPath, clsid, hidden ? 1 : 0);
    }

    private static int? ReadInt(string keyPath, string name)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(name) as int?;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteInt(string keyPath, string name, int value)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(keyPath);
            key?.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch
        {
        }
    }

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>Makes Explorer re-read the HideDesktopIcons values.</summary>
    private static void NotifyShellChanged() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
}
