using PersonnalPass.Core.Vault;

namespace PersonnalPass.Core.Tests;

internal sealed class FakeHardware(string fingerprint) : IHardwareFingerprintProvider
{
    public string Fingerprint { get; set; } = fingerprint;
    public string GetFingerprint() => Fingerprint;
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pp-tests-" + Guid.NewGuid());
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
