# GIT_AUDIT.md — аудит Git-истории проекта KazistovVv

**Дата:** 15.09.2026
**Тип документа:** отчёт. **В репозитории ничего не изменялось: ни коммитов, ни индекса, ни `.gitignore` (только чтение).**
**Репозиторий:** рабочий каталог проекта, remote `https://github.com/V1iktorK/KavistovVv.git` (имя репозитория по ТЗ не меняется).

---

## 0. Кратко о состоянии

| Показатель | Значение |
|---|---:|
| Коммитов в текущей ветке | **27** |
| Текущая ветка | **`Cline`** |
| HEAD | `6413957` — 2026-09-08, `Viiktor_k`, «fix: InputManager Awake pattern, Update() return bug + SixAxisController self-collision init order» |
| Веток локально | 9 (`Cline`, `main`, `responsible-pickle`, `gigacode-handoff-robot-kinematics`, 5 × `agents/*`) |
| Веток на remote | 3 (`origin/main`, `origin/Cline`, `origin/HEAD → main`) |
| Файлов под контролем версий | **747** |
| Изменено (не закоммичено) | **17** |
| Удалено | **6** |
| Не отслеживается (untracked) | **98** |
| Файлов сцены `MainScene.unity` в истории | менялась в **10 коммитах** |

> **Важно для понимания контекста.** По §0.0 `PROJECT_CONTEXT.md` рабочая копия потеряла локальную ветку `DeepSeek` во время инцидента 13.09.2026; в копии пользователя есть `Cline` и `main`. Локальный HEAD (`6413957`, 08.09.2026) **старше** всей работы сессий 11–15.09.2026, поэтому почти всё содержимое `Assets\_Project` числится как «изменённое» или «новое». Это не признак беспорядка в работе, а следствие инцидента.

---

## 1. Статистика

### 1.1 Авторы

| Автор | Коммитов |
|---|---:|
| `Viiktor_k <milovanovv30@gmail.com>` | 25 |
| `Viiktor_K <milovanov777viktor@ya.ru>` | 1 |
| `Viiktor_k <241238217+V1iktorK@users.noreply.github.com>` | 1 |

Все три записи — один человек с разными настройками `user.name`/`user.email` (смена почты и переход на GitHub-noreply). **Рекомендация (не выполнялась):** зафиксировать единые `user.name`/`user.email` в репозитории, чтобы статистика не размазывалась.

### 1.2 Файлы, менявшиеся чаще всего (вся история)

| Коммитов | Файл | Комментарий |
|---:|---|---|
| 12 | `.plastic/plastic.wktree` | метаданные Plastic SCM — **шум в истории** |
| 12 | `Assets/_Project/01_Scripts/Input/InputManager.cs` | ядро ввода |
| 12 | `.plastic/plastic.changes` | метаданные Plastic SCM — **шум** |
| 11 | `Packages/manifest.json` | подключение пакетов |
| 11 | `Assets/_Project/01_Scripts/Core/SixAxisController.cs` | кинематика робота |
| 11 | `Packages/packages-lock.json` | автогенерируемый файл |
| 10 | `Assets/_Project/01_Scripts/Core/FreeFlyCameraController.cs` | камера + управление |
| 10 | `ProjectSettings/ProjectSettings.asset` | настройки проекта |
| 10 | `Assets/_Project/01_Scripts/Core/HDRPAutoLighting.cs` | свет |
| 10 | `Assets/_Project/01_Scripts/Core/RobotController.cs` | робот |
| 10 | `Assets/_Project/00_Scenes/MainScene.unity` | главная сцена |
| 9 | `Assets/_Recovery/0.unity` | **автобэкап Unity** — шум |
| 9 | `Assets/_Project/01_Scripts/Input/GamepadInputProvider.cs` | ввод |
| 9 | `Assets/_Project/01_Scripts/Input/KeyboardMouseInputProvider.cs` | ввод |
| 9 | `ProjectSettings/EditorBuildSettings.asset` | список сцен |

**Вывод:** 4 из 15 самых «горячих» файлов — это служебные (`.plastic/*`, `packages-lock.json`, `Assets/_Recovery/*`). Их стоит исключить из-под контроля версий (см. раздел 6).

---

## 2. Файлы без `.meta` (потенциальные проблемы Unity)

**Проверено:** все 423 файла в `Assets\` (кроме самих `.meta`).

| Показатель | Значение |
|---|---:|
| Файлов ассетов в `Assets\` | 423 |
| **Без парного `.meta`** | **2** |
| Каталогов в `Assets\` | 114 |
| Каталогов без `.meta` | 6 (2 из них — новые папки тестов, уже получили `.meta` при импорте) |
| «Осиротевших» `.meta` (есть `.meta`, нет ассета) | **0** |

**Оба файла без `.meta`:**

```
Assets\XR\APILayers~\WindowsLayers\x64\XrApiLayer_METAX_operator.dll
Assets\XR\APILayers~\WindowsLayers\x64\XrApiLayer_METAX_operator.json
```

**Это не проблема.** Каталог `APILayers~` оканчивается на `~` — Unity **намеренно игнорирует** такие каталоги (соглашение «тильда = не импортировать»), поэтому `.meta` для них не создаётся и не нужен. Это слои OpenXR от Meta.

**До запуска импорта** без `.meta` были ещё 6 файлов — новые тесты `Assets\_Project\08_Tests\Editor\Kv*Tests.cs`; после прогона Unity они получили `.meta` автоматически (проверено: 6 `.cs.meta` + `08_Tests.meta` + `Editor.meta` созданы).

**Осиротевших `.meta` нет ни одного** — это значит, что при переименованиях и удалениях `.meta`-файлы не забывались. Отдельно подтверждено для переименований этапа 1: у `06_KazistovVv_UI`, `KazistovVvUIManager.cs`, `KazistovVvMenu.cs`, `kazistovvv_i18n`, `kazistovvv_settings.json` `.meta` переименованы вместе с ассетами, GUID сохранены, ссылки в сцене не порвались.

---

## 3. Файлы вне Git (новые, не закоммичены) — 98 позиций

### 3.1 Сводка по типу

| Расширение | Файлов | Что это |
|---|---:|---|
| `.meta` | 40 | `.meta` к новым ассетам (создаются Unity) |
| `.glb` | 17 | **посторонние glTF-файлы в корне `Assets\`** — см. 3.3 |
| `.cs` | 16 | новые скрипты (диагностика `Dsh*Diag.cs`, `RobotDH.cs`, запись, UI-модуль, тесты) |
| `.txt` | 11 | отчёты диагностики `_dsh_*.txt` в корне проекта |
| без расширения | 8 | каталоги и служебные файлы |
| `.unity` | 3 | `Assets\_Recovery\0 (3|4|5).unity` |
| `.bak` | 1 | `MainScene.unity.bak` |
| `.md` | 1 | `PROJECT_CONTEXT.md` |
| `.slnx` | 1 | `KazistovVv.slnx` (результат переименования на этапе 1) |

### 3.2 Целые подсистемы, которых нет в Git

Это главная находка раздела. Под контролем версий **отсутствуют**:

| Путь | Что там | Почему важно |
|---|---|---|
| `Assets/_Project/06_KazistovVv_UI/` (+ `.meta`) | **весь модуль десктопного интерфейса в стиле FreeCAD — 35 скриптов, ~9 000 строк** | самая крупная подсистема проекта вообще не закоммичена |
| `Assets/_Project/01_Scripts/Features/` (частично) | модули «Функции» (этапы 1–36) | значительная часть появилась после последнего коммита |
| `Assets/StreamingAssets/` (целиком) | **7 словарей локализации `kazistovvv_i18n/*.json` + `kazistovvv_settings.json` + `robots/`** | внешние данные; без них локализация и внешние настройки не соберутся у другого разработчика |
| `Assets/_Project/08_Tests/` | **215 unit-тестов (этап 5 этой сессии)** | новые тесты |
| `Assets/_Project/Docs/` | `TrajectoryAlgorithmPlan.md` | документация проекта |
| `Docs/` | отчёты этой сессии (8 файлов) | отчёты |
| `PROJECT_CONTEXT.md` | главный файл знаний проекта (~3 200 строк) | **не в Git** |
| `Assets/_Project/01_Scripts/Editor/Dsh*Diag.cs` (10 файлов) + `_dsh_*.txt` (11 отчётов) | диагностика агента | по правилам проекта в копию пользователя не переносится, но и в Git не попадала |

**Рекомендация (не выполнялась — этап «только отчёт»):** закоммитить `Assets/_Project/06_KazistovVv_UI`, `Assets/_Project/08_Tests`, `Assets/StreamingAssets`, `Assets/_Project/Docs`, `PROJECT_CONTEXT.md`, `Docs/`. Это критично: сейчас **при потере рабочей копии** (а такой инцидент уже был 13.09.2026, §0.0) весь UI-модуль и все словари восстановить из Git будет нельзя.

### 3.3 Посторонние `.glb` в корне `Assets\` — 17 файлов

```
Assets/Cline_FillLight.glb        Assets/Cline_KeyLight.glb       Assets/Cline_RimLight.glb
Assets/Desk Table Black.glb       Assets/Directional Light.glb    Assets/HDRP Auto Lighting.glb
Assets/Level.glb                  Assets/Main Camera.glb          Assets/Plane.glb
Assets/Post Process Volume.glb    Assets/Reflection Probe.glb     Assets/Reflection Probe (1).glb
Assets/Robot.glb                  Assets/ScaraRobot .glb          Assets/Sky and Fog Volume.glb
Assets/Spline.glb                 Assets/StaticLightingSky.glb
```

Все созданы **одной пачкой 2026-09-09, 07:04** (плюс `.meta` к каждому — итого 34 файла). По именам это **дамп объектов сцены в glTF** (имена совпадают с объектами `MainScene.unity`: `Main Camera`, `Directional Light`, `Sky and Fog Volume`, `Reflection Probe`, `Robot`, `ScaraRobot`).

**Оценка:** это артефакт разового экспорта, не часть проекта. Лежат **в корне `Assets\`** (а не в `Assets\_Project\03_Models\`), тянет за собой 17 `.meta`, замусоривает окно Project и попадает в сборку ассетов. **Рекомендация:** удалить (в Git их нет, потерять нечего) или перенести в `Assets/_Project/03_Models/_exports/`. **Ничего не удалялось — это решение пользователя.**

### 3.4 Изменённые файлы (17) и удалённые (6)

**Изменённые** — 10 скриптов (`FreeFlyCameraController`, `HDRPAutoLighting`, `RobotController`, `RobotSelfCollision`, `SCARAController`, `SixAxisAutoSetup`, `SixAxisController`, `ConvertRobotMaterialsToHDRP`, `InputManager`, `KeyboardMouseInputProvider`), `MainScene.unity`, `ProjectSettings.asset`, `QualitySettings.asset`, `HDRPHighQuality.asset`, `desk_table.mat`, 2 файла `.plastic/`. Часть из них изменена этой сессией (переименование `Kavistov` → `Kazistov`), часть — предыдущими.

**Удалённые (6):**
```
D Assets/CompositionLayers/UserSettings/CompositionLayersPreferences.asset(.meta)
D Assets/CompositionLayers/UserSettings/Resources.meta
D Assets/CompositionLayers/UserSettings/Resources/CompositionLayersRuntimeSettings.asset(.meta)
D KavistovVv.slnx
```
Первые пять — настройки Composition Layers (пересоздаются Unity при необходимости). Последний — **результат переименования этапа 1** (`KavistovVv.slnx` → `KazistovVv.slnx`); так как Unity генерирует имя файла решения по имени **папки** проекта, при следующей пересборке project-файлов `KavistovVv.slnx` появится снова (пустой). Это описано в `CODE_AUDIT.md`/`PROJECT_CONTEXT.md` §0.7.1 как косметика; лечится только переименованием папки проекта, чего ТЗ запрещает.

---

## 4. Большие файлы (> 10 МБ) в истории — 1

| Размер | Путь |
|---:|---|
| **15,3 МБ** | `Packages/io.realvirtual.starter/Samples~/ObjectHandling/DemoGrippingAdvanced.unity` |

Проверено по всем 1 289 объектам истории (`git rev-list --objects --all` + `git cat-file --batch-check`).
**Оценка:** файл лежит в `Packages/` (встроенный пакет realvirtual), а не в `Assets/`, и является сценой-примером пакета. 15,3 МБ — не критично, но это единственный крупный объект. Если понадобится уменьшить размер клона, кандидат — перевести на Git LFS (`.gitattributes` уже содержит макрос `[attr]lfs` и, судя по шаблону, готов к этому).

Других объектов > 10 МБ в истории нет; `.git` занимает 168,8 МБ (в основном за счёт больших бинарных ассетов — моделей и текстур из `Assets\ROOMS`, `Assets\TABLES`, `Assets\Premises`).

---

## 5. Проверка `.gitignore`

**Метод:** `git check-ignore -v --no-index <путь>` — проверяются фактические правила, а не наличие строки.

### 5.1 Что исключено правильно ✔

| Путь | Правило |
|---|---|
| `Library/` | `.gitignore:14` `/[Ll]ibrary/` ✔ |
| `Temp/` | `.gitignore:15` `/[Tt]emp/` ✔ |
| `obj/` | `.gitignore:16` `/[Oo]bj/` ✔ |
| `Build/`, `Builds/` | `.gitignore:17-18` `/[Bb]uild/`, `/[Bb]uilds/` ✔ |
| `Logs/` | `.gitignore:19` `/[Ll]ogs/` ✔ |
| `UserSettings/` | `.gitignore:20` `/[Uu]ser[Ss]ettings/` ✔ |
| `*.log` | `.gitignore:21` ✔ |
| `.vs/` | `.gitignore:43` ✔ |
| `.gigacode_vsc/`, `.codebuddy/` | `.gitignore:110-111` ✔ |
| `*.csproj`, `*.sln`, `*.user`, `*.pidb`, `*.apk`, `*.aab` | шаблон Unity ✔ |

**Ни один служебный каталог Unity (`Library`, `Temp`, `Logs`, `obj`, `.vs`) не отслеживается** — проверено `git ls-files` (результат пуст). Это правильно.

### 5.2 Что НЕ исключено и попадает в историю ✘

| Путь | Проблема |
|---|---|
| **`.plastic/`** | **отслеживается и уже попал в историю** (`plastic.wktree` и `plastic.changes` менялись по 12 раз — это 4-е и 5-е места по «горячности»). Это метаданные **другой** системы контроля версий (Plastic SCM), при живом Git они бесполезны и создают шум в диффах |
| **`.claude/`** | не исключён (навыки агента) |
| **`.vscode/`** | не исключён (`settings.json`, `launch.json`, `extensions.json`) — обычно это личные настройки |
| **`.opencode/`** | не исключён |
| `Assets/_Recovery/` | **автобэкап сцен Unity** — 9 коммитов в истории; это не исходники |
| `Packages/packages-lock.json` | 11 коммитов; обычно коммитится (это допустимо, но стоит знать) |
| `KazistovVv.slnx` / `KavistovVv.slnx` | `.slnx` не покрыт правилом `*.sln` → файл решения попадает в untracked. Unity перегенерирует его сама |
| `_dsh_*.txt`, `_dsh_*_out/`, `*.bak` | диагностические артефакты не исключены |

**Рекомендуемые добавления в `.gitignore` (НЕ вносились — этап «только отчёт»):**
```gitignore
# Plastic SCM metadata (проект ведётся в Git)
/.plastic/

# Личные настройки агентов/редакторов
/.claude/
/.vscode/
/.opencode/

# Автобэкапы сцен Unity
/[Aa]ssets/_Recovery/
/[Aa]ssets/_Recovery.meta

# Автогенерируемые файлы решения
*.slnx

# Артефакты диагностики проекта
/_dsh_*
*.bak
```

---

## 6. Сводка и приоритеты

| Приоритет | Находка | Действие (рекомендация) |
|---|---|---|
| **Критично** | В Git **нет** модуля интерфейса `06_KazistovVv_UI` (35 файлов), словарей `Assets/StreamingAssets` (9 файлов), тестов `08_Tests`, `PROJECT_CONTEXT.md` и `Docs/` | Закоммитить. При повторении инцидента 13.09.2026 восстановить их из Git будет невозможно |
| **Высокий** | 17 посторонних `.glb` (+17 `.meta`) в корне `Assets\` от 09.09.2026 — дамп объектов сцены | Удалить или перенести в `_Project/03_Models/_exports/` |
| **Средний** | `.plastic/` отслеживается (12 изменений) при живом Git | Добавить в `.gitignore`, вывести из индекса (`git rm -r --cached .plastic`) |
| **Средний** | `Assets/_Recovery/` (автобэкапы Unity) в истории — 9 коммитов | Добавить в `.gitignore` |
| **Средний** | Три разных записи автора (имя/почта) | Задать единые `user.name`/`user.email` |
| **Низкий** | `*.slnx` не исключён, Unity перегенерирует его по имени папки | Добавить `*.slnx` в `.gitignore` |
| **Низкий** | Один объект истории 15,3 МБ (`Packages/.../DemoGrippingAdvanced.unity`) | Оставить или перевести на LFS |
| **Низкий** | `_dsh_*.txt`, `*.bak` в корне проекта не исключены | Добавить в `.gitignore` |

**Что в порядке (проверено):**
- `.meta`-файлы: **0 осиротевших**, отсутствуют только у 2 файлов в намеренно игнорируемом каталоге `APILayers~`;
- все служебные каталоги Unity (`Library`, `Temp`, `Logs`, `obj`, `.vs`) **исключены и не отслеживаются**;
- переименования этапа 1 сделаны корректно — `.meta` переехали вместе с ассетами, GUID сохранены;
- удалённых «молча» ассетов без соответствующего удаления `.meta` нет.

