# OpenZ Duel — prototype 1 contre 1

## Fonctionnement

Unity 6000.3.27f2 / C#. Le PC hôte exécute la simulation existante ; l’invité commande réellement le camp bleu sans exécuter une seconde simulation. Un protocole TCP versionné transmet l’état initial puis les changements de propriétés, créations, suppressions et événements à 15 Hz. Les commandes sont validées côté hôte. Les empreintes des cartes, paramètres et schémas doivent correspondre.

Les menus Unity utilisent le kit graphique et les portraits du jeu. Le lobby propose les quatre missions L02, L07, L16 et L17, le chat, le ping, deux validations et une barrière de chargement suivie de trois secondes de compte à rebours.

Les règles et la dotation sont COMMUNES aux deux joueurs : quatre emplacements de tourelles, groupes/unités devant le fort, vitesse de construction et bonus territorial. Par défaut les deux camps reçoivent la dotation rouge de la mission, sans avantages du solo ni bots. Le handicap est facultatif et désactivé par défaut : il copie les paramètres communs pour un seul joueur, dont l’armée et la vitesse de construction peuvent être modifiées. Un repère rouge signale cette exception. Chaque modification annule les deux validations.

En partie : abandon confirmé (fort détruit, victoire adverse), menu dédié, chat avec touche configurable, ping et voix facultative avec appui pour parler et choix du microphone. Le canal vocal est séparé en UDP : mono 16 kHz mu-law, environ 18 ko/s pendant la parole. Aucun microphone n’est ouvert avant son activation explicite.

## Préparation et compilation

Le projet complet extrait est D:\OpenZ-Source\OpenZ_1.4_source. Les scripts de cette distribution sont plus récents que ceux du dépôt. Le module suivi par Git reste dans source/Assets/OpenZ/Scripts/Multiplayer.

```powershell
./tools/Prepare-DuelProject.ps1
& D:\Unity\6000.3.27f2\Editor\Unity.exe -batchmode -nographics -quit -projectPath D:\OpenZ-Source\OpenZ_1.4_source -executeMethod OpenZ.Multiplayer.Editor.DuelBuild.Windows -logFile D:\OpenZ-Source\tests\Multiplayer\.cli\Unity-build.log
```

Exécutable : builds/OpenZ-Duel/OpenZ-Duel.exe, à conserver avec tous ses fichiers voisins. Le build vérifie aussi la sérialisation Unity des commandes, événements et règles. Après la première compilation des shaders, les reconstructions utilisent BuildScriptsOnly ; retirer cette option dans DuelBuild si des ressources changent.

Le script de préparation applique des raccordements ciblés et marqués aux méthodes existantes, après contrôle de toutes ses ancres. Il peut être rejoué et s’arrête si une mise à jour du jeu exige une révision. Il ne duplique pas le moteur. Les ressources volumineuses, caches et exécutables restent ignorés par Git.

## Connexion et invitations

Menu principal > 1 CONTRE 1. L’hôte indique son adresse LAN ou publique, crée la salle et copie l’invitation. L’autre joueur la colle dans Rejoindre. L’invitation contient IP, port et jeton aléatoire. Les deux joueurs doivent utiliser le même build et donner leur prêt.

Pour ouvrir les liens openz:// depuis Windows, exécuter une fois tools/Register-DuelInvitations.ps1. Le protocole est enregistré uniquement pour le compte courant. Le lien préremplit le menu de connexion ; le joueur choisit ensuite de rejoindre. Certains clients de messagerie ne rendent pas les protocoles personnalisés cliquables ; le copier/coller reste disponible.

Internet direct : rendre accessible le port TCP choisi (27888 par défaut) sur le PC hôte et sa box. Pour la voix, rendre accessible le port UDP suivant (27889). En réseau local, utiliser l’IP locale de l’hôte. Aucun serveur permanent n’est requis.

## Contrôles

```powershell
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
./tools/Test-DuelPlayers.ps1 -Map L02
```

463 contrôles indépendants de Unity passent : lobby, handicap, validations, commandes par équipe, connexion TCP réelle locale, trames invalides/tronquées, état différentiel, références, projectiles, production, dotations symétriques, abandon et codec vocal. Ils chargent les données des quatre missions complètes. Ils ne prouvent pas seuls le rendu ni une partie Unity.

Le second script lance deux exécutables réels. Il capture le lobby, le menu en partie et le combat, valide les deux joueurs, fait charger les scènes, donne des ordres au bleu, provoque un combat, échange une trame audio UDP synthétique puis fait abandonner l’invité. Les résultats PASS/FAIL et journaux sont conservés dans tests/Multiplayer/.cli/proof-…. Lire les résultats effectifs dans ces dossiers.

L’état précis des essais réussis, des échecs et du build est consigné dans [DUEL_VALIDATION.md](DUEL_VALIDATION.md). La commande tools/Build-And-Test-Duel.ps1 reprend la compilation finale et les quatre essais.

## Limites

Pas encore de relais NAT, de page HTTPS publique pour Discord, de reconnexion ou de migration d’hôte. Un relais géré nécessite un fournisseur et sa configuration. L’invitation seule ne traverse pas toutes les box. La validation sur deux PC distants et la qualité réelle de la voix avec casques sont distinctes des essais automatisés locaux.

TCP récupère les pertes, mais elles peuvent retarder les mises à jour suivantes. Un éventuel transport UDP de l’état demandera des références acquittées et une récupération adaptée. L’audio utilise déjà un tampon borné et abandonne les trames périmées.
