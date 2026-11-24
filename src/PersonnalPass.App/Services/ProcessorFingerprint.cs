using System.Management;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Services;

/// <summary>
/// Signature du processeur lue via WMI (Win32_Processor.ProcessorId + nom), comme dans la version WinDev.
/// Attention : ProcessorId est identique pour tous les processeurs d'un même modèle ; ce n'est donc pas
/// un secret, seulement un « verrou » qui empêche d'ouvrir le coffre copié sur une autre machine.
/// </summary>
public sealed class ProcessorFingerprint : IHardwareFingerprintProvider
{
    private string? _cached;

    public string GetFingerprint() => _cached ??= Read();

    private static string Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId, Name FROM Win32_Processor");
            var parts = searcher.Get()
                .Cast<ManagementObject>()
                .Select(o => $"{o["ProcessorId"]}|{o["Name"]}".Trim())
                .OrderBy(s => s, StringComparer.Ordinal)
                .Distinct()
                .ToList();
            if (parts.Count == 0 || parts.All(p => p == "|"))
                throw new InvalidOperationException("Aucun processeur renvoyé par WMI.");
            return "cpu:" + string.Join(";", parts);
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or PlatformNotSupportedException)
        {
            throw new InvalidOperationException("Impossible de lire la signature du processeur (WMI).", ex);
        }
    }
}
