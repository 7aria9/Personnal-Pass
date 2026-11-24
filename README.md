# Personnal-Pass

Gestionnaire de mots de passe personnel pour Windows, réécrit en C# (WPF, .NET 10, thème Fluent)
à partir de l'ancienne version WinDev.

## Fonctions

- **Connexion** par identifiant + mot de passe maître à l'ouverture (création du coffre au premier lancement).
- **Liste** des enregistrements (Nom, Identifiant, Mot de passe masqué) avec recherche.
- **Ajout, modification, suppression** d'enregistrements.
- **Générateur** de mots de passe de 8 à 32 caractères : majuscules, minuscules, chiffres, caractères spéciaux
  (au moins un caractère de chaque famille cochée est garanti).
- **Copie** de l'identifiant / du mot de passe : le presse-papiers est vidé après 30 s et exclu de l'historique Windows (Win+V).
- **Verrouillage automatique** après 5 minutes d'inactivité ou au verrouillage de la session Windows (Ctrl+L pour verrouiller).
- **Liaison au processeur** : le coffre ne s'ouvre que sur la machine qui l'a créé.
- **Kit de récupération** (fichier + code) pour rouvrir le coffre après un changement de matériel ou un oubli du mot de passe.

Raccourcis : `Ctrl+N` ajouter, `Ctrl+F` rechercher, `Entrée` modifier, `Suppr` supprimer,
`Ctrl+C` copier le mot de passe, `Ctrl+B` copier l'identifiant.

## Télécharger

Chaque pull request fusionnée sur `main` publie automatiquement une
[release](https://github.com/7aria9/Personnal-Pass/releases) avec `PersonnalPass-vX.Y.Z.exe` :
un exécutable unique, sans rien à installer (le runtime .NET est inclus).
Les pull requests ouvertes produisent aussi l'exe en artefact de la vérification « Build ».

## Compiler et lancer

Prérequis : [SDK .NET 10](https://dotnet.microsoft.com/download) (ou Visual Studio 2026).

```powershell
dotnet run --project src/PersonnalPass.App        # lancer
dotnet test                                       # tests du moteur de chiffrement
dotnet publish src/PersonnalPass.App -c Release -r win-x64 -o publish   # exécutable
```

Le coffre est enregistré dans `%APPDATA%\PersonnalPass\vault.ppv` (une copie `.bak` est conservée à chaque sauvegarde).

## Structure

| Dossier | Contenu |
|---|---|
| `src/PersonnalPass.Core` | Chiffrement, format des fichiers, générateur (multiplateforme, testé) |
| `src/PersonnalPass.App` | Interface WPF (fenêtres, lecture de l'ID processeur via WMI) |
| `tests/PersonnalPass.Core.Tests` | Tests xUnit |

## Sécurité : comment c'est chiffré

```
identifiant + mot de passe ──PBKDF2-SHA256 (600 000 itérations)──┐
signature du processeur (WMI) ────────────────────────HKDF──────┴─► clé maître
clé maître  ──AES-256-GCM──► chiffre la clé de données (256 bits aléatoires)
clé de données ──AES-256-GCM──► chiffre TOUT le contenu (noms, identifiants, mots de passe)
code de récupération ──PBKDF2──► chiffre une copie de la clé de données → fichier .pprecovery
```

- Rien n'est stocké en clair dans `vault.ppv`, pas même l'identifiant ni les noms des enregistrements.
- Le mot de passe maître n'est stocké nulle part : c'est la réussite du déchiffrement (tag GCM) qui valide la connexion.
- Toute modification du fichier est détectée (chiffrement authentifié).
- Une empreinte salée du processeur permet d'afficher « matériel changé » plutôt que « mot de passe incorrect ».

### Différences avec la version WinDev

1. **La signature du processeur n'est pas un secret.** `ProcessorId` est identique sur tous les processeurs
   d'un même modèle et se lit sans droit administrateur. Seule, elle ne protège presque rien : quelqu'un qui copie
   le fichier n'a qu'à deviner le modèle de processeur. Elle est donc conservée comme « verrou matériel »,
   mais c'est le **mot de passe maître** qui protège réellement le coffre.
2. **Le fichier de récupération n'est plus une clé en clair.** Il contient la clé de données chiffrée par un
   **code de récupération** de 25 caractères (≈ 122 bits) affiché une seule fois. Le fichier seul ou le code seul
   ne servent à rien : gardez-les à deux endroits différents (clé USB d'un côté, papier de l'autre).
3. **Changement d'ordinateur** : il faut aussi la sauvegarde du coffre (`.ppv`, menu *Sécurité → Sauvegarder le coffre*).
   Sur le nouveau PC : *Importer un coffre existant*, puis fichier + code de récupération et nouveaux identifiants.

### Limites connues

- Les mots de passe déchiffrés restent en mémoire (chaînes .NET) tant que le coffre est ouvert.
- PBKDF2 est utilisé car il est intégré à .NET ; Argon2id serait plus résistant aux attaques par GPU
  (nécessite une bibliothèque externe).
- L'ancien kit de récupération reste valable après en avoir créé un nouveau : détruisez-le s'il a pu être compromis.
- Pas d'import automatique depuis la base WinDev (format HFSQL) pour l'instant.
