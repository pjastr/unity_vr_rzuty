# VR Rzuty – kosz i kręgle na Meta Quest 3S (Unity 6.3 LTS)

Projekt pokazuje, **jak działa rzucanie w VR**: chwytanie obiektów kontrolerem, wyznaczanie prędkości w chwili puszczenia i fizykę lotu. Są dwa stanowiska:

- **KOSZ:** boisko z koszem w wymiarach FIBA (obręcz na 3,05 m, średnica 45,7 cm), stojak z 5 piłkami, wykrywanie trafień (czyste, z tablicy, za 3 punkty), seria i celność. Tryby: **Trening** oraz **Wyzwanie 60 s** z rekordem.
- **KRĘGLE:** regulaminowy tor (18,29 m od linii spalonego do pierwszego kręgla), 10 kręgli z fizyką, 3 kule o różnej masie, linia spalonego, maszyna ustawiająca i **pełna punktacja 10 ramek** (strike, spare, dodatkowe rzuty w 10. ramce) z rekordem.

Najważniejsze funkcje:
- **Trzy metody liczenia prędkości rzutu:** `OVRInput` (prędkość z systemu śledzenia), historia ruchu z ostatnich 0,1 s i ostatnia klatka. Po każdym rzucie wyświetlacz nad lewym kontrolerem pokazuje wynik wszystkich trzech, więc łatwo je porównać.
- **Opcje rzutu:** mnożnik siły, pomoc w celowaniu (rozwiązanie rzutu ukośnego), podgląd toru lotu, spin z obrotu nadgarstka, chwytanie „przytrzymaj / przełącz”, **grawitacja Ziemi, Marsa albo Księżyca**.
- **Opcje stanowisk:** wysokość kosza (3,05 / 2,60 / 2,00 m), odległość rzutu (2,0 / 4,2 / 6,75 m), długość toru (18,29 / 12 / 8 m), bandy.
- **Wejście: wyłącznie kontrolery Touch przez `OVRInput`**. Śledzenie dłoni jest wyłączone (*Controllers Only*).
- **Grafika: tylko prymitywy Unity** i panele UI. Nie ma modeli, tekstur ani plików dźwiękowych, bo dźwięki (odbicie, obręcz, kręgle, toczenie kuli) są syntetyzowane w kodzie.
- **Z Meta XR SDK** używane są tylko `OVRCameraRig`, `OVRManager` i `OVRInput` (pakiet *Meta XR Core SDK*).
- **Scena powstaje automatycznie** z menu `VR Rzuty`, a **Panel kontrolny** pokazuje, co jeszcze trzeba ustawić i ma zakładkę z optymalizacją buildu.
- **Uruchamianie: Build & Run albo Patch & Run przez USB.**

---

## Spis treści

1. [Wymagania](#1-wymagania)
2. [Zawartość projektu](#2-zawartość-projektu)
3. [Pierwsze uruchomienie](#3-pierwsze-uruchomienie)
4. [Menu „VR Rzuty” i Panel kontrolny](#4-menu-vr-rzuty-i-panel-kontrolny)
5. [Build & Run przez USB](#5-build--run-przez-usb)
6. [Optymalizacja buildu: profile DEV i RELEASE](#6-optymalizacja-buildu-profile-dev-i-release)
7. [Debugowanie przez USB](#7-debugowanie-przez-usb)
8. [Instrukcja obsługi w goglach](#8-instrukcja-obsługi-w-goglach)
9. [Jak to działa](#9-jak-to-działa)
10. [Dostrajanie i rozbudowa](#10-dostrajanie-i-rozbudowa)
11. [Rozwiązywanie problemów](#11-rozwiązywanie-problemów)

---

## 1. Wymagania

| Element | Wersja / uwagi |
|---|---|
| Unity | **6000.3.24f1** (Unity 6.3 LTS) z modułem **Android Build Support** (razem z *OpenJDK* i *Android SDK & NDK Tools*). Moduł dodasz w Unity Hub › Installs › ⚙ › Add modules. |
| Gogle | Meta Quest 3S z aktualnym Horizon OS i **włączonym trybem deweloperskim** |
| Kabel | USB-C **z transmisją danych** (sam kabel do ładowania nie wystarczy) |

Pakiety z `Packages/manifest.json` instalują się same przy pierwszym otwarciu projektu:

| Pakiet | Wersja | Rola |
|---|---|---|
| `com.meta.xr.sdk.core` | 207.0.1 | `OVRCameraRig`, `OVRManager`, **`OVRInput`** |
| `com.unity.xr.openxr` | 1.18.0 | Backend OpenXR (zalecany przez Meta) |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP |
| `com.unity.inputsystem` | 1.18.0 | Zależność OpenXR. Projekt go nie używa, bo całe wejście idzie przez `OVRInput`. |
| `com.unity.ugui` | 2.0.0 | Menu, tablice wyników i napisy (Canvas + Text) |

**Interaction SDK** (`com.meta.xr.sdk.interaction.ovr`) nie jest potrzebne. Możesz je dodać, ale wydłuży build.

> Jeśli Package Manager zgłosi brak którejś wersji, wybierz w **Window › Package Manager** wersję proponowaną dla Twojego edytora.

---

## 2. Zawartość projektu

```
unity_vr_rzuty/
├─ INSTRUKCJA.md                          ← ten plik
├─ README.md
├─ Packages/manifest.json                 ← lista pakietów
├─ ProjectSettings/ProjectVersion.txt     ← 6000.3.24f1 (resztę ustawień Unity tworzy sam)
└─ Assets/VRThrowing/
   ├─ Editor/
   │  ├─ VRThrowingSetup.cs               ← menu „VR Rzuty”, konfiguracja projektu, lista kontrolna, pierwszy start
   │  ├─ VRThrowingSceneBuilder.cs        ← generator sceny: boisko z koszem, hala z torem, stojaki, tablice, menu, gracz
   │  ├─ VRThrowingBuild.cs               ← profile DEV/RELEASE, Build/Patch And Run, narzędzia adb
   │  └─ VRThrowingWindow.cs              ← Panel kontrolny (3 zakładki)
   ├─ Scripts/
   │  ├─ ThrowSettings.cs                 ← wszystkie ustawienia, presety, zapis w goglach
   │  ├─ Grabber.cs                       ← chwytanie, 3 metody prędkości rzutu, podgląd toru lotu
   │  ├─ Throwable.cs                     ← piłka: stojak / w ręce / w locie, automatyczny powrót
   │  ├─ AimAssist.cs                     ← pomoc w celowaniu (rzut ukośny, kierunek toru)
   │  ├─ ThrowPhysics.cs                  ← krok fizyki 90 Hz, solver, grawitacja z ustawień
   │  ├─ BasketballGame.cs                ← trafienia, trening, wyzwanie 60 s, wysokość i odległość kosza
   │  ├─ RimPart.cs, Backboard.cs         ← znaczniki obręczy i tablicy
   │  ├─ BowlingLane.cs                   ← przebieg rzutu, liczenie kręgli, spalony, ustawianie, tablica
   │  ├─ BowlingScore.cs                  ← punktacja 10 ramek (czysta logika, bez Unity)
   │  ├─ BowlingPin.cs, LaneStretch.cs    ← kręgiel, rozciąganie toru przy zmianie długości
   │  ├─ PlayerMover.cs                   ← krok gałką, obrót 45°, przejście między stanowiskami
   │  ├─ ThrowMenu.cs, UiPointer.cs, MenuButton.cs ← menu w goglach + skróty A/B/X/Y
   │  ├─ WristHud.cs, Toast.cs            ← wyświetlacz nad lewym kontrolerem, komunikaty
   │  ├─ ImpactSound.cs, Sfx.cs           ← dźwięki zderzeń i toczenia (synteza)
   │  ├─ ScreenFader.cs, Haptics.cs       ← mrugnięcie przy przejściu, wibracje
   │  ├─ ControllerFollower.cs, PlayerRig.cs ← modele kontrolerów, dostęp do rigu
   ├─ Scenes/VRRzuty.unity                ← tworzona przez menu (krok 2)
   ├─ Materials/*.mat                     ← tworzone automatycznie (URP Lit / Unlit)
   ├─ Physics/*.physicMaterial            ← materiały fizyczne: odbicie, tarcie (tworzone automatycznie)
   └─ Settings/VRRzuty_URP*.asset         ← tworzone automatycznie, jeśli projekt nie ma URP
```

---

## 3. Pierwsze uruchomienie

### Krok 1: otwarcie projektu
1. Unity Hub › **Add › Add project from disk** › wskaż folder `unity_vr_rzuty`.
2. Otwórz projekt w **6000.3.24f1**. Pierwszy import trwa kilka minut.
3. Mogą pojawić się okna Meta SDK (telemetria, propozycja restartu z nowym systemem wejścia). Zaakceptuj domyślne odpowiedzi; restart edytora jest normalny.

### Krok 2: Szybki start (automatycznie)
Po imporcie pojawi się okno **VR Rzuty** z przyciskiem **Szybki start**. To samo jest w górnym menu: **VR Rzuty › 0. Szybki start (konfiguracja + scena)**.

| Co | Ustawienie |
|---|---|
| Player (Android) | IL2CPP, **ARM64**, min. API 32, **Vulkan**, Linear, orientacja pozioma, `com.vrrzuty.quest` |
| Grafika | **URP**: MSAA 4×, bez HDR, bez cieni i świateł dodatkowych |
| XR | **XR Plug-in Management › Android › OpenXR** + **Meta XR Feature** + profile kontrolerów Touch |
| Meta | **Hand Tracking Support = Controllers Only** |
| Build | profil **DEV** (szybki Build & Run, IL2CPP „Faster (smaller) builds”, Development + Script Debugging) |
| Scena | `Assets/VRThrowing/Scenes/VRRzuty.unity`: boisko z koszem i tor do kręgli, **OVRCameraRig**, **Tracking Origin = Floor Level**, scena w buildzie |

Na koniec pojawia się propozycja **przełączenia platformy na Android**. Zgódź się; trwa to kilka minut i robisz to tylko raz.

### Krok 3: Project Setup Tool (jedyny obowiązkowy krok ręczny)
**Meta › Tools › Project Setup Tool** (albo przycisk w Panelu kontrolnym). Na zakładce **Android** kliknij **Fix All**, potem **Apply All**, i powtarzaj, aż znikną pozycje *Required*.

### Krok 4: sprawdzenie
**VR Rzuty › Panel kontrolny** › zakładka **1. Kontrola projektu**. Wszystkie punkty powinny mieć status `[OK]`; przy błędnych jest przycisk naprawy.

> **Building Blocks nie są potrzebne.** Generator wstawia prefab `OVRCameraRig` (to samo, co blok *Camera Rig*). Bloku *Controller Tracking* nie dodawaj, bo kontrolery mają własne schematyczne modele z prymitywów.

---

## 4. Menu „VR Rzuty” i Panel kontrolny

### Menu w górnym pasku

| Pozycja | Działanie |
|---|---|
| **0. Szybki start (konfiguracja + scena)** | punkty 1 i 2 naraz, a potem propozycja przełączenia na Android |
| **1. Konfiguruj projekt dla Quest 3S** | same ustawienia projektu (tabela w kroku 2) |
| **2. Generuj scenę (kosz i kręgle)** | tworzy albo odświeża scenę. Obiekty `Environment`, `Basketball`, `Bowling`, `Player`, `UI` i światło powstają od nowa, a rig i Twoje własne obiekty zostają. |
| **3. Build And Run (USB)** | pełny build DEV, instalacja i uruchomienie na goglach |
| **4. Patch And Run (USB, szybki)** | szybki build: na gogle trafiają tylko zmienione pliki |
| **Panel kontrolny (kontrola, build, gogle)** | okno z zakładkami (niżej) |
| Otwórz scenę / Otwórz instrukcję | skróty |

### Panel kontrolny: zakładki

**1. Kontrola projektu** to lista kontrolna: platforma, Player Settings, URP, OpenXR, Meta XR Feature, tryb „tylko kontrolery”, scena w buildzie, jeden rig bez dodatkowych kamer i Floor Level. Każdy punkt ma przycisk naprawy. Ręczny jest tylko **Project Setup Tool**, oznaczony `[RĘCZNIE]`.

**2. Build i optymalizacja** to tabela ustawień buildu z wartościami obecnymi i docelowymi dla profili **DEV** i **RELEASE**, przyciski *Zastosuj profil*, przełączniki (Script Debugging, Profiler, czyszczenie logcat) i uruchamianie (Build And Run, Patch And Run, Patch And Run tylko dla skryptów, wersja RELEASE). Na dole widać czas ostatniego buildu. Szczegóły w [punkcie 6](#6-optymalizacja-buildu-profile-dev-i-release).

**3. Gogle (USB / ADB)** pokazuje stan połączenia, pozwala zainstalować ostatni APK **bez budowania**, uruchomić, zatrzymać i odinstalować aplikację, pobrać log Unity z gogli do pliku i otworzyć okno Android Logcat.

---

## 5. Build & Run przez USB

### Przygotowanie gogli (jednorazowo)
1. Aplikacja **Meta Horizon** na telefonie › Urządzenia › Quest 3S › **Ustawienia gogli › Tryb dewelopera** › włącz.
2. Podłącz gogle kablem USB-C i załóż je. Pojawi się pytanie **„Zezwolić na debugowanie USB?”**. Zaznacz *Zawsze zezwalaj z tego komputera* i potwierdź.
3. Panel kontrolny › **3. Gogle** › **Sprawdź połączenie** powinno pokazać zielony punkt i model gogli.

### Uruchamianie

| Sposób | Kiedy |
|---|---|
| **VR Rzuty › 3. Build And Run (USB)** | pierwszy raz i po większych zmianach (scena, ustawienia, pakiety) |
| **VR Rzuty › 4. Patch And Run (USB, szybki)** | kolejne iteracje |
| Panel › 2 › **Patch And Run – skrypty** | gdy zmieniasz tylko kod C# |
| Panel › 3 › **Zainstaluj ostatni APK** | ponowna instalacja bez budowania |
| **File › Build Profiles › Android › Build And Run** | standardowa droga Unity; ustawienia z profilu DEV są już wpisane |

APK ląduje w `Builds/VRRzuty.apk` (RELEASE: `Builds/VRRzuty_release.apk`). Aplikacja uruchamia się sama; później znajdziesz ją w **Biblioteka › Nieznane źródła**.

---

## 6. Optymalizacja buildu: profile DEV i RELEASE

| Ustawienie | DEV (szybki Build & Run) | RELEASE (ładowanie / wydajność) | Dlaczego |
|---|---|---|---|
| Development Build | tak | nie | logi, profiler, debugger |
| Script Debugging | tak (przełącznik) | nie | debugger C# przez USB |
| IL2CPP Compiler Configuration | **Debug** | Release | Debug = najkrótsza kompilacja C++ |
| IL2CPP Code Generation | **OptimizeSize** („Faster (smaller) builds”) | OptimizeSpeed | mniej generowanego kodu = szybszy build |
| Managed Stripping Level | Low | Low | bezpieczne dla refleksji w Meta SDK |
| Kompresja APK | **LZ4** | LZ4HC | LZ4 szybciej się pakuje, LZ4HC daje mniejszy plik |
| symbols.zip | brak | brak | pakowanie symboli wydłuża build |
| Minify (R8) | nie | nie | zbędny krok Gradle |
| Splash screen Unity | nie | nie | **szybszy start aplikacji** |
| Incremental GC | tak | tak | mniej przycięć klatek |
| Strip Unused Mesh Components | tak | tak | mniejsze dane, szybsze ładowanie |
| Kompresja tekstur | ASTC | ASTC | natywny format GPU Questa |
| Optimized Frame Pacing | nie | nie | w VR tempo klatek ustala kompozytor Meta |
| App Bundle (AAB) | nie | nie | przez USB instaluje się APK |

Stałe dla obu profili: **IL2CPP + tylko ARM64**. Mała scena z prymitywów, bez tekstur i wypiekanego światła, oraz URP bez cieni i HDR dają mało wariantów shaderów, więc aplikacja szybko się buduje i ładuje.

**Szybka iteracja:** pierwszy build zawsze trwa najdłużej (IL2CPP + Gradle), więc nie usuwaj folderu `Library`. Pierwszy *Patch And Run* przygotowuje środowisko na goglach; kolejne są szybsze. Przełączanie DEV ↔ RELEASE wymusza ponowną kompilację IL2CPP.

---

## 7. Debugowanie przez USB

- **Logi:** w profilu DEV `Debug.Log` trafia do **Console** (wybierz gogle z listy połączeń u góry okna), do okna **Android Logcat** (Panel › 3) albo do pliku `Logs/quest_logcat.txt` (Panel › 3 › *Pobierz log Unity z gogli*). Wpisy projektu mają prefiks `[VRRzuty]`.
- **Debugger C#:** przy włączonym Script Debugging w Rider lub Visual Studio użyj *Attach to Unity Process* i wybierz urządzenie Android.
- **Profiler:** włącz *Autoconnect Profiler* w Panelu › 2 albo wybierz gogle w oknie Profiler.
- **Na żywo w goglach:** wyświetlacz nad lewym kontrolerem pokazuje prędkość każdego rzutu według trzech metod – szybki sposób, żeby sprawdzić, czy OVRInput zwraca sensowne dane.

---

## 8. Instrukcja obsługi w goglach

### Sterowanie (kontrolery Touch Plus)

| Wejście | Akcja |
|---|---|
| **Grip** (lewy lub prawy) | złap piłkę lub kulę w zasięgu dłoni (lekka wibracja = coś jest w zasięgu); **puść grip = rzut** |
| **Lewa gałka** | krok po stanowisku (ograniczony do boiska / rozbiegu) |
| **Prawa gałka ← →** | obrót o 45° |
| **Menu** (lewy kontroler) | otwiera / zamyka **menu ustawień**; chodzenie jest wtedy zablokowane |
| **Trigger** (prawy) | wybór w menu |
| **A** | wszystkie piłki tego stanowiska wracają na stojak |
| **B** | kosz: start / stop **wyzwania 60 s**; kręgle: **nowa gra** |
| **X** | podgląd toru lotu wł./wył. |
| **Y** | przejście do drugiego stanowiska (kosz ↔ kręgle) |

Unieś lewą rękę: wyświetlacz nad kontrolerem pokazuje ustawienia, prędkość i kąt ostatniego rzutu, jego spin oraz porównanie trzech metod liczenia prędkości (`OVR · hist. · klatka`).

### Jak rzucać
- **Kosz:** rzucaj jak prawdziwą piłką. Puść grip w najwyższym punkcie ruchu ręki, gdy ręka jeszcze przyspiesza. Kąt wyrzutu ok. 50° daje najlepsze „okno” wejścia piłki do obręczy. Pomaga podgląd toru (**X**).
- **Kręgle:** weź kulę ze stojaka po prawej, podejdź lewą gałką do czerwonej linii i puść kulę **nisko nad torem** ruchem wahadłowym. Obrót nadgarstka w chwili puszczenia nadaje kuli rotację. Celuj w strzałki na torze, nie w same kręgle. Głowa za czerwoną linią w chwili puszczenia = **spalony** (rzut za 0).
- Rzucone piłki **wracają same** po zatrzymaniu się albo po wypadnięciu poza halę, a kula po każdym rzucie.

### Menu ustawień

| Opcja | Wartości | Uwagi |
|---|---|---|
| Źródło prędkości | OVRInput / historia 0,1 s / ostatnia klatka | [9.3](#93-trzy-sposoby-liczenia-prędkości-rzutu) |
| Siła rzutu | ×1 / ×1,25 / ×1,5 / ×2 | mnożnik prędkości wyrzutu |
| Pomoc w celowaniu | wył. / słaba (30%) / silna (70%) | [9.4](#94-pomoc-w-celowaniu) |
| Podgląd toru lotu | wł. / wył. | linia przewidywanego lotu, gdy trzymasz piłkę |
| Rotacja (spin) | wł. / wył. | obrót kontrolera przechodzi na piłkę |
| Chwytanie | przytrzymaj / przełącz | „przełącz” = klik łapie, drugi klik puszcza |
| Grawitacja | Ziemia 9,81 / Mars 3,71 / Księżyc 1,62 m/s² | dla całej sceny |
| Wysokość kosza | 3,05 / 2,60 / 2,00 m | tylko kosz |
| Odległość od kosza | 2,0 / 4,2 (wolny) / 6,75 m (za 3) | tylko kosz, przestawia stojak i gracza |
| Długość toru | 18,29 / 12 / 8 m | tylko kręgle |
| Bandy na torze | wł. / wył. | tylko kręgle, zamykają rynny |

Opcje drugiego stanowiska są wyszarzone. Pod listą wyświetla się opis ostatnio zmienionej opcji.

| Preset | Ustawienia |
|---|---|
| **Realistyczny** | OVRInput, ×1, bez pomocy i podglądu, spin, Ziemia, regulaminowy kosz (4,2 m) i tor |
| **Trening** | OVRInput, ×1,25, słaba pomoc, podgląd toru |
| **Łatwy** | ×1,5, silna pomoc, podgląd, kosz 2,60 m z 2 m, tor 8 m z bandami |

Ustawienia i rekordy zapisują się w goglach.

### Plan sceny (widok z góry)

```
          KOSZ (x = 0)                                  KRĘGLE (x = 12)
 z 9.8  ┌──────────────────────┐              z 21  ┌───────────┐ zasłona
        │   [ tablica wyników ]│                    │  ▲▲▲▲ dół │
 z 7.6  │        ═╦═ kosz      │              z 18  │ ● ● ● ●   │ ← 10 kręgli
        │         ○ obręcz     │                    │  ● ● ●    │
        │      ┌──────┐        │                    │   ● ●     │
        │      │ pole │ 3 pkt  │                    │    ●      │
 z 3.0  │ ─────┴──────┴───── ⊙ │ 4,2 m              │ |  tor  | │ ← rynny, (bandy)
        │      🏀 gracz ⊙       │                    │ |       | │
        │  [stojak]          ⊙ │ 6,75 m       z 4.6 │ | > > > | │ strzałki
 z -3.3 └──────────────────────┘              z 0   │━━━━━━━━━━━│ linia spalonego
  tablice: STEROWANIE (lewa ściana),                 │ gracz  ◎◎◎│ stojak z kulami
           FIZYKA RZUTU (prawa ściana)        z -5  └───────────┘ (tablica 10 ramek nad torem)
```

### Kosz: punktacja
- **2 punkty** za trafienie, **3 punkty** za rzut z co najmniej 6,6 m (liczy się pozycja głowy w chwili puszczenia).
- **„Czysto!”** – piłka nie dotknęła obręczy ani tablicy; **„Z tablicy!”** – odbicie od tablicy.
- Tablica nad koszem: punkty, trafienia/rzuty i celność, bieżąca i najdłuższa seria, liczba czystych trafień, rekord wyzwania.
- **Wyzwanie 60 s (B):** licznik od nowa, ostatnie 5 sekund z sygnałem, na końcu syrena i ewentualny rekord.

### Kręgle: przebieg i punktacja
1. Rzut liczy się, gdy kula minie linię spalonego. Kula puszczona przed linią i zatrzymana wraca na stojak bez liczenia.
2. Gdy kula wpadnie do dołu, zatrzyma się albo minie 10 s, kręgle mają 2,5 s na uspokojenie się. Potem liczone są przewrócone kręgle (pochylone o więcej niż 25° albo spadłe z płyty).
3. Po pierwszym rzucie w ramce przewrócone kręgle znikają (jak w maszynie ustawiającej), a stojące zostają tam, gdzie stoją. Po ramce ustawia się wszystkie 10.
4. Tablica: oznaczenia `X` (strike), `/` (spare), `-` (0), cyfry, wynik narastająco i suma. Po 10. ramce koniec gry i ewentualny rekord. **B** = nowa gra.

### Propozycje ćwiczeń
1. **Trzy metody prędkości:** 10 rzutów z każdą metodą (Menu › Źródło prędkości). Porównaj na wyświetlaczu `OVR · hist. · klatka` i celność. Dlaczego „ostatnia klatka” daje rzuty słabe albo przypadkowe?
2. **Kąt wyrzutu:** z linii 4,2 m odczytaj kąt trafionych rzutów (wyświetlacz). Czy wysokie rzuty trafiają częściej?
3. **Grawitacja:** z podglądem toru porównaj ten sam rzut na Ziemi, Marsie i Księżycu. Jak zmienia się wysokość i czas lotu?
4. **Pomoc w celowaniu:** zagraj wyzwanie 60 s bez pomocy i z silną pomocą. Czy pomoc jest „uczciwa”? Kiedy gracz ją zauważa?
5. **Kręgle:** pełna gra na torze 18,29 m, potem na 8 m z bandami. Porównaj wyniki i masę kul (5,4 / 6,4 / 7,2 kg).
6. **Spin:** rzuć kulę z obrotem nadgarstka i bez (Menu › Rotacja). Czy tor kuli się zakrzywia?

---

## 9. Jak to działa

### 9.1 Architektura

```
OVRManager ──► OVRInput co klatkę
                    │
   ┌────────────────┼───────────────────────┬────────────────────────┐
   ▼                ▼                       ▼                        ▼
ThrowMenu        Grabber                PlayerMover             ThrowSettings
Menu, A/B/X/Y    grip → złap / puść     krok, obrót, Y          (PlayerPrefs, zdarzenie Changed)
                 prędkość rzutu (3 metody)        │                     │
                 AimAssist, podgląd toru          │         ┌───────────┼─────────────┐
                    │                             │         ▼           ▼             ▼
                    ▼                             │   ThrowPhysics  BasketballGame  BowlingLane
               Throwable (Docked/Held/Free) ──────┴──► (grawitacja) (wysokość,     (długość toru,
                 zdarzenia Grabbed/Thrown/Returned       odległość)    bandy)
                    │                                       │             │
                    └─► BasketballGame: trafienia ◄─────────┘             │
                    └─► BowlingLane: przebieg rzutu, BowlingScore ◄───────┘
```

### 9.2 Chwytanie
`Grabber` co klatkę (w `LateUpdate`, gdy kotwice kontrolerów są już zaktualizowane) dla każdej ręki:
1. Czyta grip: `OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, LTouch/RTouch)` z histerezą (złapanie > 0,6, puszczenie < 0,35), żeby piłka nie „wypadała” przy lekkim rozluźnieniu dłoni.
2. Szuka najbliższej piłki: `Physics.OverlapSphereNonAlloc` wokół punktu chwytu i `Collider.ClosestPoint`, maks. 7 cm od powierzchni piłki.
3. Złapana piłka dostaje kinematyczne `Rigidbody` i staje się dzieckiem kotwicy kontrolera, z zachowanym położeniem względem dłoni (łapiesz tam, gdzie dotknąłeś).
4. Póki piłka jest w ręce, co klatkę zapisuje się jej pozycję i obrót (bufor 48 próbek). Na tej podstawie działają metody „historia” i „ostatnia klatka”.

Po puszczeniu piłka wraca pod swojego rodzica, `isKinematic = false`, dostaje `linearVelocity` i `angularVelocity`, a dalej to już tylko fizyka.

### 9.3 Trzy sposoby liczenia prędkości rzutu

| Metoda | Jak | Wada / zaleta |
|---|---|---|
| **OVRInput** | `OVRInput.GetLocalControllerVelocity` i `GetLocalControllerAngularVelocityCw` – prędkość liczona przez system śledzenia (fuzja kamer i IMU), w układzie *TrackingSpace* | najdokładniejsza i bez opóźnienia; trzeba ją przeliczyć na świat i na punkt piłki |
| **historia 0,1 s** | nachylenie prostej dopasowanej metodą najmniejszych kwadratów do pozycji piłki z ostatnich 0,1 s | odporna na szum, ale **uśrednia** – przy rzucie z przyspieszeniem daje prędkość mniejszą niż w chwili puszczenia |
| **ostatnia klatka** | (p₁ − p₀) / Δt | bardzo zaszumiona: drganie śledzenia i nierówne Δt dają przypadkowe wyniki; często też za mała, bo w ostatniej klatce dłoń już hamuje |

Przeliczenie prędkości z OVRInput:
```csharp
Vector3 v = trackingSpace.rotation * OVRInput.GetLocalControllerVelocity(ctl);          // do świata
Vector3 w = trackingSpace.rotation * OVRInput.GetLocalControllerAngularVelocityCw(ctl); // rad/s
v += Vector3.Cross(w, ballCenter - controllerPosition);   // piłka nie leży w środku kontrolera
v += PlayerMover.Velocity;                                // gracz idzie gałką
```
Składnik `ω × r` jest ważny przy rzucie z nadgarstka: środek piłki jest kilkanaście centymetrów od punktu śledzenia kontrolera i porusza się szybciej. Gdy OVRInput zwraca zero, a ręka wyraźnie się porusza (np. chwilowa utrata śledzenia), kod przełącza się na historię i zaznacza to na wyświetlaczu.

Wersja `GetLocalControllerAngularVelocity` (bez „Cw”) jest w SDK 207 przestarzała, bo na części urządzeń zwracała odwrócony wynik.

Przy obrocie skokowym i przejściu między stanowiskami `PlayerMover` wysyła zdarzenie `Discontinuity`, a `Grabber` czyści historię. Inaczej skok pozycji wyglądałby jak bardzo szybki ruch ręki.

Na koniec: `v × mnożnik siły`, opcjonalna pomoc w celowaniu i spin (`angularVelocity` = prędkość kątowa kontrolera albo zero).

### 9.4 Pomoc w celowaniu
**Kosz** – rzut ukośny bez oporu powietrza. Dla odległości poziomej *d*, różnicy wysokości *h* i prędkości *v* kąt wyrzutu spełnia:

```
tg θ = ( v² ± √( v⁴ − g·(g·d² + 2·h·v²) ) ) / ( g·d )
```

Dwa rozwiązania to tor płaski i wysoki. Kod wybiera bliższy kątowi gracza, zachowuje wartość prędkości i miesza oba wektory: `v' = lerp(v_gracza, v_idealna, siła)`. Gdy rzut jest za słaby, by w ogóle dolecieć (wyróżnik < 0), a brakuje mniej niż 20% minimalnej prędkości `v_min² = g·(h + √(h² + d²))`, prędkość podnosi się do minimum. Pomoc nie działa, gdy kierunek rzutu odbiega od kosza o więcej niż 25°. Poprawność wzorów sprawdziłem numerycznie: przy pełnej sile pomocy tor przechodzi przez środek obręczy z dokładnością do milimetra.

**Kręgle** – obrót poziomej składowej prędkości w stronę pierwszego kręgla (o 30% albo 70% różnicy kątów).

Podgląd toru lotu używa tej samej funkcji `Estimate`, co rzut, więc pokazuje dokładnie to, co się stanie po puszczeniu, łącznie z pomocą i mnożnikiem. Tor liczy się krokami co 25 ms z `Physics.SphereCast` i kończy na pierwszej przeszkodzie.

### 9.5 Fizyka
- `ThrowPhysics`: krok fizyki **90 Hz** (domyślne 50 Hz to za mało dla piłki lecącej 10 m/s i obręczy grubości 2 cm), 10 iteracji solvera, grawitacja z ustawień.
- Piłki: `ContinuousDynamic` (nie przelatują przez cienką obręcz), interpolacja, maks. prędkość kątowa 60 rad/s.
- Materiały fizyczne (`Assets/VRThrowing/Physics`):

| Materiał | Tarcie dyn./stat. | Sprężystość | Łączenie |
|---|---|---|---|
| Basketball | 0,6 / 0,6 | 0,82 | odbicie **Maximum** – podłoga i obręcz nie muszą być sprężyste |
| BowlingBall | 0,2 / 0,2 | 0,05 | tarcie **Minimum** – o poślizgu decyduje tor |
| Lane | 0,04 / 0,06 | 0,05 | naoliwiony tor: kula najpierw się ślizga, potem toczy |
| Pin | 0,35 / 0,4 | 0,45 | kręgle odbijają się od siebie |
| Rim / Backboard | 0,5 / 0,4 | 0,55 / 0,6 | |
| Cushion | 0,8 | 0 | zasłona za dołem tłumi uderzenia |

- Kręgle: kapsuły 38 × 12 cm, 1,5 kg, `ContinuousSpeculative`, 12 iteracji solvera. Kule: 21,8 cm, 5,4 / 6,4 / 7,2 kg; piłki do kosza: 24 cm, 0,62 kg.

### 9.6 Wykrywanie trafienia do kosza
Bez triggerów, geometrycznie, w `FixedUpdate`, dla każdej piłki w locie:
1. środek piłki przecina **w dół** płaszczyznę obręczy w odległości poziomej < promień obręczy (punkt przecięcia jest interpolowany między dwoma krokami fizyki),
2. w ciągu 1 s przecina płaszczyznę **35 cm niżej**, nadal pod obręczą.

Dwa etapy odrzucają piłki, które wpadły w obręcz, ale odbiły się w górę. Dotknięcia obręczy i tablicy zapisuje `Throwable.OnCollisionEnter` (komponenty-znaczniki `RimPart`, `Backboard`).

### 9.7 Kręgle: maszyna stanów i punktacja
`BowlingLane`: `Ready → Released → Rolling → Settling → (Racking) → Ready`, a po 10. ramce `GameOver`. Logika punktacji jest w osobnej klasie `BowlingScore` bez odwołań do Unity. Przetestowałem ją na gotowych przypadkach: gra doskonała = 300, same spare'y 5/5 = 150, same 9 = 90, same rynny = 0, przykładowa gra = 167, 10. ramka X 7 / = 20.

Zmiana długości toru przesuwa obiekt `FarEnd` (płyta, kręgle, dół, zasłona), a elementy z komponentem `LaneStretch` (deska, rynny, bandy, ściany boczne) wydłużają się od linii spalonego do nowego końca.

### 9.8 Menu w VR bez Interaction SDK
World-space Canvas, każdy przycisk ma `Image` i `BoxCollider` ustawiony jako trigger, `UiPointer` robi `Physics.Raycast` z prawego kontrolera z `QueryTriggerInteraction.Collide`, a trigger kontrolera klika.

---

## 10. Dostrajanie i rozbudowa

Parametry są w Inspectorze obiektów `Player`, `Basketball` i `Bowling`. **Uwaga:** „Generuj scenę” tworzy je od nowa, więc trwałe zmiany wpisuj w kodzie (domyślne wartości pól albo `VRThrowingSceneBuilder.cs`).

| Co | Gdzie |
|---|---|
| Domyślne ustawienia przy pierwszym uruchomieniu | `Player` › **Throw Settings** (później obowiązują wartości zapisane w goglach) |
| Zasięg chwytu, punkt chwytu, progi gripa | `Player` › **Grabber** › *Grab Offset*, *Grab Reach*, *Grip Press/Release* |
| Okno metody „historia”, próg rzutu | **Grabber** › *History Window*, *Min Throw Speed* |
| Krok fizyki, iteracje solvera | **Throw Physics** |
| Prędkość kroku gałką, kąt obrotu, obszary stanowisk | **Player Mover** |
| Czas wyzwania, odległość za 3 punkty | `Basketball` › **Basketball Game** |
| Czasy toczenia i uspokajania kręgli | `Bowling` › **Bowling Lane** |
| Kiedy kręgiel jest przewrócony | kręgle › **Bowling Pin** › *Down Angle*, *Drop Threshold* |
| Masa, tłumienie piłek | piłki › **Rigidbody** |
| Odbicie, tarcie | `Assets/VRThrowing/Physics/*.physicMaterial` |

**Pomysły na rozszerzenie:**
- opór powietrza i efekt Magnusa (podkręcona piłka zakręca): siła `k · (ω × v)` w `FixedUpdate`,
- hak w kręglach: różne tarcie na początku i na końcu toru (olej tylko na pierwszych 12 m),
- rzut wolny z ruchomym koszem albo celem,
- zapis rzutów (prędkość, kąt, trafienie) do pliku CSV w `Application.persistentDataPath` i analiza w arkuszu,
- tryb dla dwóch graczy na zmianę w kręglach.

---

## 11. Rozwiązywanie problemów

Zacznij od **Panel kontrolny › 1. Kontrola projektu**. Wskazuje większość problemów i ma przyciski naprawy.

| Problem | Rozwiązanie |
|---|---|
| Brak menu **VR Rzuty** | Błędy kompilacji. Sprawdź Console; zwykle to brak pakietu Meta XR Core (Package Manager). |
| Gogle niewidoczne / „unauthorized” | Kabel z transmisją danych, tryb dewelopera, potwierdzenie „Zezwól na debugowanie USB” w goglach. |
| Aplikacja startuje jako płaskie okno 2D | Nie włączono OpenXR + Meta XR Feature (Panel › 1) albo brak `OVRCameraRig`. Uruchom Project Setup Tool › Fix All. |
| Jestem pod podłogą / za wysoko | Panel › 1 › *Tracking Origin = Floor Level*; w goglach ustaw na nowo granicę i wysokość podłogi. |
| Nie mogę złapać piłki | Dłoń musi być przy piłce (lekka wibracja = piłka w zasięgu). Zwiększ **Grabber** › *Grab Reach* albo popraw *Grab Offset*. |
| Rzuty są za słabe | Puszczaj grip w trakcie ruchu, nie po zatrzymaniu ręki. Sprawdź, czy źródło prędkości to OVRInput. Pomoże też **Siła rzutu ×1,25**. |
| Piłka „wypada” z ręki | Zmień **Chwytanie** na „przełącz” albo obniż **Grabber** › *Grip Release*. |
| Piłka przelatuje przez obręcz | Sprawdź, czy działa **Throw Physics** (90 Hz) i czy piłka ma *Collision Detection = Continuous Dynamic*. |
| Kręgle się trzęsą albo same przewracają | Zwiększ *Solver Iterations* kręgli albo krok fizyki (Throw Physics › *Physics Rate* 120). |
| Rzut w kręglach się nie liczy | Kula musi minąć czerwoną linię. Kula puszczona przed linią i zatrzymana wraca bez liczenia. |
| Wyniki/rekordy zniknęły | Odinstalowanie aplikacji (także *Odinstaluj* w Panelu › 3) usuwa `PlayerPrefs`. |
| Patch And Run się nie udaje | Zrób raz pełne **Build And Run**, potem patchuj. |

---

### Przydatne linki
- [Meta – konfiguracja projektu Unity](https://developers.meta.com/horizon/documentation/unity/unity-project-setup/)
- [Meta – OVRInput](https://developers.meta.com/horizon/documentation/unity/unity-ovrinput/)
- [Unity – Rigidbody](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rigidbody.html)
- [Unity – PhysicsMaterial](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsMaterial.html)
- [Unity – Android application patching](https://docs.unity3d.com/Manual/android-AppPatching.html)
