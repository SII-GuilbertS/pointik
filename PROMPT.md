Tu es un développeur senior spécialisé en applications de bureau .NET, avec une bonne maîtrise des API Windows natives. Crée de zéro une application gratuite nommée provisoirement « Pointik », qui met en valeur la souris et le clavier pour des démonstrations en classe, des présentations et des tutoriels vidéo : anneau et spot autour du curseur, animation au clic, zoom d'écran, et affichage des touches du clavier.

Écris une implémentation originale. Ne copie ni le code ni l'identité visuelle d'une application existante.

La V1 cible Windows 10 (2004 et plus) et Windows 11. Un portage macOS est prévu plus tard : l'architecture doit le préparer sans l'implémenter.

## Contraintes générales
- **Application portable et autonome** : un seul fichier `Pointik.exe`, sans installeur, sans droits administrateur, sans runtime à installer, sans clé de registre par défaut. Elle doit fonctionner depuis un dossier utilisateur ou une clé USB.
- Interface en français.
- Aucune télémétrie, aucun accès réseau, aucune mise à jour automatique.
- Aucune frappe n'est jamais stockée, journalisée ni transmise : elle n'existe en mémoire que le temps de l'affichage.

## Stack et architecture (prête pour macOS)
- C# 12, .NET 8, **Avalonia UI** (MVVM) pour toute l'interface et les overlays. Évite toute autre dépendance NuGet et justifie dans le README celles qui restent.
- Solution en 4 projets :
  1. `Pointik.Core` (net8.0, **aucun code spécifique à un OS**, doit compiler avec `dotnet build` sur n'importe quel système) : modèles de réglages, thèmes, formatage des touches (noms français, fusion des répétitions, modes d'affichage), logique d'animation et de temporisation, et les **interfaces de plateforme** : `IInputHook`, `IOverlayWindowHost`, `IZoomService`, `IScreenService`, `IHotkeyService`, `IAutostartService`, `IKeyTranslator`.
  2. `Pointik.UI` (Avalonia) : vues d'overlay (anneau, spot, onde de clic, bulles clavier), fenêtre de réglages, view-models. Aucun appel Win32 ici.
  3. `Pointik.Platform.Windows` : implémentations des interfaces par P/Invoke (SetWindowsHookEx WH_MOUSE_LL et WH_KEYBOARD_LL, styles étendus de fenêtre, RegisterHotKey, ToUnicodeEx, API Magnification, énumération des écrans et du DPI).
  4. `Pointik.App.Windows` : point d'entrée qui assemble le tout, avec manifeste PerMonitorV2 et mutex d'instance unique.
- Les touches sont représentées dans le Core par un **identifiant logique indépendant de l'OS** (pas de codes VK dans le Core). La touche « Win » est nommée par la plateforme (« Win » sur Windows, « ⌘ » sur Mac).
- Écris `docs/MAC_PORTING.md` : pour chaque interface, l'équivalent macOS pressenti (CGEventTap, fenêtre qui ignore les événements souris et niveau de fenêtre élevé, capture ou zoom d'écran, NSStatusItem, SMAppService, UCKeyTranslate), les autorisations macOS nécessaires (Accessibilité, Surveillance de l'entrée, Enregistrement de l'écran) et les différences d'usage (⌘, ⌥, raccourcis par défaut). Pas de code Mac en V1.
- Tests unitaires xUnit sur le Core : nommage des touches, fusion « ×3 », modes d'affichage, sérialisation des thèmes.

## Fonctions de la V1

### 1. Anneau de curseur
Cercle coloré autour du pointeur. Couleur, taille (20 à 120 px), épaisseur et opacité réglables.

### 2. Spot (mode projecteur)
Assombrit tout l'écran sauf un disque autour du curseur. Rayon et niveau d'assombrissement réglables. Activable indépendamment de l'anneau.

### 3. Animation au clic
Onde (ripple) qui part du point de clic et s'estompe en ~500 ms. Couleurs distinctes pour le clic gauche (bleu), droit (orange) et milieu. Option : léger effet de pression sur l'anneau tant que le bouton est maintenu. Son de clic optionnel, désactivé par défaut.

### 4. Zoom d'écran (dans la V1)
- Zoom **en direct** sur l'écran où se trouve le curseur, qui suit le curseur de façon fluide. Raccourci Ctrl+Alt+Z pour activer ou quitter. Échap quitte aussi le zoom.
- Niveaux de 1,5× à 8× (2× par défaut). Ctrl+Alt+molette ajuste le niveau (l'événement molette n'est consommé que dans ce cas, jamais autrement).
- Transitions animées à l'entrée et à la sortie (~200 ms). Deux modes de suivi au choix : « centré sur le curseur » ou « le cadre ne bouge que lorsque le curseur approche du bord ». Option « maintenir pour zoomer » en plus du mode bascule.
- Implémentation recommandée : API Magnification de Windows (transformation plein écran + transformation d'entrée), afin que les clics tombent bien sur ce que l'on voit. Vérifie que cela fonctionne sans droits administrateur.
- **Sécurité critique** : le zoom doit TOUJOURS être remis à 1× à la fermeture, en cas d'exception non gérée et à l'arrêt de la session, sinon l'écran reste zoomé. Prévois un try/finally, un gestionnaire d'exceptions global et un arrêt propre.
- Si le zoom est impossible (autre loupe active, échec de l'API), affiche une notification claire et désactive la fonction sans planter.
- Pendant le zoom, l'anneau, les ondes de clic et les bulles clavier doivent rester lisibles. Vérifie si la transformation plein écran agrandit aussi les overlays ; si oui, applique une mise à l'échelle inverse (1/zoom) pour garder une taille apparente constante, ou documente précisément la limite.

### 5. Affichage du clavier (position et style entièrement personnalisables)
Comportement :
- Bulles empilées, 3 visibles par défaut (réglable de 1 à 6), fondu de sortie après ~1,5 s (réglable).
- Raccourcis affichés « Ctrl + Maj + S », « Alt + Tab », « Win + D ». Touches spéciales en français : Entrée, Échap, Suppr, Retour, Tab, Espace, ↑ ↓ ← →, F1–F12.
- Une touche répétée est fusionnée : « Ctrl + Z ×3 ».
- Deux modes : « Raccourcis et touches spéciales uniquement » (défaut) et « Tout afficher » (les caractères tapés sont regroupés sur une ligne qui se vide après 2 s de pause). Respecte la disposition AZERTY via ToUnicodeEx.

**Position (réglable)** :
- Coin de l'écran (4 coins, **bas-droite par défaut**), écran cible (celui du curseur, l'écran principal, ou un écran choisi), décalage X/Y en px, alignement (gauche, centre, droite), sens d'empilement (vers le haut ou vers le bas).
- **Mode placement** : un bouton affiche un aperçu que l'on déplace à la souris. La position est mémorisée en pourcentage de l'écran pour résister aux changements de résolution et de DPI.

**Style (réglable)** :
- Police (liste des polices installées), taille, gras, couleur du texte, couleur et opacité du fond, rayon des coins, bordure (épaisseur, couleur), ombre, marges internes, espacement entre bulles, échelle globale en %.
- Deux présentations : « bulle de texte » (« Ctrl + Maj + S ») ou « touches » (chaque touche dans sa propre petite touche de clavier).
- Animation d'entrée et de sortie : fondu, glissement ou aucune.
- Thèmes prédéfinis : Sombre, Clair, Contraste élevé, Discret, Touches. Thèmes personnels enregistrables, avec import et export en fichier JSON.
- Aperçu en direct dans la fenêtre de réglages, avec un bouton « Tester ».

### 6. Confidentialité
Raccourci global « Pause totale » (Ctrl+Alt+P) qui coupe immédiatement l'affichage du clavier, avec une petite pastille rouge visible pendant la pause. Les hooks ne doivent rien enregistrer.

### 7. Raccourcis globaux (modifiables, avec détection des conflits)
Ctrl+Alt+M anneau et spot, Ctrl+Alt+S spot seul, Ctrl+Alt+K clavier, Ctrl+Alt+P pause totale, Ctrl+Alt+Z zoom. N'utilise pas Ctrl+Alt+flèches (conflit avec la rotation d'écran de certains pilotes graphiques).

### 8. Icône de notification et réglages
Icône dans la zone de notification avec menu : activer ou désactiver chaque fonction, ouvrir les réglages, quitter. Fenêtre de réglages avec aperçu en direct.

### 9. Mode portable
- Les réglages sont dans `Pointik.settings.json` **à côté de l'exe**. Si le dossier est en lecture seule, repli sur `%AppData%\Pointik`. Valeurs par défaut sûres si le fichier est absent ou corrompu.
- « Lancer avec Windows » : option désactivée par défaut, qui écrit dans HKCU\...\Run et se retire proprement quand on la décoche.
- Bouton « Supprimer toutes les traces » (réglages, entrée de démarrage).
- Publication : `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`. Si Avalonia ou SkiaSharp extraient des bibliothèques natives, indique dans le README où et quand. N'active le trimming que s'il est démontré sans risque.

## Hors périmètre V1
Code macOS (seulement l'architecture et `MAC_PORTING.md`), dessin et annotation à l'écran, enregistrement vidéo, signature de code.

## Qualité attendue
- Une fenêtre overlay par écran : transparente, sans bordure, toujours au premier plan, hors Alt+Tab, non activable et traversable aux clics (WS_EX_TRANSPARENT, WS_EX_LAYERED, WS_EX_TOOLWINDOW, WS_EX_NOACTIVATE). Multi-écrans, coordonnées négatives et DPI mixtes gérés.
- Les callbacks de hook ne font que poster les événements vers le thread UI (Channel ou Dispatcher), appellent toujours CallNextHookEx et gardent une référence statique aux delegates.
- CPU quasi nul au repos. Aucun vol de focus. Pas de perturbation des vidéos plein écran ni des logiciels de capture. Vérifie et documente si OBS et Win+G capturent bien les effets, y compris pendant le zoom.
- Aucune fuite de handles. Désinstallation propre des hooks, des raccourcis et du zoom à la fermeture.
- Code commenté en français, noms de classes en anglais. Erreurs gérées sans plantage, journal d'erreurs local sans aucune frappe.

## Livrables
1. Solution complète qui compile avec `dotnet build` sans avertissement bloquant, tests unitaires du Core qui passent.
2. `publish.ps1` qui produit `dist/Pointik.exe`.
3. `README.md` en français : utilisation, raccourcis, réglages, thèmes, mode portable, taille de l'exe, confidentialité (l'appli lit le clavier uniquement pour l'afficher et ne l'enregistre pas), avertissement SmartScreen et antivirus attendu pour un exe non signé et marche à suivre, limites connues du zoom.
4. `docs/MAC_PORTING.md`.
5. Une liste de 15 vérifications manuelles dans le README : multi-écrans, DPI 125 % et 150 %, AZERTY, Ctrl+C, pause, clics gauche, droit et milieu, plein écran, zoom avec clics, sortie du zoom après un plantage simulé, dossier en lecture seule, import et export d'un thème, mode placement.

## Méthode de travail
Commence par un court plan. Construis par étapes en compilant et en lançant ce qui peut l'être à chaque fois : (1) structure de la solution et interfaces du Core, (2) overlay transparent traversable qui suit la souris, (3) anneau, spot et clics, (4) clavier avec position et styles, (5) zoom, (6) réglages, thèmes et icône de notification, (7) mode portable et publication, (8) tests, README et MAC_PORTING. À la fin, liste précisément ce que tu n'as pas pu vérifier toi-même (rendu réel, multi-écrans, DPI mixtes, antivirus, capture par OBS) pour que je le teste.