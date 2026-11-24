namespace PersonnalPass.Core.Vault;

/// <summary>Un enregistrement du coffre : nom, identifiant et mot de passe.</summary>
public sealed class VaultEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public VaultEntry Clone() => new()
    {
        Id = Id,
        Name = Name,
        Login = Login,
        Password = Password,
        CreatedUtc = CreatedUtc,
        UpdatedUtc = UpdatedUtc,
    };
}
