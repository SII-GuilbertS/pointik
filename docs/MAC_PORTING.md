# Portage macOS de Pointik

La V1 ne contient **aucun code macOS**. Ce document décrit comment l'architecture prépare le portage et ce qu'il faudra écrire.

## Ce qui est déjà portable

| Projet | État pour macOS |
|---|---|
| `Pointik.Core` (net8.0) | Compile tel quel. Il contient toute la logique : réglages, thèmes, noms des touches, fusion « ×N », modes d'affichage, pile de bulles, ondes, contrôleur de zoom, détection des conflits de raccourcis. Les touches y sont des identifiants logiques (`KeyId`, `KeyModifiers.Meta`) : aucun code VK. |
| `Pointik.UI` (Avalonia) | Multiplateforme : overlays dessinés dans `Render`, fenêtre de réglages, icône de notification (`TrayIcon` d'Avalonia = `NSStatusItem` sur macOS). Aucun appel natif. |
| Tests | Exécutables sur macOS (`dotnet test`). |

**À créer :**
- **`Pointik.Platform.Mac` :** implémentations des interfaces ci-dessous, par P/Invoke vers les frameworks Apple ou via des liaisons Objective-C, par exemple `ObjCRuntime` léger ou Xamarin.Mac / .NET for macOS.
- **`Pointik.App.Mac` :** point d'entrée, `Info.plist`, bundle `.app`, instance unique.

**Point d'entrée :** il construit un `PlatformServices`, comme `WindowsPlatform.Create`, et le passe à `PointikApp`.

## Équivalents des interfaces

| Interface (Core) | Windows (V1) | macOS (pressenti) |
|---|---|---|
| `IInputHook` | `SetWindowsHookEx` WH_MOUSE_LL et WH_KEYBOARD_LL sur un thread dédié | **`CGEventTapCreate`** (`kCGSessionEventTap`, `kCGHeadInsertEventTap`) pour `mouseMoved`, `left/right/otherMouseDown/Up`, `scrollWheel`, `keyDown/Up`, `flagsChanged`, ajouté à un `CFRunLoop` sur un thread dédié. Tap en mode *listen-only* (`kCGEventTapOptionListenOnly`), sauf pour la molette avec ⌃⌥ pendant le zoom, où un tap actif renvoie `NULL` pour consommer l'événement. Réactiver le tap sur `kCGEventTapDisabledByTimeout`. |
| `IKeyTranslator` | `ToUnicodeEx` avec drapeau « sans changement d'état », `MapVirtualKeyEx`, composition des touches mortes | **`UCKeyTranslate`** avec la disposition courante (`TISCopyCurrentKeyboardLayoutInputSource` → `kTISPropertyUnicodeKeyLayoutData`) et un `deadKeyState` **local** au traducteur, pour ne pas perturber l'application. Correspondance `kVK_*` → `KeyId`. `MetaKeyName = "⌘"`, `AltKeyName = "⌥"`. Ctrl s'affichera « ⌃ » et Maj « ⇧ » : il faudra un `KeyNames` macOS. |
| `IOverlayWindowHost` | `WS_EX_LAYERED \| TRANSPARENT \| TOOLWINDOW \| NOACTIVATE`, `HWND_TOPMOST`, hook WinEvent pour reprendre le dessus | `NSWindow` (handle Avalonia) : `ignoresMouseEvents = YES`, `level = NSScreenSaverWindowLevel` (ou `CGShieldingWindowLevel()` pour passer au-dessus du plein écran), `collectionBehavior = canJoinAllSpaces \| fullScreenAuxiliary \| stationary \| ignoresCycle`, `hasShadow = NO`, `opaque = NO`. Utiliser un `NSPanel` *non-activating* pour ne jamais prendre le focus. |
| `IScreenService` | `EnumDisplayMonitors`, `GetDpiForMonitor`, `WM_DISPLAYCHANGE` | `NSScreen.screens` (`frame`, `visibleFrame`, `backingScaleFactor`), `NSApplicationDidChangeScreenParametersNotification`. **Attention :** l'origine de Cocoa est **en bas à gauche** et en points ; convertir en pixels « bureau virtuel » avec l'origine en haut à gauche avant de remplir `ScreenInfo`. |
| `IZoomService` | API Magnification (`MagSetFullscreenTransform`) | Pas d'équivalent public au zoom système. Deux options : **(a)** capturer l'écran (**ScreenCaptureKit**, `SCStream` à 60 i/s) et l'afficher agrandi dans une fenêtre overlay plein écran au-dessus de tout. Les clics traversent cette fenêtre et tombent **sous** la vue : il faudra déplacer le pointeur réel (`CGWarpMouseCursorPosition`) pour qu'il corresponde à ce qui est vu, ou documenter que le zoom est une « loupe de présentation » sans clic. **(b)** Piloter le Zoom d'accessibilité de macOS (raccourci ⌥⌘8), sans contrôle fin. Recommandation : (a). `OverlaysAreMagnified = false` : la logique de compensation 1/zoom de l'UI est alors désactivée. |
| `IHotkeyService` | `RegisterHotKey` | **`RegisterEventHotKey`** (Carbon, toujours disponible et sans autorisation), ou détection dans l'event tap. |
| `IAutostartService` | `HKCU\…\Run` | **`SMAppService.mainApp.register()`** / `unregister()` (macOS 13+). Repli : `LaunchAgent` dans `~/Library/LaunchAgents`, à supprimer dans « Supprimer toutes les traces ». |
| `ISecureInputDetector` | UI Automation (`IsPassword` de l'élément focalisé), réveillé par un hook WinEvent `EVENT_OBJECT_FOCUS` | **`IsSecureEventInputEnabled()`** (Carbon) : macOS active la « saisie sécurisée » dans tout champ de mot de passe, ce qui empêche d'ailleurs l'event tap de lire les frappes. Interroger cet état à chaque `keyDown` et sur `NSWorkspaceDidActivateApplicationNotification`. Repli : attribut d'accessibilité `AXSubrole == AXSecureTextField` de l'élément focalisé (`AXUIElementCopyAttributeValue`, autorisation Accessibilité). |
| `ISoundPlayer` | `PlaySound` en mémoire | `AVAudioPlayer(data:)` ou `NSSound(data:)` avec le WAV généré par `ClickSoundGenerator` (Core). |
| `IUiDispatcher` | Dispatcher Avalonia | Identique (fourni par l'UI). |

## Autorisations macOS nécessaires

| Autorisation (Réglages Système → Confidentialité et sécurité) | Pourquoi | Comment la demander |
|---|---|---|
| **Surveillance de l'entrée** (*Input Monitoring*) | Event tap clavier en écoute | `CGPreflightListenEventAccess()` / `CGRequestListenEventAccess()` |
| **Accessibilité** | Event tap *actif*, pour consommer ⌃⌥+molette pendant le zoom et déplacer le pointeur | `AXIsProcessTrustedWithOptions` avec `kAXTrustedCheckOptionPrompt` |
| **Enregistrement de l'écran** (*Screen Recording*) | Zoom par capture (ScreenCaptureKit) | `CGPreflightScreenCaptureAccess()` / `CGRequestScreenCaptureAccess()` |

À prévoir dans l'UI :
- **Premier lancement :** un écran d'accueil qui explique chaque autorisation et ouvre le bon panneau (`x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent`, etc.).
- **Fonctions sans autorisation :** elles se désactivent proprement, comme le zoom aujourd'hui quand l'API échoue.
- **Changement d'autorisation :** il exige souvent de relancer l'application, d'où un bouton « Relancer ».

L'application devra être **signée et notarisée** (Developer ID) pour que les autorisations soient conservées entre les versions et que Gatekeeper l'accepte. La signature est hors périmètre de la V1.

## Différences d'usage

| Sujet | Windows | macOS |
|---|---|---|
| Touche système | Win | ⌘ (Commande) |
| Alt | Alt / AltGr | ⌥ (Option) : sert aussi à taper des caractères (comme AltGr). La règle « AltGr = texte » doit devenir « ⌥ sans ⌘ ni ⌃ = texte ». |
| Ordre d'affichage | Win + Ctrl + Alt + Maj + touche | Convention Apple : **⌃ ⌥ ⇧ ⌘** puis la touche, souvent sans « + » (« ⌃⌥⇧⌘Z »). Prévoir un mode d'affichage « symboles » dans `KeyNames`. |
| Raccourcis par défaut | Ctrl + Alt + M / S / K / P / Z | Proposition : **⌃⌥ M / S / K / P / Z**. ⌘⌥ est très utilisé par les applications, et ⌃⌥ correspond à la V1 Windows. |
| Quitter le zoom | Échap | Échap (identique) |
| Molette + modificateurs | Ctrl + Alt + molette | ⌃ ⌥ + molette. ⌃ + molette est déjà le zoom d'accessibilité s'il est activé : à détecter et signaler. |
| Emplacement des réglages | À côté de l'exe, repli `%AppData%\Pointik` | Un bundle `.app` signé ne doit pas être modifié : réglages dans `~/Library/Application Support/Pointik`. Le « mode portable » n'a pas de sens sur macOS. |
| Lancement automatique | Clé `Run` | `SMAppService` (visible dans Réglages → Général → Ouverture) |
| Touche Supprimer | Retour / Suppr | ⌫ / ⌦ ; Entrée = ↩ |

## Points d'attention

1. **Coordonnées.** Le Core attend des pixels physiques, origine en haut à gauche, axe Y vers le bas. Sur macOS : conversion depuis les points Cocoa (Y vers le haut, `backingScaleFactor`) dans `IScreenService` **et** dans l'event tap (`CGEventGetLocation` est en points, origine en haut à gauche de l'écran principal).
2. **Plein écran et Spaces.** Les overlays doivent suivre les Spaces (`canJoinAllSpaces`) et apparaître au-dessus des applications en plein écran (`fullScreenAuxiliary`). À tester avec Keynote.
3. **Capture vidéo.** `NSWindow.sharingType` doit rester `.readOnly` (défaut) pour que les effets apparaissent dans les enregistrements.
4. **Rendu Avalonia transparent.** Sur macOS, `TransparencyLevelHint = Transparent` est pris en charge. Vérifier le CPU au repos, comme sous Windows.
5. **Tests.** Les tests du Core restent la référence. Ajouter un `KeyNames` macOS et ses tests (symboles ⌃⌥⇧⌘).
