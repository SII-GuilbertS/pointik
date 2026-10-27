# Pointik

Pointik met en valeur la souris et le clavier pendant les cours, les présentations et les tutoriels vidéo. Il affiche :

- un **anneau** autour du curseur ;
- un **spot** (mode projecteur) qui assombrit l'écran sauf autour du curseur ;
- une **onde** à chaque clic ;
- un **zoom d'écran** en direct ;
- le **dessin à l'écran** : cadres, ovales, flèches, lignes libres ;
- les **touches du clavier** à l'écran.

Logiciel gratuit, en français, pour Windows 10 (version 2004 et plus) et Windows 11.

- Un seul fichier `Pointik.exe` : pas d'installeur, pas de droits administrateur, pas de runtime .NET à installer.
- Fonctionne depuis un dossier utilisateur ou une clé USB.
- Aucune télémétrie, aucun accès réseau, aucune mise à jour automatique.

---

## Utilisation

1. Copiez `Pointik.exe` où vous voulez (Bureau, Documents, clé USB) et lancez-le.
2. Une icône (anneau jaune et point bleu) apparaît dans la zone de notification, en bas à droite de la barre des tâches.
3. **Clic gauche** sur l'icône : ouvre la fenêtre de réglages.
   **Clic droit** : menu pour activer ou désactiver chaque fonction, ouvrir les réglages ou quitter.
4. Relancer `Pointik.exe` alors qu'il tourne déjà ouvre simplement ses réglages.

Si l'icône n'est pas visible, cliquez sur la flèche « ^ » de la zone de notification. Vous pouvez l'épingler par glisser-déposer.

## Raccourcis globaux

| Raccourci | Action |
|---|---|
| **Ctrl + Alt + M** | Masquer / réafficher l'anneau et le spot |
| **Ctrl + Alt + S** | Activer / couper le spot seul |
| **Ctrl + Alt + K** | Activer / couper l'affichage du clavier |
| **Ctrl + Alt + P** | Pause totale : l'affichage du clavier est coupé immédiatement et une pastille rouge « Clavier en pause » apparaît |
| **Ctrl + Alt + Z** | Entrer dans le zoom / en sortir |
| **Échap** | Quitter le zoom |
| **Ctrl + Alt + molette** | Pendant le zoom : régler le niveau. La molette n'est interceptée que dans ce cas. |
| **Ctrl + Alt + D** | Ouvrir / fermer le mode dessin |

**Modifier un raccourci :** onglet *Raccourcis* des réglages. Cliquez sur le raccourci, puis appuyez sur la nouvelle combinaison.

**Conflits signalés en rouge :**
- deux actions sur la même combinaison ;
- combinaison sans Ctrl, Alt ou Win ;
- combinaison déjà prise par une autre application ;
- **Ctrl + Alt + flèches**, refusé parce que certains pilotes graphiques s'en servent pour faire pivoter l'écran.

Au démarrage, si un raccourci **par défaut** est déjà pris par une autre application, Pointik choisit automatiquement une combinaison de repli libre (d'abord la même touche avec Maj, par exemple Ctrl + Alt + Maj + M), l'enregistre et vous le signale. Un raccourci que vous avez choisi vous-même n'est jamais remplacé : une notification signale simplement que Windows le refuse. Les notifications s'affichent en haut à droite de l'écran, dans l'overlay : elles ne prennent jamais le focus et laissent passer les clics.

Les raccourcis de Pointik eux-mêmes ne s'affichent pas dans les bulles du clavier.

## Dessin à l'écran

**Ctrl + Alt + D** (ou *Dessiner à l'écran* dans le menu de l'icône) ouvre le mode dessin sur l'écran du curseur. Une barre d'outils apparaît en haut de l'écran.

| Action | Souris ou touche |
|---|---|
| Tracer | Glisser avec le bouton gauche |
| Cadre / Ovale / Flèche / Main levée | Boutons de la barre, ou **C** / **O** / **F** / **L** |
| Carré, cercle, flèche à 45° | Maintenir **Maj** pendant le tracé |
| Couleur et épaisseur | 6 couleurs, 3 épaisseurs dans la barre |
| Annuler la dernière forme | **Ctrl + Z** |
| Tout effacer | **Suppr** |
| Quitter (les dessins sont effacés) | **Échap**, ou Ctrl + Alt + D |

**Pendant le dessin :**
- La souris sert à dessiner : les clics n'atteignent plus les applications. Quittez le mode dessin pour reprendre la main.
- L'anneau, les ondes de clic et les bulles du clavier restent affichés.
- Le dernier outil, la dernière couleur et la dernière épaisseur sont mémorisés.

**Limites :**
- **Écrans :** le dessin se fait sur un seul écran à la fois, celui du curseur à l'ouverture.
- **Avec le zoom :** dessiner pendant le zoom fonctionne, mais le dessin est agrandi avec le reste de l'écran.

## Réglages

Chaque modification s'applique immédiatement (aperçu en direct sur l'écran et dans la fenêtre) et s'enregistre automatiquement.

| Onglet | Contenu |
|---|---|
| **Curseur** | Anneau : couleur, diamètre (20 à 120 px), épaisseur, opacité, effet de pression quand un bouton est maintenu, effacement quand la souris reste immobile (3 s par défaut, réapparition au premier mouvement). Spot : rayon et niveau d'assombrissement. Le spot est indépendant de l'anneau. |
| **Clics** | Couleurs du clic gauche (bleu), droit (orange) et milieu (vert), durée (≈ 500 ms par défaut), rayon, son de clic (désactivé par défaut). Molette : petits chevrons animés au-dessus ou en dessous de l'anneau selon le sens du défilement (jusqu'à 3 quand on défile vite), couleur réglable. |
| **Clavier** | Comportement (y compris l'affichage des clics et de la molette, et le masquage dans les champs de mot de passe), position et style (voir ci-dessous). Bouton **Tester sur l'écran**, bouton **Placer à la souris…**. |
| **Thèmes** | Thèmes prédéfinis et personnels, import et export JSON (voir ci-dessous). |
| **Zoom** | Niveau (1,5× à 8×, 2× par défaut), suivi « centré sur le curseur » ou « le cadre bouge quand le curseur approche du bord », mode bascule ou « maintenir pour zoomer », durée des transitions (≈ 200 ms). |
| **Raccourcis** | Modification des raccourcis, détection des conflits. |
| **Général** | Emplacement des réglages, lancer avec Windows, confidentialité, supprimer toutes les traces. |

### Comportement de l'affichage du clavier

- **Deux modes :**
  - **Raccourcis et touches spéciales uniquement** (défaut).
  - **Tout afficher** : les caractères tapés sont regroupés sur une ligne qui se vide après 2 s de pause.
- **Format des raccourcis :** « Ctrl + Maj + S », « Alt + Tab », « Win + D ».
- **Noms des touches spéciales :** Entrée, Échap, Suppr, Retour, Tab, Espace, ↑ ↓ ← →, F1 à F12.
- **Répétitions fusionnées :** « Ctrl + Z ×3 ».
- **Disposition clavier :** celle de la fenêtre active est respectée, AZERTY compris. AltGr (@, €, #…) et les touches mortes (^ puis e = ê) sont reconnus.
- **Bulles :** 3 visibles par défaut (1 à 6), effacement après 1,5 s (réglable).
- **Clics et molette (option) :** « Clic gauche », « Ctrl + Clic gauche », « Clic gauche ×2 » pour un double-clic, « Molette ↓ ×3 ».

### Position des bulles

- **Placement :**
  - **par coin :** coin de l'écran (bas-droite par défaut), décalage X/Y ;
  - **libre :** bouton **Placer à la souris…**. Faites glisser l'aperçu, puis appuyez sur Entrée. La position est mémorisée en pourcentage de l'écran : elle résiste aux changements de résolution et de DPI.
- **Écran cible :** celui du curseur, l'écran principal, ou un écran choisi.
- **Disposition :** alignement (gauche, centre, droite) et sens d'empilement (vers le haut ou vers le bas).

### Style et thèmes

**Style réglable :**
- police (liste des polices installées), taille, gras ;
- couleur du texte, couleur et opacité du fond ;
- rayon des coins, bordure (épaisseur, couleur), ombre ;
- marges internes, espacement entre bulles, échelle globale ;
- présentation : **bulle de texte** ou **touches de clavier** (chaque touche dans sa petite touche) ;
- animations d'entrée et de sortie : fondu, glissement ou aucune.

**Thèmes :**
- **Prédéfinis :** Sombre, Clair, Contraste élevé, Discret, Touches.
- **Personnels :** retouchez un thème, puis *Enregistrer comme thème*.
- **Partage :** *Exporter* produit un fichier `.json` autonome. *Importer* vérifie le format et ramène les valeurs dans des plages sûres.

## Mode portable

- **Emplacement des réglages :** `Pointik.settings.json`, **à côté de `Pointik.exe`**.
- **Dossier en lecture seule** (clé verrouillée, dossier protégé) : repli automatique sur `%AppData%\Pointik`. Si un fichier de réglages existe déjà à côté de l'exe, il est lu comme point de départ.
- **Fichier absent ou corrompu :** des valeurs par défaut sûres sont utilisées. Un fichier corrompu est conservé sous le nom `Pointik.settings.json.corrompu`.
- **Journal d'erreurs :** `Pointik.errors.log`, dans le même dossier que les réglages. Il ne contient que des erreurs techniques, jamais de frappe, et sa taille est plafonnée à 256 Ko.
- **Lancer avec Windows :**
  - désactivé par défaut, aucune clé de registre n'est créée ;
  - une fois coché, ajoute la valeur `Pointik` dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` ;
  - décocher retire cette valeur ;
  - si vous déplacez l'exe, décochez puis recochez l'option.
- **Supprimer toutes les traces** (onglet *Général*) supprime :
  - les réglages, à côté de l'exe et dans `%AppData%\Pointik` ;
  - le journal ;
  - l'entrée de démarrage.

  Pointik se ferme ensuite sans rien réécrire.

### Bibliothèques natives extraites

L'exe contient trois bibliothèques natives, utilisées par Avalonia pour le rendu :
- `libSkiaSharp.dll` ;
- `libHarfBuzzSharp.dll` ;
- `av_libglesv2.dll` (ANGLE).

**Où et quand :** au **premier lancement** d'une version donnée, .NET les extrait dans `%TEMP%\.net\Pointik\<empreinte>\` (environ 16,6 Mo). Les lancements suivants réutilisent ce dossier.

**Nettoyage :** ce dossier temporaire n'est pas supprimé automatiquement, car les fichiers sont en cours d'utilisation pendant que Pointik tourne. Supprimez-le à la main après avoir quitté Pointik si vous voulez ne laisser aucune trace.

**Changer l'emplacement :** définissez la variable d'environnement `DOTNET_BUNDLE_EXTRACT_BASE_DIR` (par exemple vers la clé USB).

### Taille de l'exe

`dist\Pointik.exe` pèse **≈ 43 Mo** : runtime .NET 8, Avalonia et Skia inclus, compressés.

Le *trimming* (élagage du code inutilisé) n'est **pas** activé :
- Avalonia s'appuie sur la réflexion (chargement XAML, liaisons, thème Fluent) ;
- le gain (≈ 10 à 15 Mo) ne justifie pas le risque de plantages à l'exécution, impossibles à garantir sans une campagne de tests dédiée.

## Confidentialité

- Pointik **lit le clavier uniquement pour l'afficher**. Une frappe n'existe en mémoire que le temps de son affichage : elle n'est **jamais enregistrée, journalisée ni transmise**.
- Les hooks clavier et souris ne font que transmettre l'événement à l'affichage. Ils ne conservent rien.
- **Pause totale (Ctrl + Alt + P) :** le hook clavier est retiré. Le clavier n'est alors plus lu du tout, sauf pendant un zoom, pour Échap.
- **Champs de mot de passe :** quand un champ de mot de passe a le focus, l'affichage du clavier est masqué automatiquement et une pastille orange « Clavier masqué : mot de passe » apparaît (option activée par défaut, onglet *Clavier*). La détection passe par UI Automation (propriété *IsPassword*) : elle couvre les applications Windows classiques, WinForms, WPF et UWP. Juste après un changement de focus, aucune frappe n'est affichée tant que la vérification n'est pas terminée.
- **Limite de cette détection :** elle dépend de l'accessibilité exposée par l'application. Dans les navigateurs, un champ de mot de passe d'une page web n'est reconnu que si le navigateur expose son contenu à UI Automation (c'est le cas de Chrome et Edge une fois l'accessibilité activée, à vérifier sur votre machine). En cas de doute, utilisez la pause totale (Ctrl + Alt + P).
- **Mode « Tout afficher » :** il montre aussi le texte tapé. Pensez à la pause avant une saisie confidentielle qui ne serait pas un champ de mot de passe.
- **Aucun accès réseau**, aucune télémétrie, aucune mise à jour automatique.

## Avertissement SmartScreen et antivirus

`Pointik.exe` **n'est pas signé numériquement**.

**Au premier lancement**, Windows SmartScreen peut afficher « Windows a protégé votre ordinateur ». Pour lancer quand même :
1. cliquez sur **Informations complémentaires** ;
2. puis sur **Exécuter quand même**.

Si le fichier vient d'Internet, vous pouvez aussi faire *clic droit → Propriétés → cocher « Débloquer » → OK*.

**Certains antivirus peuvent signaler l'exe.** Plusieurs caractéristiques de Pointik ressemblent à celles de logiciels malveillants :
- il installe des hooks globaux clavier et souris ;
- c'est un exe autonome qui extrait des bibliothèques natives ;
- il n'a pas de signature.

C'est un **faux positif** attendu pour ce type d'outil. Marche à suivre :
1. Vérifiez l'origine du fichier : de préférence, compilez-le vous-même avec `publish.ps1`.
2. Ajoutez une exclusion pour `Pointik.exe` dans votre antivirus.
3. Signalez le faux positif à l'éditeur de l'antivirus (Microsoft : <https://www.microsoft.com/wdsi/filesubmission>).

## Limites connues du zoom

Le zoom utilise l'API Magnification de Windows (`MagSetFullscreenTransform`). Il ne demande pas de droits administrateur.

**Ce qui fonctionne :**
- **Clics :** ils tombent bien sur ce que l'on voit. Windows dessine le pointeur dans la vue agrandie, et le pointeur réel suit.
- **Remise à 1× garantie :**
  - à la fermeture ;
  - sur exception non gérée (gestionnaire global + `finally`) ;
  - à la fin de session (WM_QUERYENDSESSION) ;
  - en cas d'erreur de l'API, et le zoom est alors désactivé avec une notification.
- **Arrêt brutal :** quand le processus est tué (Gestionnaire des tâches), Windows annule lui-même la transformation. Vérifié pendant le développement.
- **Taille des overlays :** la transformation plein écran agrandit aussi les fenêtres de Pointik. L'anneau, le spot et les ondes sont donc dessinés à l'échelle 1/zoom et gardent une taille apparente constante. Les bulles du clavier, la pastille de pause et les notifications sont replacées dans la zone visible, à taille normale.

**Limites :**
- **Stylet et tactile :** la « transformation d'entrée » (`MagSetInputTransform`) exige le privilège UIAccess, donc un exe signé installé dans `Program Files`. Elle n'est pas utilisée. Les clics au stylet ou au doigt peuvent donc tomber à côté pendant le zoom. La souris n'est pas concernée.
- **Loupe Windows :** si elle est ouverte, ou si une autre loupe plein écran est déjà active, le zoom de Pointik refuse de démarrer et l'explique dans une notification.
- **Multi-écrans :** seul l'écran du curseur est zoomé. Le comportement de `MagSetFullscreenTransform` sur un écran secondaire (coordonnées négatives, DPI différent) n'a pas pu être vérifié pendant le développement, faute de second écran.
- **Fenêtre non compensée :** la fenêtre de réglages est agrandie comme le reste de l'écran.
- **Précision :** l'API n'accepte que des positions entières. À très fort grossissement, le suivi avance donc par petits sauts d'un pixel source.

## Compilation

**Prérequis :** SDK .NET 8.

```powershell
dotnet build                # compile la solution (sans avertissement)
dotnet test                 # tests unitaires du Core
.\publish.ps1               # tests + publication → dist\Pointik.exe
```

**Diagnostic :** `Pointik.exe --reglages` ouvre directement la fenêtre de réglages au démarrage.

### Architecture

| Projet | Rôle |
|---|---|
| `Pointik.Core` (net8.0) | Aucun code spécifique à un OS. Modèles de réglages, thèmes, noms français des touches, fusion « ×N », modes d'affichage, calcul de la pile de bulles, animations, logique du zoom, interfaces de plateforme (`IInputHook`, `IOverlayWindowHost`, `IZoomService`, `IScreenService`, `IHotkeyService`, `IAutostartService`, `IKeyTranslator`). Les touches sont des identifiants logiques (`KeyId`), sans code VK. |
| `Pointik.UI` (Avalonia, MVVM) | Overlays (anneau, spot, ondes, bulles), fenêtre de réglages, view-models, icône de notification. Aucun appel Win32. |
| `Pointik.Platform.Windows` | P/Invoke : hooks WH_MOUSE_LL et WH_KEYBOARD_LL sur un thread dédié, styles étendus des fenêtres, RegisterHotKey, ToUnicodeEx, API Magnification, écrans et DPI, registre. |
| `Pointik.App.Windows` | Point d'entrée : assemblage, manifeste PerMonitorV2, mutex d'instance unique, gestionnaires d'exceptions globaux. |
| `tests/Pointik.Core.Tests` | Tests xUnit du Core. |

Le portage macOS est décrit dans [`docs/MAC_PORTING.md`](docs/MAC_PORTING.md).

### Dépendances NuGet (justification)

| Paquet | Pourquoi |
|---|---|
| `Avalonia`, `Avalonia.Desktop` | Framework d'interface multiplateforme imposé : overlays, fenêtres, icône de notification. Il prépare le portage macOS. |
| `Avalonia.Themes.Fluent` | Thème des contrôles de la fenêtre de réglages. Sans lui, les contrôles n'ont aucun style. |
| *(transitifs)* SkiaSharp, HarfBuzzSharp, ANGLE, MicroCom, Tmds.DBus | Tirés par Avalonia : moteur de rendu, mise en forme du texte, accélération graphique. Tmds.DBus ne sert que sous Linux et n'est jamais chargé sous Windows. |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | Tests uniquement : jamais livrés dans l'exe. |

Volontairement évités :
- **boîte à outils MVVM :** remplacée par deux classes, `ObservableObject` et `RelayCommand` ;
- **sélecteur de couleur Avalonia :** remplacé par une palette et une saisie `#RRGGBB` ;
- **bibliothèque de journalisation et outils JSON tiers :** `System.Text.Json` intégré suffit.

### Choix techniques notables

- **Thread Win32 dédié :** les hooks, les raccourcis et le zoom tournent sur leur propre thread et leur propre boucle de messages. Un thread UI occupé ne ralentit jamais la souris. Les callbacks de hook copient l'événement, le postent au thread UI (déplacements fusionnés) et appellent toujours `CallNextHookEx`. Les delegates sont gardés en référence statique.
- **Hooks à la demande :** aucun hook souris si aucune fonction ne suit la souris, aucun hook clavier en pause.
- **Composition Win32 « surface de redirection » :** le CPU reste à ≈ 0,5 % d'un cœur au repos, contre 4 à 5 % en composition DirectComposition/WinUI, dont la minuterie calée sur la synchro verticale ne s'arrête jamais. Le rendu n'a lieu que sur un événement ou pendant une animation.
- **Fenêtres overlay :** `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, toujours au premier plan, une par écran. Elles reprennent le dessus à chaque changement de fenêtre active (hook WinEvent hors contexte, sans injection de DLL).
- **Traduction des touches :** `ToUnicodeEx` est appelé avec le drapeau « ne pas modifier l'état du clavier » (Windows 10 1607+), pour ne jamais casser les touches mortes de l'application en cours.

---

## 15 vérifications manuelles

À faire sur la machine cible, avec l'exe publié (`dist\Pointik.exe`).

1. **Multi-écrans** : brancher deux écrans, dont un placé à gauche ou au-dessus du principal (coordonnées négatives). L'anneau suit le curseur sur les deux écrans sans saut, y compris à cheval sur la jonction. Le spot assombrit les deux écrans.
2. **DPI 125 %** : écran réglé à 125 %. L'anneau est centré exactement sur la pointe du curseur, et sa taille correspond au réglage. Les bulles restent au bon coin.
3. **DPI 150 % et DPI mixtes** : un écran à 100 %, l'autre à 150 %. Passer de l'un à l'autre : anneau centré et taille apparente cohérente. Les bulles « écran du curseur » changent d'écran sans être coupées.
4. **AZERTY** : dans le Bloc-notes, mode « Tout afficher ». Taper « Être à l'été ? » : accents, ^ + E = Ê, Maj et AltGr + à = @ s'affichent correctement. L'application reçoit le même texte, sans touche morte doublée.
5. **Ctrl + C** : en mode par défaut, Ctrl + C affiche « Ctrl + C ». Trois Ctrl + Z rapides affichent « Ctrl + Z ×3 ». Taper du texte simple n'affiche rien.
6. **Pause** : Ctrl + Alt + P. Les bulles disparaissent immédiatement et la pastille rouge « Clavier en pause » apparaît. Plus aucune frappe n'est affichée. Ctrl + Alt + P à nouveau rétablit l'affichage.
7. **Clics gauche, droit et milieu** : chaque clic produit une onde de sa couleur (bleu, orange, vert) qui s'efface en ≈ 0,5 s. Bouton maintenu : l'anneau se resserre. Le clic atteint toujours l'application sous le curseur (overlay traversable).
8. **Plein écran** : vidéo plein écran (navigateur, VLC) et diaporama PowerPoint. Les effets restent visibles, la vidéo ne saccade pas, aucun vol de focus (le diaporama continue de recevoir les touches).
9. **Zoom avec clics** : Ctrl + Alt + Z, transition douce. Cliquer sur un bouton dans la vue agrandie : le clic touche bien le bouton visé. Ctrl + Alt + molette change le niveau. Échap quitte. Tester les deux modes de suivi et le mode « maintenir ».
10. **Sortie du zoom après un plantage simulé** : zoomer, puis tuer `Pointik.exe` dans le Gestionnaire des tâches (*Fin de tâche*). L'écran doit revenir immédiatement à 1×. Recommencer en fermant la session pendant le zoom.
11. **Dossier en lecture seule** : copier l'exe dans un dossier sans droit d'écriture (ou une clé verrouillée). Modifier un réglage : `%AppData%\Pointik\Pointik.settings.json` est créé, rien n'est écrit à côté de l'exe. L'onglet Général l'indique.
12. **Import et export d'un thème** : exporter le thème « Touches », le modifier dans un éditeur (couleur du fond), le réimporter. Il apparaît dans la liste et s'applique. Importer un fichier JSON quelconque : message d'erreur clair, aucun plantage.
13. **Mode placement** : « Placer à la souris… », déplacer l'aperçu au centre-haut de l'écran, Entrée. Les bulles s'affichent à cet endroit. Changer la résolution ou le DPI : la position relative est conservée.
14. **Capture vidéo** : OBS (capture d'écran *et* capture de fenêtre) et la Xbox Game Bar (Win + G). L'anneau, les ondes et les bulles apparaissent-ils dans l'enregistrement, **y compris pendant le zoom** ? Noter le résultat pour chaque mode de capture.
15. **Propreté** : « Lancer avec Windows » cocher puis décocher (vérifier `HKCU\…\Run` avec regedit). Puis « Supprimer toutes les traces » : plus de fichier de réglages, plus de journal, plus d'entrée Run. Quitter : l'icône disparaît, le Gestionnaire des tâches n'affiche plus Pointik, le nombre de handles est revenu à la normale.
