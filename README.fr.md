# ForkPlus-Next

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus-Next)](https://github.com/hebin123456/ForkPlus-Next/releases)

Le portage multiplateforme de [ForkPlus](https://github.com/hebin123456/ForkPlus) (l'édition WPF) : la couche UI est réécrite en .NET 10 + Avalonia 12, et une seule base de code fonctionne sur Windows / Linux / macOS. Le moteur Rust sous-jacent (biturbo native), le développement assisté par IA, les 8 langues, les 12 thèmes, le workflow git mm, ainsi que les visualisations comme les cartes de chaleur de contributions et les treemaps de dépôt restent identiques à l'original.

> La configuration de l'environnement de migration, la chaîne des correctifs historiques et les leçons retenues sont documentées dans [MIGRATION.md](MIGRATION.md).

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## Fonctionnalités principales

- **Multiplateforme** : couche UI multiplateforme basée sur Avalonia 12 ; la CI produit en parallèle des builds Windows x64 / Linux x64 / macOS arm64
- **Prise en charge multilingue** : 8 langues intégrées (anglais, chinois simplifié, chinois traditionnel, japonais, coréen, français, allemand, espagnol), extensibles via des fichiers JSON
- **Thèmes multiples** : 12 habillages intégrés (Light/Dark, Solarized, GitHub, Dracula, Monokai, Violet/Vert clair & sombre) plus surcharges de couleurs personnalisées appliquées instantanément
- **Flux de travail git mm** : sous-commande `git mm` intégrée fournissant des flux de travail Lean Branching pour gérer et synchroniser les changements de plusieurs sous-dépôts
- **Développement assisté par IA** : revue de code par IA, génération automatique de messages de commit et modification de code assistée par IA
- **Carte de chaleur des contributions** : carte de chaleur des commits façon GitHub sur 53 semaines × 7 jours, avec légende de l'échelle de couleurs et résumé statistique (total des commits / plus longue série / jour le plus actif) ; le survol affiche le nombre de commits du jour et le top 3 des auteurs
- **Treemap du dépôt** : visualisation de la taille des fichiers du dépôt basée sur l'algorithme treemap de biturbo native, avec exploration par clic
- **Suivi des branches distantes** : l'action « Suivre » du clic droit devient un menu à deux niveaux groupé par distant, avec une boîte de recherche épinglée pour retrouver rapidement une branche parmi de nombreux distants
- **Optimisations des performances** : améliorations ciblées pour l'actualisation des grands dépôts, le rendu des diffs et la gestion des sous-modules
- **Statistiques de code** : intègre tokei (Rust, 200+ langages) pour compter les lignes par langage (fichiers, code, commentaires, lignes vides) avec visualisation en camembert, et prend en charge le changement de ref Workspace/branche/tag

## Structure du dépôt

```
ForkPlus-Next/
├── src/
│   ├── ForkPlus/              # Source de l'application principale (UI multiplateforme Avalonia 12), XAML, assets
│   │   ├── Biturbo/           # Liaisons P/Invoke pour la bibliothèque native biturbo
│   │   ├── Languages/         # Fichiers de traduction multilingues (JSON)
│   │   │   ├── zh-Hans.json   # Chinois simplifié
│   │   │   ├── zh-Hant.json   # Chinois traditionnel
│   │   │   ├── ja-JP.json     # Japonais
│   │   │   ├── ko-KR.json     # Coréen
│   │   │   ├── fr-FR.json     # Français
│   │   │   ├── de-DE.json     # Allemand
│   │   │   ├── es-ES.json     # Espagnol
│   │   │   └── README.md      # Description du format de fichier de langue
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Assistant askpass Git/SSH
│   ├── ForkPlus.RI/           # Assistant éditeur de rebase interactif
│   ├── ForkPlus.Tests/        # Tests unitaires xUnit (dont tests de fumée UI Avalonia.Headless)
│   ├── ForkPlus.AskPass.Tests/# Tests unitaires de l'assistant AskPass
│   └── ForkPlus.RI.Tests/     # Tests unitaires de l'assistant RI
├── third_party/               # Binaires natifs récupérés à la compilation (voir « Bibliothèque native biturbo » ci-dessous)
├── gitmm/                     # Documentation de référence du flux de travail git mm
└── .github/workflows/         # Configuration CI GitHub Actions
```

## Compilation

### Prérequis

- Windows 10 ou version ultérieure / Linux / macOS (multiplateforme)
- .NET 10 SDK
- IDE (facultatif) : Visual Studio 2026 (Windows ; ouvrez la solution en un clic via `OpenForkPlusInVS2026.cmd` à la racine du dépôt), Rider ou VS Code
- Git 2.40 ou version ultérieure (recommandé ; les versions antérieures déclenchent un avertissement au démarrage et certaines fonctionnalités peuvent dysfonctionner. L'application préfère son instance git intégrée 2.50.1 et revient au git système si absente)
- git-mm 3.0 ou ultérieur (requis pour le workflow git mm ; un avertissement s'affiche au démarrage si la version est inférieure ; sans lui, les fonctions d'espace de travail git mm sont indisponibles ; chemin git-mm configurable dans les Préférences)

### Étapes de compilation

```bash
# ① La bibliothèque de graphiques OxyPlot.Avalonia est intégrée comme référence source
#    hors dépôt (le csproj pointe vers un répertoire voisin de ce dépôt) ; clonez-la à
#    côté de ce dépôt avant la première compilation. Conservez la version officielle
#    telle quelle et ne modifiez pas sa version d'Avalonia :
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git ../oxyplot-avalonia

# ② Compiler (à la racine du dépôt) :
dotnet build ForkPlus.sln -c Release
```

Vous pouvez aussi ouvrir directement `ForkPlus.sln` à la racine du dépôt avec Visual Studio 2026.

### Bibliothèque native biturbo

Le composant natif biturbo (Rust) fournit la mise en page du treemap du dépôt, le cache du graphe de commits, l'analyse des en-têtes de révision, etc. **Ce binaire n'est pas commis dans ce dépôt** ; il est récupéré à la compilation depuis la dernière version du [dépôt Biturbo](https://github.com/hebin123456/Biturbo), avec sélection du fichier selon la plateforme :

| Plateforme | Fichier |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

Mécanismes (voir [ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj)) :

- Cible `RestoreBiturbo` (`BeforeTargets=Build`) : télécharge automatiquement la bibliothèque native manquante de la plateforme courante (PowerShell sous Windows, bash + curl sous Linux/macOS en choisissant `.so` / `.dylib` selon `uname -s`, avec reprises)
- Cibles `CopyHelperExecutables` / `PublishHelperExecutables` (`AfterTargets=Build` / `Publish`) : copient la bibliothèque native et les sorties des sous-processus AskPass/RI vers les répertoires Build / Publish
- `.gitignore` exclut déjà ces fichiers sous `third_party/`

La première compilation nécessite donc un accès réseau à GitHub ; sur la CI, le workflow les télécharge et les vérifie explicitement (non vide et > 1 Mo), la cible `RestoreBiturbo` du csproj servant de solution de repli.

### tokei

[tokei](https://github.com/XAMPPRocky/tokei) (sous licence MIT) alimente le panneau « lignes de code ». À la compilation, le **binaire précompilé** est récupéré depuis la dernière version du dépôt [hebin123456/tokei](https://github.com/hebin123456/tokei), sans chaîne d'outils Rust locale :

- Windows x64 → exe seul, enregistré sous `third_party/tokei.exe`
- Linux x64 / macOS → tar.gz (contenant le binaire `tokei` seul), extrait vers `third_party/tokei`
- L'artefact macOS est x86_64 et s'exécute sur Apple Silicon via Rosetta 2

Même mécanisme que biturbo : la cible `RestoreTokei` (`BeforeTargets=Build`) récupère automatiquement, la CI télécharge et vérifie explicitement, et `.gitignore` exclut les artefacts.

### Intégration continue

Le projet est configuré avec GitHub Actions ([`.github/workflows/build.yml`](.github/workflows/build.yml)) : à chaque push / PR sur `master`, ou déclenchement manuel, il compile en parallèle sur trois plateformes et téléverse les artefacts :

| Matrice | Runner | RID |
|--------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

Les artefacts sont des publications **dépendantes du framework** (le poste cible doit avoir le runtime .NET 10) et contiennent l'application principale, le trio de sous-processus AskPass/RI, la bibliothèque native biturbo de la plateforme, tokei et les fichiers de langue. Téléchargez-les depuis l'exécution correspondante sur la page [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) (Artifacts, conservés 14 jours).

## Tests

- Tests unitaires : `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj` (dont tests de fumée et de bout en bout Avalonia.Headless, multiplateformes, exécutés avec les tests unitaires)
- Plus de 4000 cas au total ; chaque correctif clé de la migration dispose d'une garde de régression (voir [MIGRATION.md](MIGRATION.md))

## Prise en charge multilingue

### Langues intégrées

| Code de langue | Nom affiché | Statut |
|-----------------|-------------|--------|
| `en` | English | Langue source |
| `zh-Hans` | 简体中文 | Complet |
| `zh-Hant` | 繁體中文 | Complet |
| `ja-JP` | 日本語 | Complet |
| `ko-KR` | 한국어 | Complet |
| `fr-FR` | Français | Complet |
| `de-DE` | Deutsch | Complet |
| `es-ES` | Español | Complet |

### Ajout d'une nouvelle langue

Créez un nouveau fichier `<code-langue>.json` dans le répertoire `src/ForkPlus/Languages/` — aucune modification de code n'est requise. Format de fichier :

```json
{
  "code": "ko",
  "name": "한국어",
  "translations": {
    "Preferences": "환경설정",
    "General": "일반",
    "Commit": "커밋"
  }
}
```

Voir [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md) pour plus de détails.

### API d'internationalisation

La base de code utilise les API suivantes pour l'internationalisation :

- `PreferencesLocalization.Current("English text")` — Traduction simple de chaîne
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — Traduction de chaîne paramétrée
- `PreferencesLocalization.Translate(text, language)` — Traduction pour une langue spécifique

## Téléchargement

- Artefacts de build CI : page [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) → l'exécution build correspondante → Artifacts (dépendants du framework, runtime .NET 10 requis)
- Versions officielles : [page Releases](https://github.com/hebin123456/ForkPlus-Next/releases)
- Pour les modifications de chaque version, consultez les [Release Notes](RELEASE_NOTE.md) (y compris l'historique de l'édition WPF)

## Conventions de développement

- Lors de la modification de l'application elle-même, restez dans le répertoire `src/ForkPlus` ; les binaires d'exécution sous `third_party/` (bibliothèque native biturbo, tokei) sont récupérés automatiquement à la compilation — ne commettez pas de binaires manuellement
- Pour mettre à niveau biturbo / tokei, publiez une nouvelle version dans le dépôt correspondant ; la prochaine compilation de ce dépôt la récupérera automatiquement
- `../oxyplot-avalonia` est une référence source hors dépôt — conservez la version officielle telle quelle (la leçon d'une modification accidentelle de sa version est dans MIGRATION.md)
- La configuration de l'environnement, les travaux en cours et la chaîne des correctifs historiques de la migration (WPF → Avalonia) sont documentés dans [MIGRATION.md](MIGRATION.md)

## Licence

Ce projet est open source sous la [licence MIT](LICENSE).

Copyright (c) 2026 hebin123456
