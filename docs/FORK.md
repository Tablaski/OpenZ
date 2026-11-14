# Fork Tablaski : sauvegarder et reconstruire OpenZ Duel

Ce fork conserve le prototype 1 contre 1 développé à partir d'OpenZ 1.4.
`main` est la branche stable ; `dev` contient le travail de développement.
La première sauvegarde du prototype part du commit officiel
`e7377432907eb8e3305c979f66d45c36c5ba4b3a`.

## Pourquoi le clone ne contient pas le jeu complet

Le dépôt officiel contient le code C#, les shaders et la documentation dans
`source/` et `docs/`. Le projet Unity complet (modèles, textures, sons, scènes et
réglages) est distribué séparément dans les deux archives sources de la release
1.4. Un clone ou le bouton GitHub « Download ZIP » ne remplace pas ces archives.
Voir les [instructions officielles](https://github.com/playopenz/OpenZ/blob/main/docs/BUILDING.md).

## Contenu sauvegardé dans Git

- `source/Assets/OpenZ/Scripts/Multiplayer/` et ses fichiers `.meta` : module 1vs1,
  menus, réseau, réplication, voix et outils Unity.
- `tools/Prepare-DuelProject.ps1` : installation du module et raccordements aux
  scripts du projet complet. Ces raccordements sont nécessaires au fonctionnement.
- Les autres scripts de `tools/`, les sources de `tests/Multiplayer/`, la
  documentation et les captures de `docs/duel-preview/`.

`OpenZ_1.4_source/`, les ZIP, `builds/`, les caches et les résultats temporaires
des tests sont ignorés. Cette sauvegarde conserve le travail de développement ;
elle dépend des archives officielles 1.4 pour les ressources du jeu. Les sources
de la distribution 1.4 peuvent être plus récentes que celles du dossier `source/`
du dépôt : ne pas remplacer tous les scripts extraits par ce dossier.

## Reconstruire sur une autre machine

1. Cloner `https://github.com/Tablaski/OpenZ.git` et sélectionner `dev` :

   ```powershell
   git clone --branch dev https://github.com/Tablaski/OpenZ.git
   cd OpenZ
   ```

2. Télécharger `OpenZ_1.4_source_part1.zip` et `OpenZ_1.4_source_part2.zip` depuis
   les [releases officielles](https://github.com/playopenz/OpenZ/releases).
   Choisir la version **1.4**, puis extraire les deux parties au même endroit pour
   obtenir `OpenZ_1.4_source/` à la racine du clone, contenant `Assets/`,
   `Packages/` et `ProjectSettings/`.
3. Installer Unity **6000.3.27f2** avec le support de compilation Windows.
4. Depuis la racine du clone, appliquer le module :

   ```powershell
   ./tools/Prepare-DuelProject.ps1
   ```

5. Ajouter `OpenZ_1.4_source/` dans Unity Hub, ouvrir
   `Assets/OpenZ/Scenes/MainMenu.unity`, puis utiliser le menu de compilation
   `OpenZ > Multiplayer > Build Windows POC`. Le résultat se trouve dans
   `builds/OpenZ-Duel/` ; tous les fichiers voisins de l'exécutable sont nécessaires.

Pour les tests de simulation, installer également le SDK .NET 8 puis lancer :

```powershell
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
```

Les essais complets et leurs limites sont décrits dans
[MULTIPLAYER_POC.md](MULTIPLAYER_POC.md) et [DUEL_VALIDATION.md](DUEL_VALIDATION.md).
Les scripts automatisés proposent par défaut le chemin Unity local
`D:\Unity\6000.3.27f2\Editor\Unity.exe` ; adapter leur paramètre Unity sur une
autre machine.

## Continuer le développement

Modifier le module dans `source/Assets/OpenZ/Scripts/Multiplayer/`, puis relancer
`Prepare-DuelProject.ps1` pour le copier vers le projet Unity. Une modification
faite uniquement dans le projet extrait et ignoré ne sera pas sauvegardée par Git.
Tout nouveau raccordement aux scripts d'origine doit aussi être conservé dans
le script de préparation.

Le dépôt officiel peut être conservé comme remote `upstream`, et le fork comme
remote `origin`. Les commits et les push de développement vont sur
`origin/dev`.

Pour une sauvegarde autonome complète, conserver aussi les deux ZIP sources 1.4
sur un stockage de sauvegarde, ou publier une archive dédiée dans les releases
du fork. Les fichiers exécutables peuvent également être distribués en release.
Ils ne sont pas inclus dans ce commit de sources. Conserver les licences et les
crédits du projet dans toute redistribution.
