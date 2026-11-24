using System.Windows;
using System.Windows.Threading;

namespace PersonnalPass.App.Services;

/// <summary>Copie dans le presse-papiers et l'efface automatiquement après un délai.</summary>
public static class SecureClipboard
{
    public static readonly TimeSpan ClearDelay = TimeSpan.FromSeconds(30);
    private static DispatcherTimer? _timer;
    private static string? _lastCopied;

    public static bool Copy(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            var data = new DataObject();
            data.SetText(text);
            // Demande à Windows de ne pas garder ce contenu dans l'historique (Win+V) ni de le synchroniser.
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream([0, 0, 0, 0]));
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream([0, 0, 0, 0]));
            data.SetData("CanUploadToCloudClipboard", new MemoryStream([0, 0, 0, 0]));
            Clipboard.SetDataObject(data, copy: true);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return false; // presse-papiers occupé par une autre application
        }

        _lastCopied = text;
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = ClearDelay };
        _timer.Tick += (_, _) => ClearIfOurs();
        _timer.Start();
        return true;
    }

    public static void ClearIfOurs()
    {
        _timer?.Stop();
        try
        {
            if (_lastCopied is not null && Clipboard.ContainsText() && Clipboard.GetText() == _lastCopied)
                Clipboard.Clear();
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // tant pis : on ne bloque pas l'application pour ça
        }
        _lastCopied = null;
    }
}
