# Validation du prototype — 14 novembre 2026

## Résultats

- Unity 6000.3.27f2 : build Windows réussi, zéro erreur. Dernier journal : tests/Multiplayer/.cli/Unity-delivery-build.log.
- La sérialisation Unity est contrôlée avant le build : commandes avec waypoints, événements de combat, règles communes et handicap optionnel.
- 463 contrôles .NET de simulation et de réseau passent sur les quatre missions.
- Désert, Volcan, Jungle et Car Park : PASS des deux exécutables pour chaque mission. Dégâts observés des deux côtés, ordres invités transmis, voix UDP reçue, abandon de l’invité, fort bleu détruit, vainqueur rouge et zéro exception.
- Boutons Unity : PASS pour l’entrée dans le mode, la création de salle, les règles communes par défaut, les portraits, l’activation du handicap bleu, l’édition de son armée sans changer la dotation commune et sa désactivation.
- Les captures des règles et du handicap ont été examinées visuellement. Elles proviennent du rendu Unity, pas de maquettes HTML.

## Preuves locales

| Mission | Résultats hôte et invité |
| --- | --- |
| Désert | tests/Multiplayer/.cli/proof-L02-20261114-195657 |
| Volcan | tests/Multiplayer/.cli/proof-L07-20261114-194617 |
| Jungle | tests/Multiplayer/.cli/proof-L16-20261114-194721 |
| Car Park | tests/Multiplayer/.cli/proof-L17-20261114-194820 |

Chaque dossier contient host-result.txt, guest-result.txt et les journaux complets. Les menus ont leur résultat dans tests/Multiplayer/.cli/menus-20261114-195818/result.txt. Des captures durables sont conservées dans docs/duel-preview.

Le premier scénario Jungle plaçait ses deux chars sans garantir la ligne de tir et échouait sur l’assertion de dégâts ; le placement sur un couloir dégagé corrige le scénario et le nouvel essai passe. Les premières captures de fenêtres masquées étaient noires ; le rendu explicite vers une texture et le calque du canvas permettent désormais d’inspecter les menus.

Le lanceur attend la publication complète de son invitation avant de démarrer le second processus.

## Reproduire

```powershell
./tools/Build-And-Test-Duel.ps1
```

Ce script prépare les sources, reconstruit le joueur Windows, lance les quatre duels et teste les boutons des menus. Il s’arrête en cas d’échec. Simulation seule :

```powershell
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
```

Les tests sont exécutés sur deux processus du même PC. Ils ne prouvent pas une connexion à travers toutes les box ni la qualité réelle du microphone. Une partie sur deux PC distants, la latence variable et l’écoute avec casques restent à tester. Les invitations directes IP sont disponibles ; aucun relais ni page publique HTTPS d’invitation n’est configuré.