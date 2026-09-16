# CODE_AUDIT.md — статический анализ кода проекта KazistovVv

**Дата:** 15.09.2026
**Тип документа:** отчёт. **Код в рамках этого этапа НЕ изменялся ни на строку.**
**Охват:** 177 файлов `*.cs` в `Assets\` (включая `Assets\_Project\01_Scripts`, `Assets\_Project\06_KazistovVv_UI\03_Scripts`, `Assets\_Project\08_Tests`, `Assets\_Recovery` исключена). 2 939 методов, 4 555 полей, 418 типов.
**Инструменты:** `_tools\kv_audit_code.ps1`, `_tools\kv_audit_dead.ps1` (сопоставление фигурных скобок, частотный анализ идентификаторов, хеширование нормализованных тел методов). Сырые данные: `_tools\audit_code.json`, `_tools\audit_dead.json`.

> **Как читать отчёт.** Все списки — результат автоматического анализа, а не ручной оценки.
> Часть находок — закономерные ложные срабатывания (они помечены **ЛС** с объяснением).
> Ни одна находка не исправлялась: по ТЗ этого этапа — «только отчёт, без изменений кода».

---

## 1. Мёртвый код

### 1.1 Приватные методы, на которые нет ни одной ссылки (11)

Поиск: метод объявлен `private`, его идентификатор больше нигде в проекте не встречается.
Методы Unity-сообщений (`Awake`, `Start`, `Update`, `OnEnable`, `OnDisable`, `OnDestroy`, `OnDrawGizmos`, …) исключены — их вызывает движок, а не код.

| # | Файл | Строка | Объявление |
|---|---|---|---|
| 1 | `Assets\_Project\01_Scripts\Core\FreeFlyCameraController.cs` | 1563 | `private void SelectRobotContextual` |
| 2 | `Assets\_Project\01_Scripts\Trajectory\LaserAndPhantomManagers.cs` | 371 | `private static void DestroyLeftoverContainers` |
| 3 | `Assets\_Project\01_Scripts\Trajectory\LaserAndPhantomManagers.cs` | 687 | `private float TravelTime` |
| 4 | `Assets\_Project\01_Scripts\Editor\HierarchyAutoRefresh.cs` | 72 | `private static bool ToggleValidate` |
| 5 | `Assets\_Project\01_Scripts\Trajectory\TrajectoryFlowController.cs` | 1949 | `private List<IkSolution> TopUpVariants` |
| 6 | `Assets\_Project\01_Scripts\Trajectory\TrajectoryPlannerController.cs` | 55 | `private static bool KeyDownLegacy` |
| 7 | `Assets\_Project\01_Scripts\Editor\HierarchyPhantomCleaner.cs` | 189 | `private static void MenuPurge` |
| 8 | `Assets\_Project\01_Scripts\Editor\HierarchyPhantomCleaner.cs` | 198 | `private static void MenuInventory` |
| 9 | `Assets\_Project\01_Scripts\Editor\HierarchyPhantomCleaner.cs` | 202 | `private static void MenuDumpOrphans` |
| 10 | `Assets\_Project\01_Scripts\Editor\HierarchyPhantomCleaner.cs` | 221 | `private static void MenuDeleteSpawnedCopies` |
| 11 | `Assets\_Project\01_Scripts\Input\KeyboardMouseInputProvider.cs` | 20 | `private bool TryLegacyKey` |

**Важные оговорки:**
- Пункты 7–10 (`HierarchyPhantomCleaner.Menu*`) — это обработчики пунктов меню. Они помечены атрибутом `[MenuItem(...)]`, и Unity вызывает их **по атрибуту, а не по имени**, поэтому анализатор их «не видит». **ЛС.** То же относится к любому `private`-методу с `[MenuItem]`, `[ContextMenu]`, `[Button]` (NaughtyAttributes), `[Invoke]`, `[UsedImplicitly]`, `[UnityEngine.Scripting.Preserve]`.
- Пункты 1, 2, 4, 5, 6, 11 — кандидаты на реальную чистку: ссылок нет ни в коде, ни в атрибутах.

### 1.2 Приватные поля без использования (11)

| # | Файл | Строка | Объявление |
|---|---|---|---|
| 1 | `Assets\_Project\01_Scripts\Features\KvSingularityZones.cs` | 87 | `private readonly float[] limitMargins` |
| 2 | `Assets\_Project\01_Scripts\Features\KvWorkbenchWindow.cs` | 267 | `private readonly List<Text> tabNotes` |
| 3 | `Assets\_Project\01_Scripts\Features\KvAutomation.cs` | 492 | `private int pauseDepth` |
| 4 | `Assets\_Project\01_Scripts\Features\KvAutomation.cs` | 493 | `private int pauseIndex` |
| 5 | `Assets\_Project\01_Scripts\Features\KvNetTools.cs` | 87 | `private float sendTimer` |
| 6 | `Assets\_Project\06_KazistovVv_UI\03_Scripts\Zones\KvToolbar.cs` | 63 | `private const int LayoutWide` |
| 7 | `Assets\_Project\01_Scripts\Features\KvSafetyTools.cs` | 1249 | `private InputField jointField` |
| 8 | `Assets\_Project\01_Scripts\Features\KvFeatureWindow.cs` | 106 | `private Text recordSourceText` |
| 9 | `Assets\_Project\01_Scripts\Features\KvFeatureWindow.cs` | 107 | `private Text nameFieldText` |
| 10 | `Assets\_Project\01_Scripts\Editor\DshFullVerifyDiag.cs` | 61 | `private static double[] savedQ` |
| 11 | `Assets\_Project\01_Scripts\Editor\DshUiStagesDiag.cs` | 47 | `private static float waitStart` |

**Оговорка:** сериализуемые `private`-поля `MonoBehaviour` (`[SerializeField]`) видны в инспекторе и могут задаваться сценой даже без ссылок в коде. **ЛС** для таких полей. Пункты 3, 4, 6, 7, 8, 9 — кандидаты на чистку (объявлены без `[SerializeField]` и без ссылок).

### 1.3 Типы, на которые нет ссылок в коде (17) — почти все ЛС

| Файл | Строка | Тип | Комментарий |
|---|---|---|---|
| `01_Scripts\Spatial\CalibrationTool.cs` | 2 | `class CalibrationTool` | `MonoBehaviour` — **используется в сцене/prefab**, ссылка хранится в `.unity`/`.prefab`, а не в `.cs`. **ЛС** |
| `01_Scripts\Trajectory\PostureControl.cs` | 17 | `class PostureControl` | то же (**ЛС**) |
| `01_Scripts\Trajectory\NarrowPhase.cs` | 66 | `class NarrowPhase` | то же (**ЛС**) |
| `01_Scripts\UI\MainMenu.cs` | 7 | `class MainMenu` | то же (**ЛС**) |
| `01_Scripts\Input\KeyboardController.cs` | 2 | `class KeyboardController` | то же (**ЛС**) |
| `01_Scripts\Input\InputDeviceType.cs` | 19 | `class InputDeviceExtensions` | методы-расширения вызываются «через точку», имя типа в коде не упоминается. **ЛС** |
| `01_Scripts\VR\VRInputManager.cs` | 2 | `class VRInputManager` | `MonoBehaviour` (**ЛС**) |
| `01_Scripts\VR\VRHandTracker.cs` | 2 | `class VRHandTracker` | `MonoBehaviour` (**ЛС**) |
| `01_Scripts\VR\PlacementController.cs` | 2 | `class PlacementController` | `MonoBehaviour` (**ЛС**) |
| `06_KazistovVv_UI\03_Scripts\Editor\KazistovVvMenu.cs` | 9 | `class KazistovVvMenu` | статический класс меню — вызывается движком по `[MenuItem]` (**ЛС**) |
| `01_Scripts\Editor\StandsMenu.cs` | 15 | `class StandsMenu` | то же (**ЛС**) |
| `08_Tests\Editor\KvCalibrationTests.cs` | 46 | `class KvCalibrationTests` | обнаруживается Test Runner'ом по атрибуту `[TestFixture]` (**ЛС**) |
| `08_Tests\Editor\KvEnergyOptimalTests.cs` | 75 | `class KvEnergyOptimalTests` | то же (**ЛС**) |
| `08_Tests\Editor\KvPayloadCalculatorTests.cs` | 116 | `class KvPayloadCalculatorTests` | то же (**ЛС**) |
| `08_Tests\Editor\KvTimeOptimalTests.cs` | 62 | `class KvTimeOptimalTests` | то же (**ЛС**) |
| `08_Tests\Editor\KvTrajMathTests.cs` | 22 | `class KvTrajMathTests` | то же (**ЛС**) |
| `08_Tests\Editor\KvRobotExportTests.cs` | 284 | `struct KvRobotSample` | вспомогательный тип внутри тестового файла (**ЛС**) |

**Вывод по разделу 1:** реальных «мертвецов» в продуктовом коде — **13**: 6 приватных методов (1.1 пп. 1, 2, 4, 5, 6, 11 — при этом `Menu*` из 1.1 в список не входят, они вызываются Unity) и 6 приватных полей (1.2 пп. 3, 4, 6, 7, 8, 9) плюс `DestroyLeftoverContainers`/`TravelTime`. Мёртвых типов в продуктовом коде **не обнаружено** — все «неиспользуемые» типы либо `MonoBehaviour` из сцены, либо точки входа движка, либо тестовые фикстуры.

---

## 2. Дубликаты

### 2.1 Полностью идентичные тела методов (30 совпадений)

Сравнение: тело метода нормализуется (убираются комментарии и пробелы) и хешируется MD5; совпавшие хеши группируются.

**2.1.1 Вспомогательные функции диагностик (`Assets\_Project\01_Scripts\Editor\Dsh*Diag.cs`) — 20 групп из 30**

Диагностические скрипты копировались из сессии в сессию, поэтому в них накопились одинаковые утилиты. Это **не продуктовый код** (по правилам проекта `Dsh*Diag.cs` в копию пользователя не переносятся):

| Метод | Строк | Где (файл:строка) |
|---|---|---|
| `SetLasers` | 4 | `DshStage3Diag.cs:1014`, `DshStage4Diag.cs:1647`, `DshFullVerifyDiag.cs:1862`, `DshScaraDiag.cs:1163`, `DshStage2Diag.cs:1001`, `DshScaraEightDiag.cs:645` — **6 копий** |
| `Check` | 3 | `DshStage3Diag.cs:92`, `DshStage2Diag.cs:93`, `DshStageDiag.cs:81` |
| `Check` | 3 | `DshDesktopUiDiag.cs:116`, `DshUiStagesDiag.cs:145` |
| `Aim` | 5 | `DshStage3Diag.cs:1021`, `DshStage4Diag.cs:1654`, `DshScaraDiag.cs:1170`, `DshScaraEightDiag.cs:630` |
| `aimAt` | 3 | `DshFullVerifyDiag.cs:1884`, `DshStage2Diag.cs:1024` |
| `aimAt` | 7 | `DshStage4Diag.cs:1669`, `DshScaraDiag.cs:1185`, `DshScaraEightDiag.cs:652` |
| `aimAt` | 5 | `DshFullVerifyDiag.cs:1891`, `DshStage2Diag.cs:1030` |
| `Click` | 4 | `DshStage3Diag.cs:1029`, `DshStage4Diag.cs:1662`, `DshScaraDiag.cs:1178`, `DshScaraEightDiag.cs:638` |
| `Next` | 6 | `DshStage3Diag.cs:114`, `DshStage4Diag.cs:134`, `DshScaraDiag.cs:120`, `DshStage2Diag.cs:125` |
| `Boot` | 4 | `DshStage3Diag.cs:50`, `DshStage4Diag.cs:55`, `DshScaraDiag.cs:58`, `DshStage2Diag.cs:56`, `DshStageDiag.cs:51` — **5 копий** |
| `Once` | 4 | `DshStage3Diag.cs:107`, `DshStage2Diag.cs:116` |
| `SamePoint` | 5 | `DshStage3Diag.cs:992`, `DshStage2Diag.cs:979` |
| `Line` | 4 | `DshDesktopUiDiag.cs:109`, `DshUiStagesDiag.cs:138` |
| `FindNode` | 8 | `DshDesktopUiDiag.cs:564`, `DshUiStagesDiag.cs:973` |
| `FindRecursive` | 9 | `DshDesktopUiDiag.cs:575`, `DshUiStagesDiag.cs:984` |
| `FindReachablePoint` | 36 | `DshStage3Diag.cs:1082`, `DshStage4Diag.cs:1679`, `DshStage2Diag.cs:1039` — **самая крупная копия (36 строк × 3)** |

**2.1.2 В продуктовом коде — 10 групп**

| Метод | Строк | Где | Оценка |
|---|---|---|---|
| `KvLocExtra.F` / `KvLocExtra2.F` / `KvLocExtra3.F` | 4 | `KvLocExtra.cs:41`, `KvLocExtra3.cs:27`, `KvLocExtra2.cs:30` | **Три одинаковых обёртки локализации** — три модуля-близнеца (`KvLocExtra`, `KvLocExtra2`, `KvLocExtra3`) появились в разных сессиях. Кандидат на объединение в один модуль. |
| `OnServiceMessage` | 3 | `KvStageHub.cs:139`, `KvStageHub2.cs:132`, `KvStageHub3.cs:113`, `KvStageHub4.cs:187` | 4 копии обработчика `Message` — следствие того, что хабы этапов построены по одному шаблону. |
| `OpenTab` | 3 | `KvStageHub3.cs:243`, `KvStageHub4.cs:428` | 2 копии |
| `Down` | 12 | `KvStageHub3.cs:217`, `KvStageHub2.cs:239`, `KvStageHub4.cs:391` | 3 копии (12 строк) |
| `AimPoint` | 5 | `KvStageHub3.cs:202`, `KvStageHub4.cs:345` | 2 копии |
| `Current` | 6 | `KvTimeOptimal.cs:386`, `KvEnergyOptimal.cs:380` | 2 копии (модули-близнецы «время-оптимальный» / «эко-оптимальный») |
| `Bind` | 3 | `KvSingularityZones.cs:123`, `KvHealthMonitor.cs:127` | 2 копии |
| `Destroy` | 4 | `KvSingularityZones.cs:583`, `KvDynamicObstacles.cs:610` | 2 копии |
| `OnPointerDown` | 6 | `KvDockPanel.cs:979`, `KvDockPanel.cs:1053` | **внутри одного файла** — 2 копии |
| `Refresh` | 2 | `KvSceneStudio.cs:1283`, `KvSceneStudio.cs:1333` | внутри одного файла |
| `IsAncestorOf` | 8 | `SixAxisController.cs:287`, `SixAxisAutoSetup.cs:293` | 2 копии |
| `Vec` | 3 | `KvGripper.cs:581`, `KazistovVvUIManager.cs:1877` | 2 копии |
| `GetPointerPosition` / `GetPointerDirection` | 3 / 3 | `GamepadInputProvider.cs:24,30` ↔ `KeyboardMouseInputProvider.cs:80,86` | 2 пары: два провайдера ввода дублируют тривиальные преобразования. |

### 2.2 Похожие (не идентичные) функции — качественная оценка

Помимо точных копий, в проекте есть **шесть модулей одного шаблона** — «вкладка воркбенча + сервис + черновик»:
`KvTimeOptimal.cs` (359), `KvEnergyOptimal.cs` (347), `KvCalibration.cs` (1183), `KvPayloadCalculator.cs` (592), `KvRobotExport.cs` (521), `KvRobotImport.cs` (1243).
У них совпадают не только `F`/`Current`, но и структура: `Draft`-класс, `Last`/`DraftCount`, `Message`-событие, `Compute`/`ApplySelected`/`ResetSelected`, `Bind(TrajectoryFlowController)`. Прямого дублирования логики нет (алгоритмы разные), но **каркас** повторяется шесть раз и мог бы быть вынесен в базовый класс. Это архитектурное наблюдение, а не дефект.

То же — четыре хаба этапов (`KvStageHub`…`KvStageHub4`), реализующие единый контракт `Install / RegisterCommands / BuildTreeNodes / BuildExtraProperties / OnNodeSelected / HasFeature / GetFeature / SetFeature / TreeSignaturePart`.

---

## 3. Длинные методы (>100 строк)

Всего методов 2 939; длиннее 100 строк — **40** (1,4 %). Из них 1 — тестовый, 2 — диагностические. **Продуктовых методов длиннее 100 строк: 37.**

| Строк | Файл | Строка | Метод |
|---:|---|---:|---|
| 1632 | `01_Scripts\Editor\DshFullVerifyDiag.cs` | 201 | `RunStep` *(диагностика)* |
| 696 | `01_Scripts\Editor\DshFeaturesDiag.cs` | 123 | `RunStep` *(диагностика)* |
| 610 | `06_KazistovVv_UI\03_Scripts\Core\KvIcons.cs` | 78 | `Draw` — процедурная отрисовка всех иконок одним методом |
| 609 | `08_Tests\Editor\KvRobotExportTests.cs` | 338 | `Krl_Header_HasExpectedPreambleAndDeclarations` *(тест: длинный набор проверок синтаксиса KRL)* |
| 457 | `06_KazistovVv_UI\03_Scripts\Core\KazistovVvUIManager.cs` | 806 | `RegisterCommands` |
| 400 | `01_Scripts\Features\FeatureHub.cs` | 1093 | `RegisterCommands` |
| 301 | `01_Scripts\Features\KvRobotImport.cs` | 302 | `ImportUrdf` |
| 254 | `01_Scripts\Features\KvReportPdf.cs` | 301 | `Generate` |
| 234 | `01_Scripts\Features\KvStageHub.cs` | 571 | `RegisterCommands` |
| 202 | `06_KazistovVv_UI\03_Scripts\Zones\KvSettingsView.cs` | 529 | `BuildInterface` |
| 196 | `01_Scripts\Features\KvCalibration.cs` | 1099 | `Build` |
| 177 | `01_Scripts\Features\KvWaypoints.cs` | 504 | `RebuildRoute` |
| 168 | `06_KazistovVv_UI\03_Scripts\Core\KazistovVvUIManager.cs` | 1670 | `BuildTreeModel` |
| 162 | `01_Scripts\Features\KvWaypointConstraints.cs` | 287 | `Build` |
| 161 | `01_Scripts\Features\KvStageHub3.cs` | 485 | `BuildExtraProperties` |
| 156 | `01_Scripts\Features\KvStageHub3.cs` | 733 | `RegisterCommands` |
| 154 | `01_Scripts\Features\KvCollisionOptimizer.cs` | 158 | `Rebuild` |
| 144 | `01_Scripts\Features\KvConstrainedPlanner.cs` | 594 | `Build` |
| 139 | `01_Scripts\Features\KvStageHub2.cs` | 628 | `RegisterCommands` |
| 138 | `01_Scripts\Features\KvRobotImport.cs` | 1005 | `CheckKinematics` |
| 133 | `01_Scripts\Features\KvStageHub4.cs` | 712 | `RegisterCommands` |
| 130 | `01_Scripts\Features\FeatureHub.cs` | 723 | `BuildScenarios` |
| 130 | `01_Scripts\Features\KvFeatureWindow.cs` | 159 | `Build` |
| 128 | `01_Scripts\Features\KvRobotImport.cs` | 1225 | `Build` |
| 128 | `01_Scripts\Trajectory\PoseValidator.cs` | 41 | `Init` |
| 124 | `06_KazistovVv_UI\03_Scripts\Zones\KvTreeView.cs` | 336 | `AddRow` |
| 119 | `01_Scripts\Editor\DshStage3Diag.cs` | 580 | `StepCalibration` *(диагностика)* |
| 119 | `01_Scripts\Features\FeatureHub.cs` | 314 | `WatchFlow` |
| 118 | `01_Scripts\Trajectory\CollisionWorld.cs` | 145 | `Rebuild` |
| 117 | `01_Scripts\Features\KvConstrainedPlanner.cs` | 288 | `Project` |
| 115 | `01_Scripts\Trajectory\PointMoveHud.cs` | 85 | `Build` |
| 111 | `01_Scripts\Features\KvConstrainedPlanner.cs` | 424 | `Replan` |
| 110 | `01_Scripts\Features\KvStageHub2.cs` | 439 | `BuildExtraProperties` |
| 110 | `01_Scripts\Features\KvSafetyTools.cs` | 701 | `Validate` |
| 109 | `01_Scripts\Features\KvRobotImport.cs` | 709 | `ImportStep` |
| 109 | `01_Scripts\Features\KvCalibration.cs` | 798 | `SolveBase` |
| 105 | `01_Scripts\Features\KvStartMenu.cs` | 105 | `Build` |
| 105 | `06_KazistovVv_UI\03_Scripts\Zones\KvToolbar.cs` | 178 | `RebuildGrid` |
| 104 | `01_Scripts\Trajectory\ReachabilityOracle.cs` | 237 | `QuerySixAxis` |
| 101 | `01_Scripts\Features\KvNetTools.cs` | 1110 | `Build` |

**Наблюдения.**
1. **Методы `RegisterCommands` (6 штук, 133–457 строк) — это не «простыни логики», а длинные списки регистрации команд** (`KvCommands.Register(new KvCommand { … })` по 5–12 строк на команду). Разбивать их на подметоды можно, но выигрыш читаемости невелик, а риск ошибки при правке есть. Приоритет — **низкий**.
2. `Init` в `PoseValidator.cs` (128 строк) и `Rebuild` в `CollisionWorld.cs` (118) — **критичные для робота** методы; они читаются линейно (инициализация суставов / сбор препятствий) и трогать их без нужды не следует. Приоритет — **низкий**.
3. Реальные кандидаты на разбиение (смешанная логика, высокая когнитивная сложность): `KvRobotImport.ImportUrdf` (301), `KvReportPdf.Generate` (254), `KvWaypoints.RebuildRoute` (177), `KvCalibration.Build` (196), `KvCollisionOptimizer.Rebuild` (154), `KvConstrainedPlanner.Build` (144). Приоритет — **средний**.
4. `KvIcons.Draw` (610 строк) — сознательное решение: единый мини-растеризатор иконок без ассетов. Приоритет — **низкий**.

---

## 4. Магические числа

Автоматический поиск числовых литералов в **критичных модулях** (планирование, кинематика, безопасность, математика траекторий) вне объявлений `const`.
Найдено **423** литерала в 17 файлах. Литералы `0`, `1`, `2`, `180`, `360`, `0.5`, `1.0`, `2.0` и любые строки формата (`"0.000"`, `"0.0"`) из выборки исключены.

| Файл | Литералов | Оценка |
|---|---:|---|
| `01_Scripts\Features\KvTrajMath.cs` | 94 | математика траекторий: допуски сходимости, веса штрафов, коэффициенты сглаживания |
| `01_Scripts\Features\KvCalibration.cs` | 67 | пороги регуляризации (`1e-6`, `1e-9`, `1e-12`, `1e-14`, `1e-18`), 32 итерации, стартовое приближение `{1.0, 0.7, 0.3}`, порог `≥ 12°` |
| `01_Scripts\Trajectory\TrajectoryFlowController.cs` | 54 | тайминги состояний, скорости фантомов, дистанции фиксации |
| `01_Scripts\Trajectory\Planner.cs` | 53 | шаг дискретизации, число итераций, веса оценки траектории |
| `01_Scripts\Features\KvEnergyOptimal.cs` | 26 | сетка профилей (шаги `accelScale`/`velScale`) |
| `01_Scripts\Features\KvPayloadCalculator.cs` | 25 | номиналы моментов J1..J6 (95/95/55/16/11/7), призма 600 Н, `g = 9.81f` |
| `01_Scripts\Features\KvPathSmoothing.cs` | 23 | шаги сглаживания, допуски |
| `01_Scripts\Trajectory\IkSolver.cs` | 21 | 160 итераций CCD, допуски сходимости, число стартовых приближений (3) |
| `01_Scripts\Features\KvTimeOptimal.cs` | 20 | клампы лимитов `1/1/10 … 720/5000/100000`, переход в СИ `÷900` |
| `01_Scripts\Features\KvRobotExport.cs` | 16 | скорости и зоны RAPID (`v500`, `z50`, `z10`, `v200`, `z5`), формат `9E9` |
| `01_Scripts\Trajectory\PoseValidator.cs` | 9 | допуски проверки позы |
| `01_Scripts\Core\SCARAController.cs` | 7 | диапазоны осей |
| `01_Scripts\Core\SixAxisController.cs` | 4 | диапазоны осей |
| `NarrowPhase.cs`, `ReachabilityOracle.cs`, `SafetyGate.cs`, `RobotSelfCollision.cs` | 1 каждый | одиночные допуски |

**Разбор по критичности.**

*Высокая (числа влияют на безопасность и на результат планирования, но не названы):*
- `KvCalibration.cs:442` `if (Math.Abs(det) < 1e-10) return null;` и `KvCalibration.cs:692` `if (Math.Abs(det) < 1e-18) return null;` — **два разных порога вырожденности в одном файле** (`1e-10` для `SolveToolOffset`, `1e-18` для `SolvePlaneOffset`). Пороги не названы и не связаны между собой; тесты `KvCalibrationTests` (см. `Docs\CHANGELOG.md` и отчёт по этапу 5) подтвердили практическое следствие: тесный набор точек (~1 мм) отвергается, а коллинеарный набор огромного масштаба — нет.
- `KvCalibration.cs:666` `a[r, r] += trace * 1e-9 + 1e-12;` — регуляризация Тихонова «на глазок».
- `KvCalibration.cs:672` `for (int iter = 0; iter < 32; iter++)` — фиксированное число итераций обратной задачи.
- `IkSolver.cs` — 160 итераций CCD и 3 стартовых приближения (в §0.5 контекста это описано как исправление бага «IK не сходилась», но числа остались литералами).
- `KvSafetyTools.cs:701 Validate` — единственный литерал в файле, но файл отвечает за предполётную проверку.

*Средняя (номиналы железа, которые должны быть настраиваемыми):*
- `KvPayloadCalculator.cs` — номиналы моментов по осям (`95/95/55/16/11/7`) и усилие призмы (`600`) зашиты в код. Робот в проекте один и SCARA один, поэтому это работает, но добавление второй модели робота потребует правки кода.
- `KvRobotExport.cs` — `v500 / z50 / z10 / v200 / z5` зашиты: сгенерированная программа всегда идёт на этих скоростях, изменить их без правки кода нельзя.
- `KvTimeOptimal.cs` — клампы лимитов (`1/1/10 … 720/5000/100000`) и `÷900` (перевод °/с ↔ рад/с × 10) продублированы в нескольких местах.

*Низкая:* коэффициенты сглаживания и веса штрафов в `KvTrajMath`/`KvPathSmoothing` — предмет экспериментального подбора, вынесение в константы улучшит читаемость, но не изменит поведение.

**Рекомендация (в рамках этого этапа не выполнялась):** вынести пороги калибровки и номиналы нагрузки в именованные `const` с комментарием-обоснованием либо в `KvSettings`/`kazistovvv_settings.json`. Приоритет — `KvCalibration` (пороги вырожденности) и `KvRobotExport` (скорости).

---

## 5. Потенциальные NullReference

### 5.1 Сводка

| Категория | Найдено |
|---|---:|
| Результат `GetComponent*`/`Find*` присваивается и разыменовывается без проверки на `null` (широкий критерий) | **218** |
| Из них — повышенной значимости (исключены `AddComponent`/`new GameObject(...)` в 6 строках выше и любые проверки в 5 строках ниже) | **99** |
| `FindObjectOfType` / `FindObjectsOfType` / `FindAnyObjectByType` / `FindObjectsByType` всего в проекте | 92 |
| `transform.Find` / `GameObject.Find` / `.Find(` всего в проекте | 71 |

### 5.2 Типовые **ложные срабатывания** (основная масса 218)

**(ЛС-1) `GetComponent` сразу после `AddComponent`/`new GameObject(..., typeof(X))`.** Компонент гарантированно есть — это документированный идиом Unity. Пример (`KvToolbarGroups.cs:241-248`):

```csharp
GameObject go = new GameObject("KvGroupMenuBlocker", typeof(Image), typeof(Button));
...
Image img = go.GetComponent<Image>();     // анализатор: «нет проверки» — на деле всегда есть
Button btn = go.GetComponent<Button>();
```

Такой шаблон повторяется в 60+ местах построения интерфейса (`KvWidgets`, `KvTheme`, `KvMenuBar`, `KvToolbar`, `KvDockPanel`, `KvTreeView`, `KvSettingsView`, `KvCommandPalette`, `KvContextMenu`, `KvFeatureWindow`, `KvTeachPendant`, `KvWorkbenchWindow`, `KvUiStates`, `KvHotkeyView`, `KvToolbarGroups`, `KvStartMenu`, `KvHealthMonitor`, `KvSafetyTools`, `KvAutomation`).

**(ЛС-2) Массивы от `FindObjectsByType<T>()`** не могут быть `null` (возвращается пустой массив). Пример: `KvSessionManager.cs:217`. Проверка на `null` не нужна, нужна проверка на пустоту — она там есть (`for (int i = 0; i < robots.Length; i++)`).

### 5.3 Находки, требующие внимания

**5.3.1 `transform.Find` без проверки (диагностика, но падает «в стол»)**
```
Assets\_Project\01_Scripts\Editor\DshStage2Diag.cs:271   Transform canvas = ui.transform.Find("KazistovVvCanvas");
Assets\_Project\01_Scripts\Editor\DshStage2Diag.cs:338   Transform canvas = ui.transform.Find("KazistovVvCanvas");
Assets\_Project\01_Scripts\Editor\DshStage2Diag.cs:358   Transform visible = ui.transform.Find("KazistovVvCanvas");
```
Результат `Find` может быть `null`, если имя объекта изменится. Диагностика — код одноразовый, риск низкий, **но имя объекта здесь захардкожено строкой** и уже один раз менялось (`KavistovVv_UI` → `KazistovVv_UI`; сейчас `KazistovVvCanvas`) — при следующем переименовании эти три строки молча начнут падать. Рекомендация: сравнивать с `KazistovVvUIManager.Instance.name` или искать компонент, а не объект по имени.

**5.3.2 `GetComponentInChildren<Renderer>()` в `CenterWindow.cs:250, 258`**
```csharp
previewRenderer = preview.GetComponentInChildren<Renderer>();
```
Затем `previewRenderer` используется для отрисовки превью. Если у модели префаба нет `Renderer` (или префаб пуст), будет `NullReferenceException` в `LateUpdate`/`OnGUI`. Это **самый вероятный реальный отказ** из всей выборки: содержимое превью приходит извне (префаб задаётся в сцене), проверки нет. Рекомендация: проверять на `null` и показывать состояние `empty` (в UI уже есть `KvUiStates`).

**5.3.3 `TargetMarker.cs:18`**
```csharp
var mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
```
Двойное разыменование без проверок. Если у сферы нет `MeshFilter` — падение. Файл маленький (36 строк), проверка стоит одной строки.

**5.3.4 `KvPropertiesView.cs:281`**
```csharp
rowPool[i].GetComponent<LayoutElement>().preferredHeight = 17f;
```
Строка пула должна иметь `LayoutElement` — он навешивается при создании пула (`:312`), поэтому на практике безопасно. **ЛС** с оговоркой: при изменении `BuildRow` строка упадёт.

**5.3.5 Поля, инициализируемые в `Build()` и используемые в `Update()`**
Шаблон «`Build()` создаёт объекты → `Update()`/`OnGUI()` их использует» встречается во всех зонах интерфейса (`KvUiStates`, `KvGamepadHud`, `KvDockPanel`, `KvCommandPalette`, `KvContextMenu`, `KvHotkeyView`, `KvFeatureWindow`, `KvTeachPendant`). Пока `Build()` вызывается в `Awake`/`Start` — безопасно; если когда-нибудь появится путь, где `Update` идёт раньше `Build`, будет пачка `NullReferenceException`. Сейчас такого пути нет (проверено чтением порядка вызовов), но это **системный риск**, а не отдельный баг.

**5.3.6 Обработчики событий без отписки + отложенное уничтожение объектов** — см. раздел 6.4 отчёта `Docs\PERFORMANCE_AUDIT.md`: подписчики вызываются у объектов, которые уже уничтожены, если сервис-синглтон живёт дольше подписчика. Это второй по вероятности источник реальных `NullReferenceException`/`MissingReferenceException`, и он системный.

---

## 6. Пустые catch-блоки и проглоченные исключения

### 6.1 Полностью пустые `catch` — 6, все в одном файле

| # | Файл | Строка | Контекст |
|---|---|---|---|
| 1 | `01_Scripts\Core\FreeFlyCameraController.cs` | 236 | `TryGetKey` — чтение `Input.GetKeyDown` внутри `try/catch { }`, при исключении возвращается `false` |
| 2 | `01_Scripts\Core\FreeFlyCameraController.cs` | 293 | то же (второй набор клавиш) |
| 3 | `01_Scripts\Core\FreeFlyCameraController.cs` | 329 | то же |
| 4 | `01_Scripts\Core\FreeFlyCameraController.cs` | 368 | то же |
| 5 | `01_Scripts\Core\FreeFlyCameraController.cs` | 413 | то же |
| 6 | `01_Scripts\Core\FreeFlyCameraController.cs` | 429 | то же |

Пример (`:236`):
```csharp
        }
        catch
        {
        }
        return false;
```
**Оценка.** Это защита от исключений старого `Input`-API, когда ввод отключён или недоступен (в batch-режиме `-nographics`, при выгрузке домена). Смысл «проглотить и вернуть false» здесь осознанный. **Но:** (а) `catch` без типа ловит и `OutOfMemoryException`, и `MissingReferenceException` — лучше `catch (Exception)`, а ещё лучше — конкретное исключение; (б) нет ни одной строки диагностики, поэтому если ввод сломается по другой причине, это будет невидимо. В шести местах продублирован один и тот же приём — кандидат на один общий хелпер `TryInput(Func<bool>)`. Приоритет — **низкий** (поведение менять нельзя, это защитный код).

### 6.2 `catch` с логированием — норма

Остальные обработчики в проекте пишут в лог или в отчёт: `Debug.LogWarning("[KvLoc] …")`, `Debug.LogWarning("[KvSettings] …")` и т. п. Проглоченных исключений без следа, кроме перечисленных 6, не найдено.

### 6.3 `catch (Exception) { }` в диагностике

```
Assets\_Project\01_Scripts\Editor\DshFullVerifyDiag.cs:196-197
    try { File.AppendAllText(ReportPath, text + "\n", new UTF8Encoding(false)); }
    catch (Exception) { }
```
Диагностический код: запись отчёта намеренно не должна ронять прогон. Приемлемо, но при сбое записи отчёт просто не появится без объяснения.

---

## 7. Сводка и приоритеты

| Приоритет | Что | Где | Тип |
|---|---|---|---|
| **Высокий** | `GetComponentInChildren<Renderer>()` без проверки — единственная находка с реалистичным сценарием отказа на данных из сцены | `CenterWindow.cs:250, 258` | NullReference |
| **Высокий** | Отсутствие отписок от событий сервисов-синглтонов (43 подписки `Message`, 10 анонимных делегатов) | `KvStageHub*.Awake`, `FeatureHub.cs:122-154` | Логика/утечка |
| **Средний** | Два несвязанных порога вырожденности (`1e-10` и `1e-18`) в калибровке | `KvCalibration.cs:442, 692` | Магические числа |
| **Средний** | Скорости/зоны в генерируемых программах зашиты (`v500/z50/z10/v200/z5`) | `KvRobotExport.cs` | Магические числа |
| **Средний** | Номиналы моментов и усилие призмы зашиты | `KvPayloadCalculator.cs` | Магические числа |
| **Средний** | Захардкоженное имя объекта `"KazistovVvCanvas"` в трёх местах диагностики | `DshStage2Diag.cs:271, 338, 358` | Хрупкость |
| **Средний** | `TargetMarker.cs:18` — двойное разыменование `GetComponent<MeshFilter>().sharedMesh` | `TargetMarker.cs` | NullReference |
| **Низкий** | 13 неиспользуемых приватных методов/полей | см. 1.1/1.2 | Мёртвый код |
| **Низкий** | 30 точных дубликатов тел методов (20 из них — в диагностике) | см. 2.1 | Дубликаты |
| **Низкий** | 6 пустых `catch` без типа и без логирования | `FreeFlyCameraController.cs` | Проглоченные исключения |
| **Низкий** | 37 продуктовых методов длиннее 100 строк (реально проблемных — 6) | см. раздел 3 | Читаемость |

**Чего в проекте НЕТ (хорошая новость):**
- ни одного `GetComponent` в `Update`/`LateUpdate`/`FixedUpdate` (оба найденных — в `Awake`, это правильно);
- ни одного `FindObjectOfType`/`FindObjectsOfType` в методах кадра (0 из 92 вызовов);
- ни одного LINQ-вызова в горячих путях (`using System.Linq` есть только в 2 файлах, оба — редакторские);
- ни одного незакрытого файлового потока (все 100 % обращений к файлам — через `File.ReadAllText`/`WriteAllText` или внутри `using`);
- ни одного «мёртвого» типа в продуктовом коде.

**Не проверялось автоматически и требует отдельного решения:** корректность алгоритмов (это предмет тестов этапа 5 и прогонов `Dsh*Diag`), потокобезопасность (`KvNetTools` использует сокеты — см. отчёт по производительности), а также узкие места, которые видны только под профайлером.
