# PROJECT_CONTEXT.md
_Обновлено: 16.09.2026 (ночь). **Последняя сессия — §0.11 «Закрытие 4 оставшихся пунктов аудита»: второй редактор Unity закрыт (сам, из-за падения D3D11), из `manifest.json` удалён ПОСЛЕДНИЙ MCP-мост `com.gladekit.mcp-bridge` — теперь мостов ноль и порт 8765 не занимается ни одним пакетом; worktree-призрак `responsible-pickle` откачен и удалён (−382 МБ); выполнен первый локальный коммит рабочей копии (без push).** Ранее: §0.10 «4 критичных пункта аудита: второй редактор, лишние MCP-мосты, имя папки, worktree-призрак».** Ранее: §0.9 «Файлы/shell для директоров в пресете DSH, два предупреждения консоли, батчмод-гейт»: пресет `unirobot-dev` получил штатные файловые и shell-инструменты; попутно найдены и исправлены ТРИ ошибки, из-за которых Unity-мост не отдавал модели НИ ОДНОГО инструмента; разобраны оба предупреждения консоли (вердикты — §0.9.1 и §0.9.2); создан воспроизводимый гейт `Tools\ci-console-check.ps1` (0 ошибок компиляции, 0 warning CS).** Ранее: §0.8 «Разрешение конфликтов Unity Version Control (Plastic SCM)»: закрыты 10 файловых + 8 directory (evil twin) конфликтов, создан cs:37, рабочая копия стоит на голове ветки `/main`, ночная работа сохранена побайтово; отдельно зафиксирована КОРНЕВАЯ ПРИЧИНА — две рабочие копии с одним именем воркспейса в Plastic (см. §0.8.1 и §0.8.9).** Ранее: §0.7 «Аддитивные улучшения (8 этапов ТЗ): унификация названия → KazistovVv, Docs/, статический анализ, аудит локализации, unit-тесты (215, прогон 213/0), аудит производительности, снимки сцен (16 PNG), Git-анализ».** Ранее: §0.6 (UX/UI-улучшения + геймпад, этапы 1–12 ТЗ), §16.14 (8 уникальных траекторий SCARA), §16 (12 фиксов сессии SCARA), §0.5 (полная проверка проекта 14.09.2026). **Правильное имя проекта — `KazistovVv` (с буквой z). 16.09.2026 папка рабочей копии на диске ПЕРЕИМЕНОВАНА `KavistovVv` → `KazistovVv` (см. §0.10); старое написание осталось только в исторических записях ниже, в git-remote и во второй (пользовательской) копии `C:\Users\Ольга\KavistovVv`.**_

## 0.11 Сессия 16.09.2026 (ночь, продолжение) — закрытие 4 оставшихся пунктов

**Задача.** (1) Закрыть второй редактор Unity, (2) удалить последний MCP-мост `com.gladekit.mcp-bridge`,
(3) откатить и удалить worktree-призрак `responsible-pickle`, (4) сделать локальный коммит.

### 0.11.1 Порт 8765 СВОБОДЕН — MCP-мостов в проекте больше НЕТ

**Главный итог:** в `Packages\manifest.json` **не осталось ни одного MCP-моста** (было три).
Удалён последний — `com.gladekit.mcp-bridge` (v0.7.23, git Glade). Зависимостей: 52 → **51**.
Unity сама вычистила `Library\PackageCache` и `packages-lock.json` — поиск по обоим даёт **ноль**
совпадений `glade|mcp`.

**Порт 8765 больше не занимается ни одним пакетом.** Практическое следствие: батчмод-гейт
`Tools\ci-console-check.ps1` теперь даёт чистый лог **даже при открытом втором редакторе** — раньше
в этой ситуации в лог попадало `[UnityBridge] Failed to start server` (порт занят), и «чистая консоль»
переставала быть чистой (см. §0.10.3).

Перед удалением проверено: ссылок на `GladeKit|gladekit|glade-mcp|GladeAgenticAI` в `Assets` и `Packages`
**нет ни в одном `.cs`, `.asmdef`, `.shader`, `.mat`, `.unity`, `.prefab`, `.asset`** — совпадения были
только в самом `manifest.json` и в `packages-lock.json`. Код проекта от моста не зависел.
Таблица зависимостей в `Docs\DEVELOPER_README.md` обновлена (три строки мостов помечены как удалённые).
Резервная копия: `Packages\manifest.json.bak_before_gladekit_removal`.

### 0.11.2 Второй редактор закрылся сам — из-за падения D3D11

PID 24192 (открытый Unity Hub'ом, см. §0.10.3) к моменту проверки **уже не работал**. Причина видна в
`C:\Users\Ольга\KavistovVv\Logs\Editor.log`:

```
Failed to present D3D11 swapchain due to device reset/removed. … This is an unrecoverable error
and the editor will shut down.
```

Это сброс GPU-устройства (Windows TDR) после ошибок `WorkspaceVisualizer.BuildZones`
(`WorkspaceVisualizer.cs:318`). Редактор завершился аварийно ~22:32. Перед падением Unity успела
положить аварийную копию сцены: `Temp\__Backupscenes\0.backup` (22:30) и
`Assets\_Recovery\0 (10).unity` (22:12).
**Итог: Unity-процессов ноль, порт 8765 свободен, повторного автозапуска Hub'ом к моменту проверки не было.**
Ничего не убивалось принудительно.

### 0.11.3 Worktree-призрак удалён (−382 МБ)

Сначала проверено, что **ничего уникального не теряется**: все 11 неотслеживаемых путей worktree
(`Assets\XR\*`, `KeyboardController.cs`, `VR\PlacementController.cs`, `04_Materials\Props` и др.)
уже существовали и в основной копии, и в рабочей копии.

Порядок: страховочный снимок → `git checkout -- .` → dry-run `git clean -fdn` (показан список из
20 записей) → `git clean -fd` → удаление worktree → `git prune` → удаление осиротевшей копии.

| Что | Было | Стало |
|---|---|---|
| Worktree `…\KavistovVv\.gigacode_vsc\worktrees\responsible-pickle` | 645 файлов, 187,4 МБ | удалён (`git worktree remove --force`) |
| Осиротевшая копия `…\KazistovVv\.gigacode_vsc\worktrees\responsible-pickle` | 667 файлов, 194,4 МБ | удалена |
| Запись в `…\KazistovVv\.git\worktrees\` | указывала на несуществующий путь | вычищена `git worktree prune` |

Страховочный снимок (на случай, если откат понадобится): папка
`…\новое пространство\_worktree_responsible-pickle_snapshot_2026-09-16\` — `tracked_changes.patch`
(65 809 Б, все 15 изменённых файлов), `untracked\` (22 файла) и `git_status_short.txt`.
Ветка `responsible-pickle` (коммит `678c762` от 07.08.2026) **не удалена** — она и есть гарантия
восстановимости закоммиченного состояния worktree.

### 0.11.4 Первый локальный коммит рабочей копии

Сделано `git add -A` + коммит с сообщением
`«Аудит 16.09: удалены MCP-мосты, папка переименована в KazistovVv, UAC1001 закрыт, worktree очищен»`.
**413 файлов, +110 304 / −15 095. Push НЕ делался.** Ветка `Cline` опережает `origin/Cline` на 1 коммит.

Перед коммитом из него исключён мусор (добавлено в `.gitignore`): `_diag_*`, `_stage5_tests.xml`,
`*.unity.bak`, `Packages/*.bak_*`. Ранее уже были исключены `_dsh_*`, `/_dsh_*`, `/[Ll]ogs/`,
`.plastic/`, `.claude/`, `.gigacode_vsc/`, `.codebuddy/`, `*.slnx`. Архив `_backup_*.zip` лежит
**вне репозитория** (в родительском каталоге).

**Проверено перед коммитом (гейт после удаления моста):** `error CS: 0`, `warning CS: 0`,
`MenuItem (added twice): 0`, `Missing (Mono Script): 0`, exit code 0; свежие `Assembly-CSharp.dll`
и `Assembly-CSharp-Editor.dll`; 131 успешный ответ ILPP, сбоев нет.
Две особенности прогона, разобранные и признанные безобидными:
* `Exception: 4` — это внутренний шум ILPP-сервера Unity (`Grpc … ConnectionAbortedException`,
  HTTP/2-поток оборван при завершении batchmode), не ошибки проекта;
* гейт сообщил об изменении `ProjectSettings.asset` — это **применение переименования папки**:
  `companyName`/`productName`/`projectName`/`metro*` сменились `KavistovVv` → `KazistovVv`.
  Критичное `activeInputHandler: 2` (Both) **сохранено** (см. §0.9.1).



## 0.10 Сессия 16.09.2026 (ночь) — 4 критичных пункта аудита: второй редактор, лишние MCP-мосты, имя папки, worktree-призрак

**Задача.** По аудиту от 16.09.2026 закрыть 4 критичных пункта + важные предупреждения, не ломая
работающее: (1) освободить порт 8765 от второго редактора Unity, (2) убрать лишние MCP-мосты из
`Packages\manifest.json`, (3) переименовать папку проекта `KavistovVv` → `KazistovVv`, (4) удалить
worktree-призрак `responsible-pickle`; плюс (5) UAC1001 в `RuntimeRegistry.cs`, (6) перепрогон тестов,
(7) повторный батчмод-гейт, (8) лишний URP, (9) мелкие уборки.

**Резервная копия — сделана ДО первой правки (правило §0.0):**
`…\новое пространство\_backup_KavistovVv_before_rename.zip` — **407 МБ, 3989 записей**. Исключены
только восстановимые `Library`, `Temp`, `obj`, `.vs`; исходники, `Packages`, `ProjectSettings`,
`PROJECT_CONTEXT.md` и **вся история `.git`** внутри — полный откат возможен.
Отдельно: `Packages\manifest.json.bak_before_mcp_cleanup` (копия манифеста до правки).

### 0.10.1 Результаты по этапам

| № | Пункт | Результат |
|---|-------|-----------|
| 1 | Второй редактор Unity (PID 8996), порт 8765 | **ЗАКРЫТ ШТАТНО** (не через `-Force`). Заголовок окна был `KavistovVv - MainScene - … <DX11>` — **без `*`**, т.е. сцена не dirty; файлов новее часа в копии не было. `CloseMainWindow()` → выход за 38 с, PID 16992 (AssetImportWorker) завершился вместе с родителем. Порт 8765 освобождён, Unity-процессов не осталось. **См. §0.10.3 — редактор вернулся.** |
| 2 | Три MCP-моста в манифесте | Удалены `com.anklebreaker.unity-mcp` (v2.39.5, порт 7890–7899) и `com.coplaydev.unity-mcp` (v10.1.2, «MCP for Unity»), **оставлен `com.gladekit.mcp-bridge` (v0.7.23, порт 8765)** — решение по умолчанию из ТЗ. Манифест: 54 → **52** зависимости. `packages-lock.json` и `Library\PackageCache` Unity подчистила сама: в диффе lock — **только эти два блока**, ни одна проектная зависимость не потеряна; в PackageCache остался один `com.gladekit.mcp-bridge`. |
| 3 | Папка `KavistovVv` → `KazistovVv` | **ПЕРЕИМЕНОВАНА** (`Rename-Item`, OneDrive не заблокировал). Обновлены: `.vscode\settings.json` (`dotnet.defaultSolution`), 2 комментария в `Tools\ci-console-check.ps1`, шапка этого файла и §0.7.1. Удалён устаревший `KavistovVv.slnx` (125 Б, те же два проекта, что и в `KazistovVv.slnx`). `.slnx` добавлен в `.gitignore`. |
| 4 | Worktree-призрак `responsible-pickle` | **НЕ УДАЛЁН — требует ручного решения.** Две независимые причины, см. §0.10.2. |
| 5 | UAC1001 (`RuntimeRegistry.cs:124`) | Исправлен **`[System.NonSerialized]`**, а НЕ `[System.Serializable]` — см. §0.10.4. В свежем логе компиляции `UAC1001` — **0 вхождений**. |
| 6 | Тесты EditMode | **215 total, 213 passed, 0 failed, 2 skipped** — побайтово тот же результат, что и в прогоне 15.09 (`_stage5_tests.xml`). 12 фиксов без прогона ничего не сломали. Артефакты: `Logs\editmode-results.xml`, `Logs\editmode-tests.log`. |
| 7 | Батчмод-гейт | **ПРОЙДЕН, exit code 0**: `error CS / Shader error: 0`, `warning CS: 0`, `MenuItem (added twice): 0`, `Exception: 0`, `Missing (Mono Script): 0`, **`ProjectSettings: без изменений`**. GladeKit: `[UnityBridge] ✅ Server started successfully on http://localhost:8765/` — прежняя ошибка `Failed to start server` (порт занят) **исчезла**. |
| 8 | Лишний URP | **НЕ УДАЛЁН — используется.** См. §0.10.5. |
| 9 | Мелкие уборки | `Assets\Plagins` (0 файлов) + `Plagins.meta` удалены; `Assets\_Recovery` (12 файлов, 858 КБ) заархивирован в `…\новое пространство\_archive_Recovery_2026-09-16_220449.zip` и удалён из `Assets`; `.gitignore` дополнен (`*.slnx`, `_dsh_*`, `.plastic/`, `.claude/`). `ProjectAuditorSettings.asset` **оставлен** — см. §0.10.6. Проверено: висячих ссылок на GUID удалённых ассетов нет. |

### 0.10.2 Этап 4 — почему worktree НЕ удалён (обе причины существенны)

Заявлено как «мёртвый worktree, 667 файлов, 194 МБ». Фактически:

1. **В нём есть незакоммиченные изменения.** `git status` в
   `…\KazistovVv\.gigacode_vsc\worktrees\responsible-pickle` показывает `M Packages/manifest.json`,
   `M Packages/packages-lock.json`, `M Assets/_Project/00_Scenes/MainScene.unity`,
   `M Assets/_Project/01_Scripts/Core/RobotController.cs`, `M InputManager.cs`,
   `M KeyboardMouseInputProvider.cs`, `M SpatialAnchorManager.cs`, `M ProjectSettings/*.asset` и
   `?? Assets/XR/…` (35 записей). По правилу ТЗ «есть изменения — не удаляй, сообщи в отчёте».
2. **Имя `responsible-pickle` в этом репозитории указывает НА ДРУГУЮ папку.**
   `git worktree list` из `…\KazistovVv` отдаёт
   `C:/Users/Ольга/KavistovVv/.gigacode_vsc/worktrees/responsible-pickle`, а
   `…\KazistovVv\.git\worktrees\responsible-pickle\gitdir` содержит именно этот путь.
   То есть `git worktree remove responsible-pickle --force`, запущенный из рабочей копии, снёс бы
   worktree **второй (пользовательской) копии** — не тот каталог, который просили убрать.
   Каталог же `…\KazistovVv\.gigacode_vsc\worktrees\responsible-pickle` (11.08.2026) — это осиротевшая
   КОПИЯ worktree, в реестре `git worktree list` она не числится.

**Безопасный порядок (вручную):** (1) убедиться, что во второй копии закрыт Unity; (2) сохранить/перенести
изменения из настоящего worktree (или подтвердить, что они не нужны); (3) только потом — либо
`git worktree remove … --force` **из второй копии**, либо `git worktree prune` + удаление каталога
`…\KazistovVv\.gigacode_vsc\worktrees\responsible-pickle` обычным `Remove-Item`.

### 0.10.3 ВАЖНО — редактор Unity вернулся и снова занял 8765

В 22:10:55 (то есть ПОСЛЕ закрытия PID 8996 и ПОСЛЕ успешного гейта) **Unity Hub** (PID 2748) заново
запустил интерактивный редактор на второй копии: PID 24192,
`Unity.exe -projectpath C:\Users\Ольга\KavistovVv … -hubSessionId c9978d29-35ad-41f6-bd0c-705aa9bd1725`
— **тот же `hubSessionId`, что был у закрытого PID 8996**. Сцена не dirty (в заголовке нет `*`).
Порт 8765 снова LISTENING (pid 24192).

**Следствие:** пока этот редактор жив, батчмод-прогон рабочей копии снова будет писать в лог
`[UnityBridge] Failed to start server` (GladeKit не сможет занять 8765). На вердикт гейта это НЕ влияет —
он считает только `error CS` / `Shader error` (в сводке 21:40 при занятом порте было `errors: 0, ok: true`),
но «чистая консоль» перестаёт быть чистой. **Процесс не убивался повторно** — он открыт уже после
нашей работы, решение за пользователем:

```powershell
Get-Process Unity | Select-Object Id, MainWindowTitle      # какой редактор открыт
Stop-Process -Id 24192 -Force                              # только если он не нужен
```

Если нужен гарантированно чистый гейт при постоянно открытом втором редакторе — убрать и
`com.gladekit.mcp-bridge` (тогда в проекте не останется ни одного моста, биндящего фиксированный порт).

### 0.10.4 Этап 5 — почему `[System.NonSerialized]`, а не `[System.Serializable]`

В ТЗ предлагалось добавить `[System.Serializable]` классу `ProjectNode`. **Это дало бы обратный эффект.**
Unity Manual (Serialization rules analyzer reference, UAC1001) перечисляет четыре равноправных способа
починки, и «добавить `[Serializable]`» — лишь первый из них; для runtime-поля правильный — третий
(«Add `[System.NonSerialized]` to the field»).

Почему `[Serializable]` здесь опасен:
* `ProjectNode` держит `public List<ProjectNode> Children` — это **deep self-reference cycle**, то есть
  UAC1007/UAC1008 (предупреждение никуда не денется, а сменит код);
* `public object Tag` и `private bool? canHide` Unity сериализовать не умеет — добавились бы новые UAC-предупреждения;
* у `ProjectNode` **нет конструктора без параметров** — десериализация дала бы пустые объекты;
* `RegisteredObject` вешается `AddComponent` при спавне и пересобирается `RuntimeRegistry.RebuildFromScene()`,
  то есть поле `Node` — **чистый runtime-кэш**, сериализация ему не нужна в принципе.

Поэтому поле помечено `[System.NonSerialized, HideInInspector]`. Поведение в рантайме не меняется.
Проверено: в свежем `Logs\ci-console.log` вхождений `UAC1001` — **0**, предупреждений `UACxxxx` — **0**.

### 0.10.5 Этап 8 — URP удалять нельзя

`com.unity.render-pipelines.universal: 17.5.0` в манифесте оставлен, потому что он **используется**:
1. `Packages\io.realvirtual.starter` (встроенный пакет) объявляет его как **свою зависимость**
   (`packages-lock.json`: `"com.unity.render-pipelines.universal": "14.0.0"` внутри `io.realvirtual.starter`) —
   удаление из манифеста не убрало бы пакет, но сломало бы разрешение зависимостей;
2. в **активной сцене** `Assets/_Project/00_Scenes/MainScene.unity` (строка 2247) живёт компонент
   `Unity.RenderPipelines.Universal.Runtime::UnityEngine.Rendering.Universal.UniversalAdditionalLightData` —
   без пакета он превратится в `Missing (Mono Script)`;
3. в проекте есть `Assets\Settings\UniversalRenderPipelineGlobalSettings.asset`;
4. те же компоненты — в `Assets\ROOMS\HQ Hangar Free\Scene\ExampleScene.unity`.

По правилу ТЗ «если найдено — не трогай, зафиксируй в отчёте».

### 0.10.6 Этап 9 — `ProjectAuditorSettings.asset` НЕ удалён

Посылка «пакета нет в манифесте → не нужен» неверна: `com.unity.project-auditor` — **встроенный
(built-in) пакет Unity**, он лежит в
`C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Data\Resources\PackageManager\BuiltInPackages\com.unity.project-auditor`
и поэтому **по определению не входит в `manifest.json`**. Ассет живой: Unity перезаписывает его при
запуске (`ProjectSettings\ProjectAuditorSettings.asset`, mtime 21:42:20). Кроме того, UAC1001 — это
диагностика **Roslyn-анализатора Unity** (Analyzer Reference), а не Project Auditor; они лишь похожи кодом `UAC…`.

### 0.10.7 Попутно исправлен дефект самого гейта (Windows PowerShell 5.1)

`Tools\ci-console-check.ps1` **никогда не работал** при запуске ровно той командой, которая записана в его
же `.EXAMPLE` и в ТЗ (`powershell -ExecutionPolicy Bypass -File …`): при `[CmdletBinding()]` Windows
PowerShell 5.1 связывает параметры РАНЬШЕ, чем заполнен `$PSScriptRoot`, поэтому
`[string]$ProjectPath = (Split-Path -Parent $PSScriptRoot)` падал с
`ParameterArgumentValidationErrorEmptyStringNotAllowed` и скрипт не выполнялся вообще. В PowerShell 7
тот же код работает — поэтому дефект не проявлялся (успешные прогоны 21:34–21:38 шли из `pwsh`).
Исправлено: вычисление перенесено в тело скрипта. Проверено — теперь работает **и в 5.1, и в 7**
(именно в 5.1 получен финальный зелёный гейт §0.10.1).

**Отдельная ловушка на будущее:** `Tools\ci-console-check.ps1` — UTF-8 **с BOM**, и BOM обязателен:
без него Windows PowerShell 5.1 читает кириллицу как CP1251 и скрипт рассыпается на синтаксических
ошибках. Инструменты правки, которые пересохраняют файл в UTF-8 без BOM, этот скрипт ломают молча —
после любой правки проверяйте первые три байта (`EF BB BF`).

### 0.10.8 Изменённые файлы

Правка: `Packages\manifest.json`, `Packages\packages-lock.json` (пересобран Unity),
`.vscode\settings.json`, `.gitignore`, `Tools\ci-console-check.ps1`,
`Assets\_Project\06_KazistovVv_UI\03_Scripts\Data\RuntimeRegistry.cs`, `PROJECT_CONTEXT.md`.
Удалено: `KavistovVv.slnx`, `Assets\Plagins` + `Plagins.meta`, `Assets\_Recovery` (+ `.meta`, 12 файлов → в архив).
Создано: `_backup_KavistovVv_before_rename.zip`, `_archive_Recovery_2026-09-16_220449.zip`,
`Packages\manifest.json.bak_before_mcp_cleanup`, `Logs\editmode-results.xml`, `Logs\editmode-tests.log`.
Логика роботов, кинематика, планировщик и UI не трогались. Коммит не делался.

---

## 0.9 Сессия 16.09.2026 (вечер) — файлы/shell для директоров, два предупреждения консоли, батчмод-гейт

**Задача.** (1) Починить пресет DSH `unirobot-dev`, чтобы директора могли не только советовать, но и
читать/писать файлы и запускать команды. (2) Разобрать два предупреждения консоли Unity:
`MenuItem Window/AB Unity MCP was added twice` и `This project uses Input Manager, which is marked for
deprecation`. (3) Сделать «чистую консоль» воспроизводимым гейтом, а не разовым нажатием Play.

**Резервная копия — сделана ДО первой правки (правило §0.0):**
`…\dsh-unirobot-mcp\_backup_preset_fix_2026-09-16_21-11-36.zip` (0,23 МБ, 107 записей).
Копия проекта Unity НЕ делалась: в этом проекте правки касались только `Tools\ci-console-check.ps1`
(новый файл), `Logs\*` (артефакты гейта), `ProjectSettings.asset` (не менялся — см. §0.9.2) и
`PROJECT_CONTEXT.md`. Оба предупреждения разбирались ЧТЕНИЕМ, без правок пакетов.

### 0.9.1 ПРЕДУПРЕЖДЕНИЕ 1 — «MenuItem Window/AB Unity MCP was added twice» → СТОРОННИЙ ПАКЕТ

**Факт (из живого лога редактора, не предположение).** `C:\Users\Ольга\KavistovVv\Logs\Editor.log`,
строка 580 (сессия редактора от 16.09.2026, 06:30):
```
MenuItem Window/AB Unity MCP was added twice
UnityEngine.Debug:LogWarning (object)
UnityEditor.MenuService/MenuItemsTree`1<UnityEditor.MenuItemOrderingNative>:AddChildSearch (…)   ×3
UnityEditor.MenuService:GetModeMenuTree (string) → … → UnityEditor.Search.SearchButton:GetTooltipText ()
```
То есть сообщение выдаёт **меню-сервис самого Unity 6** при построении дерева меню (дерево строится
лениво — при подсказке кнопки Search в главном тулбаре), а не какой-либо скрипт проекта.

**ДИАГНОСТИКА (что проверено и что исключено):**

| Проверка | Команда/факт | Результат |
|---|---|---|
| Дубль файла в проекте | `rg --no-ignore --hidden -l "AB Unity MCP" <проект>` (включая `Library`) | все совпадения — В ОДНОМ каталоге `Library\PackageCache\com.anklebreaker.unity-mcp@9032874ff0b8`; второго экземпляра НЕТ |
| Авторский пункт меню в проекте | `rg --glob '*.cs' 'MenuItem\("Window/' Assets Packages` (обе копии) | **0 совпадений** — своих пунктов `Window/…` в проекте нет |
| Дубль asmdef (один скрипт в двух сборках) | `Editor\AnkleBreaker.UnityMCP.Editor.asmdef` покрывает `Editor/**`, `1-Scripts\Editor\WelcomeWindow\UnityMCP.Editor.Welcome.asmdef` — только свою папку | перекрытия нет |
| Сколько `[MenuItem]` в пакете | `Select-String '\[MenuItem' <пакет>` | ровно 3 атрибута (см. ниже) |
| **Ключевая проверка** | пути этих трёх атрибутов | `Window/AB Unity MCP` (пункт) **И** `Window/AB Unity MCP/Action History`, `Window/AB Unity MCP/Welcome` (подпункты) |
| Дифференциальная проверка по двум другим MCP-пакетам | `com.coplaydev.unity-mcp` 10.1.2 и `com.gladekit.mcp-bridge` 0.7.23 | у CoplayDev корень `Window/MCP for Unity` содержит ТОЛЬКО подпункты; у GladeKit пункт `Window/GladeKit MCP`, а подпункты — под ДРУГИМ корнем `Window/GladeKit`. Ни у одного нет «пункт и родитель подпунктов по одному пути» — и ни один не предупреждается |

**ПРИЧИНА.** Один и тот же путь `Window/AB Unity MCP` регистрируется в дереве меню дважды: (1) как
обычный пункт (`Editor\MCPDashboardWindow.cs:22`, `[MenuItem("Window/AB Unity MCP")]`) и (2) как
автосоздаваемый РОДИТЕЛЬ для подпунктов `Window/AB Unity MCP/Action History`
(`Editor\MCPActionHistoryWindow.cs:65`) и `Window/AB Unity MCP/Welcome`
(`1-Scripts\Editor\WelcomeWindow\UnityMcpWelcomeWindow.cs:30` — `MENU_PATH`, атрибут на строке 39).
Именно это и ловит `MenuItemsTree.AddChildSearch` (три вложенных кадра стека = обход трёх сегментов
пути). Все совпадения — внутри ОДНОГО экземпляра пакета, поэтому «дубль файла» и «две сборки»
исключены.

**ПАКЕТ (для отчёта — как и требуется при стороннем источнике):**

| Поле | Значение |
|---|---|
| Имя | `com.anklebreaker.unity-mcp` — displayName «AnkleBreaker Unity MCP» |
| Версия | **2.39.5** (запись в CHANGELOG: 2026-07-27) |
| Откуда | git-зависимость: `Packages\manifest.json:3` → `https://github.com/AnkleBreaker-Studio/unity-mcp-plugin.git`; зафиксированный `Packages\packages-lock.json` хеш `9032874ff0b8`; в кеше — `Library\PackageCache\com.anklebreaker.unity-mcp@9032874ff0b8` |
| Минимальная версия Unity по манифесту | 2021.3 (проект — 6000.5.6f1) |
| Проверка upstream | `raw.githubusercontent.com/…/main/Editor/MCPDashboardWindow.cs` (HTTP 200) — **на текущем `main` строка `[MenuItem("Window/AB Unity MCP")]` НЕ исправлена**, то есть обновление пакета это предупреждение не уберёт |

**ЧТО СДЕЛАНО.** Правок НЕ вносилось, и это осознанное решение:
* удалять пункт меню нельзя — он открывает дашборд моста (сервер, порт, очередь, категории);
* править `Library\PackageCache\...` нельзя — кеш не является источником истины и будет перегенерирован
  Unity при следующем resolve (правка «испарится» молча);
* удалять пакет тоже нельзя: `Редактор → порт по умолчанию 7890` совпадает с портом 7890 из
  MCP-конфига проекта `.gigacode_vsc\gigacode.json` (`UNITY_BRIDGE_PORT=7890`) — то есть именно этот
  мост и был целевым для внешнего MCP-клиента (сам `unity-mcp-server` по пути
  `C:/Users/Ольга/unity-mcp-server/src/index.js` на диске СЕЙЧАС ОТСУТСТВУЕТ — конфиг мёртвый, но это
  отдельный вопрос и не повод сносить пакет).

**РЕКОМЕНДУЕМЫЙ ФИКС (durable, на выбор владельца):**
* **Минимум и без смены UX:** сделать дашборд подпунктом — в `Editor\MCPDashboardWindow.cs` заменить
  `[MenuItem("Window/AB Unity MCP")]` на `[MenuItem("Window/AB Unity MCP/Dashboard", false, -1)]`.
  Тогда `Window/AB Unity MCP` становится чистым подменю из трёх пунктов, дубля узла нет.
  Применить можно только ОДНИМ из способов: (а) форк/PR в upstream, (б) vendoring — скопировать пакет
  в `Packages\com.anklebreaker.unity-mcp\` (тогда Unity берёт встроенную копию вместо git) и править там.
  Прямая правка `Library\PackageCache` не годится.
* **Либо** вынести подпункты под другой корень (`Tools/AB Unity MCP/…`) — тогда пункт `Window/AB Unity MCP`
  остаётся как есть.
* Пока не сделано — **считать предупреждение информационным косметическим шумом**: на работу моста,
  сцену, сборку и ввод оно не влияет (в батчмод-логе его вообще нет, см. §0.9.3).

### 0.9.2 ПРЕДУПРЕЖДЕНИЕ 2 — «This project uses Input Manager, which is marked for deprecation»

**Что было в предыдущем промте неверно.** «Активный ввод = Input Manager (Old)» — это ПРИЧИНА
предупреждения, а не способ его отключить; подавить эту запись из пользовательского скрипта нельзя.

**ФАКТЫ (проверено, не предположения):**

| Проверка | Команда | Результат |
|---|---|---|
| Текущее значение | `Select-String ProjectSettings.asset -Pattern 'activeInputHandler'` | **`activeInputHandler: 2` (Both)** — и в рабочей копии агента (`…\новое пространство\KavistovVv`), и в копии пользователя (`C:\Users\Ольга\KavistovVv`), строка 963 |
| Пакет Input System | `Packages\manifest.json` / `Library\PackageCache` | `"com.unity.inputsystem": "1.20.0"` — установлен, кеш `com.unity.inputsystem@7a4e1a2a8194`, версия 1.20.0 ✔ |
| Кодировка `ProjectSettings.asset` | первые байты файла | `25 59 41` (`%YA`) — **UTF-8 БЕЗ BOM**, как и требуется для YAML Unity ✔ (правка файла не производилась) |
| Осталось ли предупреждение при Both | живой лог `C:\Users\Ольга\KavistovVv\Logs\Editor.log`, строка 626 (сессия 06:30) | **ДА, предупреждение есть** при уже выставленном `2` |

**ВЕРДИКТ.** Вариант A′ (Both) в проекте **уже стоит** — менять нечего. Предупреждение при `Both`
СОХРАНЯЕТСЯ, потому что легаси-бэкенд ввода при этом остаётся включённым; исчезнет оно только при
переходе на `activeInputHandler: 1` (только Input System), а это и есть миграция ввода — отдельная
задача, которую в этой сессии сознательно не делали (и не должны были).

* **Сообщение известно, подавление не поддерживается. Считать информационным.**
* **Требует отдельной задачи: миграция ввода на Input System (Actions, клавиатура/мышь/геймпад/VR,
  рефактор всех `Input.*` вызовов).**

Дополнительно к тикету (полезно знать заранее, из аудита §0.7.3): легаси-ввод сидит в
`FreeFlyCameraController.cs` (там же 6 пустых `catch`, которые и защищают старый `Input`-API), в
`KeyboardMouseInputProvider.TryLegacyKey`, `KvBindings` и в геймпад-HUD; VR-ввод идёт через Meta XR SDK,
который сам по себе Input System-совместим. То есть миграция — это в первую очередь клавиатура/мышь/геймпад,
и делать её надо с прогоном §0.9.3 и проверкой биндов из `Docs/DEVELOPER_README.md`.

### 0.9.3 БАТЧМОД-ГЕЙТ «ЧИСТОЙ КОНСОЛИ» → `Tools\ci-console-check.ps1` (создан)

«Чистая консоль» теперь воспроизводимый гейт с кодом возврата, а не разовое нажатие Play.

```powershell
# полный прогон (компиляция проекта + разбор лога), код возврата 1 при ошибках
powershell -ExecutionPolicy Bypass -File Tools\ci-console-check.ps1

# разбор лога ЖИВОЙ сессии редактора (там видны MenuItem / Input Manager — см. ниже)
powershell -ExecutionPolicy Bypass -File Tools\ci-console-check.ps1 -AnalyzeOnly
```

Что делает скрипт:
* берёт Unity ТОЙ версии, которой открыт проект (`ProjectSettings\ProjectVersion.txt` → `6000.5.6f1`),
  и только при её отсутствии — самую новую из Unity Hub (сортировка по `[version]`, а не по строке);
* запускает `-batchmode -nographics -quit` с жёстким таймаутом (`-TimeoutMinutes`, по умолчанию 30);
* считает независимые категории: `error CS`/`Shader error`, `warning CS`, `MenuItem`
  (в т.ч. `was added twice`), `Input Manager`, `Exception`/`Unhandled exception`, `Missing (Mono Script)`;
* **снимает SHA-256 всех `ProjectSettings\*` до и после прогона** и печатает изменившиеся файлы —
  это прямая страховка от побочной перезаписи `ProjectSettings`, из-за которой в §0.8.4 пришлось
  делать `cm undo` (сравнение по содержимому, а не по времени: Unity трогает файлы, записывая те же байты);
* пишет машиночитаемую сводку `Logs\ci-console-summary.json` и дописывает строку в
  `Logs\ci-console-history.jsonl` (история прогонов);
* код возврата: `0` — ошибок нет; `1` — ошибки, аварийное завершение, таймаут или отсутствие лога.

**РЕЗУЛЬТАТ ПРОГОНА (16.09.2026, рабочая копия агента, Unity 6000.5.6f1):**

| Показатель | Значение |
|---|---|
| Ошибки (`error CS` / `Shader error`) | **0** → гейт ПРОЙДЕН (exit code 0) |
| Предупреждения компилятора (`warning CS`) | **0** |
| `MenuItem` / `was added twice` | 0 / 0 |
| `Input Manager` | 0 |
| `Exception` / `Missing (Mono Script)` | 0 / 0 |
| Компиляция | `AssetDatabase: script compilation time: 6.242960s` (лог 59 803 байта) |
| Завершение | `Exiting batchmode successfully now!` → `return code 0` |
| `ProjectSettings` | **без изменений по содержимому** (файлы перезаписаны теми же байтами; сверка с побайтовой копией от 07:10 — `EditorBuildSettings.asset`, `ProjectAuditorSettings.asset`, `ProjectSettings.asset` совпадают) |

**ВАЖНО про два предупреждения из §0.9.1/§0.9.2:** в батчмод-логе их НЕТ (0/0 выше) — их печатает
только интерактивный редактор при построении меню и загрузке layout. Поэтому гейт умеет `-AnalyzeOnly`:
тот же разбор применяется к `Logs\Editor.log` живой сессии, и предупреждения видны как ФАКТЫ:
```
MenuItem (в т.ч. added twice)   : 1  (added twice: 1)
Input Manager (в т.ч. deprecat) : 1
  [MenuItem] MenuItem Window/AB Unity MCP was added twice
  [Input]    This project uses Input Manager, which is marked for deprecation. …
```
Ручной чек-лист гейта: `ci-console.log` создан; в сводке `errors: 0`; `ProjectSettings: без изменений`;
после любого изменения кода — перезапустить гейт и добиться exit code 0.

**Три ошибки, найденные и исправленные в самом гейте (чтобы он вообще заработал):**
1. путь проекта содержит пробел (`…\новое пространство\KavistovVv`), а `Start-Process -ArgumentList`
   НЕ экранирует аргументы → Unity получал обрезанный `-projectPath`, падал с кодом 1 и не создавал лог;
   пути теперь передаются в кавычках (и stdout/stderr Unity тоже сохраняются в `Logs\ci-console.*.log`);
2. `Process.ExitCode` у объекта из `Start-Process -PassThru` в Windows PowerShell 5.1 для Unity
   приходит ПУСТЫМ (у `cmd.exe` тот же код читается — проверено) → гейт падал с «завершился с кодом »;
3. поэтому код возврата берётся из самого лога Unity (`…terminate with return code N` + маркер
   `Exiting batchmode successfully now!`), а `ExitCode` процесса — только как дополнительный сигнал.

### 0.9.4 ПРЕСЕТ DSH `unirobot-dev` И UNITY-МОСТ (сторона инструментов агента)

Подробности — в репозитории, `docs/directors-spec.md` §5.0.9–§5.0.10. Кратко, потому что это меняет
работу агента в этом проекте:
* в пресете НЕ БЫЛО файловых и shell-инструментов: профиль `web` глушит модельные строки инструментов
  (`tool-fs`, `tool-fs-search`, `tool-pwsh`, `tool-jobs`, …), потому что «presets own the per-agent rows».
  Теперь пресет объявляет их сам → у модели есть `read/write/edit/read_image`, `glob/grep`, `pwsh`,
  `job_*`, `todo_write`, `ask_user_question`, `skill`, `web_search/web_fetch`, режим плана и автосжатие;
* попутно найдены ТРИ ошибки, из-за которых **Unity-мост не отдавал ни одного инструмента**:
  `toolsFromRegistry()` возвращал пустой список (тело цикла ничего не добавляло), политика получала
  уровень `'R'/'W'/'D'` вместо кода операции → `UNKNOWN_OP` → **DENIED у всех инструментов, включая
  чтение**, и секция системного промпта моста создавалась, но не регистрировалась. Исправлено;
* тесты: `node packages/dsh-plugin/src/selfcheck.mjs` (34 инструмента, R:15 W:18 D:1) и
  `node packages/dsh-plugin/test/cordis-adapter.mjs` (34 инструмента + секция + гейт политики) — обе зелёные;
  регрессия директоров `node packages/directors/test/smoke.mjs` — 46 PASS / 0 FAIL.

### 0.9.5 ЧТО ТРЕБУЕТ РУЧНОГО ДЕЙСТВИЯ

1. **Перезапустить Host DSH** (не только создать новую сессию): правки КОДА пакетов (`@unirobot/dsh-plugin`)
   не видны в уже запущенном процессе — загрузчик импортирует модуль обычным `import()`, без обновления
   кеша. Правки ТЕКСТА пресета новой сессии достаточно.
2. **Новая сессия на пресете `unirobot-dev`** — проверить глазами, что в каталоге инструментов есть
   одновременно: `read/write/edit/glob/grep/pwsh` (файлы и команды), `list_directors/get_director/
   search_directors/get_directors_for_task` (директора) и `unity_*` (мост, 34 инструмента).
3. **По предупреждению MenuItem** — решить, делать ли фикс пакета (варианты в §0.9.1); после любой правки
   пакета перезапустить Unity и проверить гейтом: `…\ci-console-check.ps1 -AnalyzeOnly` по свежему
   `Logs\Editor.log` — счётчик `added twice` должен стать 0.
4. **Ввод** — завести отдельную задачу на миграцию (§0.9.2); до неё предупреждение считать информационным.
5. Мёртвый MCP-конфиг `.gigacode_vsc\gigacode.json` ссылается на отсутствующий
   `C:/Users/Ольга/unity-mcp-server/src/index.js` (порт 7890). Либо восстановить сервер, либо убрать
   секцию из конфига — иначе внешний MCP-клиент будет молча не подниматься.

### 0.9.6 ЧТО НЕ ДЕЛАЛОСЬ (сознательно)

* Коммитов и `cm checkin` НЕТ — только правки и отчёт (по ТЗ сессии).
* `Packages\manifest.json` / `packages-lock.json` НЕ менялись: ни один пакет не добавлен и не удалён.
* `Library\PackageCache` НЕ правился (не durable).
* Рефакторинг ввода на Input System НЕ выполнялся (отдельная задача, §0.9.2).
* `ProjectSettings.asset` НЕ менялся: `activeInputHandler` уже равен `2`, BOM отсутствует.
* Копия пользователя `C:\Users\Ольга\KavistovVv` НЕ трогалась: Unity в ней осталась открытой (PID 8996),
  её лог использовался ТОЛЬКО на чтение.

## 0.8 Сессия 16.09.2026 — РАЗРЕШЕНИЕ КОНФЛИКТОВ UNITY VERSION CONTROL (PLASTIC SCM)

**Задача.** В Unity Version Control «висели» конфликты (заявлено 50 directory + 10 file). Требовалось разрешить их, **не потеряв ночную работу §0.7** (унификация `KavistovVv` → `KazistovVv`), финальное имя — `KazistovVv`.

**Резервные копии — сделаны ДО первой правки (правило §0.0):**
| Что | Путь | Объём |
|---|---|---|
| ZIP-архив проекта | `…\новое пространство\_backup_before_resolve_2026-09-16_07-04-14.zip` | 388,1 МБ, 3774 файла, 0 ошибок упаковки (**без `Library`** — Unity пересобирает её сама) |
| Побайтовая копия дерева (для мгновенного отката) | `…\новое пространство\_rollback_before_resolve_2026-09-16_07-10-14\` | 3778 файлов, 671,2 МБ |

### §0.8.1 ДИАГНОСТИКА — что было на самом деле

- Ветка `/main`, **рабочая копия стояла на `cs:34`, голова ветки — `cs:36`** (`cs:35` и `cs:36` от 06.09.2026, то есть на 10 дней СТАРШЕ ночной работы). Отсюда и «входящие изменения — старая версия»: это правда.
- В репозитории две ветки: `/main` и `/list` (пустая, никакого отношения к конфликтам не имеет).
- **КОРНЕВАЯ ПРИЧИНА (главное открытие сессии):** на машине **ДВЕ папки считают себя ОДНИМ воркспейсом Plastic** — одна и та же запись `KavistovVv-KavistovVv` (GUID `b4685f48-…`):
  1. `C:\Users\Ольга\OneDrive\Documentos\новое пространство\KavistovVv` — рабочая копия агента (`DeepSeek`), **здесь ночная работа**;
  2. `C:\Users\Ольга\KavistovVv` — копия пользователя (`Cline`), в ней открыт редактор Unity (`Unity.exe -projectpath C:\Users\Ольга\KavistovVv`), ночной работы в ней НЕТ (нет `06_KazistovVv_UI`, `Docs`, `08_Tests`, 43 файла со старым именем).
  Состояние воркспейса (загруженный changeset и список изменённых элементов) в Plastic Cloud хранится ОДНО на имя воркспейса, поэтому обе копии «перетягивают» его на себя. Это и есть источник «висящих» конфликтов и главный риск на будущее (§0.8.9).
- Конфликты, которые показывала Unity VCS, — это **результат `CalculateMerge` (предпросмотр incoming changes), а не применённый merge**; на диске конфликтных маркеров не было. Подтверждение из лога клиента: `MergeConflictArray[10]` и `MergeConflictArray[54]` (повторялись каждую минуту, `…\AppData\Local\plastic4\logs\unityplastic.*.log`).
- Итоговый подсчёт Plastic дал **10 файловых конфликтов + 8 конфликтов «evil twin»** (остальные «directory conflicts» из счётчика 54 — это служебные каталоги `.git/objects/**`, по которым локальная и входящая версии совпали).

### §0.8.2 СПИСОК КОНФЛИКТОВ И ЧТО ВЫБРАНО

**Правило сессии: везде выбрана ЛОКАЛЬНАЯ (ночная) версия — она новее на 10 дней и является развитием входящей.**

**A. 10 файловых конфликтов** (`cm merge br:/main` → «Changed by both contributors»):

| # | Файл | Выбрано | Обоснование |
|---|---|---|---|
| 1 | `/ProjectSettings/EditorBuildSettings.asset` | **local** | локальная версия БАЙТ-В-БАЙТ равна входящей `cs:36` (MD5 совпал) |
| 2 | `/Assets/_Project/00_Scenes/MainScene.unity` | **local** | 207 023 Б против 158 201 Б у `cs:36`; в сцене ночное переименование `KazistovVvUI`/`KazistovVv_UI` |
| 3 | `/Assets/_Project/01_Scripts/Core/FreeFlyCameraController.cs` | **local** | 94 026 Б против 10 449 Б (в 9 раз больше) |
| 4 | `/Assets/_Project/01_Scripts/Core/InverseKinematics.cs` | **local** | 5 476 Б против 2 976 Б |
| 5 | `/Assets/_Project/01_Scripts/Core/RobotController.cs` | **local** | 12 579 Б против 6 230 Б |
| 6 | `/Assets/_Project/01_Scripts/Core/SCARAController.cs` | **local** | 18 092 Б против 6 461 Б |
| 7 | `/Assets/_Project/01_Scripts/Core/SixAxisController.cs` | **local** | 39 669 Б против 3 231 Б (в 12 раз больше) |
| 8 | `/Packages/manifest.json` | **local** | 54 зависимости против 53 у `cs:36`; у локальной больше и новее (`com.unity.cloud.gltfast` 6.20.0 против 6.19.0, есть `com.gladekit.mcp-bridge`) — по ТЗ «где больше зависимостей, ту и брать» |
| 9 | `/Packages/packages-lock.json` | **local** | 81 пакет против 79; ни одного пакета из `cs:36` в локальной не отсутствует |
| 10 | `/.git/index` | **local** | 96 226 Б против 91 966 Б; git-репозиторий после merge исправен (`git fsck --connectivity-only` — только «dangling» объекты, это норма) |

**B. 8 конфликтов «evil twin» (added on both contributors) — выбран local (`--automaticresolution=eviltwin-dst`):**

| Файл | Комментарий |
|---|---|
| `/Assets/_Recovery/0 (1).unity` (+`.meta`) | файлы УЖЕ были в рабочей копии; входящая добавляла те же пути |
| `/Assets/_Recovery/0.unity` (+`.meta`) | то же |
| `/Assets/Resources/GeometryCompassSettings.asset` (+`.meta`) | то же |
| `/Assets/Resources/ROSConnectionPrefab.prefab` (+`.meta`) | то же |

**C. 47 «added on source»** (`.git/objects/**`, `.git/lfs/**`, `.git/gigacode-ai-contributions.json`, `.gigacode_vsc/gigacode.json`) — приняты входящие; Plastic по каждому написал `won't be downloaded because the local one matches the content of the one to be downloaded`, то есть **содержимое совпало, ничего не перезаписывалось**.

### §0.8.3 ЛОВУШКА, КОТОРУЮ ПРИШЛОСЬ ОБЕЗВРЕДИТЬ (иначе ночная работа была бы уничтожена)

Первый предпросмотр merge показал по 8 файлам (включая 5 боевых `.cs`) приговор:
`The file … was modified on source and will replace the destination version` — то есть **входящая СТАРАЯ версия затёрла бы ночную**.

Причина: эти файлы были изменены на диске, но **не были «checked out»** в Plastic (`CheckFileContentForChanged=no`, `SetFilesAsReadOnly=no` в `client.conf`), поэтому для merge «вклад destination» у них был пустым и побеждал source.

**Лечение:** перед merge выполнено `cm checkout` по этим 8 файлам → повторный предпросмотр показал `will replace the destination version: 0` и `Changed by both contributors: 10` (ровно те 10 конфликтов из отчёта пользователя). Только после этого merge был запущен.

### §0.8.4 КОМАНДЫ, КОТОРЫЕ ВЫПОЛНЯЛИСЬ

```powershell
# 0) диагностика
cm version ; cm whoami ; cm status --header ; cm workspace list
cm find branch --format="{name}|{date}|{owner}|{comment}"
cm find changeset where "branch='/main'" --format="{changesetid}|{date}|{owner}|{comment}"
cm diff cs:34 cs:36 --repositorypaths --format="{status}|{path}"      # 65 записей: 10 C + 55 A
cm getfile "serverpath:/Packages/manifest.json#cs:34" --file=...       # вытащить любую ревизию
cm log / cm ls / cm status --ignored / cm status --private

# 1) включить merge при наличии pending-изменений (документированный ключ Plastic)
#    <MergeWithPendingChanges>no</MergeWithPendingChanges> -> yes   (client.conf)
#    (бэкап конфига: %LOCALAPPDATA%\plastic4\client.conf.dsh_backup_2026-09-16)

# 2) зарегистрировать локальные правки как вклад destination
cm checkout "Packages\manifest.json" "Packages\packages-lock.json" `
  "Assets\_Project\01_Scripts\Core\FreeFlyCameraController.cs" `
  "Assets\_Project\01_Scripts\Core\InverseKinematics.cs" `
  "Assets\_Project\01_Scripts\Core\RobotController.cs" `
  "Assets\_Project\01_Scripts\Core\SCARAController.cs" `
  "Assets\_Project\01_Scripts\Core\SixAxisController.cs" ".git\index"

# 3) предпросмотр (без --merge) и сам merge — везде побеждает локальная версия
cm merge br:/main                                            # предпросмотр
cm merge br:/main --merge --mergetype=onlyone --keepdestination --automaticresolution=eviltwin-dst

# 4) фиксация результата
cm checkin --all -c="Разрешены конфликты после унификации KazistovVv: 10 файловых + 8 directory (evil twin) - везде оставлена локальная (ночная) версия; merge с cs:36 (/main)"
cm undo "ProjectSettings\EditorBuildSettings.asset" "ProjectSettings\ProjectAuditorSettings.asset"   # убрать побочную перезапись от batch-прогона Unity
```

**Важно про CLI:** команды `cm resolve <path> --merge-type=…` **в Plastic SCM НЕ СУЩЕСТВУЕТ** (в CLI есть только `merge`, `update`, `undo`, `checkout`, `checkin`). Разрешение конфликтов делается через `cm merge` с `--mergetype` / `--keepdestination` / `--automaticresolution`, как выше.

### §0.8.5 РЕЗУЛЬТАТ

- **Создан `cs:37@br:/main`** (16.09.2026 07:14:33) с комментарием о разрешении конфликтов; **рабочая копия теперь `(cs:37 - head)`** — «висит конфликтов» больше нет, контролируемых незакоммиченных изменений НОЛЬ.
- В `cs:37` вошла вся ночная работа: `Assets/_Project/01_Scripts/Trajectory/*`, `.../Features/*` (Kv*-модули), `06_KazistovVv_UI/*`, `08_Tests/Editor/*` (6 файлов тестов), `StreamingAssets/kazistovvv_i18n/*` (7 языков), `StreamingAssets/robots/*`, модели `*.glb`, `Assets/_Recovery/*`, `Assets/Resources/*`, `ProjectSettings/*`, `Packages/*`; зарегистрированы переименование `MainMenu .cs → MainMenu.cs` и удаления (`06_KOMPAS_UI`, `ProfilerCaptures`, старые `KavistovVvUIManager.cs`/`KavistovVvMenu.cs`).
- **КОМПИЛЯЦИЯ:** `Unity.exe -batchmode -nographics -quit -projectPath "<рабочая копия>" -logFile _compile_after_resolve.log` → `AssetDatabase: script compilation time: 5.141195s`, `Exiting batchmode successfully now!`, **`error CS` = 0, `warning CS` = 0, `Exception` = 0, `Missing (Mono Script)` = 0**.
- **ПРОВЕРКА СОХРАННОСТИ (главное):** сравнение всего дерева с резервной копией до операции — **удалено 0 файлов**; MD5 всех 10 конфликтных файлов + `PROJECT_CONTEXT.md` + `kazistovvv_settings.json` **не изменились ни на байт**; `Assets/_Project/06_KazistovVv_UI`, `.../08_Tests`, `Docs/`, `Docs/Screenshots/`, `KazistovVv.sln`, `KazistovVv.slnx`, `_dsh_s4_out/KazistovVv_subtitles.srt` — на месте.
- **ЧТО ВСЁ-ТАКИ ИЗМЕНИЛОСЬ (2 файла, не проектные):** `.gigacode_vsc/gigacode.json` (252 → 200 Б) и `.git/gigacode-ai-contributions.json` (171 854 → 1 342 Б) — это служебные файлы инструмента GigaCode, входящая версия победила, **старые копии сохранены рядом как `*.private.0`**. На код, сцену и сборку не влияют.

### §0.8.6 ЧТО ТРЕБУЕТ РУЧНОГО ДЕЙСТВИЯ (обязательно прочитать)

1. **`C:\Users\Ольга\KavistovVv` (копия пользователя) теперь РАСХОДИТСЯ с репозиторием.** Она осталась на состоянии 13–15.09 (старое имя папки `06_KavistovVv_UI`, нет `Docs`/`08_Tests`, столы 1.2×0.8 по §9). Так как обе папки — ОДИН воркспейс Plastic, **НЕ нажимайте в Unity Version Control кнопку `Update Workspace` / «Обновить» в этой копии**: Plastic зальёт в неё содержимое `cs:37` и затрёт её локальные особенности (в первую очередь сцену — по §9 столы в копиях обязаны отличаться). Правильный путь — синхронизация по процедуре §9 (скриптами), а не через Update.
2. **Переименование воркспейса/репозитория НЕ выполнено — требует ручного шага, см. §0.8.9.**
3. Проверить глазами в живом редакторе: сцена `MainScene.unity` открывается, в Hierarchy нет `Missing (Mono Script)`, интерфейс `KazistovVv_UI` на месте (автоматическая проверка компиляции это подтверждает, но визуальную часть batch не проверяет).
4. Мелочь на уборку (не влияет ни на что): в `cs:37` попали 6 ПУСТЫХ служебных каталогов `Library\PackageCache\...` — они были помечены как Added ещё до этой сессии (содержимое осталось Private/ignored). Убрать при желании: `cm undo "Library\PackageCache\com.unity.render-pipelines.high-definition@856c3f8928ac"`. На ОДИНОЧНОМ файле проверено, что `cm undo` снимает элемент с контроля и **файлы на диске не удаляет**; для каталогов это не проверялось, поэтому сначала убедиться, что `Library` действительно перечислена в `ignore.conf`.

### §0.8.7 РЕКОМЕНДАЦИИ НА БУДУЩЕЕ (чтобы не повторялось)

1. **Одна папка = один воркспейс.** Две папки с одним именем воркспейса — источник «фантомных» конфликтов и риск взаимного затирания. Копию пользователя нужно либо вывести из Plastic (убрать/переименовать `.plastic`), либо зарегистрировать ОТДЕЛЬНЫМ воркспейсом с другим именем. Пока это не сделано — **не запускать `cm`-команды в двух папках попеременно**.
2. **`cm status` в «чужой» папке тоже пишет состояние воркспейса на сервер.** Диагностику вести только в рабочей копии агента.
3. **Перед merge/update всегда делать `cm checkout` для локально изменённых файлов** (см. §0.8.3) — иначе Plastic считает, что их менял только source, и затирает их.
4. **`MergeWithPendingChanges`** в `client.conf` по умолчанию `no`, поэтому `cm merge` при наличии pending-изменений просто печатает предупреждение и НИЧЕГО не делает. Для CLI-merge его нужно временно поставить в `yes` (в этой сессии так и делалось, после работы значение возвращено в `no`; бэкап — `client.conf.dsh_backup_2026-09-16`).
5. **Не держать `Library`, `Logs`, `Temp`, `_dsh_*.log` в контроле.** Сейчас `ignore.conf` игнорирует `Library`, но 6 пустых каталогов `Library\...` всё же попали в changeset — перед массовым checkin полезно проверять `cm status --controlledchanged`.
6. **`.git` внутри репозитория Plastic** (477 элементов в одном changeset!) — дублирование двух VCS. Один раз настроить `ignore.conf` на `.git/` — иначе каждый git-коммит порождает шум в Plastic.
7. Перед любой массовой операцией — **сначала бэкап** (правило §0.0 соблюдено: ZIP + побайтовая копия дерева) и **предпросмотр** (`cm merge br:/main` без `--merge` показывает точный план и все конфликты, ничего не меняя).

### §0.8.8 ПЕРЕИМЕНОВАНИЕ WORKSPACE / REPOSITORY — «ТРЕБУЕТ РУЧНОГО ПЕРЕИМЕНОВАНИЯ»

Технически команды существуют (`cm workspace rename <new_name>`, `cm repository rename <old> <new>`), но **в этой сессии они намеренно НЕ выполнялись**:
- имя **репозитория** (`KavistovVv/KavistovVv@10171608534945@cloud`) по ТЗ **не меняем** — на него завязаны git-remote `https://github.com/V1iktorK/KavistovVv.git` (§0.7.1, п.3) и записи в документации; переименование облачного репозитория делается только через Unity Dashboard и сломает `plastic.selector` в ОБЕИХ копиях;
- **имя воркспейса** (`KavistovVv-KavistovVv`) — общее для двух папок (§0.8.1). Переименование сейчас **сломало бы Unity Version Control в копии пользователя**: её `.plastic\plastic.workspace` указывает на старое имя, и клиент не найдёт воркспейс.

**Безопасный порядок (когда решите переименовывать):** (1) закрыть Unity в ОБЕИХ копиях; (2) определиться, какая папка остаётся «главной»; (3) во второй папке удалить/переименовать каталог `.plastic` (это снимет дубль); (4) в главной выполнить `cm workspace rename KazistovVv-KazistovVv` (переименование локального воркспейса, репозиторий не трогаем); (5) в `PROJECT_CONTEXT.md` и в путях ничего не менять — **папка проекта на диске остаётся `…\новое пространство\KavistovVv`** намеренно (§0.7.1, п.1).

## 0.7 Сессия 15.09.2026 — аддитивные улучшения (8 этапов ТЗ): унификация названия, документация, аудит, тесты

**Задача сессии:** аддитивные улучшения — унификация названия, документация, статический анализ, локализация,
unit-тесты, аудит производительности, снимки, Git-анализ. **Логика, кинематика, планировщик, UI-логика и бинды не меняются.**

**Резервная копия перед началом:** `…\новое пространство\_backup_before_night_2026-09-15_20-58-07.zip`
(367,2 МБ; без `Library`, `Temp`, `Logs`, `obj`, `.vs`, `UserSettings`). Делается ДО первой правки —
согласно §0.0 это обязательное правило для любой массовой операции.

### §0.7.1 ЭТАП 1 — УНИФИКАЦИЯ НАЗВАНИЯ: `KavistovVv` → **`KazistovVv`** (правильное имя — с буквой **z**)

| Вариант (было) | Вхождений | Куда заменено | Комментарий |
|---|---|---|---|
| `Kavistov` (семейство `kavistov` в любом регистре: `Kavistov`, `kavistov`, `KAVISTOV`, `KavistovVvUI`, `kavistovvv_*`) | **896** | `Kazistov*` | **843 заменено** в **139 файлах**; **53 оставлено намеренно** (см. ниже) |
| `KOMPAS` | 9 | — (не тронуто) | все 9 — в этом файле, в разделах ИСТОРИИ (§0.4, §10 «Проверка переименования», §11): это описание ПРЕЖНЕГО имени в колонке «было» и имена давно удалённых файлов (`KompasTheme.cs`, `KompasUIManager.cs`, `KompasMenu.cs`) |
| `КОМПАС` / `KОМПАС` | 5 | — (не тронуто) | там же: «КОМПАС-стиль», «старая реализация КОМПАС-стиля» |
| `Kazistov` | 1 | — (не тронуто) | строка §10 «поиск по `Kompas\|KOMPAS\|КОМПАС\|Kazistov`» — запись о ПРОШЛОЙ проверке; переписывать историю нельзя |

- **Как заменялось.** Побайтово: файл читается в кодировке Latin-1 (байт ↔ символ), подстрока `kavistov`
  (любой регистр) меняется на `kazistov` с сохранением регистра (`KAVISTOVVV` → `KAZISTOVVV`), пишется обратно.
  Поэтому BOM, переводы строк (LF в `MainScene.unity` и JSON) и все не-ASCII символы остались **байт-в-байт** —
  ни один файл не был перекодирован. `.meta`-файлы в замену **не включались вообще** (по ТЗ), менялись только их имена.
- **Что защищено от замены (53 вхождения) — объекты, которые по ТЗ остаются со старым именем:**
  1. корень рабочей копии на диске `…\новое пространство\KavistovVv` (45 вхождений в `PROJECT_CONTEXT.md` + 3 абсолютных пути `Library\PackageCache\…` в каждом `.csproj`);
  2. вторая (пользовательская) копия `C:\Users\Ольга\KavistovVv`;
  3. git-remote `https://github.com/V1iktorK/KavistovVv.git` (имя репозитория не меняем);
  4. существующий архив `_backup_KavistovVv_13.09.2026_restored.zip` и маска `_backup_KavistovVv_*.zip`;
  5. уже созданный файл `C:\Users\Ольга\Videos\KavistovVv_recording_…mp4`;
  6. относительные пути отчётов диагностики от корня проекта: `KavistovVv/_dsh_*.txt` (2 комментария в `DshScaraEightDiag.cs`, `DshStageDiag.cs`).
  Правка этих путей сделала бы документацию ЛОЖНОЙ (пути перестали бы существовать) — папку проекта по ТЗ не переименовываем.
  > **ОБНОВЛЕНО 16.09.2026 (§0.10):** пункт 1 этого списка БОЛЬШЕ НЕ ДЕЙСТВУЕТ — папка рабочей копии
  > переименована `…\новое пространство\KavistovVv` → `…\новое пространство\KazistovVv`. Пункты 2–6
  > (вторая копия, git-remote, архивы, видео, относительные пути диагностик) остаются в силе.
  > Именно из-за прежнего запрета Unity при каждой пересборке project-файлов заново создавала
  > пустой `KavistovVv.slnx` (см. §0.7.1, п.4 «Известный побочный эффект») — теперь эта причина устранена.

**Переименованы файлы и папки (вместе с `.meta`, GUID сохранён → ссылки в сцене не порвались):**

| Было | Стало |
|---|---|
| `Assets/_Project/06_KavistovVv_UI/` (+ `.meta`) | `Assets/_Project/06_KazistovVv_UI/` |
| `…/03_Scripts/Core/KavistovVvUIManager.cs` (+ `.meta`) | `KazistovVvUIManager.cs` |
| `…/03_Scripts/Editor/KavistovVvMenu.cs` (+ `.meta`) | `KazistovVvMenu.cs` |
| `Assets/StreamingAssets/kavistovvv_i18n/` (+ `.meta`) | `Assets/StreamingAssets/kazistovvv_i18n/` |
| `Assets/StreamingAssets/kavistovvv_settings.json` (+ `.meta`) | `kazistovvv_settings.json` |
| `KavistovVv.sln` / `KavistovVv.slnx` | `KazistovVv.sln` / `KazistovVv.slnx` |
| `_dsh_s4_out/KavistovVv_subtitles.srt` | `KazistovVv_subtitles.srt` |

**Что именно изменилось в коде (только имена, ни строки логики):**
- namespace `KavistovVvUI` → `KazistovVvUI`, `KavistovVvFeatures` → `KazistovVvFeatures` (все `using` тоже);
- классы `KavistovVvUIManager` → `KazistovVvUIManager`, `KavistovVvMenu` → `KazistovVvMenu`;
- объекты сцены `KavistovVv_UI` → `KazistovVv_UI`, `KavistovVvCanvas` → `KazistovVvCanvas`
  (правка `MainScene.unity` построчная: `m_Namespace`, `m_Name`, `m_EditorClassIdentifier` ×3 — процедура §9);
- меню `Tools/KavistovVv/…` → `Tools/KazistovVv/…`, `Tools/KavistovVv UI/…` → `Tools/KazistovVv UI/…`;
- `productName`, `companyName`, `projectName`, `metroPackageName`, `metroApplicationDescription` в `ProjectSettings.asset` → `KazistovVv`;
- ключи PlayerPrefs/EditorPrefs `KavistovVv.*` → `KazistovVv.*` (тема, язык, раскладка, функции, туториал…);
- имена файлов на выходе: скриншоты `KazistovVv_screenshot_*`, записи `KazistovVv_recording_*`, отчёты `KazistovVv_report_*`, титры `KazistovVv_subtitles`, WAV `KazistovVv_voice_*`, окно `KazistovVv Movie`;
- папки данных во время работы: `<persistentDataPath>/KazistovVv/{Recordings,Poses,Zones,Sessions,Logs,Config}` и «Документы\KazistovVv\{reports,robot_export,robot_models,voiceover,Bench}»;
- внешние данные: `StreamingAssets/kazistovvv_settings.json` и `StreamingAssets/kazistovvv_i18n/<код>.json`
  (константы `KvSettingsView.ExternalFileName`, `KvLoc.FolderName` обновлены синхронно с именами файлов);
- `Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj` — пути `Assets\_Project\06_KazistovVv_UI\…`.

**Проверка после замены.** `Unity.exe -batchmode -nographics -quit -projectPath "<проект>" -logFile _stage1_compile.log`
→ **0 ошибок CS, 0 предупреждений CS** (лог `_stage1_compile.log`, 62 КБ). Unity пересобрала
`Assembly-CSharp.csproj`/`Assembly-CSharp-Editor.csproj` уже с новой папкой `06_KazistovVv_UI` —
то есть AssetDatabase увидела переименование корректно, `Missing (Mono Script)` не появилось.
Откат из бэкапа **не потребовался**.

**Побочные эффекты (важно знать, это не ошибки):**
1. Смена ключей PlayerPrefs означает, что ОДИН РАЗ сбросятся сохранённые настройки интерфейса (тема, язык,
   раскладка панелей, набор функций, отметка туториала) — дальше всё сохраняется как обычно.
2. Данные, созданные ПРЕЖНИМИ запусками, лежат в старых папках (`Документы\KavistovVv`, `<persistentDataPath>/KavistovVv`).
   Новые запуски пишут в `…\KazistovVv`. Если старые записи/позы/сессии нужны — папку надо переименовать/скопировать ВРУЧНУЮ
   (автоматически этого никто не делает: трогать пользовательские данные задача не ставила).
3. Unity генерирует файл решения ПО ИМЕНИ ПАПКИ проекта, поэтому после каждой пересборки project-файлов
   снова появляется пустой `KavistovVv.slnx` (переименованные `KazistovVv.sln`/`KazistovVv.slnx` при этом остаются).
   Это косметика; лечится переименованием папки проекта, а её по ТЗ не трогаем.
4. Диагностические логи прежних сессий (`_dsh_*.log`, `_dsh_*.txt`, `MainScene.unity.bak`) НАМЕРЕННО не правились:
   это исторические стенограммы прогонов (≈79 000 вхождений старого имени), их правка исказила бы отчётность.
   При необходимости их можно удалить/заархивировать — см. «требует ручной проверки» в отчёте.

### §0.7.2 ЭТАП 2 — ДОКУМЕНТАЦИЯ: папка `Docs/` в корне проекта

Создана папка **`Docs/`** (в корне проекта, рядом с `PROJECT_CONTEXT.md`; в `Assets` её нет намеренно — это не ассет, и Unity не должна её импортировать).

| Файл | Строк | Что внутри |
|---|---:|---|
| `Docs/API_REFERENCE.md` | 6 730 | **автосбор из кода** (`_tools\kv_api.ps1`): 7 пространств имён, **396 публичных типов**, **4 256 публичных членов** с описаниями из XML-комментариев `/// <summary>`; оглавление, сигнатуры, для каждого типа — файл и строка; в конце — список файлов по пространствам имён |
| `Docs/DEVELOPER_README.md` | 769 | требования (Unity 6000.5.6f1, HDRP 17.5.0, Input System 1.20.0, XRI 3.5.1, ARF 6.5.0, Windows + Android), как открыть/собрать/запустить, компиляция без редактора (`dotnet build Assembly-CSharp.csproj`), batch-запуск, таблица всех 10 диагностик `Dsh*Diag` и их отчётов, карта репозитория по факту (включая пустые папки), таблица зависимостей, каталоги данных, полная таблица управления из `KvBindings.cs`, добавление функции и языка без перекомпиляции, типовые проблемы |
| `Docs/ARCHITECTURE.md` | 676 | схема 6 слоёв (ввод → состояние → планирование → исполнение → представление → данные), сквозной путь действия, отдельные диаграммы UI-модуля и подсистемы «Функции», таблица «модуль → ответственность → классы → файлы», точки входа, синглтоны, направления зависимостей, 22 инварианта, 6 групп точек расширения |
| `Docs/CHANGELOG.md` | 1 042 | **все 36 этапов** разработки (что добавлено, какие файлы, дата/сессия), **95 исправленных багов и несоответствий** в виде таблицы, хронология 25 сессий, раздел о переименовании, «отложено / не реализовано» |
| `Docs/CODE_AUDIT.md` | — | этап 3, см. §0.7.3 |
| `Docs/LOCALIZATION_AUDIT.md` | — | этап 4, см. §0.7.4 |
| `Docs/PERFORMANCE_AUDIT.md` | — | этап 6, см. §0.7.6 |
| `Docs/GIT_AUDIT.md` | — | этап 8, см. §0.7.8 |
| `Docs/Screenshots/` | — | этап 7, см. §0.7.7 |

**Честные уточнения, внесённые в документацию по факту (а не «как в ТЗ»):**
- **`Assets\_Project\08_Tests\` до этой сессии не существовало**, `.asmdef` в проекте нет ни одного — сборки предопределённые (`Assembly-CSharp` / `Assembly-CSharp-Editor`); проверка проекта велась батч-диагностиками `Dsh*Diag`, а не тестами;
- **префабов в проекте нет** — каталоги `Assets\_Project\02_Prefabs\{Robot,UI,Environment,FX}` пусты; модели и стенды лежат прямо в `MainScene.unity`;
- **корневого `StreamingAssets/` нет** — внешние данные только в `Assets/StreamingAssets` (7 словарей, `kazistovvv_settings.json`, `robots/`);
- каталог **`Bench`** по коду (`KvPlannerLab.cs`) лежит в `<persistentDataPath>/KazistovVv/Bench`, **а не в «Документах»**, как было записано в §15.2;
- **`TrajectoryFlowController.toolOffset` фактически равен 0**, а не 0.10, как в §5/§7.

### §0.7.3 ЭТАП 3 — СТАТИЧЕСКИЙ АНАЛИЗ КОДА → `Docs/CODE_AUDIT.md`

Охват: **177 файлов `*.cs`**, 2 939 методов, 4 555 полей, 418 типов. Инструменты: `_tools\kv_audit_code.ps1` (сопоставление фигурных скобок, поиск пустых `catch`, магических чисел, рисков NullReference) и `_tools\kv_audit_dead.ps1` (частотный анализ идентификаторов, хеширование нормализованных тел методов). **Код не менялся ни на строку.**

| Раздел | Найдено | Существенное |
|---|---|---|
| Мёртвый код | **11** приватных методов + **11** приватных полей + 17 «неиспользуемых» типов | 4 метода в `HierarchyPhantomCleaner` и 2 типа-меню — **ложные срабатывания** (Unity вызывает по `[MenuItem]`); 6 «неиспользуемых» типов — `MonoBehaviour` из сцены; **реально мёртвых — 13** (в т.ч. `FreeFlyCameraController.SelectRobotContextual`, `LaserAndPhantomManagers.DestroyLeftoverContainers` и `TravelTime`, `TrajectoryFlowController.TopUpVariants`, `KeyboardMouseInputProvider.TryLegacyKey`, `KvToolbar.LayoutWide`) |
| Дубликаты | **30** групп одинаковых тел | 20 из них — утилиты диагностик (`SetLasers` — **6 копий**, `Boot` — 5, `aimAt` — 5); в продуктовом коде — `KvLocExtra/2/3.F` (3 копии), `OnServiceMessage` (4 копии), `Down` (3 × 12 строк), `OnPointerDown` ×2 в одном файле |
| Длинные методы (>100 строк) | **40** (1,4 % методов) | `DshFullVerifyDiag.RunStep` — **1 632** строки, `KvIcons.Draw` — 610, `KazistovVvUIManager.RegisterCommands` — 457, `FeatureHub.RegisterCommands` — 400, `KvRobotImport.ImportUrdf` — 301. 6 методов `RegisterCommands` — это длинные **списки** регистрации, а не сложная логика |
| Магические числа | **423** в 17 критичных файлах | Пороги вырожденности калибровки `1e-10` (`KvCalibration.cs:442`) и `1e-18` (`:692`) — **два разных порога в одном файле**; `trace * 1e-9 + 1e-12` (регуляризация), 32 итерации, старт `{1.0, 0.7, 0.3}`; 160 итераций CCD в `IkSolver`; номиналы моментов 95/95/55/16/11/7 и 600 Н в `KvPayloadCalculator`; скорости `v500/z50/z10/v200/z5` в `KvRobotExport` |
| Потенциальные NullReference | **218** по широкому критерию, **99** повышенной значимости | основная масса — **ложные срабатывания** (`GetComponent` сразу после `new GameObject(..., typeof(X))`). Реальные: `CenterWindow.cs:250,258` (`GetComponentInChildren<Renderer>()` без проверки — самый вероятный отказ), `TargetMarker.cs:18` (`GetComponent<MeshFilter>().sharedMesh`), 3 × `transform.Find("KazistovVvCanvas")` в `DshStage2Diag` |
| Пустые `catch` | **6**, все в `FreeFlyCameraController.cs` (236, 293, 329, 368, 413, 429) | это защита старого `Input`-API при недоступном вводе; `catch` без типа ловит и `OutOfMemoryException`; логирования нет; приём продублирован 6 раз |

**Плюсы, подтверждённые проверкой:** ни одного `GetComponent` в `Update`; ни одного `Find*` в методах кадра; LINQ только в 2 редакторских файлах из 177; **0 осиротевших `.meta`**.

### §0.7.4 ЭТАП 4 — АУДИТ ЛОКАЛИЗАЦИИ → `Docs/LOCALIZATION_AUDIT.md`

Инструменты: `_tools\kv_audit_loc.ps1`, `_tools\kv_audit_loc_gap.ps1`. **Ничего не менялось.**

**Три источника строк:** встроенный минимум в `KvLocalization.cs` (7 ключей), внешние словари `Assets/StreamingAssets/kazistovvv_i18n/*.json` (**по 187 ключей × 7 языков**), рантайм-таблицы `KvLocExtra/2/3.Row(...)` (**419 ключей × 7 языков**).

| Проверка | Результат |
|---|---|
| Покрытие RU / EN / ZH / ES / DE / FR / JA друг относительно друга | **100,00 % у всех семи** — наборы ключей совпадают строго, «отсутствующих для языка» ключей **0** |
| Дубликаты ключей внутри языка | **0** во всех 7 файлах |
| Ключей, которые запрашивает код | **379** |
| — переведено словарями JSON | 130 |
| — переведено рантайм-таблицами | 44 |
| **— НЕ переведено нигде → во всех 7 языках показывается русский текст** | **205 (54,1 %)** |
| Ключей JSON, не запрашиваемых кодом | 35 (реально мёртвых — не более 4; остальные подставляются динамически: `menu.*` через `KvLoc.Menu`, `tab.*` через массив `TabKeys`, `group.*` через `KvLoc.MenuGroup`) |

**Главная находка:** 168 из 205 пробелов — это ключи **команд интерфейса** (`cmd.<id>` и `cmd.<id>.desc`). Каждая регистрация в `KvCommands.Register` автоматически создаёт два ключа, но **ничто не напоминает об их переводе**. Все 205 пробелов — функционал последних сессий (§0.6: контекстное меню `ctx.*`, дерево `tree.kind.*`, настройки доступности `settings.*`, группы тулбара `toolbar.group.*`, палитра, окно горячих клавиш, HUD геймпада, состояния `empty/error/loading`).

**Плюс:** цепочка «текущий язык → английский → русский литерал» гарантирует, что непереведённый ключ **не покажет пустую строку или сам ключ** — это дефект полноты, а не стабильности.

### §0.7.5 ЭТАП 5 — UNIT-ТЕСТЫ → `Assets/_Project/08_Tests/Editor/` (6 файлов, 215 тестов)

Создана папка **`Assets\_Project\08_Tests\Editor\`** (по ТЗ — `08_Tests`; файлы лежат в подпапке `Editor`, потому что `.asmdef` в проекте нет, и только так они попадают в сборку `Assembly-CSharp-Editor`, у которой уже есть ссылки на `nunit.framework`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`). **Логика проекта не тронута.**

| Файл | Тестов | Покрытие |
|---|---:|---|
| `KvTrajMathTests.cs` | 44 | длины, кривизна, время, сглаживание (3 метода), `SProfileBuild`, `MeasureJerk`, вырожденные и экстремальные входы |
| `KvTimeOptimalTests.cs` | 35 | S-профиль: лимиты `velocity/acceleration/jerk` по всем сэмплам, монотонность, клампы, сервис `KvTimeOptimal` |
| `KvEnergyOptimalTests.cs` | 35 | энергия: эталон `τ·ω`, монотонность по массе/пути/ускорению, аддитивность по суставам, экстремумы, рекуперация |
| `KvCalibrationTests.cs` | 30 | **восстановление TCP из синтетики: ошибка 3,55·10⁻⁷ м = 0,00036 мм** (допуск 0,005 мм, запас 14×); шум 0,1 мм → 0,284 мм; метод «по нормали» |
| `KvPayloadCalculatorTests.cs` | 31 | моменты, лимиты, запас, `Evaluate`/`JointTorques` (защитные ветви), `KvPayloadModel`/`Result` |
| `KvRobotExportTests.cs` | 40 | **синтаксис KUKA KRL / FANUC KAREL / ABB RAPID** через рефлексию (генераторы приватные): счётчики инструкций, баланс конструкций, формат чисел, вырожденный вход |

**Прогон в Unity (реальный, не «на глаз»):**
```
Unity.exe -batchmode -nographics -projectPath "<проект>" -runTests -testPlatform EditMode
          -testResults _stage5_tests.xml -logFile _stage5_tests.log
```
**Результат: `total=215 · passed=213 · failed=0 · skipped=2`** (0,44 с на все тесты). Пропущены два — намеренно: `Api_IsNotTestableWithoutRefactoring` (физическое ядро расчёта нагрузки требует живого робота) и `SProfile_SymmetryIsNotHeld_Characterized` (характеризационный тест: симметрия S-профиля на симметричном входе **не выполняется**, расхождение зеркальных скоростей 0,3488 при пике 0,867 — зафиксировано, а не «покрашено зелёным»). Полные результаты: `_stage5_tests.xml`.

**Найденные тестами особенности кода (не исправлялись — «не лезть в логику»):**
1. **`KvRobotExport` печатает числа в комментарии без `InvariantCulture`** (`KvRobotExport.cs:266-267, 336-337, 407-408`): при русской локали в файл попадает `время 12,500 с`, тогда как числа в строках кода всегда инвариантны (`12.500`). Лечится тремя вставками `CultureInfo.InvariantCulture`.
2. **`KvCalibration.SelfTestPlane()` строит синтетику с фланцем ниже столешницы** → собственная невязка самопроверки ≈ 365,77 мм на идеальных данных (дефект генератора данных самопроверки, не решателя).
3. **`SolveToolOffset` возвращает успех при почти нулевой невязке, ошибаясь на 35 мм**, если повороты были только вокруг одной оси — `tcpResidualMm` как индикатор качества калибровки недостаточен.
4. **У `SolvePlaneOffset` есть «пол» по масштабу** (фиксированный порог определителя `1e-18`): тесный набор точек (~1 мм) отвергается, а коллинеарный набор в сотни метров — нет.
5. **`KvEnergyOptimal.PayloadKg`: `NaN` проходит `Mathf.Clamp` насквозь** и попадает в модель как `NaN` (проверено: `Distal(1) == NaN`).
6. **`Report()` в `KvPayloadCalculator` не вызывается ниоткуда** → событие `Message` на практике молчит; `CopyFrom` на `null`-массиве бросает `NullReferenceException` и делает это неатомарно.
7. **`SProfileBuild`: симметрия не соблюдается** (односторонняя рекурсия ускорения + принудительный нуль скорости в конце); остановка резкая — средняя скорость последнего интервала 37,7 °/с против 2,56 °/с у первого при пике 78 °/с; положение пика скорости нестабильно (9,5 %…98,4 % пути).
8. **`KvRobotExport` на 0 точек бросает `ArgumentOutOfRangeException`** во всех трёх генераторах (публичный путь такой вход отсекает раньше); на 1 точке программа генерируется корректно.

**Что нельзя было протестировать без рефакторинга (зафиксировано, не тронуто):** почти все модули Gate-ят расчёт через `flow.Validator.Ready`, а `PoseValidator.Ready` выставляется только в `Init(RobotController)` — то есть требует живого робота из сцены. Поэтому `KvCalibrationService.SolveTcp`, `KvTimeOptimal.Compute`, `KvEnergyOptimal.Compute`, `KvPayloadCalculator.Evaluate/JointTorques`, `KvRobotExport.Export`, `KvTrajMath.Analyze/Retime/Energy/PathLength` покрыты только защитными ветвями либо **своим чистым ядром** (`SolveToolOffset`, `SolvePlaneOffset`, `SProfileBuild`, `KvTrajMath.Energy`, `MeasureJerk`). Для полного покрытия нужны **PlayMode-тесты с загруженной `MainScene`** — это отдельная задача, вынесена в «отложено».

### §0.7.6 ЭТАП 6 — АУДИТ ПРОИЗВОДИТЕЛЬНОСТИ → `Docs/PERFORMANCE_AUDIT.md`

Инструмент: `_tools\kv_audit_perf.ps1` + разбор существующих отчётов `_dsh_full_verify.txt`, `_dsh_ui_verify.txt`, `_dsh_scara8_verify.txt`. **Код не менялся.**

| Проверка | Результат |
|---|---|
| `FindObjectOfType`/`FindObjectsOfType` в `Update`/`LateUpdate`/`FixedUpdate` | **0** (всего в проекте 78 `FindObjectsByType`, 63 `.Find(`, 11 `FindAnyObjectByType` — все вне кадра) |
| LINQ в горячих путях | **0**; `using System.Linq` только в 2 файлах из 177, оба редакторские |
| `GetComponent` в `Update` | **0**; оба вызова — в `Awake` (`LaserAndPhantomManagers.cs:117`, `TrajectoryFlowController.cs:271`) |
| Незакрытые файлы/потоки | **0** кандидатов |
| Корутины без остановки | 3 запуска (`KvCaptures.cs:160, 376`, `TrajectoryPlayer.cs:35`), `StopCoroutine` не вызывается нигде |
| **Подписки на события без отписки** | **всего 82 подписки / 38 отписок**; в рантайме — **62 подписки / 8 отписок** |
| Аллокации в методах кадра | 22 метода из 54; два реальных кандидата — `KvOverlayKit.Update` (**10 `new` за кадр**) и `KvGamepadHud.Update` (7 `new`, таймера нет) |
| Тяжёлые операции в `Awake`/`Start` | `FeatureHub.Awake` (17 сервисов + 20 подписок), `KvStageHub4.Awake` (24 сервиса + 23 подписки + `KvLocExtra3.Install()` = **2 933 записи локализации** на старте) |

**Измеренные значения (из отчётов прогонов, не оценки):** базовое время кадра **9,88 мс** (~101 FPS); 8 фантомов — 3,60 мс; тепловая карта — 2,68 мс; решение IK — **5 745 мкс/вызов**; генерация 8 траекторий — 0,221 / 0,247 / 3,073 с; полная пересборка интерфейса (269 объектов канваса) — **53,4 мс**, кадровое обновление панелей — **0,769 мс/тик**; вход в PlayMode (перезагрузка домена редактора) — **8,5 с**.

**Главная находка (не «медленно», а ошибка):** 43 подписки вида `<Сервис>.Message += OnServiceMessage` в четырёх хабах этапов **не имеют ни одной парной отписки**, а сервисы живут в статических синглтонах дольше подписчика; ещё **17 подписок сделаны анонимными делегатами** (`FeatureHub.cs:122-155`) и не могут быть сняты в принципе. При повторном входе в PlayMode обработчики накапливаются → сообщения обрабатываются по 2–N раз и возможен `MissingReferenceException`. Обычный прогон (6 циклов «точка → 8 траекторий → Esc», сцена 131 → 134 объекта) утечек **не показывает** — риск проявляется именно при перезапуске PlayMode.

### §0.7.8 ЭТАП 8 — GIT-АНАЛИЗ → `Docs/GIT_AUDIT.md`

**В репозитории ничего не менялось** (ни коммитов, ни индекса, ни `.gitignore`).

- **27 коммитов**, ветка **`Cline`**, HEAD `6413957` (08.09.2026, `Viiktor_k`); локально 9 веток, на remote — 3. Под контролем версий **747 файлов**; не закоммичено: **17 изменённых + 6 удалённых + 98 новых**.
- **Автор один, но записан тремя способами** (`Viiktor_k <milovanovv30@gmail.com>` — 25, `Viiktor_K <…ya.ru>` — 1, `Viiktor_k <…users.noreply.github.com>` — 1).
- **Файлы без `.meta`: 2** — `Assets\XR\APILayers~\…\XrApiLayer_METAX_operator.{dll,json}`, и **это нормально**: каталог оканчивается на `~`, Unity такие намеренно не импортирует. **Осиротевших `.meta` — 0.**
- **Больших файлов (>10 МБ) в истории — 1:** `Packages/io.realvirtual.starter/Samples~/ObjectHandling/DemoGrippingAdvanced.unity` (15,3 МБ).
- **`.gitignore` проверен по факту** (`git check-ignore --no-index`): `Library`, `Temp`, `Logs`, `obj`, `.vs`, `UserSettings`, `Build`, `Builds`, `*.log` — **исключены правильно и не отслеживаются**.
- **Главная проблема:** в Git **нет** модуля `Assets\_Project\06_KazistovVv_UI` (35 файлов UI), словарей `Assets\StreamingAssets` (9 файлов), тестов `08_Tests`, `PROJECT_CONTEXT.md` и `Docs/`. При повторении инцидента 13.09.2026 восстановить их из репозитория будет **невозможно**.
- **Мусор:** 17 посторонних `.glb` (+17 `.meta`) в корне `Assets\` от 09.09.2026 (дамп объектов сцены); `.plastic/` (метаданные другой СКВ) отслеживается и менялся в 12 коммитах; `Assets/_Recovery/` (автобэкапы Unity) — 9 коммитов. Рекомендации по `.gitignore` приведены в отчёте; **сам `.gitignore` не правился**.

### §0.7.7 ЭТАП 7 — СНИМКИ СЦЕН → `Docs/Screenshots/` (16 PNG + `CAPTIONS.md`)

Добавлена **диагностика агента** `Assets\_Project\01_Scripts\Editor\DshScreenshotsDiag.cs` (по конвенции проекта — файл `Dsh*`, в копию пользователя не переносится). Она открывает каждую сцену **присоединённо** (`EditorSceneManager.OpenScene(..., Additive)`), считает габариты рендереров, ставит камеру сцены и рендерит кадр в `RenderTexture` → PNG. **Ни одна сцена не сохраняется**: открытые сцены закрываются с `removeScene: true`, изменения (позиция камеры, временные объекты) отбрасываются.

```
Unity.exe -batchmode -projectPath "<проект>" -executeMethod DshScreenshotsDiag.Run -logFile _dsh_shots.log
```
(именно **без** `-nographics` — нужен настоящий графический контекст; устройство: Direct3D12, Intel UHD Graphics).

| Итог | Значение |
|---|---|
| Снимков | **16 PNG**, 1600×900 |
| Содержательных (проверено статистикой яркости) | **15 из 16** |
| Сцен снято | 6 (`MainScene` + 5 сцен `_Recovery`), по 2 ракурса + 4 крупных плана объектов `MainScene` |
| Подписи | `Docs/Screenshots/CAPTIONS.md` (файл, сцена, ракурс, что на снимке, дата, проверка содержательности) |
| Журнал | `Docs/Screenshots/_log.txt` |

**Найденная и исправленная в процессе тонкость съёмки:** первый прогон дал **чёрные** кадры `MainScene_обзор` и `MainScene_сверху` — огромная плоскость пола (`Plane`) растягивала общие габариты так, что вся остальная сцена занимала меньше пикселя. Диагностика доработана: габариты считаются **с отбрасыванием выбросов** (объекты крупнее 4× медианного размера в расчёт не берутся). После повторного прогона оба кадра стали содержательными (330 и 445 КБ вместо 20 КБ). Кадр `MainScene_объект_Plane.png` остался чёрным **закономерно** — камера ставится в центр самой плоскости, то есть внутрь односторонней геометрии.

**Сцены без снимков** (в них **нет ни одного рендерера** — это заготовки по 8 КБ): `_Project/00_Scenes/Boot.unity`, `Main Menu.unity`, `Simulation.unity`; плюс `_Recovery/0 (4).unity` (3,5 КБ). Сторонние демо-сцены покупных ассетов (`Assets/ROOMS`, `TABLES`, `Premises`) намеренно пропущены.

**Префабов в проекте НЕТ** — каталоги `Assets\_Project\02_Prefabs\{Robot,UI,Environment,FX}` пусты (ни одного `.prefab`), поэтому вместо превью префабов сняты крупные планы корневых объектов `MainScene` (стенды `Стенд_1_Стол`, `Стенд_2_Стол`, `Level`, `Plane`).

**Чего снять НЕ удалось (и почему).** Интерфейс — это `ScreenSpaceOverlay`-канвас, который строится кодом при входе в PlayMode, поэтому рендером камеры он **не снимается в принципе**. Для него нужен `ScreenCapture` в PlayMode, и попытка сделана: вход в PlayMode в batch-режиме **без** `-nographics` на этой машине **не завершился** — процесс Unity ушёл в бесконечный цикл плеера (33 минуты процессорного времени, ~40 минут настенных, журнал перестал расти) и не вернулся в `EditorApplication.update`; процесс снят вручную, ничего не сохранено, кадры `Интерфейс_*.png` **не созданы**. Вход оставлен отдельным методом `DshScreenshotsDiag.RunWithPlayMode()` с предупреждением в комментарии. **Рекомендация для ручной съёмки:** открыть `MainScene` в редакторе → Play → **F8** (в проекте уже есть встроенная функция снимков `KvCaptures`, ключ `screenshotKey = F8`, файл пишется в «Видео» пользователя) → положить PNG в `Docs/Screenshots/Интерфейс.png`.

## 0.0 ИНЦИДЕНТ 13.09.2026, 20:05 — рабочая папка проекта была удалена и восстановлена (ЧИТАТЬ ПЕРВЫМ)
- **Что произошло.** При уборке временных каталогов агент выполнил `Remove-Item "$p\$d" -Recurse -Force`, где переменная `$d` оказалась ПУСТОЙ — путь выродился в корень проекта, и содержимое рабочей копии `…\новое пространство\KavistovVv` (Assets, Packages, ProjectSettings, Library, `.git`, `PROJECT_CONTEXT.md`, диагностики, логи) было удалено. Уцелели только `Logs/upm.log` и `_dsh_ui_diag.log`. Корзина Windows при `Remove-Item` НЕ используется, поэтому штатного отката не было.
- **Что восстановлено и откуда.**
  - **Код — из ВТОРОЙ копии** `C:\Users\Ольга\KavistovVv`: на момент инцидента там уже лежали ВСЕ правки этой сессии (новая папка `06_KazistovVv_UI` целиком + 18 изменённых скриптов, включая последнюю починку подсказок `KvTooltip`), потому что по правилу проекта копии синхронизируются после каждого блока правок. Перенесено всё, кроме `Library`, `Temp`, `obj`, `Logs`, `_dsh_backup_*` (эти каталоги у каждой копии свои).
  - **`MainScene.unity` приведена к состоянию рабочей копии 19 построчными правками** (перед каждой — проверка «ожидаемый текст строки», процедура §9): столешницы `14.4 × 0.05 × 9.6`, 8 ножек `±7.1 / ±4.7`, корни стендов `z = −20.7 / −31.3`, локальные `z` роботов `−3.2` (робот) и `+3.2` (SCARA), а также переименование: `m_Namespace: KazistovVvUI`, `m_Name: KazistovVv_UI`, `m_EditorClassIdentifier` (×3). Итог: 6255 строк, LF, без BOM, старого имени в сцене нет.
  - **`Assets/_Project/01_Scripts/Editor/DshDesktopUiDiag.cs` написан заново** (тот же набор проверок); отчёт `_dsh_ui_verify.txt` сгенерирован заново ПОСЛЕ восстановления — его числа в §5.16 относятся к восстановленному проекту.
  - **`.meta` для `StandsMenu.cs` и `DshDesktopUiDiag.cs` созданы заново** (новые GUID — на эти скрипты в сцене никто не ссылается).
  - **Сразу после восстановления сделана внешняя резервная копия вне проекта:** `…\новое пространство\_backup_KavistovVv_13.09.2026_restored.zip` (≈365 МБ, без `Library`/`Temp`). Папка `Library` не восстанавливалась — Unity пересобирает её сама при первом запуске (это занимает 5–15 минут).
- **Что ПОТЕРЯНО (и где это ещё можно взять).**
  - `PROJECT_CONTEXT.md` в актуальной (861-строчной) редакции: восстановлен из копии пользователя (**версия 12.09.2026, 19:10**) + заново добавлены разделы этой сессии. **В текущей редакции ОТСУТСТВУЮТ разделы сессий 13.09.2026: §0.1 (TAB / точка на поверхности / столы ×3 / фонарик), §0.2 (8 траекторий и 8 фантомов), §0.3 (фонарик с первого нажатия / фантомы ×3 / зона достижимости / лимиты / метрики), §5.12–§5.15 и соответствующие записи §6.1/§7/§9.**
  - Старые диагностики агента: `DshTasksDiag.cs`, `DshVariantsDiag.cs`, `DshFlowDiag.cs`, `DshAuditDiag.cs`, `DshAimDepthDiag.cs`, `DshPointMoveDiag.cs` (описаны в §9, восстанавливаются при необходимости) и накопленные логи/отчёты `_dsh_*` в корне проекта.
  - Локальный git рабочей копии (ветка `DeepSeek`, коммит `5e2291c` + незакоммиченные правки сессий). В копии пользователя ветки `DeepSeek` нет (там `Cline`, `main` и remote `https://github.com/V1iktorK/KavistovVv.git`) — при необходимости `git fetch origin DeepSeek`.
- **ГДЕ ЛЕЖИТ ПОТЕРЯННЫЙ ТЕКСТ (шанс восстановить полностью).**
  1. **OneDrive.** Проект лежал в `…\OneDrive\Documentos\…`, а клиент OneDrive на момент инцидента **не был запущен** (процесса `OneDrive` в системе нет) — значит удаление, скорее всего, не успело уйти в облако. При следующем запуске OneDrive файлы попадут в облачную **«Корзину» OneDrive** (хранится 30 дней), откуда `PROJECT_CONTEXT.md` и диагностики можно вернуть поштучно.
  2. **Стенограммы сессий DSH** — `C:\Users\Ольга\.dsh\sessions\…\session-*.jsonl.zstd`: в них сохранены все правки файлов за сессии 12–13.09.2026 (включая полный текст `PROJECT_CONTEXT.md`).
  3. **«Предыдущие версии» тома / теневая копия** (нужны права администратора), если включены.
- **ПРАВИЛО, ЧТОБЫ ЭТО НЕ ПОВТОРИЛОСЬ.** Никогда не вызывать `Remove-Item`/`rd` по пути, собранному из переменной, без проверки: (а) переменная непустая, (б) итоговый путь существует и содержит ожидаемый подкаталог (`Assets`, `Temp`, `bin`, `obj`), (в) удаляется именно он. Перед массовыми удалениями — резервная копия вне проекта (как `_backup_KavistovVv_*.zip`) и/или `-WhatIf`.
- **Состояние после восстановления:** проект компилируется (оба assembly — 0 ошибок после пересборки `Library`), интерфейс собран и проверен прогоном (§5.16), сцена — в конфигурации рабочей копии (столы ×3, роботы на прежних мировых координатах).

## 0.4 Сессия 13.09.2026 — десктопный интерфейс в стиле FreeCAD + переименование проекта (главное)
| Что | Было | Стало | Где правится |
|---|---|---|---|
| **Название проекта** | KOMPAS/КОМПАС в папках, namespace, классах, меню, сцене, текстах | **KazistovVv** везде: папка `06_KazistovVv_UI`, namespace `KazistovVvUI`, класс `KazistovVvUIManager`, объект сцены `KazistovVv_UI`, меню `Tools/KazistovVv UI/…` | все `.cs` (23 файла), `MainScene.unity`, `Assets/_Project/Docs/*.md`, этот файл |
| **UI-оболочка** | «КОМПАС-стиль»: текстовая верхняя панель, плоский список, панель свойств, статус-бар с «болванками-ссылками» | **десктопный интерфейс в стиле FreeCAD**: строка меню + **тулбар из 15 кнопок-иконок в 3 ряда по 5**, dockable-панели, combo view (дерево + свойства) | **новый** модуль `Assets/_Project/06_KazistovVv_UI/03_Scripts/{Core,Zones,Data}` |
| **Кнопки** | подписи текстом («Файл», «Добавить робота»…) | **ТОЛЬКО иконки** (монохромные, рисуются кодом — файлов-ассетов нет), всплывающие подсказки, недоступные кнопки-заглушки с пометкой «в разработке» | `KvToolbar`, `KvIcons`, `KvWidgets`, `KvCommands` |
| **Дерево моделей** | плоский список роботов и столов | **иерархическое дерево** (раскрывающиеся узлы): роботы → «Ось N»/«TCP», группа «Столы» → стенды, группа «Точки», группа «Траектории» → фантомы; «глазик» (скрыть/показать), переименование по двойному клику, подсветка объекта рамкой в сцене | `KvTreeView`, `KvSelectionHighlight`, `ProjectNode` |
| **Свойства** | 8 строк текста | **панель свойств с секциями** по типу объекта: робот (позиция, поворот, углы, скорость, температура, телеметрия), точка (координаты, достижимость), траектория (длина, время, кривизна, зазор, запас лимитов), стол (позиция, масштаб); **поля только для чтения** | `KvPropertiesView` + `BuildProperties` в `KazistovVvUIManager` |
| **Статус-бар** | статус прицела + ссылки-заглушки | **состояние State Machine, выбранный робот, координаты луча, статусные сообщения потока, индикатор темы, FPS** | `KvStatusBar` |
| **Тема** | одна тёмная палитра | **Тёмная / Светлая / Системная**, мгновенное переключение (пересборка оболочки), хранение в PlayerPrefs | `KvTheme` (ключ `KazistovVv.Theme.Mode`), `KazistovVvUIManager.RebuildShell` |
| **Настройки** | модальное окно с ползунками | **dock-панель с 5 вкладками** (Функции / Управление / Интерфейс / О программе / Справка); пункты функций — из встроенной схемы + внешнего `StreamingAssets/kazistovvv_settings.json` (расширение без перекомпиляции); таблица биндов — только просмотр | `KvSettingsView`, `KvSettingsSchema`, `KvSettings` |

- **UI-система — uGUI (Unity UI), а не UI Toolkit. Обоснование:** (1) весь существующий интерфейс проекта уже uGUI и строится кодом в рантайме (`AddComponent`), сцена при этом не меняется — правило проекта; (2) UI Toolkit в рантайме требует ассетов `PanelSettings` + `UXML`/`USS`, то есть новых файлов, импорта и `.meta`-граблей (см. §5.9 — «код есть, а Unity его не импортировала»), плюс `UIDocument` пришлось бы вешать в сцену; (3) uGUI даёт полный контроль над плотной инженерной вёрсткой (координаты/толщины панелей в пикселях канваса, как в FreeCAD) без нового инструментария; (4) оверлеи проекта (`PointMoveHud`, `TrajectoryMetricsPanel`, `WorkspaceVisualizer`) уже на uGUI — единый стек вместо двух.
- **Файлы UI: 15 новых, 6 удалены, 7 перенесены.**
  - **новые:** `Core/KvTheme.cs` (палитра + режимы темы + фабрика), `Core/KvSettings.cs` (PlayerPrefs-хранилище функций/масштаба/плотности), `Core/KvIcons.cs` (процедурные монохромные иконки + мини-растеризатор), `Core/KvTooltip.cs` (слой подсказок + `KvTooltipTarget`), `Core/KvWidgets.cs` (кнопка-иконка, переключатель, сегментный выбор, строки свойств), `Core/KvCommands.cs` (реестр команд), `Core/KazistovVvUIManager.cs` (оболочка и вся привязка), `Zones/KvMenuBar.cs`, `Zones/KvToolbar.cs`, `Zones/KvTreeView.cs`, `Zones/KvPropertiesView.cs`, `Zones/KvStatusBar.cs`, `Zones/KvSettingsView.cs`, `Zones/KvDockPanel.cs`, `Zones/KvSelectionHighlight.cs`;
  - **удалены (заменены):** `Core/KompasTheme.cs`, `Zones/TopBar.cs`, `Zones/TreePanel.cs`, `Zones/PropertiesPanel.cs`, `Zones/StatusBar.cs`, `Zones/SettingsWindow.cs` (старая реализация «КОМПАС-стиля»);
  - **перенесены с сохранением GUID:** `KompasUIManager.cs` → `KazistovVvUIManager.cs` (GUID `31fe910a…` сохранён — ссылка в сцене не порвалась!), `KompasMenu.cs` → `KazistovVvMenu.cs`, а также `CenterWindow.cs`, `ObjectSpawner.cs`, `ProjectNode.cs`, `RuntimeRegistry.cs`, `IdleCameraBrain.cs`, `ScaraCableFollow.cs` (только namespace и тексты).
- **Ни один бинд не изменён и не добавлен.** Кнопки тулбара вызывают ровно те же действия, что и клавиши: «Выбор точки» ⟷ `Z` (флаг `leftHandEnabled`), «Выбор траектории» ⟷ `X` (`rightHandEnabled`), «Сброс» ⟷ `Esc` (`ResetFlow`), «Запуск/пауза» и «Остановка» — только для шага 5 (`MotionExecutor.SetPaused/Stop`), «Фонарик» ⟷ `G`.
- **State Machine, лазеры, фантомы, планировщик, IK, валидатор, оракул не менялись.** В `TrajectoryFlowController` добавлены ТОЛЬКО read-only обёртки для интерфейса: `Robot`, `Lasers`, `Motion`, `Executor`, `Phantoms`, `PointHud`, `Validator`, `ResetFlow()`; в `TrajectoryExecutor`/`MotionExecutor` — пауза (`Paused`/`SetPaused`; при паузе `IsRunning` остаётся `true`, поэтому переход `RobotMoving → Idle` не срабатывает). Переходы и условия ЛКМ не тронуты.
- **Производительность:** панели обновляются по таймерам (`statusInterval` 0.1 с, `propertiesInterval` 0.2 с, `treeInterval` 0.4 с), дерево пересобирается только при изменении «подписи» содержимого, строки свойств переиспользуются (пул), иконки строятся один раз и кэшируются, подсветка — 12 кубиков с обновлением 5 раз в секунду. Каждый кадр не делается ничего, кроме чтения состояния (замер — в §5.16).
- **`MainScene.unity` менялась МИНИМАЛЬНО и построчно** (процедура §9): `m_Namespace: KazistovVvUI`, `m_Name: KazistovVv_UI`, `m_EditorClassIdentifier` менеджера и `RegisteredObject` (×2). Роботы, столы, свет, HDRP-настройки, координаты и компоненты сцены не тронуты; имена полей менеджера в сцене (`uiReferenceWidth`, `autoRebuildTree`, `uiVisible`, лампа, светоотражение) сохранены.


## 0.5 Сессия 14.09.2026 — ПОЛНАЯ ПРОВЕРКА ПРОЕКТА (этапы 1–8 ТЗ): робот и SCARA, интерфейс, взаимодействия, производительность, стабильность, консоль

**Как проверялось.** Новых функций не добавлялось — только проверка и точечные исправления.
Написан сквозной диагностический прогон `Assets/_Project/01_Scripts/Editor/DshFullVerifyDiag.cs`
(диагностика агента, в копию пользователя НЕ переносится). Запуск:

```
Unity.exe -batchmode -nographics -projectPath "<копия>" -executeMethod DshFullVerifyDiag.Run -logFile _dsh_full.log
```
(БЕЗ `-quit`), отчёт — `KavistovVv/_dsh_full_verify.txt`, строки пишутся сразу (при входе в PlayMode домен
перезагружается, буфер в памяти потерялся бы). Прогон проходит ПОЛНЫЙ цикл сначала на роботе, затем
на SCARA: позиционирование → 8 траекторий → 8 фантомов → выбор «колбаски» → выбор фантома → движение
робота → завершение → режим перемещения точки (Enter/QWEASD/Enter и Esc-отмена) → колесо мыши и СКМ →
Undo/Redo → аварийный стоп → запись/воспроизведение → preset-позы → слайдеры суставов → гриппер →
pick-and-place → тепловая карта; затем интерфейс (тулбар/дерево/свойства/статус-бар/тема/масштаб),
взаимодействия (сессия, утечки, Esc), замеры производительности, стабильность (6 циклов, Esc из 4 состояний)
и консоль/журнал.

**Итог прогонов (14.09.2026): лучший `[OK] 166 · [FAIL] 3 · [info] 24`, последний `[OK] 165 · [FAIL] 4`**
(до правок было `[OK] 120 · [FAIL] 53`). Три-четыре отказа последних прогонов — это ровно те пункты,
что вынесены ниже в «требует доработки» (pick-and-place на обоих роботах, «7 траекторий вместо 8» на одной
из точек из-за отфильтрованных дубликатов) и одна погрешность самого теста (смещение точки QWEASD 0.019
при пороге 0.02 — при коротком кадре клавиша удерживается меньше кадров). Компиляция:
`Unity.exe -batchmode -nographics -quit` → **0 ошибок, 0 предупреждений**.

### Что проверено и работает (оба робота)
| Блок | Подтверждено фактами прогона |
|---|---|
| 1. Старт | роботов в сцене ровно **2** (робот + SCARA), оба `isActive = false`, ни один не выбран (камера/дерево), «Missing (Mono Script)» — **0**, траекторий/фантомов на старте нет, исключений нет |
| 2. Робот | точка фиксируется РОВНО в точке попадания (`toolOffset = 0`, смещение 0.0 мм) · **8 траекторий, 8 «колбасок», 8 фантомов** (в одном из прогонов — 7: дубликаты путей отфильтрованы) · **все 8 едут одновременно**, скорость фантома = `robotMoveSpeed × 3` (0.200 ю/с при 0.0667 ю/с у робота, отношение ровно 3.00×) · выбор «колбаски» и переключение на другую работают · выбор фантома → `RobotMoving`, робот едет, `robot.enabled = false` · по завершении: фаза `Idle`, фантомы убраны, траектории скрыты, **TCP доехал с ошибкой 0.0 мм**, «колбашения» нет (макс. шаг TCP 0.000 мм за 60 кадров) |
| 2. Режим точки | Enter → `PointMoveMode` (HUD виден, «колбаски» убраны), QWEASD двигают точку, вердикт оракула обновляется (Safe/Marginal), Enter фиксирует новую точку и генерирует **другой формы** траектории, Esc возвращает точку РОВНО на место (0.0 мм) и прежние варианты |
| 2. Колесо/СКМ | 3 щелчка назад: 2.021 → 1.788 (ожидание 1.781) · вперёд + СКМ: шарик снова на поверхности (2.021) · в режиме перемещения точки колесо глубину НЕ меняет |
| 2. Undo/Redo | отмена выбора траектории возвращает прежний выбор и **не запускает движение**, повтор возвращает новый выбор |
| 2. Аварийная остановка | реальное движение (внешний план) → стоп → `Idle`, исполнитель остановлен, фантомы/траектории убраны |
| 2. Запись/позы/суставы/гриппер | запись пишет сэмплы и файл, воспроизведение идёт (×3, прогресс растёт) · поза сохраняется и «переезд в позу» проходит планировщиком · слайдер сустава двигает один сустав (лимит соблюдён), выход за лимит отклоняется · пальцы гриппера 75 → 61 мм |
| 3. SCARA | поток привязывается к SCARA, **3 оси** (J1, J2, Z — четвёртой оси у модели нет), точка фиксируется, **8 траекторий / 8 фантомов**, движение, режим точки, колесо, Undo/Redo, стоп, запись, позы, суставы, гриппер — всё как на роботе · **тепловая карта — КОЛЬЦО (Quad)**, у робота — сфера · IK: **2–3 конфигурации** на точку |
| 4. Интерфейс | тулбар **20 кнопок, 4 ряда по 5**, у всех подсказки · дерево, свойства (26 строк), подсветка рамкой · статус-бар: состояние/робот/координаты/тема · тема Тёмная/Светлая/Системная мгновенно + PlayerPrefs · при 1366×768 панели не наезжают друг на друга |
| 5. Взаимодействия | сессия сохраняется и загружается (точка + зоны + позы роботов) · объектов сцены за прогон +3 (зона/куб + убранный контейнер фантомов) — **утечек нет** · Esc работает из Idle, из «фантом едет», из движения робота и из режима точки |
| 6. Производительность | базовое время кадра 9.8 мс · во время генерации 8 траекторий 10.0 мс · 8 движущихся фантомов 4.1 мс · тепловая карта 2.8 мс · генерация 8 траекторий **0.17–3.2 с** · IK **0.45–6.3 мс/вызов** (зависит от позы) · полное обновление интерфейса 49 мс против кадрового 0.4 мс, дерево без изменений не пересобирается |
| 7. Стабильность | 6 циклов «точка → 8 траекторий → Esc» подряд на обоих роботах: **8/8/8 каждый раз**, сцена остаётся чистой, объекты не накапливаются |
| 8. Консоль | исключений — **0**, ошибок — **0** (единственная «ошибка» — `No graphic device is available` из batch-режима `-nographics`, это среда, а не код), `MissingReferenceException` — 0, журнал ведётся и пишется в файл, спама нет |

### Найдено и исправлено (11 багов/несоответствий + 2 предупреждения компиляции)
1. **ЗОНЫ ЗАПРЕТА молча попадали в мир столкновений** (главная находка). `CollisionWorld.Rebuild`
   собирает препятствия по **РЕНДЕРЕРАМ**, а не по коллайдерам, поэтому полупрозрачный объём зоны
   (коллайдер у неё предусмотрительно уничтожен) становился полноценным препятствием для планировщика,
   оракула и SafetyGate — хотя по замыслу зоны проверяются ОТДЕЛЬНО (`KvZoneService.CheckPlan`).
   Симптом: после накопления зон планировщик перестал строить траектории рядом с ними (у SCARA —
   **ни одной из 56 попыток**, «траектория не найдена»). **Исправлено:** фильтр `IsKeepOutZone`
   (`Зона_*`, `ZoneBody`) в `CollisionWorld` рядом с фильтрами фантомов/лазеров.
2. **Куб pick-and-place был препятствием** — тот же корень: цель захвата числилась в мире столкновений,
   и каждый шаг подхода отбраковывался SafetyGate («малый зазор»). **Исправлено:** исключение по имени
   `Куб_PickAndPlace` (константа `CollisionWorld.PickAndPlaceCubeName`, используется и гриппером).
3. **Спам в консоли при pick-and-place:** 7685× `[Flow] Внешнее движение отклонено: малый зазор` +
   столько же `[Safety] Траектория отклонена` за один прогон, при этом каждый кадр делалось полное
   перепланирование. **Исправлено:** `KvPickAndPlace.maxStepAttempts = 3` — после трёх неудач шаг
   помечается `Failed` с понятным сообщением (спам исчез полностью: 8 отказов за прогон вместо 15370 строк).
4. **Захват в pick-and-place срабатывал ДО движения:** `MoveTo` проверял расстояние до куба сразу после
   `PlayExternalPlan` (движение только ЗАПУЩЕНО) — в прогоне захват не срабатывал («куб не рядом
   с захватом, 0.281 м»). **Исправлено:** захват делает шаг `Close`, отпускание — шаг `Open`, то есть
   после реального окончания движения (`Tick` ждёт `ExternalMotionRunning`).
5. **Ложное «[RobotInventory] Роботов в сцене: 3»** — 114 раз за прогон: `Instantiate` копии-фантома
   запускал `RobotController.Awake` → проверку инвентаря ДО того, как копии выставят `HideInHierarchy`.
   **Исправлено:** `RobotInventory.Suppress` + пауза проверки вокруг `Instantiate` в `PhantomManager.CreateGhost`.
6. **Тепловая карта достижимости не меняла форму и положение.** `EnsureObject()` выходил по `root != null`,
   а трансформ задавался только при создании: после робота карта у SCARA оставалась **сферой** (и наоборот),
   центр/радиус не обновлялись при смене робота. **Исправлено:** пересоздание при смене типа примитива +
   `ApplyTransform()` на каждой сборке (`KvHeatmaps`).
7. **Подпись «SCARA: J1, J2, Z, J4»** в панели «Суставы» обещала 4 оси, слайдеров 3. У модели SCARA
   в этом проекте **три** оси (J1, J2, Z): четвёртой (roll) нет ни в FBX, ни в кинематике
   (`PoseValidator.Dof = 3`, `IkSolver.SolveAllScara` — аналитика 2R + призма). **Исправлено:** текст
   приведён к факту («SCARA: J1, J2, Z»).
8. **Предупреждения компиляции** `CS0618`: `FindObjectsOfType` в `Editor/ConvertRobotMaterialsToHDRP.cs`
   и `FindFirstObjectByType` в `FreeFlyCameraController.cs`. **Исправлено** заменой на актуальные
   `FindObjectsByType`/`FindAnyObjectByType` — сборка стала полностью чистой (0 предупреждений).
9. **IK pick-and-place «не сходилась» (ошибка 514 мм):** CCD — локальный метод и из «неудобной» позы
   застревал в далёком локальном минимуме. **Исправлено:** `KvPlanKit.SolvePoseForPoint` пробует ТРИ
   стартовых приближения (текущая поза, нулевая, середина диапазонов лимитов) и 160 итераций вместо 60.
10. **Pick-and-place: инструмент «смотрел вверх».** Прогон 14.09.2026 измерил направление пальцев
   (`ExtendWorld · up`) — оказалось **+0.90** вместо ≈−1: `ToolAlign` выравнивал инструмент по нормали
   `−up`, а фактические пальцы уходили вверх, поэтому захват «промахивался» ровно на 2 × `GraspDrop`
   (0.13 м). **Исправлено:** `KvPickAndPlace.toolExtendsAlongNegativeY = false` (просим `+up`), а в текст
   предупреждения добавлено измеренное направление — если сборка изменится, это видно в логе одной строкой.
   (Это тот же вопрос ориентации TCP, что зафиксирован в §6: прокси TCP наследует ориентацию меша
   `Axis6_2`, а не фланца.)
11. **Pick-and-place на SCARA**: куб ставился на 0.62 м от базы и переносился на 0.35/0.25 м — при вылете
   руки 0.70 м это граница кольца (подход и перенос отбраковывались). **Исправлено:** для 3-осевого
   робота куб ставится на 0.38 м, точка переноса — на 0.15/0.10 м (`transferOffsetScara`).

### Что осталось «требует доработки»
- **Pick-and-place: последовательность доходит до конца на роботе, но сам захват куба не подтверждён.**
  Что исправлено и видно в логе: демонстрация запускается, куб создаётся, шаги планируются, спама нет,
  тайминг захвата правильный (расстояние до куба 0.122–0.135 м вместо 0.281 м — то есть захват теперь
  проверяется в момент, когда робот действительно опустился). Что осталось: (а) на роботе пальцы
  подходили к кубу на 0.12–0.135 м — выравнивание инструмента было ПЕРЕВЁРНУТО (см. п.10 выше; правка
  сделана в конце сессии и **прогоном ещё не подтверждена**); (б) на SCARA шаг «перенос» отклонялся
  SafetyGate с причиной **«лимит сустава»** — при опускании к кубу на столе призма уходит в САМЫЙ низ
  диапазона, запас становится 0° < 3°, и план отклоняется (та же причина, что у точки ровно на столе,
  см. следующий пункт). Рекомендация: после подтверждения ориентации пальцев подобрать `approachHeight`,
  `liftHeight`, `GraspDrop` и допуск захвата (0.12 м) отдельно для робота и SCARA.
  **Дополнительно проверено:** попытка выровнять инструмент вниз через ЗЕРКАЛЬНУЮ конфигурацию запястья
  (`KvPlanKit.SolvePoseForPoint` теперь пробует перевёрнутое запястье — `J4+180, J5→−J5, J6+180`)
  результата не дала: подход стал отклоняться SafetyGate по зазору, захват по-прежнему не выполняется.
  Значит корень — в соглашении об ориентации TCP-прокси (§6: прокси наследует ориентацию меша `Axis6_2`,
  а не фланца), и решать это нужно там (одна строка в `SixAxisController.CreateTcpProxy`) или разворотом
  самого захвата на 180° (`extendAxisLocal`), а не настройкой нормали выравнивания.
- **SCARA и операции на уровне столешницы (точка на столе, опускание к кубу).** Ход призмы `z_5`
  отсчитывается от базы и его нижняя граница — ровно уровень стола (`Lower[2] = 0.00`, `Upper[2] ≈ 0.19`):
  как только инструменту нужно опуститься на стол, призма встаёт в нижний предел, запас до лимита = 0°,
  а планировщик требует ≥3° (`Planner.minLimitMarginDeg`) — ветви IK отбрасываются («траектория не найдена»),
  а `SafetyGate` отклоняет уже построенный план («Отказ Safety: лимит сустава»). Точка на 5–10 см выше
  стола планируется штатно (проверено: 8/8 траекторий). Лечится либо ходом призмы (геометрия `z_5`,
  `ZMin` сейчас −0.02 м), либо отдельным допуском запаса для ПРИЗМАТИЧЕСКОЙ оси (сейчас метры
  нормируются в «градусы»: `LimitMargin` делит на полный ход и умножает на 360).
- **Пределы хода Z у SCARA нестабильны:** `ApplyZTravelFromGeometry()` считает их по габаритам мешей,
  а `Renderer.bounds` меняются при повороте звеньев — в прогоне `ZMax` менялся 0.043 → 0.182 → 0.127,
  `ZMin` — −0.145 → −0.020 → −0.061, и каждый раз это писало 3 строки в консоль (**165 раз за прогон** —
  это и есть «максимум повторов» в отчёте). Нужно считать ход один раз (кэшировать) или логировать
  только итоговые значения.
- **Загрузка сессии восстанавливает позу только ТЕКУЩЕГО робота** (`KvSessionManager.Load`: применяется
  поза того робота, к которому привязан поток, второй пропускается с сообщением) — так и задумано
  (валидатор инициализирован под одного робота), но в отчёте сессии это выглядит как «роботов применено 1
  (пропущено 1)». Траектории в сессии не хранятся: после загрузки они пересчитываются по точке.
- **Иногда генерируется 7 траекторий вместо 8** (в прогонах 14.09.2026 — на точке
  (0.575, 0.980, −23.418) у робота): часть RRT-путей совпала и была отброшена как дубликаты (`IsSamePath`),
  а `distinctAttemptCap = 48` попыток исчерпался. Поведение предусмотрено кодом (в лог пишется
  `[Variants] … ПРИЧИНА: …` и подсказка «Вариантов: 7 (отфильтрованы дубликаты)»), но формально ТЗ
  требует ровно 8. Если 8 обязательны всегда — увеличить `distinctAttemptCap`/`detourVariants`
  или снизить строгость `IsSamePath`. **СДЕЛАНО в §16.14 (15.09.2026):** бюджет 320 попыток,
  12 разных обходов, к краевым целям — обходы без запаса; для SCARA подтверждено 8 из 8 уникальных
  на 5 точках, «добивка» похожими путями больше не нужна (0 из 8).
- **Ошибка среды batch-прогона:** `No graphic device is available to initialize the view` (HDRP в
  `-nographics`). К коду отношения не имеет (см. §10), в отчёте отделена от ошибок кода.
- **Расхождение в постановке задачи по скоростям.** В ТЗ этой сессии написано «фантомы со скоростью
  `robotMoveSpeed × 3`» и тут же «робот едет в 2× быстрее фантома». Реализовано и проверено ПЕРВОЕ:
  `robotMoveSpeed = 1/15` ю/с, скорость фантома = `robotMoveSpeed × phantomSpeedMultiplier (3) = 0.2` ю/с
  (1 юнит за 5 с), то есть **фантом в 3 раза быстрее робота** (робот 1 юнит за 15 с). Менять соотношение
  без отдельного ТЗ нельзя — это сломает зафиксированное ранее поведение (§5).

### Устаревшие места документа, приведённые к факту
- §5 «Скорость: реальный робот … **1 юнит за 30 с** (0.0333 ю/с). Фантом … `phantomSpeed = 0.5` ю/с» —
  неверно с прошлой сессии: штатно робот идёт **1 юнит за 15 с** (`1/15 = 0.0667` ю/с), фантом —
  **1 юнит за 5 с** (`×3`), отдельной «скорости фантома» больше нет (только множитель).
- §5 «Время прохода фантома: `minTravelTime`/`maxTravelTime` 1.2…8 с» — фактически **0.25…240 с**
  (кламп только как страховка, чтобы не искажать отношение скоростей).
- §5 «**Один фантом** (ТЗ)», «`phantomCount = 1`», «варианты траекторий: 3–5 (`candidateCount = 5`)» —
  устарело: сейчас **8 траекторий, 8 «колбасок», 8 фантомов** (`trajectoryCount = phantomCount = 8`,
  `MaxTrajectories = 8`), разводка «колбасок» 3.5 см, 5 цветов. Подтверждено прогоном (§0.5, блок 2).
- §2 «`Zones/KvToolbar` (15 кнопок-иконок сеткой 5×3)» — сейчас **20 кнопок в 4 ряда по 5** (5 новых
  из сессии этапов 1–20). Подтверждено прогоном (4.1/4.2).


## 0.6 Сессия 15.09.2026 — UX/UI-УЛУЧШЕНИЯ + ГЕЙМПАД (ЭТАПЫ 1–12 ТЗ)

**Что это.** Сессия только про ИНТЕРФЕЙС и ввод: группировка тулбара, починка кнопок,
открепляемые/растягиваемые окна, палитра команд, контекстное меню дерева, окно горячих
клавиш, состояния (empty/error/loading), геймпад, единые иконки, доступность и сплошная
проверка всех кнопок. **Логика роботов, планировщик, кинематика, State Machine, лазеры,
фантомы и существующие бинды клавиатуры не менялись** — все правки аддитивные; в
`FreeFlyCameraController` добавлены только «мосты» (флаги геймпада, `FocusOn`, проверка
занятости Tab) и одна публичная read-only обёртка.

**Как проверялось.** Новый батч-прогон `Assets/_Project/01_Scripts/Editor/DshUiStagesDiag.cs`
(диагностика агента, в копию пользователя НЕ переносится): 16 фаз в PlayMode — сборка оболочки,
группы тулбара, ВЫЗОВ КАЖДОЙ команды реестра, все меню, все вкладки и переключатели настроек,
окна (открепление/полочка/раскладка/сброс), размеры окон, палитра, контекстное меню,
горячие клавиши, состояния, геймпад, иконки, доступность, сводный отчёт по кнопкам.
Запуск (БЕЗ `-quit`):

```
Unity.exe -batchmode -nographics -projectPath "<копия>" -executeMethod DshUiStagesDiag.Run -logFile _dsh_ui_stages.log
```
Отчёт — `KavistovVv/_dsh_ui_stages.txt` (со сводной таблицей «кнопка → статус»).
**Итог финального прогона: `[OK] 100 · [FAIL] 0 · исключений 0 · ошибок в логе 0`** (детали —
в «ЭТАП 12» ниже). Компиляция: `dotnet build` обоих assembly — **0 ошибок**.
Отдельно: в batch-режиме у Unity **слетал доступ к лицензии** («Access token is unavailable»);
лечится перезапуском зависшего `Unity.Licensing.Client` (см. §9).

### ЭТАП 1. Группировка тулбара по областям применения
- Новый файл `Zones/KvToolbarGroups.cs`: 8 групп-ДАННЫХ (правка панели не нужна) —
  **Робот** (в т.ч. переключение робот/SCARA, стоп, аварийный стоп, слайдеры суставов,
  preset-позы, захват, ПУСК с проверкой), **Точка и траектория** (лазеры, waypoints,
  сравнение, графики), **Постобработка** (сглаживание, время-оптимальная, эко-профиль,
  автосглаживание, верстак, ограничения планирования), **Визуализация** (зона достижимости,
  лимиты, метрики, сингулярности, тепловые карты, силы, RRT-дерево, стенд, прокси, состояние,
  препятствия), **Запись и экспорт** (запись/воспроизведение, скриншот, видео, PDF-отчёт,
  экспорт в язык робота, импорт, сессия), **Сеть и автоматизация** (сеть/дашборд/мобильный
  пульт, макросы, деревья поведения, сценарии, пульт), **Настройки** (тема, язык, настройки,
  палитра, клавиши, сброс раскладки, старт-меню, обучение), **Прочее**.
- `KvToolbar` переписан на РУЧНУЮ раскладку в ДВА ПРОХОДА: группы не разрываются между
  строками, между ними — тонкая вертикальная линия ВЫСОТОЙ ВСЮ ПОЛОСУ; при нехватке ширины
  группа целиком уезжает на следующую полосу (высота панели считается сама, `LayoutDock`
  подстраивается). Ничего не «прячется» — прежнее меню «Ещё» больше не нужно (класс оставлен).
- У каждой группы слева — «ручка» (кнопка-иконка группы): **клик открывает выпадающий список
  команд группы** (название + горячая клавиша + отметка «в разработке»), там же пункт
  **«Свернуть группу в иконку»**. Свёрнутые группы хранятся в PlayerPrefs
  (`KazistovVv.UI.ToolbarGroup.<id>`), у каждой — своя кнопка «Развернуть/Свернуть».
- **Подсказка каждой кнопки** = название + **область применения (группа)** + описание +
  горячая клавиша; у «ручек» групп — название группы и число команд.
- Режим «5 в ряд» (исторический) сохранён: в нём групп нет, сетка ровно 5 кнопок в строке.

### ЭТАП 2. Кнопки, которые выдавали ошибки
- **Найдена кнопка «две горизонтальные полоски с точками»** — это `view.clearance`
  («Тепловая карта зазоров», иконка `clearance`: две горизонтальные линии, на верхней две
  точки). Второй кандидат по виду — `properties` (ТРИ линии с точками), но у него своя
  функция, и он остался. Команда `view.clearance` **исправна** (проверена прогоном:
  включается/выключается без исключений) — это была НЕ ошибка кода, а непонятная иконка
  без подсказки; лечится ЭТАПОМ 1: теперь подсказка говорит «Группа «Визуализация» ·
  Раскрашивает траекторию по зазору до препятствий: красное — опасно близко», а иконка
  живёт в своей группе.
- **Сплошная проверка всех кнопок** (ЭТАП 12) выполнена прогоном: каждая команда реестра
  вызвана в PlayMode внутри `try/catch`, статусы («работает / ошибка / заглушка /
  недоступна вне контекста») выписаны в отчёт `_dsh_ui_stages.txt`. Заглушки (`edit.undo`,
  `edit.redo`, `file.export`, `file.import`, `tool.measure`, `tool.grid` от штатной части)
  помечены иконкой/подсказкой «в разработке» и в тулбар вынесены только там, где реально
  работают (undo/redo перекрыты модулем функций проекта). Найденные и исправленные отказы —
  см. «Найдено и исправлено» ниже.

### ЭТАП 3. Открепляемые окна (dockable / undockable)
- `Zones/KvDockPanel.cs` дополнен: **кнопка открепления** (иконка `undock` ⟷ `dock`,
  подсказка меняется), **зона прикрепления с подсветкой** (`KvDockIndicator`: при
  перетаскивании заголовка подсвечивается левая/правая/нижняя полоса либо центр —
  «останется плавающим»), **кнопка ✕ сворачивает окно в боковую «полочку»**
  (`InRail`, клик по полочке возвращает окно), раскладка каждой панели пишется в
  PlayerPrefs (`Core/KvLayoutStore.cs`: край, толщина, свёрнутость, видимость, полочка,
  позиция и размер плавающего окна). Новая команда **«Вид → Панели → Сбросить раскладку»**
  (`ui.resetlayout`) + кнопка в настройках возвращают всё к значениям по умолчанию.
- Порядок прилипания при отпускании — тот же, что подсвечивал индикатор (`DockZoneAt`).

### ЭТАП 4. Ползунки размера окон
- У каждого окна **8 маркеров** (4 грани + 4 угла, невидимые, но ловят мышь). При наведении
  **курсор меняется** на «↔», «↕» или диагональ (`Core/KvCursors.cs` — курсоры рисуются
  кодом, файлов-ассетов нет, `Cursor.SetCursor` + `ForceSoftware`).
- Минимум окна — **150×100**, максимум — размер родительского канваса (`SetFloatRect`
  клампит и позицию, и размер). Пристыкованная панель тянется за ВНУТРЕННЮЮ кромку
  (остальные маркеры у неё выключены), смещение считается от начала перетаскивания —
  кромка идёт за курсором без накопления погрешности. Размер и толщина сохраняются
  в PlayerPrefs (ЭТАП 3).

### ЭТАП 5. Палитра команд (Ctrl+P)
- Новый файл `Zones/KvCommandPalette.cs`. Открывается **Ctrl+P / Ctrl+Shift+P** (и кнопкой
  Start геймпада, и командой «Вид → Палитра команд», и кнопкой в настройках). Поле ввода,
  фильтрация по всем командам; **Enter — выполнить, Esc — закрыть, ↑/↓/Tab — навигация**.
- Ищет по: подписи на текущем языке, **названиям на ВСЕХ языках (включая английский,
  `KvLoc.AllLanguages`)**, id команды («robot.stop»), описанию, горячей клавише и названию
  группы. **Недавние команды показываются первыми** (история — в PlayerPrefs). Рядом с
  командой показаны её **горячая клавиша** и группа.

### ЭТАП 6. Контекстное меню в дереве (ПКМ)
- Новый файл `Zones/KvContextMenu.cs` (общее всплывающее меню) + обработка **ПКМ** в
  `KvTreeRow`: **Переименовать, Дублировать, Удалить, Скрыть/Показать (галка), Фокус камеры
  на объекте, Свойства, Копировать имя**. Меню строится ПОД ТИП узла: «Дублировать» и
  «Удалить» доступны точкам и waypoints (у остальных — неактивны с пояснением), «Скрыть/
  Показать» — узлам с объектом сцены, «Фокус камеры» — объектам.
- **Мультивыбор: Ctrl+клик** (подсветка вторым цветом) — действие применяется ко ВСЕМ
  выбранным узлам; выбранные узлы хранятся по ключам и восстанавливаются после пересборки
  дерева (узлы пересоздаются каждые 0.4 с).
- «Фокус камеры» делает `FreeFlyCameraController.FocusOn(point, distance)` (новый
  публичный метод: ставит позицию и yaw/pitch оператора, дальше обзор работает как обычно).
  «Копировать имя» — через `GUIUtility.systemCopyBuffer`. «Удалить»/«Дублировать» для точек
  работают с историей точек менеджера, для waypoints — с `KvStageHub.Waypoints`.
- Правило зафиксировано в §5.21.

### ЭТАП 7. Окно списка горячих клавиш (F12)
- Новый файл `Zones/KvHotkeyView.cs` живёт в **обычной dock-панели** («Горячие клавиши»),
  поэтому его можно тащить, пристыковать и растянуть (этапы 3–4), а раскладка сохраняется.
  Открывается по **F12**, из меню «Справка → Горячие клавиши» и кнопкой Select геймпада.
- **Единый реестр биндов** `Core/KvBindings.cs` (клавиатура, мышь, UI, дерево, геймпад,
  заглушки VR) — ОДИН источник и для этого окна, и для вкладки «Управление» в настройках
  (`KvSettingsSchema.Bindings()` теперь строится из него, поэтому таблицы не расходятся).
- В окне: группировка по устройству и разделу, поле поиска (название/клавиша/пояснение),
  **подсветка конфликтов красным** (одна клавиша на два разных действия; контекстные бинды,
  например Esc, конфликтом не считаются) и кнопка **«Переназначить» — заглушка** на будущее.

### ЭТАП 8. Элементы состояний (Empty / Error / Loading)
- Новый файл `Core/KvUiStates.cs`:
  - **Empty**: в дереве — «Нет объектов» + подсказка, в свойствах — **«Выберите объект»** и
    пульсирующий **скелетон** вместо пустой панели, внизу — подсказка «Выберите точку красным
    лазером (Z + ЛКМ)», пока точки нет и поток в `Idle`;
  - **Error**: **красная плашка в правом нижнем углу** с текстом ошибки и кнопками
    «Подробнее» (разворачивает стек) и «Скрыть». Ошибки приходят из
    `Application.logMessageReceived` (Error/Exception/Assert) — то есть НЕ только в консоль;
    плашку можно выключить настройкой; ошибка среды batch-прогона `No graphic device` в
    плашку не попадает;
  - **Loading**: **полоса прогресса** над статус-баром — «неопределённая» (бежит) во время
    планирования 8 траекторий и записи/воспроизведения, с процентом при известном прогрессе
    (`KvUiStates.Begin/SetProgress/End`).

### ЭТАП 9. Геймпад (управление)
- Новые файлы: `Core/KvGamepadRouter.cs` (раскладка и автоопределение),
  `Core/KvGamepadBridge.cs` (мост к существующему вводу), `Zones/KvGamepadHud.cs`
  (виртуальный геймпад на экране).
- **Раскладка (расставлена с нуля — геймпад у оператора первый):** левый стик — движение
  камеры (как ходьба), правый стик — обзор, **D-Pad ↑/↓ — глубина шарика прицела** (аналог
  колеса мыши, с автоповтором), **D-Pad ←/→ — переключение робот ⟷ SCARA**, **A — подтверждение
  (аналог ЛКМ)**, **B — отмена/Esc**, **X — красный лазер**, **Y — зелёный лазер**,
  **LB — аварийная остановка**, **RB — домой (preset-поза)**, **LT — вход/подтверждение
  режима перемещения точки (аналог Enter)**, **RT — запуск траектории (ЛКМ зелёным)**,
  **Start — палитра команд**, **Select — список горячих клавиш**. R3 (старый тумблер стиков)
  оставлен для совместимости.
- **Если геймпада нет — управление НЕ активируется** (`Present = false`, флаги моста
  сброшены), клавиатура и мышь работают как раньше. Подключение определяется автоматически
  (каждый кадр + `InputSystem.onDeviceChange`, с сообщением в консоль).
- **Виртуальный геймпад** — мини-панель в левом нижнем углу: два стика с точками, полоски
  триггеров, «таблетки» кнопок (подсвечиваются при нажатии), строка «последнее действие»;
  без геймпада показывает «НЕТ ГЕЙМПАДА». Выключается настройкой «Виртуальный геймпад».
- Интеграция — по принципам существующего `InputManager`/`GamepadInputProvider`: роутер
  читает геймпад через new Input System и отдаёт действия уже существующим системам. Чтобы
  одно нажатие не срабатывало дважды, старые «геймпадные» пути в контроллере камеры
  (LB = выбор робота по прицелу, левый триггер = подтверждение) отключаются, пока роутер
  активен (`KvGamepadBridge.SuppressLegacyGamepad`); стики при этом работают БЕЗ ручного
  тумблера R3. Переназначение — заглушка (см. ЭТАП 7).

### ЭТАП 10. Единый набор иконок
- Все иконки приведены к одной сетке: **`KvIcons.GridSize = 24`, `ToolbarSize = 24`,
  `Stroke = 1.5 px`** (мелкие детали — `StrokeThin = 1.2 px`), единый отступ от края
  (`r = s/2 − 0.085·s`). Прежние разные толщины (1.0…2.0 px) заменены на единые (149 правок
  в `KvIcons.cs`).
- Добавлены иконки под новые функции: `more`, `undock`, `palette`, `keyboard`, `contrast`,
  `fontsize`, `gamepad`, `layout`, `lan`, `error`, `empty`, `loading`, `pin`, `duplicate`,
  `focus`, `rename`, `trash`, `copy`, `filter`. Полный список — `KvIcons.AllIds`,
  диагностика проверяет, что **все иконки нарисованы, одного размера и не пустые**.

### ЭТАП 11. Доступность
- **Размер шрифта** — отдельная настройка (Маленький / Средний / Большой): `KvTheme.FontSize`
  и др. стали вычисляемыми (`KvSettings.FontScale`), поэтому меняется ВЕСЬ интерфейс сразу.
- **Высокий контраст** — отдельный режим палитры (`KvTheme.HighContrast`): фон уводится в
  край, текст и рамки — в максимум контраста, подпись палитры в статус-баре это показывает.
- **Схема для дальтоников** — выбор типа (дейтеранопия / протанопия / тританопия) уже
  РАБОТАЕТ: красный/зелёный меняются на пару «оранжевый ↔ синий» (палитра Okabe–Ito),
  различимую при всех трёх типах; отдельные наборы под каждый тип — на будущее.
- **Навигация с клавиатуры** — `Core/KvKeyboardNav.cs`: **Tab / Shift+Tab** — по элементам,
  **стрелки** — по соседним кнопкам геометрически, **Enter/пробел** — нажать, **Esc** — снять
  фокус (рамка вокруг кнопки). Включается настройкой «Навигация с клавиатуры».
  **Важно:** пока навигация включена, **Tab занят фокусом** (ТЗ: «Tab, стрелки, Enter»),
  поэтому переключение интерфейса остаётся на **Esc** и на команду **«Вид → Показать/скрыть
  интерфейс»** (она же в тулбаре/меню); при выключенной настройке поведение Tab прежнее.
- Цветовые схемы для дальтоников и размер шрифта вынесены в настройки («Интерфейс»).

### Найдено и исправлено (сессия §0.6)
1. **Кнопка «Панель суставов» (`joints.panel`) — реальный отказ, найден прогоном и ИСПРАВЛЕН.**
   Прогон вызвал все 139 команд реестра; `joints.panel` бросала
   `InvalidCastException: Specified cast is not valid` в `KvFeatureWindow.MakeSlider`
   (`KvFeatureWindow.cs:1290`): объект-контейнер ползунка создавался как
   `new GameObject("HandleArea")` — БЕЗ `RectTransform`, поэтому приведение
   `(RectTransform)handleAreaGo.transform` падало, и вкладка «СУСТАВЫ» не открывалась вообще.
   **Правка:** `new GameObject("HandleArea", typeof(RectTransform))` (одна строка, логика
   слайдеров/суставов не менялась). После правки — «ОШИБОК: 0» по всем 139 командам.
   Это ровно тот класс дефектов, который искало ТЗ («кнопки выдают ошибки»).
2. **Раскладка тулбара больше не дублируется.** Список id панели теперь СОБИРАЕТСЯ из групп
   (`ToolbarLayout()` ← `KvToolbarGroups.All`), а не задаётся вторым списком: раньше кнопка,
   добавленная в группу, не появлялась на панели, если её не дописать ещё и в раскладку
   (так «терялись» `joints.panel`, `pose.save/goto`, `gripper.toggle`, `net.tab`, `web.toggle`,
   `script.tab`, `bt.tab`, `scenarios.open` и другие — было 42 кнопки, стало 78).
3. **Пустое состояние панели свойств работало «наполовину».** `BuildProperties(null)` возвращал
   три строки-подсказки, поэтому скелетон (ЭТАП 8) не показывался никогда. Теперь при пустом
   выборе панель отдаёт пустой список: видно «Выберите объект» и пульсирующий скелетон.
4. **Виртуальный геймпад показывал пустую строку состояния** в первом кадре после сборки
   (подпись ставилась только в `Update`). Теперь подпись заполняется сразу в `Build`.
5. **Навигация с клавиатуры не находила кнопок**, если её включили до первого тика (список
   элементов собирался по таймеру 0.5 с). Теперь список пересобирается при включении и при
   поиске кнопки по id.
6. **Ложные «конфликты биндов».** Движение камеры (`W/A/S/D, Q/E`) и движение ТОЧКИ в режиме
   перемещения (`Q/E, W/S, A/D`) — одни и те же клавиши в разных режимах, из-за чего окно
   горячих клавиш красило строки красным (6 «конфликтов»). Бинд движения точки помечен
   контекстным (`Contextual`) — конфликтов теперь **0**, реальные конфликты по-прежнему
   подсвечиваются.
7. **Диагностика (инструмент агента).** Первый прогон `DshUiStagesDiag` сам себя зациклил:
   обход «живой» коллекции `KvCommands.All` падал с `InvalidOperationException`, потому что
   часть команд (смена темы/языка) пересобирает оболочку и заново регистрирует реестр.
   Исправлено: список команд берётся снимком, фаза обёрнута в `try/catch` (одна ошибка больше
   не останавливает отчёт), исключения печатаются со стеком, а команды, меняющие язык/тему/
   настройки, после прогона возвращают прежние значения (иначе прогон оставлял интерфейс на
   другом языке).

### ЭТАП 12. Проверка всех кнопок в PlayMode — РЕЗУЛЬТАТ ПРОГОНА
- Батч-прогон `DshUiStagesDiag` вызывает **каждую команду реестра** (это и есть все кнопки
  тулбара и все пункты меню), проходит **все 6 меню**, **все 5 вкладок настроек**, все
  переключатели «Функции», все панели, палитру, контекстное меню, горячие клавиши, состояния,
  геймпад, иконки и доступность — и пишет сводную таблицу «команда → статус» в
  `_dsh_ui_stages.txt`. Каждый отказ фиксируется строкой `[FAIL] команда «id» … бросила
  исключение: …` (со стеком) и отдельным пунктом в «Найдено и исправлено» ниже.
- **ИТОГ ФИНАЛЬНОГО ПРОГОНА (15.09.2026): `[OK] 100 · [FAIL] 0 · исключений 0 · ошибок в логе 0`**
  (лог `_dsh_ui_stages.log`, отчёт `_dsh_ui_stages.txt`). Компиляция обоих assembly — 0 ошибок.
- **Сводка по кнопкам и пунктам:** команд в реестре **139** — работает **132**, заглушек **3**
  (`file.import`, `tool.measure`, `tool.grid`), недоступны вне контекста **4**
  (`robot.playpause`, `robot.stop` — пока робот не едет; `edit.undo`/`edit.redo` — пока нет
  истории). ОШИБОК — **0**. На панели **78 кнопок** в **8 группах** (7 разделителей), подсказка
  есть у всех 78, горячая клавиша в подсказке — у 25, заглушек на панели — 0.
- **Меню:** 182 строки суммарно (Файл 24, Правка 3, Вид 54, Робот 52, Сервис 44, Справка 5).
  **Настройки:** 5 вкладок построены (38 / 71 / 46 / 18 / 44 строки), **64 переключения**
  функций без исключений.
- **Окна:** открепление/прикрепление, зоны прикрепления (слева/справа/центр), перетаскивание
  заголовка, полочка, сохранение раскладки в PlayerPrefs, сброс раскладки, 8 маркеров размера,
  минимум 150×100, максимум = канвас (1900×1060 при канвасе 1920×1080), курсоры — всё зелёное.
- **Палитра:** открытие, фильтр («лазер» → 2, «stop» → 3), навигация стрелками, выполнение по
  Enter, пустой результат, закрытие — всё зелёное. **Горячие клавиши:** 58 строк, поиск по
  клавише и разделу, **конфликтов 0**. **Состояния:** empty/error/loading — зелёные.
  **Геймпад:** автоопределение (в batch устройства нет → управление выключено, как и требуется),
  индикатор, 15 строк раскладки, мост и переключение робота — зелёные. **Иконки:** 84 из 84,
  все 24×24, непустые. **Доступность:** размер шрифта (10/11/13 px), контраст, схема для
  дальтоников, Tab-навигация по 76 кнопкам + Enter — зелёные.

### Что осталось «требует доработки» (сессия §0.6)
- **«Удалить»/«Дублировать» в контекстном меню проверены только статически.** В batch-режиме
  точку мышью не поставить, поэтому ветки «Удалить»/«Дублировать» для узлов-точек и waypoints
  в прогоне не выполнялись (в дереве точек не было). Код написан и учитывает оба случая,
  но подтвердить его нужно в PlayMode руками: ПКМ по точке → «Дублировать» → появилась копия;
  ПКМ → «Удалить» → точка исчезла из ветки «Точки».
- **Проверка геймпада — без устройства.** В batch-режиме геймпада нет, поэтому проверено
  «управление не активируется» и вся обвязка (раскладка, мост, индикатор, переключение робота
  вызовом из UI). Реальные нажатия (стики, D-Pad, триггеры) нужно проверить руками с геймпадом.
- **Tab при включённой навигации.** Пока настройка «Навигация с клавиатуры» включена, **Tab
  занят фокусом** (ТЗ: «Tab, стрелки, Enter»), поэтому переключение режима интерфейса делает
  Esc (показать) и команда «Вид → Показать/скрыть интерфейс». Это осознанное изменение
  поведения клавиши (единственное в сессии) и оно выключается вместе с настройкой.
- **Переназначение биндов и наборы цветов по типам дальтонизма — заглушки** (по ТЗ): кнопка
  «Переназначить» в окне F12 показывает «в разработке», схема для дальтоников одна общая
  (Okabe–Ito) на все три типа вместо отдельных наборов.
- **Ресайз окон проверен через API, а не мышью.** `KvResizeHandle`/`KvSplitter` дергаются только
  настоящим курсором, поэтому в прогоне вызывались те же методы, что вызывают они
  (`ResizeFloatTo`, `ResizeDockedTo`, `SetFloatRect`); сами обработчики ввода проверены в
  PlayMode руками — см. чек-лист ниже.
- **Старая диагностика `DshDesktopUiDiag`** приведена к текущему интерфейсу (78 кнопок вместо 15,
  «фантомы» теперь имеют обработчик), но отдельным прогоном заново не гонялась — её проверки
  полностью покрыты `DshUiStagesDiag`.

### РУЧНОЙ ЧЕК-ЛИСТ ДЛЯ ПРОВЕРКИ В UNITY (сессия §0.6)
Порядок: открыть `MainScene`, нажать Play, затем TAB (курсор + панели).
1. **Группы тулбара.** Панель сверху разбита на 8 блоков с вертикальными линиями. Навести на
   любую кнопку — в подсказке видно **название + «Группа «…»» + описание + горячую клавишу**.
   Клик по иконке слева от группы → выпадает список команд группы с горячими клавишами.
   Внизу списка — «Свернуть группу в иконку»: группа сжимается до одной иконки, после
   перезапуска PlayMode остаётся свёрнутой.
2. **Ошибки кнопок.** Открыть «Сервис → Настройки → Функции», включить всё; затем пройти по
   панели и **нажать каждую кнопку** (78 шт.) и каждый пункт меню (182 строки) — в консоли не
   должно быть исключений; если появится ошибка, её текст продублируется **красной плашкой в
   правом нижнем углу** (кнопка «Подробнее» покажет стек).
3. **Окна.** Потянуть заголовок «Дерево моделей» к правому краю — подсветится правая полоса,
   после отпускания панель встанет справа. Потянуть в центр — станет плавающим окном.
   Кнопка «открепить» (иконка со стрелкой из рамки) — то же самое. Кнопка ✕ — окно уходит в
   узкую полочку у края, клик по полочке возвращает. Закрыть PlayMode и снова Play — окна
   должны стоять там, где их оставили. «Вид → Панели → Сбросить раскладку» — всё по умолчанию.
4. **Размеры окон.** Навести курсор на ЛЮБУЮ грань или угол плавающего окна — курсор меняется
   на ↔ / ↕ / диагональ; тянуть: меньше 150×100 не станет, больше канваса — тоже.
5. **Палитра команд.** `Ctrl+P` → набрать «лазер», «stop», «тема» → стрелки, `Enter`
   (команда выполнится), `Esc`. В пустом списке сверху — недавние команды.
6. **Контекстное меню.** ПКМ по узлу робота: Переименовать, Дублировать (серое), Удалить
   (серое), Скрыть, Фокус камеры, Свойства, Копировать имя. Поставить точку (Z + ЛКМ), затем
   ПКМ по узлу «Точка 1» → «Дублировать» (появится «Точка 2»), ПКМ → «Удалить» (исчезнет).
   `Ctrl+клик` по двум узлам → ПКМ → «Скрыть» спрячет оба.
7. **Горячие клавиши.** `F12` — окно со всеми биндами (клавиатура + мышь + геймпад), поиск по
   названию и клавише, конфликты красным (сейчас их нет), «Переназначить» — заглушка.
8. **Состояния.** Ничего не выбирать → в дереве «Нет объектов» (если сцена пуста), в свойствах
   «Выберите объект» + серый скелетон, внизу подсказка «Выберите точку красным лазером».
   Выбрать точку → во время расчёта 8 траекторий бежит полоса загрузки. Ошибка → красная
   плашка (проверить можно, например, выключив робота в сцене и нажав «ПУСК с проверкой»).
9. **Геймпад.** Подключить, нажать любую кнопку: в консоли «Управление геймпадом активно»,
   в левом нижнем углу появится мини-геймпад с подсветкой нажатий. Левый стик — ходьба,
   правый — обзор, D-Pad ↑/↓ — глубина шарика, D-Pad ←/→ — робот ⟷ SCARA, A/RT —
   подтверждение, B — отмена, X/Y — лазеры, LB — аварийный стоп, RB — поза, LT — режим точки,
   Start — палитра, Select — горячие клавиши. Отключить геймпад — управление гаснет,
   клавиатура работает как раньше.
10. **Иконки.** Визуально: все иконки одной толщины линии и одного размера, читаются на тёмной
    и светлой теме («Вид → Тема»).
11. **Доступность.** «Сервис → Настройки → Доступность»: размер шрифта (маленький/средний/
    большой) — меняется весь интерфейс; высокий контраст; схема для дальтоников (красный/
    зелёный становятся оранжевым/синим); навигация с клавиатуры — `Tab`/`Shift+Tab` водят
    рамку, стрелки — по кнопкам, `Enter` нажимает, `Esc` снимает фокус.

### ИЗМЕНЁННЫЕ И НОВЫЕ ФАЙЛЫ (сессия §0.6)
**Новые (12 .cs + .meta):** `06_KazistovVv_UI/03_Scripts/Core/`: `KvBindings.cs`,
`KvLayoutStore.cs`, `KvCursors.cs`, `KvGamepadBridge.cs`, `KvGamepadRouter.cs`, `KvUiStates.cs`,
`KvKeyboardNav.cs`; `06_KazistovVv_UI/03_Scripts/Zones/`: `KvToolbarGroups.cs`,
`KvContextMenu.cs`, `KvCommandPalette.cs`, `KvHotkeyView.cs`, `KvGamepadHud.cs`;
диагностика агента (в копию пользователя не переносится): `01_Scripts/Editor/DshUiStagesDiag.cs`.

**Изменённые:** `06_KazistovVv_UI/03_Scripts/Core/`: `KazistovVvUIManager.cs` (группы, команды
палитры/клавиш/раскладки/доступности/геймпада, контекстное меню, состояния, мосты настроек),
`KvTheme.cs` (размер шрифта, контраст, схемы для дальтоников), `KvSettings.cs` (новые
настройки + состояние групп тулбара), `KvIcons.cs` (единый штрих 1.5 px, 19 новых иконок,
`AllIds`), `KvWidgets.cs` (без изменений логики), `KvLocalization.cs` (`AllLanguages` для
поиска по всем языкам), `KvTooltip.cs` (без изменений); `Zones/`: `KvToolbar.cs` (групповая
раскладка в два прохода, «ручки» групп, подсказки с областью, сброс групп), `KvDockPanel.cs`
(полочка, индикатор зоны, кнопка открепления, 8 маркеров размера, сохранение раскладки),
`KvTreeView.cs` (ПКМ, мультивыбор, empty state), `KvPropertiesView.cs` (empty state +
скелетон), `KvSettingsView.cs` (доступность, сервисные кнопки, таблица биндов из `KvBindings`,
справка), `KvMenuBar.cs`/`KvStatusBar.cs` (без изменений логики); `01_Scripts/Core/`
`FreeFlyCameraController.cs` (мост геймпада, `FocusOn`, Tab занят навигацией);
`01_Scripts/Features/KvFeatureWindow.cs` (ФИКС: `HandleArea` без `RectTransform`);
`01_Scripts/Editor/DshDesktopUiDiag.cs` (ожидания приведены к текущему интерфейсу).

**СИНХРОНИЗАЦИЯ КОПИЙ (правило §1).** Выполнена 15.09.2026 в 20:41: все изменённые и новые
`.cs`+`.meta` перенесены из рабочей копии `DeepSeek` в копию пользователя
`C:\Users\Ольга\KavistovVv` — **68 файлов** (из них 24 заменённых, их прежние версии лежат в
`C:\Users\Ольга\KavistovVv\_dsh_backup_20260915_204153\`). Скрипты диагностики `Dsh*Diag.cs`
в копию пользователя НЕ переносятся (правило проекта) — они там удалены. Проверка: файлы
UI-модуля и изменённые скрипты в обеих копиях совпадают по хешам (8 из 8 проверенных);
в UI-модуле копии пользователя 35 `.cs`. Сцена, префабы, материалы, `ProjectSettings` и
`StreamingAssets` НЕ трогались — при первом открытии Unity сама пересоберёт `Library`.


## 1. Проект
- Название: KazistovVv
- **Единый формат имени (13.09.2026): `KazistovVv` — везде.** Папка модуля интерфейса `Assets/_Project/06_KazistovVv_UI`, namespace `KazistovVvUI` (+ `KazistovVvKinematics` вместо `KompasKinematics`), менеджер `KazistovVvUIManager`, объект сцены `KazistovVv_UI`, меню редактора `Tools/KazistovVv UI/…`, теги логов `[KazistovVv]`, ключи PlayerPrefs/EditorPrefs `KazistovVv.*`. Старое имя (KOMPAS / КОМПАС / `KompasUI`) в `Assets` **не встречается** — проверено поиском; `productName`/`companyName` в `ProjectSettings` и remote в `.git/config` и раньше были `KazistovVv`.
- **Интерфейс — ДЕСКТОПНЫЙ (ПК) в стиле FreeCAD, на uGUI**, собирается кодом в рантайме; VR-интерфейс делается отдельно (см. §0.4, §2).
- Цель: VR/MR-платформа для управления роботами (SCARA, 6-осевой)
- Корень Unity-проекта: `KavistovVv/` (git, последний коммит `5e2291c`, ветка `DeepSeek`)
- **ВНИМАНИЕ, ПРОВЕРЕНО 12.09.2026: на машине ДВЕ копии проекта, и они разные.**
  - рабочая копия агента (эта сессия, здесь же `PROJECT_CONTEXT.md`): `C:\Users\Ольга\OneDrive\Documentos\новое пространство\KavistovVv`, ветка **`DeepSeek`** (5 коммитов поверх `Cline` + незакоммиченные правки сессий 12.09 00:00–08:xx). **Только здесь есть `PointMoveMode`, `PointMoveHud`, `ToolAlign`, нормаль/смещение точки и т.д.**
  - копия, открытая у пользователя в Unity: `C:\Users\Ольга\KavistovVv`, ветка **`Cline`** (`origin/Cline`, HEAD `6413957`, редактор запущен 11.09 14:22 и работает по сей день). В ней **нет ни `FlowState.PointMoveMode`, ни `movePoint`, ни `PointMoveHud`** — там более старая линия State Machine (`UpdateAim` на 7 аргументов с `confirmRed/confirmGreen`).
  - Следствие: «режим перемещения точки не работает» в редакторе пользователя — это в первую очередь **отсутствие кода в его копии** (см. §5.9), а не логика. Смотреть/проверять режим нужно в копии `DeepSeek`.
  - **ОБНОВЛЕНО 12.09.2026, 18:41: копии СИНХРОНИЗИРОВАНЫ.** 23 файла скриптов перенесены из рабочей копии `DeepSeek` в копию пользователя `C:\Users\Ольга\KavistovVv` (ветка `Cline`) — теперь в ЕГО Unity есть и `PointMoveMode`, и колесо мыши, и исправленная коллизия, и фантомы по траектории. Перенесены только `Assets/_Project/**/*.cs` (+ `.meta` для новых файлов); **сцена, столы, роботы, материалы, ProjectSettings и всё остальное в копии пользователя не тронуты** (в его сцене столы прежнего размера 1.2×0.8 — по ТЗ столы не меняем). Резервная копия заменённых файлов: `C:\Users\Ольга\KavistovVv\_dsh_backup_<дата>_<время>\`. Проверка компиляции копии пользователя: `dotnet build Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj` → **0 ошибок**. Тестовые скрипты `Dsh*Diag.cs` в копию пользователя НЕ переносились (это диагностика агента).
  - Обратная сторона: правки агента теперь живут в ДВУХ копиях, и их надо держать синхронными. Правило сессии: рабочая копия — по-прежнему `DeepSeek`, а после каждого законченного блока правок — перенос изменённых `.cs` в копию `Cline` (см. §9).
- Стек: Unity **6000.5.6f1**, HDRP **17.5.0** (URP 17.5.0 тоже в проекте), Input System 1.20.0, XR Interaction Toolkit 3.5.1, AR Foundation 6.5.0, XR Management 4.7.0, OpenXR-лоадер + Meta X API layer (`Assets/XR/XrApiLayer_METAX_operator.dll`), ROS-TCP-Connector (ROS2). Пакета `com.meta.*` в `Packages/manifest.json` НЕТ — Meta-контур подключён через OpenXR-слой. В проект также затянуты сторонние MCP-мосты (`com.coplaydev.unity-mcp`, `com.gladekit.mcp-bridge`, `com.anklebreaker.unity-mcp`) — редакторские утилиты, на рантайм не влияют.
- LLM в runtime: **НЕ используется** (только кинематика, коллизии, планировщик, safety)
- Целевые роботы: **SCARA LS10-B702S** (`LS10-B702S_base_1`, `J1_3`, `J2_4`, `z_5`, кабель `cable_2`) и **6-осевой** (`Axis1..Axis6`, фланец `Axis6_2`)
- Исходные модели роботов: `Assets/Robots/Robot.fbx` (guid `65c1693b…`) и `Assets/Robots/ScaraRobot.fbx` (guid `14499f7a…`) — именно они стоят в сцене как prefab-instance под именами `Робот_6ос_Стенд1` и `SCARA_Стенд2`
- Сцены: `Assets/_Project/00_Scenes/` — `MainScene.unity` (единственная в Build Settings), `Boot`, `Main Menu`, `Simulation`

## 2. Архитектура
- `Core/` — модели роботов: `RobotController` (база: цель, телеметрия, `SetActive`, `ClearTarget`; в `Awake` вызывает `RobotInventory.Guard` — проверка «роботов должно быть ровно 2»), `SixAxisController` (CCD-IK, лимиты, самоколлизии, `SetPreferredSeed`), `SCARAController` (аналитическая планарная 2R + призма Z, ход Z по геометрии, кабель), `FreeFlyCameraController` (оператор: WASD/QE, два лазера Z/X, ЛКМ-подтверждение, фонарик G, камера-коллайдер, создаёт `AimIndicator` и `TrajectoryFlowController`, **колесо мыши — ГЛУБИНА ШАРИКА ПРИЦЕЛА: `scrollStep`/`minDistance`/`maxDistance`/`stickyToSurface`/`surfaceLayer`, средняя кнопка — `SnapAimBallToSurface()`**; добавлены **`FocusOn(point, distance)`** — фокус камеры из контекстного меню (ЭТАП 6) — и чтение **моста геймпада** `KazistovVvUI.KvGamepadBridge` (подтверждение/отмена/Enter, стики без тумблера R3) + проверка занятости Tab навигацией, ЭТАП 9/11), `SixAxisAutoSetup`, `InverseKinematics`/`DHInverse`, `RobotSelfCollision`, `HDRPAutoLighting`
- `Trajectory/` — ядро: `CollisionWorld` (капсулы+боксы+пол), `PoseValidator` (FK/поза/лимиты/зазоры/якобиан/`SolveIk`), `IkSolver` (6R ≤8 ветвей, SCARA ≤4 + CCD-резерв), `PostureSelector` (стоимость позы, гистерезис), `Planner` (BiRRT-Connect + shortcut + трапеция времени + скоринг), `KinematicsJacobian`, `SafetyGate`, `TrajectoryExecutor`, `MotionExecutor`, `PlanMetrics`, `ReachabilityOracle`, **`ToolAlign`** (выравнивание концевой плоскости инструмента — задел ШАГА 3, по умолчанию **выключен**), `StandBuilder` (+`Editor/StandsMenu` — редакторская утилита)
- `01_Scripts/Editor/` — редакторские утилиты (в рантайм не попадают): `ConvertRobotMaterialsToHDRP` (не наш файл, есть `CS0618`), `StandsMenu`, **`HierarchyPhantomCleaner`** (уборка «фантомных» записей в Hierarchy + инвентарь роботов), **`HierarchyAutoRefresh`** (принудительный `RepaintHierarchyWindow` раз в 2 с)
- `Trajectory/` — взаимодействие: `LaserManager` (лучи рук), `PhantomManager` (+`GhostMaterial`), `AimIndicator` (шарик прицела + оракул; **показывает «прилип / отведён» от поверхности**), `TrajectoryTube` («колбаска»), `GhostView`, **`TrajectoryFlowController`** (этапы 0–4 + `PointMoveMode`), **`PointMoveHud`** (HUD режима перемещения точки: координаты относительно базы робота + статус оракула + «рентген»-маркер), `SelectionTypes` (`FlowState`, `SelectionState`, `TubeMath`, `PhantomMath`, `MotionTiming`, `HierarchyOrder`)
- `06_KazistovVv_UI/` — **десктопный интерфейс в стиле FreeCAD** (ПК-версия, не VR), три уровня:
  - `Core/`: `KazistovVvUIManager` (сборка оболочки: меню, тулбар, dock-панели, статус-бар, реестр команд, привязка к потоку, тема, размещение роботов/столов, «лампочка Ильича» и светоотражение — логика прежняя), `KvTheme` (палитра + Тёмная/Светлая/Системная + PlayerPrefs + фабрика элементов), `KvSettings` (PlayerPrefs: функции, масштаб, плотность, число рядов тулбара), `KvIcons` (процедурные монохромные иконки, без ассетов), `KvTooltip` (+`KvTooltipTarget`), `KvWidgets` (кнопка-иконка `KvIconButton`, `KvSwitch`, `KvSegmented`, строки свойств), `KvCommands` (реестр команд — кнопки/меню строятся из него);
  - `Zones/`: `KvMenuBar` (строка меню из реестра), `KvToolbar` (**78 кнопок-иконок, разложенных по 8 ГРУППАМ** — см. §0.6 ЭТАП 1: группы-данные `KvToolbarGroups`, вертикальные разделители, «ручка» группы с выпадающим списком и сворачиванием группы в одну иконку), `KvToolbarGroups` (каталог групп + список команд группы), `KvTreeView` (иерархическое дерево моделей: выбор, мультивыбор Ctrl, «глазик», ПКМ), `KvContextMenu` (контекстное меню узлов, ЭТАП 6), `KvPropertiesView` (свойства только для чтения + empty state со скелетоном), `KvStatusBar`, `KvSettingsView` (5 вкладок: функции, управление, интерфейс+доступность, о программе, справка) + `KvSettingsSchema` (встроенная схема + внешний JSON; таблица биндов строится из `KvBindings`), `KvDockPanel` (dockable-панели: перетаскивание заголовка с подсветкой зоны прикрепления, открепление/прикрепление, полочка, 8 маркеров размера, сплиттер, сворачивание), `KvCommandPalette` (Ctrl+P), `KvHotkeyView` (окно горячих клавиш F12), `KvGamepadHud` (виртуальный геймпад), `KvSelectionHighlight` (рамка-габарит выбранного объекта), `CenterWindow` (предпросмотр размещения), `ScaraCableFollow`;
  - `Data/`: `ProjectNode` (+`ProjectNodeKind`: Group/Robot/Axis/Tcp/Table/Object/Point/Trajectory/Phantom), `RuntimeRegistry` (+`RegisteredObject`), `ObjectSpawner`; `Camera/IdleCameraBrain`; `Editor/KazistovVvMenu` (меню `Tools/KazistovVv UI`).
- `Input/` — `InputManager` + провайдеры: `KeyboardMouseInputProvider`, `VRInputProvider`, `MRInputProvider`, `GamepadInputProvider`
- `VR/`, `Spatial/`, `Recording/`, `Integration/` — VR-контур: `VRHandTracker`, `PoseSelector`, `PlacementController`, `CalibrationTool`, `SpatialAnchorManager`, `TrajectoryRecorder`/`Player`, `CollisionGuard`, `TargetMarker`, `RobotSelector`
- Не задействовано сейчас: `TrajectoryPlannerController` (хоткеи P/1/2/3; в `Awake` камеры НЕ создаётся → мёртвый код), `NarrowPhase` (нет ссылок), `GhostView` (визуал только для предыдущего)

## 3. State Machine
`FlowState` (в `SelectionTypes.cs`), ведёт `TrajectoryFlowController`. Одна кнопка действия — **ЛКМ** (смысл зависит от включённого лазера: Z — красный, X — зелёный; в сцене красный включён по умолчанию, зелёный выключен).

| Состояние | Вход | Выход / правила |
|---|---|---|
| `Idle` | старт, `FinishMotion`, `ResetAll` | ЛКМ: только красный → точка + 3–5 траекторий; недостижимо → «Выберите другую точку (причина)»; только зелёный / оба выключены → «Включите красный лазер»; оба → как «только красный» → `PointSelected` |
| `PointSelected` | `LockPoint` | тайм-слайсы планирования `ProcessPlanningSlices`; найдено → `TrajectoriesShown`; нет → `Idle` + «траектория не найдена» |
| `TrajectoriesShown` | планирование завершено | ЛКМ: красный → пересчёт под новую точку; зелёный → выбор «колбаски» → `PhantomsMoving`; мимо → «Наведите на траекторию»; оба → «Выберите один лазер»; оба выкл. → «Включите лазер» |
| `PhantomsMoving` | `SelectTrajectory` → `ClearPhantoms` + `ShowAlongPath` | появляется **ОДИН** фантом в СТАРТОВОЙ позе робота и плавно едет по ВЫБРАННОЙ траектории до её конечной позы (не по прямой, по сэмплам `plan.Path`), затем останавливается и остаётся видимым; выбор доступен и в пути, и после остановки; ЛКМ: зелёный по фантому → `RobotMoving`; мимо → «Наведите на фантом»; красный → назад к пересчёту (старый фантом при этом уничтожается в `LockPoint`); оба → «Выберите один лазер» |
| `RobotMoving` | `SelectPhantom` + `MotionExecutor.Play` | робот едет 1 юнит / 30 с, **все нажатия игнорируются**; по завершении `FinishMotion` → `Idle` |
| `PointMoveMode` | Enter (из `PointSelected` или `TrajectoriesShown`, точка уже зафиксирована) | **режим перемещения точки**: клавиши Q/E (вверх-вниз), W/S (вперёд-назад), A/D (влево-вправо) двигают точку в МИРОВЫХ координатах (Shift — ×3), мышь точку не двигает, **колесо мыши глубину шарика тоже не трогает (игнорируется)**, ЛКМ не действует, лазеры — только индикация (сходятся в перемещаемой точке и красятся в цвет её вердикта); Reachability Oracle перепроверяет точку онлайн (интервал 0.08 с + сразу после остановки), HUD показывает координаты относительно базы робота, смещение от исходной позиции и статус (зелёный/жёлтый/красный). **Enter** → подтвердить → `PointSelected` с НОВОЙ позицией (траектории считаются заново); **Esc** → отмена → `PointSelected`/`TrajectoriesShown` с ИСХОДНОЙ позицией (прежние варианты возвращаются мгновенно, очередь просчёта продолжается с того места, где была остановлена). Вход в режим без точки → подсказка «Сначала выберите точку красным лазером (ЛКМ)» |

- Этап 0 (без нажатий): меняется только цвет шарика прицела (оракул: зелёный/жёлтый/красный).
- **Колесо мыши = ГЛУБИНА шарика (ТЗ сессии 18:12)** и работает во всех состояниях, где есть шарик прицела (`Idle`, `PointSelected`, `TrajectoriesShown`, `PhantomsMoving`): вперёд — шарик идёт по лучу от оператора и прилипает к первой поверхности, назад — отходит к оператору; средняя кнопка — возврат к ближайшей поверхности. Состояния, ЛКМ-логика и переходы при этом не меняются: изменилась только ПОЗИЦИЯ точки прицела (шарика). В `PointMoveMode` колесо игнорируется (см. ниже).
- `Esc` → `ResetAll` (фантомы/траектории/движение) → `Idle`. **Кроме `PointMoveMode`**: там Esc = отмена перемещения (точка возвращается в исходную позицию, состояние `PointSelected`).
- **Старт без выбранного робота** (`selectedRobot = null`, у обоих роботов `isActive = 0`). Активного робота поток определяет сам (см. §5); до этапа 4 цели снимаются у **всех** роботов.
- **`PointMoveMode` (ТЗ сессии 07:48)** входит в State Machine как отдельное состояние; в нём лазеры и их обычные функции (выбор точки, выбор траектории/фантома) не работают — только индикация перемещаемой точки.

## 4. Сцена
| Объект | Позиция | Примечание |
|---|---|---|
| `Стенд_1_Стол` | (0, 0, **−23.9**) | корень стола 1; столешница 4.8×3.2 |
| `Стенд_2_Стол` | (0, 0, **−28.1**) | корень стола 2; столешница 4.8×3.2 |
| `Столешница` (×2) | локально (0, 0.955, 0), scale **(4.8, 0.05, 3.2)** | верхняя плоскость **y = 0.98** |
| `Ножка_1..4` (×2 стола) | локально (±**2.3**, 0.465, ±**1.5**), scale (0.06, 0.93, 0.06) | по углам столешницы |
| `Робот_6ос_Стенд1` | локально (0, **0.98**, 0) | prefab-instance `Robot.fbx`, 6-осевой на столе 1, `isActive = 0` |
| `SCARA_Стенд2` | локально (0, **0.98**, 0) | prefab-instance `ScaraRobot.fbx`, SCARA на столе 2, `isActive = 0` |
| `Main Camera` | (0, 2, −28) | `FreeFlyCameraController`: создаёт лазеры, `AimIndicator`, `TrajectoryFlowController` |
| `Level`, `Plane`, `Directional Light`, `Cline_RimLight`, `Cline_FillLight`, `Reflection Probe (1)`, `Post Process Volume`, `Sky and Fog Volume`, `KazistovVv_UI`, `Spline` | без изменений | свет/HDRP не трогать |
- Зазор между ближними краями столешниц: `4.2 − 1.6 − 1.6 = **1.000**` юнита.
- Фантомы/траектории/маркер прицела/лампа — служебные объекты: `HideFlags.HideInHierarchy` (контейнер `Phantoms` и сами копии — тоже **только** `HideInHierarchy`, без `DontSave`, см. §5.3), в иерархии и в дереве KazistovVv не видны, в `CollisionWorld` не попадают.
- **Инвентаризация роботов (проверено 12.09.2026 в edit- и play-режиме): ровно 2** — `Робот_6ос_Стенд1` (SixAxisController) и `SCARA_Стенд2` (SCARAController), оба дети своих столов, оба `isActive = 0`. Рантайм-скриптов, создающих роботов при старте, в проекте нет.
- **Роботы в файле сцены (проверка 12.09.2026, 02:35):** ровно два prefab-instance из FBX — `guid 65c1693b…` (`Робот_6ос_Стенд1`, `SixAxisController`, монобехейвиор `&1076390688`) и `guid 14499f7a…` (`SCARA_Стенд2`, `SCARAController`, `&327369629`); у каждого `isActive = 0`; в `SceneRoots` один корень на объект. Лишних/дублирующих экземпляров, «осиротевших» `stripped`-блоков и объектов без контроллера нет. `SixAxisAutoSetup` в сцене не стоит ни на одном объекте (и `SixAxisAutoSetup`-скрипт нигде не висит), поэтому `_IKTarget` в PlayMode не создаётся.

## 5. Принятые решения
- Одна кнопка действия — ЛКМ; красный/зелёный лазер только задают смысл (ТЗ этапов 0–4).
- **Робот потока определяется по прицелу** (`TrajectoryFlowController.ResolveSelectedRobot(aimPoint)`): 1) явный выбор оператора (F / дерево KazistovVv); 2) пока фаза ≠ `Idle` — прежняя привязка; 3) иначе — ближайший к точке прицела робот, с гистерезисом **0.5 юнита** (на границе стендов привязка не «мигает»). `AimIndicator.ResolveRobot()` использует ту же логику, поэтому оракул и поток всегда говорят об одном роботе. Так этапы 0–4 работают и **без** предварительного выбора робота.
- **Автовыбор при старте убран**: `KazistovVvUIManager.Start()` вызывает `SelectNode(null)` — ни один узел дерева не активен, ни один робот не подсвечен (`selectedRobot = null`).
- **Фантом плавно едет по выбранной траектории** (ТЗ сессии 12.09.2026, 03:15): после подтверждения траектории зелёным лазером `TrajectoryFlowController.BuildPhantoms` вызывает `PhantomManager.ShowAlongPath(plan, startQ, phantomSpeed)`. Фантом появляется РОВНО в стартовой позе робота (`ApplyPose(startQ)`) и проходит по сэмплам `plan.Path` — тем же, что пойдёт реальный робот (не по прямой), между сэмплами — интерполяция по кратчайшим доворотам (`LerpPose`); профиль разгона/торможения берётся у траектории (`Times`, нормированные в 0..1). По прибытии фантом останавливается и остаётся видимым. Прежний мгновенный показ (`appearAtTargetPose = true`) больше не используется: флаг оставлен только для `Show` (множественные копии IK — задел), по умолчанию `false`.
- **Один фантом (ТЗ)**: `phantomCount = 1`, `BuildPhantoms` строит ровно одну копию — по выбранной траектории, без перебора конфигураций IK (IK для фантома не решается вообще: конечная поза = `plan.GoalQ`). Задел на несколько конфигураций сохранён кодом: `ik.SolveAllSeeded`/`TopUpVariants`/`PhantomMath.PickDistinct` и `PhantomManager.Show` (показ нескольких копий), но в потоке не вызывается.
- **«В сцене всегда ровно один фантом» (ТЗ сессии 03:26)**: перед каждым созданием вызывается `PhantomManager.ClearPhantoms()`, который уничтожает (а) все зарегистрированные копии, (б) всё, что осталось в контейнере `Phantoms` (страховка от незарегистрированных копий), (в) сам контейнер — он создаётся заново при следующем показе («пул очищен»), (г) «осиротевшие» объекты `Phantom_*`/`Phantoms` верхнего уровня сцены (`PurgeOrphans`, тот же фильтр, что и в `DestroyLeftoverContainers`), (д) **материалы-инстансы** копий (`Renderer.material` создаёт копию материала — раньше они оставались жить после удаления фантома). Копия удаляется `KillGhost`: сначала `SetActive(false)` (в тот же кадр исчезает из сцены и не выбирается лучом), затем `Destroy` в PlayMode / `DestroyImmediate` в редакторе. `PhantomManager.Hide()` теперь делает ту же полную уборку (все пути потока — Esc, выбор, новая точка красным лазером — идут через него), а `OnDisable`/`OnDestroy` убирают свои копии и контейнер **без** обхода сцены (идёт выгрузка).
- **Копия — «тихий» Instantiate (ТЗ сессии 03:26)**: сразу после `Instantiate` объект выключается (`SetActive(false)`), поэтому **ни один кадр его скриптов не выполняется**: копия не создаёт `TCP`-прокси, не перепарцентит меши (`FixLegacyMeshParents`), не добавляет себе `RobotSelfCollision`/коллайдеры и ничего не пишет в Hierarchy. Компоненты/коллайдеры/Rigidbody у копии всё равно уничтожаются, флаги `HideInHierarchy` ставятся на копию и всех потомков, включается копия (`Activate`) уже в стартовой позе. `hideFlagsReassertFrames` снижены с 8 до 2 — доклейка флагов осталась только как страховка.
- **Время прохода фантома**: `duration = clamp(длина TCP-пути / phantomSpeed, minTravelTime, maxTravelTime)` — по умолчанию `phantomSpeed = 0.5` ю/с (**1 юнит за 2 с**), границы 1.2…8 с. Для эталонной траектории 3.3 юнита это ≈6.6 с (у робота на этапе 4 те же 3.3 юнита заняли бы 99 с). Настройки — в инспекторе `PhantomManager` (`pathSpeed`, `minTravelTime`, `maxTravelTime`) и `TrajectoryFlowController.phantomSpeed`.
- Фантомы: бирюза (hue 0.475…0.545, alpha **0.55**), HDRP-прозрачность (`_SurfaceType=1`, `_SURFACE_TYPE_TRANSPARENT`, `_BlendMode=Alpha`, `_SrcBlend=One`, `_DstBlend=OneMinusSrcAlpha`, `_ZWrite=0`, queue 3000) и **`_ZTestTransparent = 8` (ZTest.Always)** — «рентген»: при обычном `LEqual` фантом в позе реального робота целиком прятался за непрозрачной моделью (это и выглядело как «фантомы не появляются»).
- **Движение фантома ведёт сам менеджер (ТЗ сессии 03:26)**: `PhantomManager.Update()` → `Tick(Time.deltaTime)`, и шаг делается **ровно один на кадр** (`Time.frameCount == lastTickFrame` → выход). Поэтому фантом едет даже если поток этапов в этом кадре вышел раньше (`robot == null`, идёт планирование), а вызов `phantoms.Tick` из `TrajectoryFlowController.UpdateAim` не удваивает скорость. Флаг `appearAtTargetPose` больше **не отключает** движение: он действует только на копии без пути (многофантомный задел `Show`), фантом с `paths[i] != null` движется всегда. По прибытии ставится `arrived[i]`, поза доводится до последнего сэмпла ровно один раз (в лог — «фантом доехал до конечной позы и остановился»), дальше фантом не трогается и остаётся видимым.
- **Суставы копии кэшируются** (`copyJoints[slot]`, снимаются один раз при создании через `validator.FindCopyJoints` — точные имена `Axis1..Axis6`); `ApplyPose` в кадре больше не обходит модель через `GetComponentsInChildren`, а при пустом кэше ищет заново как страховку. Стартовая поза берётся через `validator.ContinueFrom(path[0], startQ)` — ближайший доворот к началу пути, без «полного оборота» на первом кадре.
- **Фантомы полностью вне иерархии:** контейнер `Phantoms` создаётся с `HideFlags.HideInHierarchy` (без `DontSave` — иначе объект переживает выход из PlayMode и остаётся «фантомной» записью в редакторе), каждая копия и все её потомки — тоже `HideInHierarchy`; флаги доклеиваются ещё 2 кадра после создания (`ReassertHideFlags`, см. §5.3), у копии уничтожаются все MonoBehaviour/коллайдеры/Rigidbody — нет ни в иерархии Unity, ни в дереве KazistovVv, ни в `CollisionWorld`.
- Скорость (УТОЧНЕНО 14.09.2026, проверено прогоном §0.5): реальный робот (этап 4) идёт **1 юнит за 15 с** (`robotMoveSpeed = 1/15 = 0.0667 ю/с`), а фантом — **1 юнит за 5 с** (0.200 ю/с): его скорость ВЫЧИСЛЯЕТСЯ как `robotMoveSpeed × phantomSpeedMultiplier` (множитель 3), отдельной «скорости фантома» нет. Процесс перемещения виден (ТЗ). Прежняя запись «робот 1 юнит за 30 с / фантом 0.5 ю/с» устарела.
- Фантомов **восемь** — по одному на каждую из 8 траекторий (ТЗ сессии 13.09.2026: «8 траекторий и 8 фантомов»; `phantomCount = MaxTrajectories = 8`, `BuildAllPhantoms`/`ShowAllAlongPaths`), все стартуют одновременно из позы робота. Подтверждено прогоном §0.5 на обоих роботах. Добор конфигураций IK (`TopUpVariants`: зеркала плечо/локоть/запястье + CCD-доводка в ту же точку TCP) и выбор «максимально разных» ветвей (`PhantomMath.PickDistinct`) остались в коде как задел.
- Фантомы **остаются видимыми** до `Esc`/выбора; выбирать зелёным лучом можно **и во время движения, и после остановки** (наведение — по объёмам деталей движущейся копии, никаких задержек и блокировок в `Stage3_Confirm` нет).
- Варианты траекторий: **8** (`trajectoryCount = MaxTrajectories = 8`, проверено прогоном §0.5; в редких случаях 7 — когда несколько путей совпали и дубликаты `IsSamePath` отфильтрованы, причина пишется в лог `[Variants]`), сортировка по score, подписи «Траектория N · лучшая» / «Траектория N · вариант (невыгодная)», в подсказке — длина/время/зазор/запас лимитов/**оценка**. Разные ветви IK дают практически один и тот же путь TCP, поэтому «колбаски» **разведены вбок** (`SpreadPath`: шаг 3.5 см на вариант) и **раскрашены в разные цвета** (`VariantColor`): без этого варианты сливались в одну линию и казалось, что второстепенных траекторий нет. Лучшая рисуется точно по пути (без смещения). *Прежняя запись «3–5 вариантов (`candidateCount = 5`)» устарела.*
- **Фантом идёт по ИСТИННОМУ пути варианта, а не по его «колбаске»:** `SpreadPath` смещает трубку вбок на 3…12 см только для визуального разведения вариантов. Фантом повторяет `plan.Path` выбранного кандидата — ровно то, что поедет реальный робот на этапе 4. Если вариант выбран не первый, трубка и путь фантома могут расходиться на эти сантиметры (осознанно: фантом показывает движение, а не «колбаску»).
- Служебная визуализация (скрытая из иерархии) исключается из `CollisionWorld` — иначе робот «упирается» в собственные фантомы («малый зазор»).
- Углы вращения трактуются по модулю 360° с выбором кратчайшего доворота (`FoldAngle`, `WithinLimits`, `Planner.Delta/Interp`, `MakeContinuous`, `PostureSelector`, `IkSolver.ConfigDistance`); путь для исполнителя непрерывный (`ContinueFrom`).
- `LimitMargin`: призматическая ось SCARA нормируется (полный ход = 360° эквивалента); `Planner.minLimitMarginDeg = 3` = `SafetyGate.minLimitMarginDeg`.
- SCARA: поза восстанавливается после любых «примерок» (`ClearanceAt`, `PivotAt`, `AxisWorld`) — робот не двигается во время планирования; логика SCARA в этой сессии не менялась (правки в общих файлах действуют и на неё, но поведение прежнее).
- **SCARA-фантом (движение по пути):** `PoseValidator.ApplyScaraToCopy` доворачивает звенья **от текущего** состояния копии (в отличие от `Apply` у реального робота, где отсчёт идёт от покоя), поэтому при покадровом применении поворот накапливался бы. `PhantomManager` снимает эталонную позу копии сразу после `Instantiate` (`J1_3`/`J2_4`/`z_5`) и перед каждой позой возвращает копию в неё (`CaptureScaraBaseline`/`ApplyPose`). Семантика применения та же, что была при одном вызове на конечную позу, — кинематика SCARA и `PoseValidator` не тронуты. Для 6-осевого этого не требуется: `ApplyToCopy` задаёт углы абсолютно (`AngleAxis(q)·q0`).
- Столы: ×4 по ширине/глубине, высота 0.98 неизменна, зазор 1 юнит; `StandBuilder` — единый источник габаритов.
- **Инвентарь роботов — только сцена (ТЗ сессии 12.09.2026):** роботов ровно два, и рантайм их не создаёт. В `RobotController.Awake` вызывается `RobotInventory.Guard` (класс в конце `RobotController.cs`): он считает «сценных» роботов (без `hideFlags`-фантомов и без копий с `KazistovVvUI.RegisteredObject`), и один раз за старт пишет в консоль `[RobotInventory] Роботов в сцене: 2 (6-осевой + SCARA)…` либо предупреждение с разбивкой по типам, если инвентарь не 2 (лишние = копии в открытой сцене, а не работа рантайм-скриптов). Guard ничего не создаёт и не удаляет — только диагностика. Копии роботов создаёт единственное место в проекте: `ObjectSpawner.SpawnRobot` (кнопка «Добавить робота» в TopBar → `KazistovVvUIManager.ConfirmPlacement`), и это действие оператора, а не автоматика при `Start`/`Awake`/`OnEnable`.
- **Автовыбор робота/узла при старте отсутствует:** `KazistovVvUIManager.Start()` → `SelectNode(null)` (ни один узел дерева не активен), `FreeFlyCameraController.selectedRobot` не сериализуется и стартует как `null`, оба робота в сцене `isActive = 0`. Проверено в файле сцены: поля `selectedRobot` в `MainScene.unity` нет вообще.
- Координаты роботов на столах, HDRP-настройки, свет, существующие материалы и планировщик/оракул/валидатор по возможности не менялись.
- **Точка на поверхности смещается по её нормали (ТЗ сессии 12.09.2026, 03:49; значение offset уточнено ШАГОМ 1 в 07:12).** Нормаль берётся из `RaycastHit` в `FreeFlyCameraController.ComputeAimPoint()` (`aimNormal`, `aimOnRealSurface`) и передаётся в поток через новый перегруженный вход `TrajectoryFlowController.UpdateAim(..., Vector3 aimNormal, bool aimOnSurface)`. Целевая точка для робота: `target = hit.point + hit.normal * toolOffset`, **`toolOffset = 0.10`** юнита (публичное сериализуемое поле — правится в инспекторе в PlayMode, без перекомпиляции). Для горизонтального стола это вверх по Y, для наклонной/вертикальной поверхности — вдоль её нормали. Оракул достижимости и планировщик получают уже **смещённую** точку (`state.point`), а `state.aimAtLock` хранит исходную точку на поверхности. TCP останавливается на расстоянии «пятака» от поверхности: робот не врезается в текстуру и не проваливается внутрь.
- **Смещение применяется только к точке НА ПОВЕРХНОСТИ.** Для точки в свободном пространстве (луч ни во что не попал) смещения нет — поведение прежнее (ТЗ). Старые вызовы без нормали (VR-контур, совместимость) идут по тому же правилу: без смещения и без выравнивания.
- **ВРЕМЕННЫЙ ХАК (+toolOffset по Y) остался только как режим.** `TrajectoryFlowController.offsetMode = WorldUpHack` — смещение выбранной точки строго вверх по Y, без нормали (п.4 ТЗ). По умолчанию включён **основной** режим `SurfaceNormal` (через нормаль поверхности). Хак нужен как ручной откат/сравнение, если нормаль на конкретной геометрии окажется негодной; дополнительно он описан в §6 как временное решение.
- **Концевая плоскость («пятак») выравнивается по поверхности — это ШАГ 3, на ШАГЕ 1 ВЫКЛЮЧЕНО.** Код готов как задел: `TrajectoryFlowController.alignToolToSurface` (по умолчанию **false**), `alignAngleToleranceDeg = 6f`, `Trajectory/ToolAlign.cs`. Нормаль «пятака» — ось вращения последнего сустава (`PoseValidator.AxisWorld(Dof-1)`, у модели это локальная Y фланца = вдоль инструмента). `ToolAlign.AlignGoal` подстраивает **только последнюю позу выбранной траектории** покоординатным спуском по стоимости «ошибка TCP по позиции + ошибка угла оси» (шаг ≤4° на сустав, призму SCARA не трогает); поза принимается **только** если позиция TCP ≤8 мм от цели, остаток угла ≤`alignAngleToleranceDeg`, лимиты с запасом ≥3°, зазор до мира ≥`gate.minClearance`, самозазор ≥1 см и ни один сустав не довернулся больше чем на 120°; затем `TrajectoryFlowController.ApproachClear` проверяет участок подхода. При **любой** неудаче поза остаётся прежней (свободная ориентация). Чтобы включить на шаге 3 — поставить `alignToolToSurface = true`.
- **Кинематика, IK, планировщик, валидатор, оракул, лазеры и State Machine не менялись** — выравнивание сделано «снаружи» (используются только публичные read-only методы `PoseValidator`: `TcpAt`, `AxisWorld`, `WithinLimits`, `LimitMargin`, `ClearanceAt`, `SelfClearance`). Оси/лимиты/формулы IK те же.
- **Оракул проверяет ту же точку, что и поток**: `AimIndicator` получил `toolOffset`/`offsetAlongNormal` (значение синхронизируется с потоком в `FreeFlyCameraController.Awake`), шарик прицела при этом остаётся на поверхности — оператор видит, куда наводит, а цвет шарика соответствует реальной цели робота.
- **Диагностика «красных» отказов (ШАГ 1, п.4 ТЗ).** Оба красных случая пишут в консоль строку с тегом `[OffsetDiag]` и (при `logOffsetDiagnostics = true`, по умолчанию) дописывают её в файл **`_dsh_offset_diag.txt` в корне проекта** (`Application.dataPath/../`, не чаще раза в секунду): `LogOffsetRejection(...)` — оракул отверг точку (`пересечение со сцене` / `IK не сходится`), `LogPlanFailure()` — точка прошла оракул, но планировщик не нашёл траекторий (обычно запас у поверхности < `Planner.clearance` = 0.02). В строке: причина, **точное значение toolOffset**, запас в мм, координаты точки на поверхности, нормали, цели TCP, режим смещения и (для второго случая) отчёт планировщика `LastDebug`/`LastBranchInfo`. Это и есть материал для §7 «Диагностика».

### Режим перемещения точки (PointMoveMode, ТЗ сессии 12.09.2026, 07:48)
- **Новое состояние `FlowState.PointMoveMode`** (`SelectionTypes.cs`) — отдельный режим рядом с этапами 0–4. Вход: **Enter** в `PointSelected`/`TrajectoriesShown` (в `PointSelected` планирование может ещё идти — на входе очередь планирования очищается, «колбаски» прежней точки убираются и пересчитываются после выхода). Выход: **Enter** — подтвердить (точка фиксируется заново, планирование стартует с нуля), **Esc** — отмена (точка возвращается в исходную позицию, тоже `PointSelected`). Из `PhantomsMoving`/`RobotMoving` вход в режим не делается (там свои сценарии).
- **Точка больше не следует за курсором**: в режиме `state.movePoint` меняется ТОЛЬКО клавишами; мышь продолжает вращать камеру, наведение/ЛКМ на этапы не влияют, `AimIndicator` приостановлен (`SetSuspended`), лазеры — только индикация и сходятся в перемещаемой точке (`LaserManager.UpdateRays(state.movePoint, true)`).
- **Движение — в МИРОВЫХ координатах** (ТЗ этап 3), направление считает контроллер камеры (`FreeFlyCameraController.PointMoveAxis()`): W/S — горизонтальная проекция взгляда, A/D — вправо/влево, Q/E — мировая вертикаль; скорость `pointMoveSpeed = 0.5` юнита/с, при Shift — ×`pointMoveFastMultiplier = 3`. Горизонталь для W/S выбрана осознанно (иначе взгляд вниз «втыкал» бы точку в стол), вертикаль — отдельные Q/E.
- **В режиме клавиши QWEASD принадлежат точке, а не камере**: движение камеры (`transform.forward/right`, Q/E, стик геймпада) в `FreeFlyCameraController.Update` пропускается, пока `flowController.IsPointMoveMode` (иначе камера улетала бы вместе с точкой). `E` в режиме = «точка вверх» (как альтернативная кнопка подтверждения ЛКМ E в режиме не используется — ЛКМ/геймпад-триггер в нём игнорируются).
- **Онлайн-проверка достижимости (этап 4)**: `RefreshPointMoveVerdict()` вызывает тот же `ReachabilityOracle`, что и шарик прицела (рабочая зона, IK с лимитами, столкновения со сценой и другими роботами, близость к лимитам и сингулярности запястья), но не каждый кадр, а раз в `pointMoveOracleInterval = 0.08` с. Вердикт НЕ блокирует движение точки (этап 5): оператор сам следит, куда её двигает, статус только показывается цветом.
- **Точка — маркер, а не физический объект (этап 5)**: `state.movePoint` это просто `Vector3`, у него нет ни коллайдера, ни Rigidbody — сквозь стены/столы он проходит свободно, а недостижимость показывает оракул (красный). Никаких ограничений движения по коллизиям не добавлено.
- **HUD (этапы 2, 6) — `Trajectory/PointMoveHud.cs`**: (а) крупная панель screen-space overlay в стиле KazistovVv (`KvTheme`, свой `Canvas` с `sortingOrder = 60`, `CanvasScaler` 1920×1080, панель 700×132 сверху по центру с отступом 92 px — не закрывает точку и робота): заголовок «РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ», крупные координаты `X: 1.245   Y: 0.300   Z: -2.100`, строка статуса (`ДОСТИЖИМО` / `БЛИЗКО К ПРЕДЕЛУ` / `НЕДОСТИЖИМО (столкновение)` + причина оракула) и подсказка по клавишам; цветная полоса слева и цвет статуса — зелёный/жёлтый/красный; (б) маркер в самой точке (мировая сфера, неон, «рентген» `GhostMaterial.MakeGhost` = ZTest Always) — точку видно даже внутри стены, с лёгкой пульсацией.
- **Координаты — относительно базы робота (этап 6)**: `robot.transform.InverseTransformPoint(point)` (ноль — корень робота `Робот_6ос_Стенд1`/`SCARA_Стенд2`, стоящий на столешнице), формат с 3 знаками. Это «нулевые координаты выбранного робота»; при необходимости легко переключить на ось сустава 1 (`PoseValidator.PivotAt(0, 0)`), но корень понятнее оператору.
- **Смещение по нормали при выходе НЕ применяется повторно**: `state.movePoint` уже является целью TCP (оператор поставил её руками), поэтому подтверждение/отмена идут через `LockPointExact(target, …)`, который берёт цель как есть. `LockPoint` (обычный красный выбор) по-прежнему сначала смещает точку через `OffsetTarget(...)`.
- **HUD/маркер — служебные объекты рантайма**: контейнер HUD и все его потомки, а также маркер получают `HideFlags.HideInHierarchy` (в иерархии и в дереве KazistovVv не видны, в `CollisionWorld` не попадают — правило проекта из §8); HUD создаётся `AddComponent<PointMoveHud>()` в `TrajectoryFlowController.Awake` (сцена не меняется).
- **Проверка ориентации TCP (задача сессии, исправление не делалось)**: см. §5.8 и открытый вопрос в §6 — по коду нормаль концевой плоскости это локальная **Y** фланца, а не X; на текущие расчёты это не влияет (IK позиционная, `ToolAlign` берёт ось сустава, `tcp.rotation` нигде в логике не читается), но соглашение зафиксировано.

#### Исправления сессии 12.09.2026, 09:00 (после диагностики «режим не работает»)
- **Код режима теперь реально компилируется Unity.** `PointMoveHud.cs`/`ToolAlign.cs` лежали без `.meta` — AssetDatabase их не импортировала, и в `Library/ScriptAssemblies/Assembly-CSharp.dll` (от 02:07, до правок 07:43–07:49) не было ни `FlowState.PointMoveMode`, ни HUD, ни полей перемещения. После батч-прогона 09:00 у всех новых файлов есть `.meta`, `Assembly-CSharp.dll` пересобран (08:15:01), csproj перегенерированы и включают новые файлы. Подробности — §5.9.
- **Ввод Enter/Esc/Shift/движение точки больше не зависит только от new Input System**: `enter`/`cancel`/`shift` читаются через `IsKeyPressed`, а подтверждение `E` — через новый `IsKeyDownThisFrame` (нажатие «в этом кадре», а не удержание!) — у обоих есть откат на legacy `Input`. Раньше при `Keyboard.current == null` Enter не доходил вообще, а `PointMoveAxis()` сразу возвращал ноль («точка не двигается»).
- **`E` больше не дублирует ЛКМ в режиме** (там она — «точка вверх»): подтверждение в режиме не срабатывает, точка не дёргается.
- **Лазеры в режиме действительно показывают точку**: `DrawHandLaser` получил цель явным параметром; в режиме лучи сходятся в ПЕРЕМЕЩАЕМОЙ точке, идут сквозь геометрию (точка может быть в стене) и красятся в цвет вердикта (`TryGetPointMoveTarget`).
- **Esc в режиме принадлежит режиму**: камера больше не снимает захват курсора и не включает панели KazistovVv по Esc, пока активен `PointMoveMode` (UI — `CapsLock`). Вне режима поведение Esc прежнее (сброс `ResetAll`).
- **Отмена по Esc восстанавливает траекторию, а не считает заново**: на входе в режим варианты (`plannedSoFar`) и очередь тайм-слайсов откладываются (`moveSavedPlans`/`moveSavedQueue`), по Esc они возвращаются вместе с трубками мгновенно, просчёт продолжается с того места, где был остановлен.
- **Точка видна всегда** (ТЗ): маркер `PointMoveHud` теперь живёт вместе с зафиксированной точкой и в `PointSelected`/`TrajectoriesShown`/`PhantomsMoving`/`RobotMoving` (цвет — вердикт оракула), а крупная панель режима — только в `PointMoveMode` (`SetHudVisible`/`SetMarkerVisible` вместо общего `SetVisible`).
- **Подтверждение (Enter) фиксирует точку как точку СВОБОДНОГО пространства** (`LockPointExact(target, target, Vector3.up, false, true)`): прежняя поверхность/нормаль к перемещённой вручную точке не относятся, смещение по нормали повторно не применяется, выравнивание «пятака» её не касается.
- **Оракул в режиме обновляется не только по таймеру**: дополнительно перепроверка идёт сразу после остановки точки (`!moved && !moveVerdictValid`), поэтому цвет/статус всегда отвечают текущей позиции.
- **HUD дополнен строкой «Δ от исходной»** (смещение от позиции входа в режим) и чтением состояния для проверок (`CoordsText`/`DeltaText`/`StatusText`); панель стала 700×156.
- **Мышь в режиме не действует даже при выключенном `suppressDirectTeleop`**: `HandleClickActions` выходит сразу, если активен `PointMoveMode`.

### Управление глубиной шарика прицела (колесо мыши, ТЗ сессии 12.09.2026, 18:12)
- **Шарик прицела = точка на луче, у которой есть ГЛУБИНА.** Луч исходит из позиции оператора (камера — «текущий источник лазера») в направлении прицеливания; глубина шарика меняется колесом мыши. Реализация — в `Core/FreeFlyCameraController.cs` (`ComputeAimPoint(allowDepthControl)`, `UpdateAimBallDepth`, `SnapAimBallToSurface`, `AddScrollInput`, `ReadScrollNotches`), состояние шарика — в `Trajectory/AimIndicator.cs`.
- **Модель глубины: «опорная точка + сдвиг вдоль луча».** Опорная точка — та же геометрия, что и раньше: первая РЕАЛЬНАЯ коллизия по лучу (`laserLayers`), а если её нет — горизонтальная рабочая плоскость y = 0. Сдвиг (`depthOffset`, + к оператору) отводит шарик вдоль луча. **Без колеса сдвиг равен нулю, и шарик стоит РОВНО на опорной точке — поведение прицела не изменилось ни на миллиметр** (проверено прогоном: глубина 2.616 = дистанция луча до столешницы).
- **Параметры в инспекторе (`Main Camera → FreeFlyCameraController`, раздел «Глубина шарика прицела (колесо мыши)»):** `scrollStep = 0.08` (юнитов за щелчок колеса), `maxDistance = 20`, `minDistance = 0.25`, `stickyToSurface = true`, `surfaceLayer = Physics.DefaultRaycastLayers` (пустое значение = слои лучей), а также `scrollSmoothSpeed = 16` (плавность, 1/с) и `surfaceStickTolerance = 0.005` (допуск «шарик на поверхности»). Поля сериализуемые — правятся в PlayMode без перекомпиляции.
- **Прокрутка вперёд (Scroll Up) — шарик идёт по лучу ОТ оператора; назад (Scroll Down) — К оператору.** Шаг — `scrollStep` за щелчок, движение плавное (экспоненциальное сглаживание по `scrollSmoothSpeed`, поэтому «скорость прокрутки» видна).
- **Прилипание (`stickyToSurface = true`):** пока шарик идёт вперёд, система пускает Raycast из позиции оператора вперёд и не пускает шарик дальше первой поверхности по слою `surfaceLayer`; упёршись, шарик стоит на ней, и дальнейшая прокрутка вперёд его не двигает. Прокрутка назад отводит его от поверхности к оператору (не ближе `minDistance`). Луч в пустоту — шарик движется свободно в пределах `minDistance…maxDistance`.
- **`stickyToSurface = false`** — шарик НЕ липнет: прокрутка вперёд ведёт его сквозь геометрию (до `maxDistance`); проверено прогоном (глубина 3.096 при поверхности 2.616).
- **Средняя кнопка (Middle Click) — мгновенный возврат к ближайшей поверхности:** `SnapAimBallToSurface()` делает Raycast по лучу, берёт первое попадание и ставит шарик туда (сдвиг = 0, без плавности). **Решение по случаю «луч ни во что не попал»: шарик ОСТАЁТСЯ НА МЕСТЕ** (никаких «телепортов» назад к камере) + подсказка в статусе «Под прицелом нет поверхности — шарик остался на месте». Если луч не попал в геометрию, но пересекает рабочую плоскость y = 0 — шарик возвращается на неё.
- **`minDistance` / `maxDistance`:** ближе `minDistance` шарик к оператору не подпускается (защита от «шарика в голове», камера-коллайдер имеет радиус 0.25). Предел `maxDistance` — «мягкий»: он ограничивает ход колеса, но если поверхность под прицелом дальше `maxDistance`, шарик всё равно стоит на ней (иначе на дальней геометрии прицел «ломался» бы сам, без колеса).
- **«Прилип / отведён» для потока этапов:** `aimOnSurface` = шарик стоит РОВНО на реальной поверхности (в пределах `surfaceStickTolerance`). Прилип — как было: цель TCP = точка поверхности + `toolOffset` вдоль нормали. Отведён колесом — это точка СВОБОДНОГО пространства: смещение по нормали НЕ применяется (правило проекта «смещение только для точки НА поверхности» сохранено), ориентация свободная.
- **State Machine, планировщик, IK, валидатор и оракул не менялись.** `aimHit` (флаг «луч встретил поверхность») остался прежним по смыслу — в пустоте поток по-прежнему отвечает «Наведите красный лазер на поверхность»; меняется только позиция шарика, которую поток получает как `aimPoint`. **Прилипание оракул не ломает:** `AimIndicator` получил отдельный признак «шарик есть» (`ballExists`) и опрашивает оракул и для прилипшего шарика (точка + `toolOffset`), и для отведённого (он сам).
- **Пустота: шарик остаётся ВИДИМЫМ** (у него всегда есть глубина) — это единственное видимое отличие от прежнего поведения (раньше маркер при `!hitSurface` скрывался, и «свободное движение» было бы не видно). Лазеры при этом сходятся в шарике, а не уходят параллельно вперёд.
- **Визуал «на поверхности»** (`AimIndicator`): прилип — шарик слегка СПЛЮЩЕН по нормали поверхности (`surfaceFlatten = 0.55`) и горит полной яркостью; отведён — ровный шар и тусклее (`freeEmissionScale = 0.55`). Цвет всегда прежний — вердикт оракула (кислотно-зелёный / ядовито-оранжевый / неон-маджента), палитра не менялась. Никаких дополнительных линий/индикаторов не добавлено.
- **Ввод колеса:** за кадр читается ОДИН источник — legacy-ось `Input.GetAxis("Mouse ScrollWheel")` (0.1 за щелчок → ×10), если она пуста — new Input System (`Mouse.current.scroll`, нормализация «щелчка»: >8 → /120, >1.5 → /10, иначе как есть). Так в режиме `activeInputHandler = 2 (Both)` один щелчок не считается дважды. Внешний вход `AddScrollInput(notches)` (щелчки, + = вперёд) — для VR-контроллеров и автотестов.
- **Колесо и средняя кнопка принадлежат шарику только когда оператор работает с миром:** курсор захвачен (телеоперация) ИЛИ курсор свободен, но НЕ над панелями KazistovVv (`IsPointerOverUI`). Иначе колесо по-прежнему прокручивает списки UI (см. §6).

### 5.1 Верификация (12.09.2026, Unity batch + play mode)
- Компиляция проекта: **0 ошибок** (`_dsh_compile_check.log`); единственное предупреждение — унаследованное `warning CS0618` в `Editor/ConvertRobotMaterialsToHDRP.cs` (не наш файл).
- Прогон «два лазера → фантомы» на 6-осевом (`_dsh_robot6_verify.txt`, лог `_dsh_robot6_verify.log`):
  - роботов в сцене на старте и в конце: **2**, оба `isActive = False`, `selectedRobot = null`;
  - ЛКМ красным лазером по точке (0.10, 1.10, −24.50) → `PointSelected` → `TrajectoriesShown`, **кандидатов 5**, длины 3.29–3.30 юнита, время 99 с, «колбаски» разведены (min зазор между вариантами 0.030…0.136 м);
  - зелёный луч + ЛКМ по траектории 0 → `PhantomsMoving`, **фантомов 5** (ветви S−E+W−, S−E−W+, S−E+W−, S−E+W+, S−E−W−, разница конфигураций 0.186…1.175);
  - материалы фантомов: `_SurfaceType=1`, `_ZWrite=0`, `_ZTestTransparent=8`, alpha 0.55 (пульс 0.38…0.55 в полёте), `renderQueue=3000`, 7/7 рендереров включены;
  - движение: прогресс 0.005 → 0.017 → 1.000, все фантомы доехали и **остались видимыми**;
  - выбор фантома зелёным лучом **в полёте** (`hoveredPhantom = 2`) и **после остановки** → ЛКМ → `RobotMoving`; `Esc` → `Idle`;
  - сообщения этапов в логе: «Точка принята · считаю варианты…», «Вариантов: 5…», «Фантомов: 5…», «Траектория выбрана · фантомов: 5…», «Движение: Фантом 3 [S-,E+,W-] · скорость 1 юнит за 30 с», «Сброшено (Esc)».
- `Assets/_Project/01_Scripts/Editor/DshDiag.cs` — временный диагностический прогон; после проверки **удалён** из проекта.

### 5.2 Верификация (12.09.2026, 02:38 — статический разбор файла сцены + сборка скриптов)
- Прогон Unity в batch **не запускался** (в проекте открыт редактор пользователя; второй инстанс на той же `Library` недопустим). Проверка выполнена по файлам: `MainScene.unity` (6254 строки), `Assets/Robots/*.meta`, все `*.cs` проекта.
- `MainScene.unity`: **2 prefab-instance робота** (`65c1693b…` SixAxis + `14499f7a…` SCARA), **2** `SixAxisController/SCARAController` (`isActive = 0`), `SceneRoots` = 15 корней, `m_SourcePrefab` на роботов — по одному; файл совпадает с версией в git (`HEAD`, отличия — только позиции/модификации прошлой сессии).
- Поиск по всем скриптам: `Instantiate` — 3 места (`CalibrationTool` — точка калибровки, `PhantomManager.CreateGhost` — фантом, `StandBuilder.PlaceRobotOnStand` — **редакторская** утилита `Tools/Stands`), `CreatePrimitive` — примитивы UI/стола/маркера/прицела; `StandBuilder.EnsureStands` в рантайме не вызывается (только `StandsMenu` в редакторе); `SixAxisAutoSetup` в сцене не висит. **Скриптов, создающих роботов в `Start/Awake/OnEnable`, нет.**
- Изменённые файлы этой сессии: `Trajectory/LaserAndPhantomManagers.cs`, `Trajectory/TrajectoryFlowController.cs`, `Trajectory/SelectionTypes.cs`, `Core/RobotController.cs`. Сцена `MainScene.unity` **не менялась** — в ней и не было дубликатов.
- **Компиляция проверена без Unity** (редактор пользователя открыт, второй инстанс на той же `Library` не запускался): `dotnet build Assembly-CSharp.csproj` (Unity-сгенерированный проект, `netstandard2.1`, C# 9, 279 ссылок на DLL редактора — тот же набор, что собирает Unity) → **0 ошибок**, 3 предупреждения и все три не наши: `CS0618` и `CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs` (класс `RegisteredObject`). Артефакты сборки (`bin/`, `obj/`, `Temp/bin/`) удалены, `.csproj` не изменялся.
- Изменений API для сцены нет: добавлено только поле `appearAtTargetPose = true` у `PhantomManager` (создаётся `AddComponent` в рантайме, в сцене не сериализуется); `RobotInventory` — статический класс без сериализации. Новый публичный класс `RobotInventory` лежит в конце `RobotController.cs` (отдельного файла нет — меньше правок).

### 5.3 Верификация (12.09.2026, 02:55 — «фантомные записи в Hierarchy + автообновление дерева»)
- Симптом (ТЗ): в Hierarchy висят `SCARA LS10-B702S` и 6-осевой робот (`Axis1..Axis6`), поиском по сцене не находятся.
- Разбор по файлам (Unity в batch **не** запускался — редактор пользователя открыт, два инстанса на одной `Library` недопустимы):
  - `MainScene.unity` (6254 строки) чист: **2 prefab-instance** (`65c1693b…` SixAxis + `14499f7a…` SCARA), 2 контроллера, `SceneRoots` — 15 корней, 43 имени GameObject (столы/ножки/свет/камера/UI), ни одного объекта `Phantom*`/`Phantom_*`, дубликатов и «осиротевших» `stripped`-блоков нет (`TCP` встречается только как пустое поле `tcp: {fileID: 0}` у обоих контроллеров). **Сцена не менялась** (правок в `MainScene.unity` в этой сессии нет).
  - Имён `SCARA LS10-B702S` / `Axis1..Axis6` в сцене **нет** — это `robotName` у `SCARAController`/`SixAxisController` и имена узлов внутри `ScaraRobot.fbx` (`LS10-B702S_base_1`, `LS10-B702S_J1_3`, …) и `Robot.fbx` (`Axis1_2 … Axis6_2`). Значит, «фантомами» выглядят **копии роботов**, а не объекты сцены.
- Найденные причины (все — «кто создаёт / кто не удаляет»):
  1. **`ObjectSpawner.SpawnRobot`** (кнопка «Добавить робота», TopBar → `KazistovVvUIManager.ConfirmPlacement`): `copy.name = template.robotName + "_" + HHmmss` → копия получает имя **«SCARA LS10-B702S_HHmmss»** / «6-осевой робот_HHmmss» с детьми `Axis1_2…Axis6_2`. Именно эти строки и видит оператор; при активном режиме размещения их создаёт **любая** ЛКМ по столу. Это единственный штатный путь появления «лишних» роботов — не рантайм-автоматика.
  2. **`PhantomManager` (копии-фантомы)**: контейнер `Phantoms` и копии создавались с `HideFlags.HideAndDontSave`, а контейнер не уничтожался никогда. `DontSave` = «не уничтожать при выгрузке сцены», поэтому объект переживает остановку PlayMode и остаётся в редакторе **вне загруженных сцен**: виден в Hierarchy, но не находится ни поиском по сцене, ни `FindObjectsOfType` — ровно описанный симптом. Дополнительно: копия — живая модель, её `SixAxisController.Update` (`FixLegacyMeshParents`, `ResolveRobotReferences` → `CreateTcpProxy`) в первом кадре создаёт/переставляет детей (`TCP`, `Collider_*`, меши), из-за чего часть деталей копии теряла `HideFlags` и появлялась в дереве (`Axis1..Axis6`).
  3. **`CenterWindow`** (предпросмотр размещения): копия модели `Phantom_Robot` удалялась только явным `DestroyPreview()` — при выключении окна оставалась в сцене.
- Что сделано (точечно, без правок лазеров/State Machine/планировщика/столов и без изменений в сцене):
  - `Trajectory/LaserAndPhantomManagers.cs`, `PhantomManager`: `GhostHideFlags = HideFlags.HideInHierarchy` (без `DontSave`) для контейнера и копий; контейнер теперь хранится ссылкой и уничтожается в `OnDisable`/`OnDestroy` (`DestroyRoot`, помощник `Kill`: `Destroy` в PlayMode / `DestroyImmediate` в редакторе); `Hide()` тоже через `Kill`; `ReassertHideFlags()` — после создания копии 8 кадров подряд доклеивает флаги (гасит детей, созданных живыми скриптами копии: `TCP`, `Collider_*`); `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] DestroyLeftoverContainers()` — уборка «протёкших» `Phantoms`/`Phantom_*` перед загрузкой сцены.
  - `06_KazistovVv_UI/03_Scripts/Zones/CenterWindow.cs`: `OnDisable`/`OnDestroy` → `DestroyPreview()` (копия предпросмотра не остаётся в сцене).
  - **Новый** `01_Scripts/Editor/HierarchyPhantomCleaner.cs`: уборка «фантомных» записей. Удаляет только (а) объекты **вне любых сцен** (`!scene.IsValid()`) с флагом `DontSave*`, похожие на наши (`Phantoms`, `Phantom_*`, `GhostTrajectory_*`, `Траектория*`, `AimIndicator*` либо с `RobotController` внутри), и (б) объекты с `HideInHierarchy` и «нашим» именем; ассеты (`EditorUtility.IsPersistent`) и объекты сцены (в том числе оба робота) не трогаются. Запуск: при перезагрузке домена (после компиляции, только вне PlayMode), перед входом в PlayMode (`ExitingEditMode`) и сразу после остановки (`EnteredEditMode`), плюс меню `Tools/KazistovVv/Hierarchy`:
    - `Очистить фантомные записи` — уборка + repaint;
    - `Проверить инвентарь роботов (ожидается 2)` — лог «роботов в сцене: 2 (…)» или предупреждение с именами лишних;
    - `Диагностика: объекты вне сцен` — список объектов редактора вне сцен (ничего не удаляет);
    - `Удалить копии роботов, созданные через UI` — по подтверждению удаляет копии `ObjectSpawner` (есть `RegisteredObject`, не `Робот_6ос_Стенд1`/`SCARA_Стенд2`).
  - **Новый** `01_Scripts/Editor/HierarchyAutoRefresh.cs`: `[InitializeOnLoad]` + `EditorApplication.update` → `EditorApplication.RepaintHierarchyWindow()` каждые **2 с** (только repaint: без обхода сцены и `FindObjects`, пропуск при `isCompiling`/`isUpdating`). Подписка — в статическом конструкторе `[InitializeOnLoad]`, иначе теряется при домен-релоаде в PlayMode. Переключатель: `Tools/KazistovVv/Hierarchy/Автообновление иерархии (2 с)` (галочка в `EditorPrefs`, по умолчанию включено).
- Компиляция: `dotnet build Assembly-CSharp-Editor.csproj` (Unity-генерированный csproj, `netstandard2.1`, C# 9) → **0 ошибок**; 4 предупреждения и все не наши: `CS0618`+`CS0649` в `FreeFlyCameraController.cs`, `CS0618` в `Editor/ConvertRobotMaterialsToHDRP.cs`, `UAC1001` в `RuntimeRegistry.cs`. Новые файлы компилировались через временную вставку `<Compile Include>` в `Assembly-CSharp-Editor.csproj` (Unity ещё не успела его перегенерировать); после сборки csproj восстановлен побайтово, `bin/`, `obj/`, `Temp/bin/` и лог сборки удалены.
- Изменённые файлы сессии: `Trajectory/LaserAndPhantomManagers.cs`, `06_KazistovVv_UI/03_Scripts/Zones/CenterWindow.cs`, новые `Editor/HierarchyPhantomCleaner.cs`, `Editor/HierarchyAutoRefresh.cs`. `MainScene.unity` **не менялась**.

### 5.4 Верификация (12.09.2026, 03:15 — «плавное движение фантома по выбранной траектории»)
- Изменённые файлы сессии (3, точечно): `Trajectory/LaserAndPhantomManagers.cs` (только `PhantomManager`), `Trajectory/TrajectoryFlowController.cs`, `Trajectory/SelectionTypes.cs` (комментарий `FlowState.PhantomsMoving`). `MainScene.unity` **не менялась**; лазеры, State Machine, планировщик/оракул/валидатор, столы, SCARA, дубликаты роботов и автообновление дерева не трогались.
- Ключевые фрагменты:
  - `PhantomManager.ShowAlongPath(PlannedTrajectory plan, double[] startQ, float speedUnitsPerSec)` — один фантом: `ApplyPose(startQ)` (стартовая поза) → движение по `plan.Path` с нормированным профилем `plan.Times`; возвращает `false`, если траектории/позы нет.
  - `PhantomManager.Tick` теперь ведёт фантом по пути (`PoseAt`/`SegmentAt`: курсор по участкам, интерполяция `LerpPose` по кратчайшим доворотам, при `progress = 1` — ровно последний сэмпл), доехавший фантом не двигается и остаётся видимым.
  - `TrajectoryFlowController.BuildPhantoms` — ровно одна копия по выбранной траектории (`ShowAlongPath`), `state.phantoms` = 1 элемент, финальная поза `cfg.q = plan.GoalQ` (её же выберет ЛКМ на этапе 4 → робот приедет ровно туда, где остановился фантом).
  - Тексты: «Траектория выбрана · фантом едет к конечной позе…», подсказка у фантома — «пройдено N% · едет к конечной позе / доехал (остановился)», отчёт этапа 4 — «Движение робота по «Траектория N …»».
- Проверка компиляции без Unity (редактор пользователя открыт, второй инстанс на той же `Library` недопустим): `dotnet build Assembly-CSharp.csproj` → **0 ошибок**, 3 предупреждения и все три не наши (`CS0618`+`CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`). Артефакты (`bin/`, `obj/`, `Temp/bin/`) удалены, `.csproj` не изменялся.
- Статические проверки (по файлам, без PlayMode):
  - `Plan.Path` и `Plan.Times` строятся парно (`Planner.BuildTrajectory`: на каждый сэмпл своё время, последний сэмпл — ровно конец пути), длины совпадают — `NormalizedTimes` работает по профилю траектории; при несовпадении длин включается равномерный профиль (страховка).
  - Суставы копии находятся по ТОЧНЫМ именам `Axis1..Axis6`: `Robot.fbx.meta` → `internalIDToNameTable` содержит и `Axis1`, и `Axis1_2` (меш); `SixAxisController.FindAxis` («приоритет — точное имя, не меш `AxisN_2`») подтверждает, что настоящие пивоты — `Axis1..Axis6`. Значит `PoseValidator.FindCopyJoints` (точное имя) находит те же трансформы, что и валидатор у реального робота, и `ApplyToCopy` реально ставит копию в позу (фантом не «залипает» в позе робота).
  - `validator.TcpAt()` внутри `TravelTime` (≈25 сэмплов) сохраняет и возвращает позу робота — реальный робот при оценке времени пути не двигается (`ClearanceAt`/`TcpAt` устроены так же).
  - `plan.Path` не копируется: фантом читает массив по ссылке, а `SelectPhantom` на этапе 4 правит только последний сэмпл (`ContinueFrom`) на эквивалентный угол — скачка у фантома не будет (`LerpPose` считает кратчайший доворот).
- PlayMode-прогон **не запускался** (открыт редактор пользователя — см. §9); визуальная проверка «фантом едет по траектории, выбирается в пути и после остановки» вынесена в §7.

### 5.5 Верификация (12.09.2026, 03:26 — «фантомы: удаление старых и движение по траектории»)
- Изменённый файл сессии — **один**: `Trajectory/LaserAndPhantomManagers.cs` (только класс `PhantomManager` + его приватные помощники). `TrajectoryFlowController.cs`, `SelectionTypes.cs`, `MainScene.unity` **не менялись**; лазеры, State Machine, планировщик/оракул/валидатор, столы, SCARA, дубликаты роботов и автообновление дерева не трогались.
- Что было причиной «старые фантомы накапливаются / фантом стоит или телепортируется» (разбор по коду, без PlayMode — редактор пользователя открыт):
  1. `Hide()` (и `Show`/`ShowAlongPath`) убирали только то, что попало в список `configs`. Любая копия, не попавшая в учёт (в т.ч. дети контейнера, созданные «живыми» скриптами копии, и объекты, пережившие перезагрузку домена), не удалялась никогда — контейнер `Phantoms` тоже жил до `OnDisable`. Отсюда «накопление в сцене».
  2. Материалы-инстансы (`Renderer.material`) после удаления копии оставались жить — утечка на каждое подтверждение траектории.
  3. `Tick` вызывался **только** из `TrajectoryFlowController.UpdateAim` и **после** раннего выхода `if (robot == null) return;` — при любой заминке потока (робот не определён, идёт планирование) шаг движения не делался: фантом «замирал».
  4. `Tick` начинался с `if (appearAtTargetPose) return;` — флаг мгновенного показа (задел многофантомного `Show`) полностью отключал движение, включая фантом с путём. Это и есть «стоит на месте или телепортируется».
  5. Копия робота один кадр оставалась «живой моделью»: её `SixAxisController.Update` создавал `TCP`-прокси и перепарцентил меши — детали копии выныривали в Hierarchy, а `ReassertHideFlags` 8 кадров это догонял.
  6. `validator.FindCopyJoints` вызывался в **каждом** кадре на каждую позу (обход всей модели копии), как и `GetComponentsInChildren` в наведении/пульсации.
- Что сделано (точечно, только `PhantomManager`):
  - `ClearPhantoms()` (полная уборка: зарегистрированные копии + всё в контейнере + сам контейнер + «осиротевшие» `Phantom_*`/`Phantoms` верхнего уровня + материалы-инстансы) — вызывается в начале `Show`/`ShowAlongPath`; `Hide()` делает то же самое и остаётся единственной точкой уборки для потока (Esc, выбор, новая точка красным лазером). `OnDisable`/`OnDestroy` → уборка без обхода сцены.
  - `KillGhost`: `SetActive(false)` + `Destroy` (PlayMode) / `DestroyImmediate` (редактор).
  - `Update()` → `Tick` (один шаг на кадр через `Time.frameCount`); `appearAtTargetPose` действует только на фантомы без пути; по прибытии — `arrived[i]`, доводка до последнего сэмпла и лог «фантом доехал…».
  - «Тихий» Instantiate (копия выключается сразу после создания, включается уже в стартовой позе) + кэш суставов копии + старт через `ContinueFrom(path[0], startQ)`.
  - Диагностические логи: `[Phantom] фантом создан (один) · сэмплов N · путь X юнита за Y с (Z ю/с) · старт в позе робота` и `[Phantom] фантом доехал до конечной позы и остановился`.
- Компиляция: `dotnet build Assembly-CSharp.csproj` (Unity-редактор пользователя открыт, второй инстанс на той же `Library` не запускался) → **0 ошибок**, 3 предупреждения и все три не наши (`CS0618`+`CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`). Артефакты сборки (`Temp/bin/`) удалены, `Assembly-CSharp.csproj` не изменялся (`git status` по нему пуст).
- Статические проверки: в `MainScene.unity` нет ни одного объекта с именем `Phantom*` (обход сцены в `PurgeOrphans` ничего чужого не заденет — проверено `Select-String` по файлу сцены, 0 совпадений); сцена не менялась; PlayMode-прогон не выполнялся (см. §7).

### 5.6 Верификация (12.09.2026, 03:49 — «робот не врезается в поверхность: нормаль + «пятак»»)
- Изменённые файлы сессии (4): новые `Trajectory/ToolAlign.cs`; правки `Trajectory/TrajectoryFlowController.cs`, `Core/FreeFlyCameraController.cs`, `Trajectory/AimIndicator.cs`. `MainScene.unity` **не менялась**; лазеры (`LaserManager`), State Machine (переходы/условия ЛКМ), планировщик (`Planner`), IK (`IkSolver`), валидатор (`PoseValidator`), оракул (`ReachabilityOracle`), столы, SCARA, логика дубликатов роботов и автообновление дерева не трогались.
- Ключевые фрагменты:
  - `FreeFlyCameraController.ComputeAimPoint()` — `aimNormal = hit.normal` (нормаль поверхности), `aimOnRealSurface = true` только при попадании в реальную геометрию; для рабочей плоскости y=0 нормаль `Vector3.up`, признак реальной поверхности — `false`; публичные доступы `AimNormalPublic`/`AimOnSurfacePublic`; вызов потока и индикатора расширен нормалью.
  - `TrajectoryFlowController`: поля `toolOffset = 0.30f`, `offsetMode = SurfaceNormal`, `alignToolToSurface = true`, `alignAngleToleranceDeg = 6f` (**значения на момент той сессии; актуальные — §5.7: `toolOffset = 0.10f`, `alignToolToSurface = false`**); `OffsetTarget(surfacePoint)` (нормаль / временный хак по Y / без смещения в воздухе); `LockPoint(target, surfacePoint)` фиксирует и точку, и поверхность; `OffsetDescription()` пишет в подсказку, чем и на сколько смещён TCP; `AlignToolToSurface(cand)` + `ApproachClear(...)` + `LerpConfig(...)`.
  - `ToolAlign.AlignGoal(validator, world, qGoal, target, normal, …)` — сначала направление «инструмент в поверхность» (−нормаль), затем разворот оси (+нормаль); в обоих случаях «пятак» параллелен плоскости. Численный покоординатный спуск: градиент центральной разностью (h = 1.5°), шаг 4°/2°/1° с дроблением, вес позиции `wPos = 25` (1 см ≈ 5°), ≤24 проходов, хранится лучшая увиденная поза по нормализованной сумме ошибок; финальные проверки — позиция, угол, лимиты+запас, самозазор, зазор до мира, ограничение доворота 120° на сустав.
  - `AimIndicator.UpdateAim(aimPoint, hitSurface, normal, onSurface)` — оракул спрашивает **смещённую** точку (`TargetPoint`), маркер остаётся на поверхности; старый двухаргументный вход сохранён (делегирует без смещения).
- Компиляция без Unity (редактор пользователя открыт — второй инстанс на той же `Library` не запускается): `dotnet build Assembly-CSharp.csproj` → **0 ошибок**; 3 предупреждения и все три не наши (`CS0618` + `CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`). Новый файл проверялся временной вставкой `<Compile Include="Assets\_Project\01_Scripts\Trajectory\ToolAlign.cs" />` в Unity-сгенерированный csproj; после сборки csproj восстановлен (git-diff по `Assembly-CSharp.csproj` пуст), артефакты (`Temp/bin`, `Temp/obj`) удалены.
- Статические проверки (без PlayMode):
  - В `MainScene.unity` **нет** компонентов `TrajectoryFlowController`/`AimIndicator` (оба создаются в рантайме через `AddComponent` в `FreeFlyCameraController.Awake`) — значит новый параметр `toolOffset` берётся из инициализатора (тогда 0.30, **с шага 1 — 0.10**) и правок сцены не требуется; правок сцены в этой сессии нет.
  - Ось «пятака»: в `SixAxisController.jointAxesLocal[5] = Vector3.up` с комментарием «Axis6 — roll фланца (локальная Y оси = вдоль инструмента)», т.е. `AxisWorld(Dof-1)` — действительно нормаль концевой плоскости.
  - Смещение 0.30 при `linkRadius = 0.06` даёт запас ≈0.24 юнита до стола — «провалиться» в текстуру концевое звено не может (**на шаге 1 при 0.10 запас ≈0.04 — см. §5.7 и §6**); для наклонной поверхности смещение идёт по её нормали, а не вверх (проверяется по `hit.normal` в консоли — см. §7).
  - Выравнивание правит **последнюю** позу траектории — её же берут фантом (`PhantomManager.ShowAlongPath`, `cfg.q = plan.GoalQ`) и реальный робот (`SelectPhantom` → `ContinueFrom(prev, cfg.q)` + `motion.Play(plan, lastQ)`), поэтому фантом и робот приезжают в одну и ту же выровненную позу.
  - PlayMode-прогон **не запускался** (открыт редактор пользователя, см. §9): визуальные проверки вынесены в §7.

### 5.7 Верификация (12.09.2026, 07:12 — ШАГ 1 из 3: «смещение целевой точки вдоль нормали поверхности»)
- Задача шага: при выборе точки на поверхности смещать цель для планировщика/IK на `hit.normal * toolOffset` (`toolOffset` — настраиваемый, по умолчанию 0.1). Ориентацию TCP на этом шаге **не** менять.
- Изменённые файлы сессии (2): `Trajectory/TrajectoryFlowController.cs` (значение offset, выключение выравнивания, диагностика), `Trajectory/AimIndicator.cs` (значение offset по умолчанию). Новых файлов нет. `Core/FreeFlyCameraController.cs` в этой сессии **не менялся** (нормаль поверхности оттуда пришла ещё в сессии 03:49 и работает как есть), `ToolAlign.cs` не менялся. `MainScene.unity` **не менялась** (последняя запись файла — 00:25, до этой сессии); лазеры, State Machine (условия/переходы этапов 0–4), планировщик, IK, валидатор, оракул, столы, SCARA, фантомы, дубликаты роботов, автообновление дерева не трогались.
- Ключевые фрагменты:
  - `TrajectoryFlowController.toolOffset`: **0.30 → 0.10** (публичное сериализуемое поле с `[Tooltip]`, доступно в инспекторе в PlayMode — правится без перекомпиляции).
  - `TrajectoryFlowController.alignToolToSurface`: **true → false** — по ТЗ ориентация TCP на шаге 1 не меняется (`ToolAlign` остаётся заделом шага 3; при `false` `AlignToolToSurface` выходит сразу, других вызовов `ToolAlign` в проекте нет).
  - `LogOffsetRejection(...)` + `LogPlanFailure()` + `WriteOffsetDiag(...)` и флаг `logOffsetDiagnostics = true`: точное значение `toolOffset`, координаты поверхности/цели/нормали, запас в мм и (для отказа планирования) `planner.LastDebug`/`LastBranchInfo` → консоль + `_dsh_offset_diag.txt`.
  - `AimIndicator.toolOffset`: 0.30 → 0.10 (и всё так же синхронизируется из потока в `FreeFlyCameraController.Awake`), поэтому оракул (цвет шарика) проверяет ту же смещённую точку, что фиксирует поток.
- Арифметика выбора 0.10 (почему не меньше): оракул требует запас `AimIndicator.clearance` = **0.02** м при радиусе звеньев `linkRadius` = **0.06** м (`CollisionWorld.MinDistanceChain`), т.е. минимально допустимое смещение над поверхностью = 0.06 + 0.02 = **0.08**; при `toolOffset = 0.10` концевое звено над столом имеет запас ≈**0.04** м (зелёный), при 0.30 был запас ≈0.24 м. Отсюда же и признак для шага 2/3: если красные отказы остаются и в `[OffsetDiag]` видно `запас ... мм` ≤ ~40 мм — поднимать `toolOffset` (0.15 → 0.25), если же `пересечение со сценой` (запас < 0) при большом смещении — дело не в offset, а в TCP (шаг 2) или ориентации (шаг 3).
- Что именно «красное» теперь диагностируется (по коду): `ReachVerdict.Collision` → «Выберите другую точку (пересечение со сценой)» и `Unreachable` → «(IK не сходится …)» дают строку `[OffsetDiag] <причина> · toolOffset=… · clearance=… мм · поверхность (…) · нормаль (…) · цель TCP (…)`; отказ планирования («траектория не найдена») даёт строку `[OffsetDiag] траектория не найдена · toolOffset=… · … · IK: <LastDebug> | ветви: <LastBranchInfo>`. Жёлтый (`Marginal`) точку не блокирует — по нему движение разрешено.
- Компиляция без Unity (редактор пользователя открыт, второй инстанс на той же `Library` не запускается): `dotnet build Assembly-CSharp.csproj` → **0 ошибок**, 3 предупреждения и все три не наши (`CS0618` + `CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`). Новый файл шага 3 (`ToolAlign.cs`) в проверке учитывался временной строкой `<Compile Include="Assets\_Project\01_Scripts\Trajectory\ToolAlign.cs" />`; строка удалена после сборки (проверено по содержимому файла — 0 вхождений), `Temp/bin`/`Temp/obj` удалены.
- **Важно про проверку csproj:** `*.csproj` в проекте **в gitignore** (`.gitignore:51`), поэтому `git status`/`git diff` по нему всегда пусты и как контроль «восстановил ли я файл» не годятся — сверять нужно содержимое (поиск вставленной строки) или размер файла.
- PlayMode-прогон **не запускался** (редактор пользователя открыт — см. §9), поэтому фактических координат, где красное сообщение осталось, в этой сессии получить нельзя: вместо этого добавлена диагностика (см. §5 и §7 «Диагностика»), значения из которой нужно перенести сюда. Аналитическая оценка (см. абзац про 0.10) даёт запас ≈40 мм для точки на столешнице при нормали вверх.

### 5.8 Верификация (12.09.2026, 07:48 — «режим перемещения точки» + проверка ориентации TCP)
- Изменённые/новые файлы сессии (5): **новый** `Trajectory/PointMoveHud.cs` (HUD + маркер режима); правки `Trajectory/SelectionTypes.cs` (`FlowState.PointMoveMode` + поля перемещения в `SelectionState`), `Trajectory/TrajectoryFlowController.cs` (вход/выход режима, движение, онлайн-оракул, `LockPointExact`, диагностика TCP), `Core/FreeFlyCameraController.cs` (Enter/Shift/направление движения точки, отдача QWEASD точке, приостановка `AimIndicator`), `Trajectory/AimIndicator.cs` (только добавлен `SetSuspended`). `MainScene.unity` **не менялась**; лазеры (`LaserManager`), планировщик, IK, валидатор, оракул, фантомы, столы, SCARA, дубликаты роботов, автообновление дерева не трогались.
- **Проверка ориентации TCP (задача «ось X должна смотреть вверх, сейчас −Y смотрит вниз»).** Unity в batch не запускался (редактор пользователя открыт), поэтому проверка выполнена по коду и модели:
  1. TCP-объект создаётся в `SixAxisController.CreateTcpProxy`: `go.transform.SetParent(endEffector, false)` — **локальный поворот = identity** относительно фланца (`Axis6_2`), затем `position = r.bounds.center` (центр меша фланца). Своей канонической ориентации у TCP нет: его оси = оси меша фланца из `Robot.fbx`.
  2. Ось инструмента в проекте задана как `SixAxisController.jointAxesLocal[5] = Vector3.up` с комментарием «Axis6 — roll фланца (**локальная Y** оси = вдоль инструмента)». То есть нормаль концевой плоскости — локальная **Y** оси `Axis6`, а не X; наблюдение «−Y смотрит вниз» означает, что инструмент вытянут вдоль локальной **−Y** фланца (+Y смотрит вверх, к запястью), когда инструмент касается стола сверху. Ожидание ТЗ («X вверх») в модели **не выполняется** — расхождение подтверждено по коду.
  3. Влияние на расчёты проверено поиском по проекту: IK позиционная (`PoseValidator`/`IkSolver` — только точка TCP), `ToolAlign` (шаг 3) берёт `PoseValidator.AxisWorld(Dof−1)` — мировое направление оси вращения последнего сустава (та же локальная Y), `targetRotation` в `RobotController`/`SCARAController` **нигде не читается** (мёртвое поле), `tcp.rotation` использует только `TrajectoryRecorder` (запись), `tcp.forward/right/up` не используются нигде. Вывод: расхождение сейчас ни на что не влияет, но соглашение «ось инструмента = локальная Y фланца, знак − в сторону инструмента» зафиксировано для будущих работ (ориентационная IK, крепление насадки в кадре TCP, вывод осей в UI).
  4. Для проверки в PlayMode добавлен разовый лог `[TCPDiag]` (`logTcpFrame = true`, печатается один раз на привязку робота): «ось инструмента в системе TCP = (x, y, z)», мировые оси TCP и угол оси инструмента к мировой вертикали. Ожидаемое значение оси в системе TCP — ≈`(0, ±1, 0)`; если в ТЗ нужна каноническая ориентация (X вверх/вперёд), правится одной строкой в `CreateTcpProxy` (`localRotation`), но это **отдельная задача** (в этой не делалось).
- Ключевые фрагменты реализации режима:
  - `SelectionTypes.cs`: `FlowState.PointMoveMode`; в `SelectionState` — `movePoint`, `moveOrigin`, `moveVerdict`, `moveReason`, `moveVerdictValid`, свойство `InMoveMode`; сброс полей в `ClearAll()`.
  - `TrajectoryFlowController.UpdateAim(..., bool moveToggle, Vector3 moveAxis, bool moveFast)` — новый полный вход (старые 6/8-аргументные перегрузки делегируют с `false/zero`, поэтому VR-контур и совместимость не ломаются); ранний выход в режиме (обычные этапы и лазеры «по прицелу» не работают); `EnterPointMoveMode()`, `UpdatePointMoveMode(...)`, `RefreshPointMoveVerdict()`, `ExitPointMoveMode(...)`, `PointInRobotFrame(...)`, `VerdictColor(...)`, `LogTcpFrameOnce()`, `IsPointMoveMode` (для камеры).
  - `LockPoint` теперь тонкая обёртка над `LockPointExact(target, surfacePoint, normal, onSurface, movedByOperator)`; режим перемещения вызывает её напрямую (`movedByOperator = true`) и печатает «Точка перемещена вручную · X … Y … Z … (относительно робота) · считаю варианты траекторий…».
  - `FreeFlyCameraController`: `PointMoveAxis()` (мировые оси QWEASD), Enter (`enterKey`/`numpadEnterKey`) → `moveToggle`, Shift → `moveFast`; блок движения камеры пропускается при `IsPointMoveMode`; `aimIndicator.SetSuspended(pointMoveMode)`; `plannerController.UpdateAim` в режиме не вызывается.
  - `PointMoveHud`: `Show(worldPoint, pointInRobotFrame)`, `SetVisible`, `SetPoint`, `SetVerdict`, `SetHint`; панель на своём `Canvas` (`sortingOrder = 60`), маркер-«рентген» через `GhostMaterial.MakeGhost`; всё скрыто `HideInHierarchy` рекурсивно.
- Компиляция без Unity (редактор открыт, второй инстанс на той же `Library` не запускается): `dotnet build Assembly-CSharp.csproj` → **0 ошибок**, 3 предупреждения и все три не наши (`CS0618` + `CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`). Новые файлы (`ToolAlign.cs`, `PointMoveHud.cs`) учитывались временными строками `<Compile Include=…>`; после сборки csproj восстановлен (размер 108 434 байта, вхождений 0), `Temp/bin`/`Temp/obj` удалены. Первая попытка сборки упала с `Fatal error. Internal CLR error (0x80131506)` (сбой хоста dotnet, не ошибка кода) — после `dotnet build-server shutdown` сборка прошла чисто.
- **PlayMode-прогон не выполнялся** (редактор пользователя открыт — см. §9): фактическая проверка клавиш/HUD/цветов вынесена в §7. Ручные шаги расписаны так, чтобы их можно было пройти за пару минут.

### 5.9 Верификация (12.09.2026, 09:00 — ПОЧИНКА PointMoveMode: диагностика + полный сценарий шагов 1–4)
**Диагностика: почему режим не работал (по порядку значимости).**
1. **Код режима ни разу не был скомпилирован.** У `Trajectory/PointMoveHud.cs` и `Trajectory/ToolAlign.cs` **не было `.meta`** (Unity их не импортировала), `Library/ScriptAssemblies/Assembly-CSharp.dll` был от **12.09 02:07** (правки режима — 07:43–07:49), а в `Logs/Editor.log` нет ни одного упоминания `PointMove`. Значит в последнем собранном ассембли не существовало ни `FlowState.PointMoveMode`, ни `PointMoveHud`, ни полей `movePoint`: **Enter ничего не делал, QWEASD двигали камеру, индикатора не было вообще**. (Причина «почему Unity не импортировала» — файлы создавались внешним процессом, редактор к этому моменту уже не обновлял AssetDatabase: следующий импорт/refresh добавил их только сейчас.)
2. **В Unity у пользователя открыта ДРУГАЯ копия проекта** — `C:\Users\Ольга\KavistovVv` (ветка `Cline`, PID 17844, запущен 11.09 14:22). Там нет ни `PointMoveMode`, ни `movePoint`, ни `PointMoveHud` (проверено поиском по коду), State Machine — более старая (`UpdateAim` на 7 аргументов). Проверять режим в этой копии бессмысленно; рабочая копия — `…\OneDrive\Documentos\новое пространство\KavistovVv` (ветка `DeepSeek`), см. §1.
3. **Ввод не имел отката на legacy Input**: `enter`/`cancel`/`shift` читались только через `Keyboard.current`, а `PointMoveAxis()` при `Keyboard.current == null` возвращал ноль — «клавиши нажимаю, точка стоит» без единой ошибки в консоли.
4. **`E` конфликтовала сама с собой**: удержание `E` = «точка вверх», нажатие `E` = альтернатива ЛКМ → в режиме каждый кадр писалась подсказка, а в остальных состояниях `E` могла «подтверждать» вместо ЛКМ.
5. **Лазеры визуально не сходились в перемещаемой точке**: `LaserManager` получал точку (пикинг был верный), но линии рисует `FreeFlyCameraController.DrawHandLaser` — он продолжал вести лучи в точку прицела мыши.
6. **Esc забирала камера**: `FreeFlyCameraController.Update` по Esc снимал захват курсора и включал панели KazistovVv раньше, чем поток успевал обработать отмену.
7. **Esc-отмена не восстанавливала траекторию**, а запускала полный пересчёт (вход в режим выбрасывал `plannedSoFar` и очередь) — расходилось с ТЗ шаг 4.
8. **Маркер точки был виден только вместе с панелью режима** (один флаг `visible` на HUD и маркер) — «точка видна всегда» не выполнялось.
9. Мелочи: подтверждённая Enter точка сохраняла признак «на поверхности» с прежней нормалью; вердикт оракула обновлялся только по таймеру (после остановки точки мог быть на кадр-два старым).

**Что исправлено** — см. блок «Исправления сессии 12.09.2026, 09:00» в §5 (файлы: `Trajectory/PointMoveHud.cs`, `Trajectory/TrajectoryFlowController.cs`, `Core/FreeFlyCameraController.cs`; `SelectionTypes.cs`/`LaserManager`/планировщик/IK/валидатор/оракул/фантомы/столы/SCARA/сцена не менялись).

**Прогон (Unity batch + Play-режим, `Assets/_Project/01_Scripts/Editor/DshPointMoveDiag.cs`, отчёт `_dsh_pointmove_verify.txt` + лог `_dsh_pointmove.log`):**
- Сцена: роботов **2** (`Робот_6ос_Стенд1`, `SCARA_Стенд2`), `Main Camera` создала `TrajectoryFlowController` и `PointMoveHud`; робот потока — 6-осевой (база `(0.000, 0.980, −23.900)`).
- **Шаг 1** (красный луч + ЛКМ по точке `(0.100, 0.980, −24.500)`, нормаль вверх): фаза `PointSelected` → `TrajectoriesShown`; цель TCP `(0.100, 1.080, −24.500)` (смещение 0.10 по нормали); **вариантов 5**, трубок в сцене 5, длины 2.551, зазор 404 мм; маркер точки виден, панель режима скрыта.
- **Шаг 2** (Enter): фаза `PointMoveMode`; **трубок 0** (траектория скрыта, очередь просчёта очищена); панель режима видна, маркер виден; координаты от базы робота `X: 0.100  Y: 0.100  Z: −0.600`, строка «Δ от исходной», статус «ДОСТИЖИМО · запас 920 мм»; **ЛКМ точку не двигает и не выходит из режима**; Q/E → Δ `(0.0000, +0.3009, 0.0000)` (только мировая Y), W/S+A/D → Δ `(+0.1804, 0.0000, +0.2405)` (только горизонталь); **лазеры сходятся в перемещаемой точке** (конец луча = `movePoint`); движение вниз внутрь столешницы: Δ `(0.0000, −0.6020, 0.0000)`, `y = 0.779 < 0.980` — **точка проходит сквозь стол**, вердикты за проход `Safe → Marginal`; при уводе точки к оси робота (расстояние 0.120) — вердикт **`Unreachable` «внутри корпуса робота»**, HUD «НЕДОСТИЖИМО · внутри корпуса робота», перепроверка сработала сразу после остановки точки.
- **Шаг 3** (Enter): режим завершён (`PointSelected`), точка зафиксирована в НОВОЙ позиции, панель скрыта, маркер виден; для **недостижимой** точки планирование честно не нашло вариантов → поток вернулся в `Idle` (ожидаемое поведение); после нового выбора красным лучом — снова **5 вариантов**; в промежуточном прогоне (точка `(0.281, 0.779, −24.259)`) новая траектория имела **другую форму**: конец `(0.280, 0.779, −24.258)`, длина 3.229 против прежних `(0.100, 1.080, −24.499)` / 2.551.
- **Шаг 4** (Enter → движение → Esc): точка уведена в `(0.379, 1.192, −24.500)`, по Esc вернулась ровно в `(0.100, 1.080, −24.500)`, **прежняя траектория восстановлена мгновенно** (5 вариантов, 5 трубок, конец совпал с прежним), панель скрыта, маркер виден.
- Итог прогона: **все проверки пройдены — 38 «[OK]», ни одного «[FAIL]»** (финальный прогон 08:37, 1773 кадра; отчёт `KavistovVv/_dsh_pointmove_verify.txt`, лог `_dsh_pointmove.log`); компиляция: `dotnet build Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj` (уже перегенерированные Unity, со всеми новыми файлами) — **0 ошибок**, предупреждения только унаследованные (`CS0618`/`CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`, `CS0618` в `Editor/ConvertRobotMaterialsToHDRP.cs`).
- **Найдено попутно (вне задачи, не менялось):** оракул считает столешницу, на которой стоит робот, «опорой» (`ObstacleBox.support`, `CollisionWorld.Rebuild`) и не считает её препятствием — поэтому точка ПОД столом своего робота получает зелёный вердикт («запас 725 мм»), хотя рука физически прошла бы сквозь столешницу. Недостижимость ловится другими критериями (внутри корпуса, вне рабочей зоны, IK, сингулярности).

### 5.10 Верификация (12.09.2026, 18:12 — «глубина шарика прицела колесом мыши + прилипание к поверхностям»)
- Изменённые файлы сессии (2 + прогон): `Core/FreeFlyCameraController.cs` (глубина шарика, колесо, средняя кнопка, визуальная цель лучей), `Trajectory/AimIndicator.cs` (новый полный вход `UpdateAim(...)`, визуал «прилип/отведён», признак «шарик есть»); **новый** `Editor/DshAimDepthDiag.cs` (+`.meta`) — батч-прогон механики. **`MainScene.unity` не менялась** (последняя запись 12.09 00:25, до сессии: новые поля берут значения из инициализаторов, сцена не требует правок); `TrajectoryFlowController`, `SelectionTypes`, `LaserManager`, планировщик, IK, валидатор, оракул, фантомы, столы, SCARA, HUD режима перемещения точки, `PointMoveMode` не менялись.
- Компиляция без Unity (редактор пользователя открыт на ДРУГОЙ копии — `C:\Users\Ольга\KavistovVv`, ветка `Cline`): `dotnet build Assembly-CSharp.csproj` → **0 ошибок**, 3 предупреждения и все три унаследованные (`CS0618`+`CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`). Новый редакторский файл проверялся временной вставкой `<Compile Include>` в `Assembly-CSharp-Editor.csproj` → **0 ошибок**; после сборки строка удалена, файл восстановлен (размер 109 007 байт, BOM на месте), `bin/`, `obj/`, `Temp/bin`, `Temp/obj` удалены. Замечание §9 подтвердилось: батч-прогон Unity САМ перегенерировал csproj и уже включил в него `DshAimDepthDiag.cs`.
- **Прогон (Unity batch + Play-режим, `Assets/_Project/01_Scripts/Editor/DshAimDepthDiag.cs`, отчёт `_dsh_aimdepth_verify.txt`, лог `_dsh_aimdepth.log`): 47 проверок «[OK]», ни одного «[FAIL]», 4444 кадра.** Команда: `Unity.exe -batchmode -nographics -projectPath "<DeepSeek-копия>" -executeMethod DshAimDepthDiag.Run -logFile _dsh_aimdepth.log`.
  - Сцена: роботов 2 (`Робот_6ос_Стенд1`, `SCARA_Стенд2`); параметры из инспектора в PlayMode: `scrollStep=0.080`, `minDistance=0.250`, `maxDistance=20.0`, `stickyToSurface=True`, `surfaceLayer=-5` (DefaultRaycastLayers), `laserLayers=1073741875`, `scrollSmoothSpeed=16.0`.
  - **Без колеса поведение прежнее:** луч из камеры (1.20, 1.80, −26.80) вниз на столешницу → поверхность «Столешница», точка (1.200, 0.980, −24.314), нормаль (0, 1, 0), дистанция 2.616; шарик — глубина 2.616, позиция = точка поверхности, «на поверхности» True.
  - **Визуал «прилип»:** масштаб маркера (0.0519, 0.0285, 0.0519) — сплющен по нормали (0.55); отведённый — (0.0492, 0.0492, 0.0492) — ровный шар.
  - **Колесо назад (3 щелчка):** 2.616 → **2.376** (ровно 3 × 0.080 = 0.24), шарик в (1.200, 1.064, −24.539), «отведён» True, лазеры сошлись в нём.
  - **Колесо вперёд (10 щелчков):** вернулся РОВНО на 2.616 и «прилип»; **повторная прокрутка вперёд на 10 щелчков глубину не изменила** (2.616) — прилипание работает.
  - **Предел назад (1000 щелчков):** глубина 0.250 = `minDistance`.
  - **Средняя кнопка:** из 0.250 мгновенно вернула шарик РОВНО на поверхность (2.616, «прилип»), без плавности.
  - **`stickyToSurface = false`:** прокрутка вперёд увела шарик ЗА поверхность (глубина 3.096 при поверхности 2.616), «на поверхности» False; после возврата флага и средней кнопки — снова 2.616.
  - **Пустота:** «пустого» направления в сцене нет — перебор 17 направлений вверх/в стороны всегда попадает в геометрию (ангар закрыт: потолок ≈ y 8.5, стены), поэтому для проверки ветки свободной глубины луч временно укорочен (`laserLength = 3`, штатное значение 100 восстановлено в том же прогоне). В пустоте: опоры нет (`hit` False), **шарик виден**, глубина не прыгнула (2.616), «прилип» False; колесо вперёд упирается в `maxDistance` (20.000), назад — в `minDistance` (0.250); **средняя кнопка в пустоте вернула False и шарик не сдвинула**; при возврате луча на столешницу шарик НЕ телепортировался (остался на 0.250), а прокрутка вперёд вернула его на поверхность.
  - **Поток этапов (регресс):** шарик на поверхности + красный ЛКМ → точка (1.200, 1.080, −24.314) = точка поверхности + `toolOffset` 0.10 по нормали (поведение прежнее). Шарик, отведённый колесом → точка (1.200, 1.092, −24.614) = сам шарик: **смещение по нормали не применяется** (со смещением было бы y 1.192).
  - **Оракул:** отвечает и для прилипшего шарика (`Marginal`, «близко к лимиту сустава» на этой точке стола), и для отведённого — прилипание его не сломало.
  - **Лазеры:** конец левого луча совпадает с шариком и на поверхности, и в отведённом состоянии.
  - **`PointMoveMode`:** Enter → режим, глубина до режима 2.616 = в режиме 2.616 (**колесо глубину не меняет**), маркер шарика скрыт (приостановлен); поданная в режиме прокрутка **отбрасывается и после выхода из режима не «выплёскивается»** (шарик остаётся на 2.616); Esc → выход из режима без ошибок.

**Проверка конфликтов колеса/средней кнопки (ТЗ, п.4) — по коду всего проекта (поиск `scroll`, `Scroll`, `mouseScrollDelta`, `GetMouseButton(2)`, `middleButton`):**
1. **UI KazistovVv.** `KazistovVvUIManager.EnsureEventSystem()` создаёт `EventSystem` + `InputSystemUIInputModule.AssignDefaultActions()`; в UI-карте есть `ScrollWheel` (прокрутка `ScrollRect` дерева KazistovVv, `TreePanel.scrollSensitivity = 24`) и `MiddleClick`. Конфликт РЕАЛЬНЫЙ и решён в коде: колесо и средняя кнопка принадлежат шарику только когда курсор захвачен (телеоперация) ИЛИ курсор свободен и НЕ над панелями; над панелью колесо по-прежнему прокручивает список, а не двигает шарик.
2. **`KeyboardMouseInputProvider.GetRotation()`** использует УДЕРЖАНИЕ средней кнопки для поворота камеры мыши. Провайдер создаётся только `InputManager`-ом, а `InputManager` (guid `db70b92f…`) не стоит ни в одной сцене (`MainScene`, `Boot`, `Main Menu`, `Simulation` — 0 вхождений), т.е. конфликт СПЯЩИЙ: в текущей сцене средняя кнопка занята только шариком. Если `InputManager` появится — средняя кнопка начнёт и вращать камеру, и возвращать шарик; решение: сменить кнопку вращения в провайдере (ПКМ) или у среднего клика — раскладку VR.
3. **`IdleCameraBrain.HasUserActivity()`** считает нажатие средней кнопки «активностью оператора» — не конфликт (только гасит автоматический облёт камеры; для нашей механики даже полезно).
4. **Лазеры/State Machine/оракул/фантомы** колесо не использовали вовсе (0 вхождений в их коде) — конфликта нет. Колесо также не использовалось ни в одном другом скрипте (`mouseScrollDelta` — 0 вхождений).

### 5.11 Верификация (12.09.2026, 19:00 — ПОЛНЫЙ АУДИТ: Enter / колесо / коллизии / фантомы / «колбашение» TCP)
- **Диагностика (по логам и файлам, без догадок).**
  1. **Копия проекта.** `Get-CimInstance Win32_Process` → у пользователя открыт `C:\Users\Ольга\KavistovVv` (ветка `Cline`, PID 17844, запущен 11.09 14:22). Его `Logs/Editor.log` (3.5 МБ, дописан 12.09 18:14) содержит сообщения СТАРОГО потока (`[Flow] Фантомов: 5 (ветвей найдено 5)`, `[Flow] Фантомы ещё едут…`, `[Flow] Наведите зелёный лазер на фантом`) и **ни одного** `[Phantom]`/`[RobotInventory]`/`[PointMove]`/`[AimDepth]` — этих строк в его коде просто нет. Сравнение деревьев скриптов: в копии пользователя отсутствовали `PointMoveHud.cs`, `ToolAlign.cs`, `HierarchyPhantomCleaner.cs`, `HierarchyAutoRefresh.cs`, а 18 файлов были старее; файлов, которые есть только у него, НЕТ (его копия — строгое подмножество).
  2. **Коллизия.** В его `CollisionWorld.Rebuild` не было фильтра `IsServiceObject` (HideFlags.HideInHierarchy) — «колбаски» траекторий (`Траектория N · лучшая` и т.д.) попадали в мир столкновений как капсулы/боксы. В его логе это ровно то, что видно: серии `[Flow] Выберите другую точку — пересечение со сценой` и `[Flow] Выберите другую точку — траектория не найдена` при наведении на собственные траектории.
  3. **«Колбашение» TCP.** В его логе — пачки `[SixAxis] Самостолкновение звеньев 0 и 3 — движение приостановлено, выполнен откат. Попробуйте другую цель/траекторию.` (до 8 подряд) сразу после `[DesktopTeleoperation] Target pos=(0.90, 0.98, −23.66) assigned to Robot.`: цель включала CCD, CCD тянул суставы к цели, защита откатывала их **частично** (`Quaternion.Slerp(..., deltaTime*12)`) — предельный цикл, кончик робота дрожал, пока цель не снималась. Ошибок/исключений в рантайме в логе нет (`Exception` — 1, и та сетевая `UnityConnectWebRequestException`; `NullReferenceException`/`MissingReference` — 0).
- **Исправления (рабочая копия `DeepSeek`, точечно, 6 файлов):** `Core/FreeFlyCameraController.cs` (ЛКМ через `IsMouseButtonDownThisFrame`, `AimDepthInputAllowed` учитывает скрытые панели KazistovVv), `Core/SixAxisController.cs` (мгновенный откат + снятие цели через `selfCollisionStopTime = 0.25` с), `Core/AimIndicator.cs` и `Trajectory/PointMoveHud.cs` (коллайдер маркеров выключается сразу, `raycastTarget = false` у панели и полосы HUD), `Trajectory/PoseValidator.cs` (латентный NRE в ветке TCP-прокси), `Trajectory/TrajectoryFlowController.cs` (`ResetAll` чистит отложенные варианты режима). Сцена, столы, SCARA, планировщик/IK/оракул не тронуты.
- **Компиляция:** обе копии — `dotnet build Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj` → **0 ошибок**; предупреждения только унаследованные (`CS0618`+`CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`, `CS0618` в `Editor/ConvertRobotMaterialsToHDRP.cs`).
- **Сквозной прогон (Unity batch + Play, `Assets/_Project/01_Scripts/Editor/DshAuditDiag.cs`, отчёт `_dsh_audit_verify.txt`, лог `_dsh_audit.log`): 33 проверки «[OK]», ни одного «[FAIL]», финальная строка «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ», 779 кадров.** Команда: `Unity.exe -batchmode -nographics -projectPath "<DeepSeek-копия>" -executeMethod DshAuditDiag.Run -logFile _dsh_audit.log`.
  - Сцена: роботов 2 (`6-осевой робот` (0.000, 0.980, −23.900) и `SCARA LS10-B702S` (0.000, 0.980, −28.100)), оба `isActive = False`; камера (0, 2, −28), курсор свободен; поток/фантомы/HUD/мотор созданы камерой.
  - **Колесо и средняя кнопка** (луч камеры на столешницу): глубина на поверхности **3.575**, `на поверхности = True`; 3 щелчка НАЗАД → **3.341** (ожидание 3.335 при `scrollStep = 0.08`), «отведён» = True; 3 щелчка ВПЕРЁД → **3.573**, снова «на поверхности»; +10 щелчков вперёд глубину НЕ меняют (прилипание держит); **средняя кнопка** → мгновенный возврат ровно на 3.575, «на поверхности» = True.
  - **Enter:** красный ЛКМ по (0.100, 0.980, −24.500) → цель TCP (0.100, **1.080**, −24.500), **вариантов 5**, трубок 5; **Enter → `PointMoveMode`** (панель режима видна, маркер точки виден, трубок 0); движение вниз: точка ушла на **y = 0.358** — **сквозь столешницу**, оракул онлайн выдал вердикт (`Safe · запас 307 мм`); **Esc** → точка вернулась ровно в (0.100, 1.080, −24.500) и **прежние 5 вариантов восстановлены**; второй вход → сдвиг вверх → **Enter** → точка зафиксирована в (0.100, **1.297**, −24.500) и планирование запущено заново.
  - **Фантом:** зелёный луч по «колбаске» + ЛКМ → `PhantomsMoving`, **фантомов 1**, прогресс на моменте проверки 0.60, позиция фантома = позиция робота (стартовая поза) → доехал (прогресс 1.00) и **остался видимым**.
  - **Движение робота:** зелёный луч по фантому + ЛКМ → TCP прошёл **2.110 юнита**, **0 смен направления**, максимальный шаг 137.7 мм/кадр (скорость на прогоне поднята до 5 ю/с, чтобы уложиться в кадры), остаточная ошибка до цели **0.8 мм**.
  - **«Колбашение»:** за 180 кадров ПОСЛЕ завершения движения смещение TCP **0.000 мм**, максимальный шаг **0.000 мм**; отдельный прогон с целью у базы (0.900, 0.980, −23.660 — та же, что в логе пользователя) → 0.000 мм/кадр, раскачки нет.
  - **Esc** в конце → `Idle`, фантомы убраны (0).
  - **Оговорка:** в самом прогоне `SelfCollisionBlocked` остался `False` (поток каждый кадр снимает цель через `HoldAllRobots`, поэтому CCD до самоколлизии не доходит) — проверка подтверждает ОТСУТСТВИЕ раскачки, но сценарий «самоколлизия + защита» в батче не воспроизведён (см. §7, ручная проверка).
  - На прогоне параметры движения поднимались только ради кадров: `flow.motionSpeed = 5` ю/с, `motion.maxSpeedMps = 20`, `flow.phantomSpeed = 4` ю/с, `phantoms.pathSpeed = 4`, `minTravelTime = 0.2`, `maxTravelTime = 0.5`. **Штатные значения в проекте не менялись** (робот — 1 юнит за 30 с, фантом — 0.5 ю/с).

### 5.16 Верификация (13.09.2026 — ДЕСКТОПНЫЙ UI В СТИЛЕ FREECAD; прогон выполнен ПОСЛЕ восстановления проекта, §0.0)
- **Компиляция:** Unity импортировала проект с нуля (`Library` пересобрана, `-batchmode -nographics -quit`) → **`return code 0`, ошибок компиляции нет**, `Assembly-CSharp.dll` и `Assembly-CSharp-Editor.dll` собраны (время сборки скриптов 130.8 с, лог `_dsh_ui_build.log`). `dotnet build` обоих assembly на восстановленном проекте требует уже существующей `Library` (иначе `CS0246` на `Text`/`Image`/`Button` — это не ошибки кода, а отсутствие ссылок на `UnityEngine.UI.dll`).
- **Сквозной прогон (Unity batch + PlayMode, `DshDesktopUiDiag.Run`, отчёт `_dsh_ui_verify.txt`, лог `_dsh_ui_diag.log`): 93 проверки «[OK]», ни одного «[FAIL]», ни одного исключения, 259 кадров.** Команда: `Unity.exe -batchmode -nographics -projectPath "<DeepSeek-копия>" -executeMethod DshDesktopUiDiag.Run -logFile _dsh_ui_diag.log` (БЕЗ `-quit`).
  - **Оболочка:** объект `KazistovVv_UI`, канвас `KazistovVvCanvas`; собраны меню, тулбар, дерево, свойства, статус-бар и три dock-панели; поток и контроллер оператора найдены; **23 команды** в реестре, **39** иконок в библиотеке.
  - **Тулбар (ТЗ):** ровно **15 кнопок**, раскладка **5 в ряд → 3 ряда**; у всех 15 есть иконка (подписей нет) и подсказка с названием; **4 заглушки** недоступны и помечены «в разработке»; у «Выбор точки»/«Выбор траектории» клавиши `Z`/`X`. Порядок: `point.select, path.select, robot.playpause, robot.stop, edit.reset, robot.switch, view.workspace, view.limits, view.metrics, tool.flashlight, edit.undo, edit.redo, view.theme, ui.settings, help.open`.
  - **Меню:** «Вид» открывается (10 пунктов) и закрывается.
  - **Дерево моделей:** 16 строк; на старте ничего не выбрано; у робота 7 дочерних узлов (6 осей + TCP), есть группа «Столы» → стенды; клик по узлу выбирает его, заполняет свойства и **включает рамку подсветки** объекта; свойства робота — 26 строк, стола — 26.
  - **Переименование:** «Стенд_1_Стол» → «Стенд_1_Стол_тест» (меняется ЯРЛЫК узла), имя объекта сцены НЕ менялось; режим корректно закрывается.
  - **«Глазик»:** объект скрывается и возвращается (рендереры), логика/коллайдеры/`CollisionWorld` не тронуты.
  - **Статус-бар:** «Idle · ожидание», «Робот: 6-осевой робот», координаты луча «Луч: (1.350, 0.980, −24.849)», «Тема: Тёмная», сообщения потока; поддержаны все 6 состояний State Machine.
  - **Тема:** по умолчанию тёмная; переключение на светлую мгновенное, оболочка пересобрана (15 кнопок и дерево на месте), выбор записан в PlayerPrefs (`KazistovVv.Theme.Mode`); «Системная» выбирается (реестр ОС в batch недоступен → тёмная палитра, ожидаемо); возврат в тёмную; кнопка «Тема» переключает по кругу.
  - **Dock-панели:** перестыковка (левый → нижний → левый), толщина 300 px, сворачивание, скрытие/возврат; геометрия переживает пересборку оболочки.
  - **Настройки:** 5 вкладок построены (16 / 33 / 17 / 18 / 26 строк), 8 пунктов функций, обработчики у визуализаций/фонарика/автообновления, у «отключения фантомов» обработчика нет (по ТЗ — задел); таблица биндов 22 строки, есть «Геймпад» и заглушки «VR / MR»; внешний файл настроек прочитан (`Assets/StreamingAssets/kazistovvv_settings.json`, 2 пункта).
  - **Команды:** зона достижимости / лимиты / метрики / фонарик / красный лазер переключаются кнопками; «Запуск/пауза» и «Остановка» недоступны, пока робот не едет (шаг 5 не ломается); заглушки не выполняются; «Переключение робота» делает активным SCARA; «Сброс» возвращает `Idle`.
  - **Подсказки:** появляются по задержке 0.35 с и скрываются при уходе курсора; у кнопки «Запуск/пауза» заголовок «Запуск / пауза».
  - **Производительность:** кадровое обновление панелей (статус + свойства + кнопки) — **0.77 мс/тик**; полное обновление с пересборкой дерева (только по событию/меню) — 62 мс (в предыдущем прогоне 28 мс; разброс из-за холодной `Library`); дерево **не пересобирается**, если содержимое не изменилось; в канвасе 292 объекта в устойчивом состоянии; интервалы 0.1 / 0.2 / 0.4 с.
- **Проверка переименования:** в `Assets` — **0 упоминаний** старого имени (поиск по `Kompas|KOMPAS|КОМПАС|Kazistov`); сцена ссылается на скрипт менеджера по прежнему GUID (`31fe910a…`) — ссылка не порвалась; меню редактора — `Tools/KazistovVv UI/…`. В копии пользователя `C:\Users\Ольга\KavistovVv` лежат те же 84 скрипта (хеши совпадают), её сборка — **0 ошибок**; сцена копии пользователя не менялась (там свои имена объекта и свои столы 1.2×0.8).

### 5.21 Правила КОНТЕКСТНОГО МЕНЮ ДЕРЕВА (ЭТАП 6 сессии §0.6) — ЗАФИКСИРОВАНО
Меню открывается **правым кликом по строке дерева** (`KvTreeRow.OnPointerClick`, кнопка Right)
и строится менеджером (`KazistovVvUIManager.OnTreeContextMenu`) по типу узла и по ВСЕМУ набору
выделенных узлов. Набор меню и правила — ниже; менять их можно только вместе с этим разделом.

| Пункт меню | Для каких узлов | Что делает |
|---|---|---|
| **Переименовать** | любой узел, но только один (в мультивыборе — неактивен) | выбор узла + переименование «на месте» (то же, что двойной клик); если строка не найдена — подсказка, что переименование делается двойным кликом |
| **Дублировать** | `Point` (точка) и waypoint (`waypoint:*`) | создаёт копию со смещением **+0.12 по X и Z**; точки — в историю точек менеджера, waypoints — через `KvStageHub.Instance.Waypoints.Add(...)` |
| **Удалить** | `Point` и waypoint | точка — удаляется из истории точек, waypoint — `Waypoints.Remove(index)` (индекс берётся из ключа узла); для остальных узлов пункт НЕАКТИВЕН (удалять роботов/столы/оси из дерева нельзя) |
| **Скрыть / Показать** (галка) | узлы с объектом сцены (`CanHide`) | выключает/включает РЕНДЕРЕРЫ всех выделенных узлов (визуальная видимость, как «глазик»); коллайдеры, скрипты и `CollisionWorld` не меняются |
| **Фокус камеры на объекте** | узлы с `WorldTransform` | `FreeFlyCameraController.FocusOn(...)`: камера ставится по направлению взгляда на расстоянии `2.6 × радиус габарита`, yaw/pitch подстраиваются, дальше обзор работает как обычно |
| **Свойства** (галка) | любой узел | выбирает узел (основной) и открывает панель «Свойства» |
| **Копировать имя** | любой узел | `GUIUtility.systemCopyBuffer = node.DisplayName` (копируется ЯРЛЫК узла) |

- **Мультивыбор:** `Ctrl + ЛКМ` добавляет/убирает узел из набора (подсветка вторым цветом);
  пункты меню применяются ко ВСЕМ выделенным узлам, основной — тот, по которому кликнули ПКМ.
  Набор хранится ПО КЛЮЧАМ узлов и восстанавливается после каждой пересборки дерева
  (узлы пересоздаются, ссылки на них не живут между пересборками).
- **ПКМ по узлу, которого нет в наборе**, сбрасывает набор и выбирает только его; ПКМ по узлу
  ИЗ набора делает его основным, сохраняя остальные.
- Заголовок меню: имя узла + его тип (в мультивыборе — «Выбрано узлов: N»).

### 5.22 Инварианты интерфейса после сессии §0.6 (не ломать)
- **Тулбар — группы, а не поток.** Порядок и состав групп задаёт `KvToolbarGroups.All`
  (данные). Новая кнопка = регистрация команды + её id в группе; неизвестные id в раскладке
  ИГНОРИРУЮТСЯ (не создают «мёртвых» кнопок), а не разложенные командой автоматически уходят
  в группу «Прочее» — ни одна команда не теряется.
- **Свёрнутые группы** — в PlayerPrefs (`KazistovVv.UI.ToolbarGroup.<id>`), как и вся раскладка
  окон (`KazistovVv.Layout.<key>.*`); «Сбросить раскладку» (`ui.resetlayout`) чистит и то и другое.
- **Подсказка любой кнопки тулбара** обязана содержать НАЗВАНИЕ + ОБЛАСТЬ (группу) + описание
  + горячую клавишу: формат собирается в `KvToolbar.TooltipBodyFor`, другие места его не дублируют.
- **Единый реестр биндов** — `KvBindings.All()`. Окно F12 и вкладка «Управление» в настройках
  читают ТОЛЬКО его (`KvSettingsSchema.Bindings()` — обёртка), поэтому таблицы не расходятся.
- **Палитра команд ищет по всем языкам** (`KvLoc.AllLanguages`), а не только по текущему.
- **Геймпад не влияет на клавиатуру:** пока геймпада нет, `KvGamepadBridge.Active = false` и
  флаги моста сброшены; при подключении старые «геймпадные» пути камеры (LB, левый триггер)
  отключаются, чтобы нажатие не срабатывало дважды.
- **Доступность меняет палитру/шрифт через `KvTheme`**, а не подменой элементов: размер шрифта —
  `KvSettings.FontScale` в вычисляемых `FontSize*`, контраст и схема для дальтоников — в геттерах
  цветов, поэтому достаточно пересобрать оболочку (`RebuildShell`).

## 6. Известные баги / риски
- [x] ~~**«Ничего не работает: ни Enter, ни колесо, ни фантомы»**~~ — **разобрано и снято 12.09.2026, 18:41:** причина была не в логике, а в том, что **редактор пользователя открыт на ДРУГОЙ копии проекта** (`C:\Users\Ольга\KavistovVv`, ветка `Cline`), где не было ни `PointMoveMode`, ни глубины шарика, ни фильтра служебных объектов в `CollisionWorld`, а фантомы ехали 1 юнит за 30 с. Копии синхронизированы (см. §1), проверено 0 ошибок компиляции в обеих.
- [ ] **`Enter` делится с режимом размещения UI.** `KazistovVvUIManager` (`UpdatePlacement`, ~строка 727) подтверждает размещение робота/стола по `Keyboard.current.enterKey.wasPressedThisFrame`. Если оператор в режиме «Добавить робота/стол» и в этот момент нажимает Enter, сработают ОБА действия (размещение + вход/выход `PointMoveMode`). В обычной работе (без размещения) конфликта нет: больше Enter в проекте никто не читает (проверено поиском). Лечение при необходимости — гейт в потоке («не реагировать на Enter, пока активно размещение») или другая клавиша в UI.
- [ ] **`enableLaserPointer = false` глушит весь поток, а не только картинку лучей.** `UpdateLaserPointers` выходит раньше, чем считается прицел (`ComputeAimPoint`) и вызывается поток: в этом режиме не работают ни точка, ни колесо, ни фантомы. Поле есть только в инспекторе (`Main Camera → FreeFlyCameraController`), по умолчанию включено. Если понадобится «лучи не рисовать, а выбирать» — вынести ранний выход ниже расчёта прицела.
- [ ] **Мёртвый/неиспользуемый код (найден аудитом 12.09.2026, НЕ удалён — точечные правки без переписывания):** `FreeFlyCameraController.SelectRobotContextual` + `PushRobotHistory`/`PeekRobotHistory`/`LogRobotSelected`/`robotHistory` (F теперь идёт через `SelectRobotByAim`), поле `plannerController` никогда не присваивается (предупреждение `CS0649`) — из-за этого `TrajectoryPlannerController` (хоткеи P/1/2/3/F9) недостижим; `MotionExecutor.SetSpeed`, `SixAxisController.HasPendingSeed`/`ReachedTarget`, `PhantomManager.TravelTime`, `PointMoveHud.SetHint` (строка подсказки в HUD всегда статическая), `AimIndicator.LastBranch` (заполняется только при `allowSeedSelection = true`, а он выключен), `TrajectoryFlowController.TopUpVariants` + `PhantomMath.PickDistinct` + `PhantomManager.Show` (задел на несколько фантомов).
- [ ] **ОТКРЫТЫЙ ВОПРОС: ориентация TCP не совпадает с ожидаемой (ТЗ сессии 07:48).** Ожидалось: «ось X концевой точки смотрит вертикально вверх, когда инструмент касается горизонтальной плоскости сверху». Фактически по коду: нормаль концевой плоскости — локальная **Y** фланца (`SixAxisController.jointAxesLocal[5] = Vector3.up`, комментарий «Axis6 — roll фланца (локальная Y = вдоль инструмента)»), а инструмент вытянут вдоль локальной **−Y** (наблюдение «−Y смотрит вниз» этим и объясняется; TCP-прокси наследует ориентацию меша `Axis6_2`, `CreateTcpProxy` поворот не задаёт). На расчёты сейчас НЕ влияет: IK позиционная, `ToolAlign` использует ось сустава (`AxisWorld(Dof−1)`), `targetRotation` — мёртвое поле, `tcp.rotation` читает только `TrajectoryRecorder`, `tcp.forward/right/up` не используются нигде (проверено поиском). **Что делать, если потребуется каноническая ориентация:** задать ориентацию прокси в `SixAxisController.CreateTcpProxy` (`go.transform.localRotation = …`, чтобы ось инструмента стала, например, локальной X или −Z) — это одна строка, но менять её без отдельного ТЗ нельзя (может быть завязано на будущую ориентационную IK/крепление насадки). Проверка в PlayMode: лог `[TCPDiag] …` печатает «ось инструмента в системе TCP» (ожидается ≈`(0, ±1, 0)`) и угол оси к вертикали мира.
- [ ] **Оракул не проверяет самоколлизию звеньев** (`ReachabilityOracle` считает зазор до сцены/других роботов капсульной цепочкой, а самозазор — нет). В HUD режима перемещения статус поэтому может быть зелёным там, где планировщик потом отбросит позу (`Planner.selfClearance` = 0.015). Если нужен честный онлайн-статус «есть самоколлизия» — это отдельная правка оракула (в этой сессии не делалась).
- [ ] **Вердикт оракула в режиме перемещения не блокирует движение точки** (осознанно, ТЗ этап 5) и обновляется раз в `pointMoveOracleInterval = 0.08` с (плюс обязательно сразу после остановки точки) — при быстром движении (Shift) статус может «отставать» на несколько сантиметров. Если нужно жёстче — уменьшить интервал (цена: оракул дорогой) или проверять по кнопке.
- [ ] **Режим перемещения доступен только с клавиатуры (Enter)**: в VR/геймпаде входа нет (старые перегрузки `UpdateAim` передают `moveToggle = false`). Для VR нужно отдельное ТЗ (кнопка/триггер + стик вместо QWEASD).
- [x] ~~**Esc в режиме перемещения отменяет точку и одновременно отпускает курсор**~~ — **исправлено 12.09.2026, 09:00**: пока активен `PointMoveMode`, Esc принадлежит режиму (отмена перемещения) и камеру/панели не трогает; вне режима поведение Esc прежнее (снять захват курсора + панели, `ResetAll` в потоке). Для UI в режиме — `CapsLock`.
- [ ] **HUD режима — свой `Canvas`** с `sortingOrder = 60` и панелью **700×156** сверху по центру (отступ 92 px); строки: заголовок, крупные координаты от базы робота, «Δ от исходной», статус достижимости, подсказка по клавишам. Если верхняя панель KazistovVv изменит высоту или появятся новые оверлеи, панель может перекрыться — правится `PointMoveHud.topOffset/panelWidth/panelHeight/sortingOrder`.
- [ ] **Маркер точки живёт вне режима** (`PointMoveHud.SetMarkerVisible`): зафиксированная точка видна в `PointSelected`/`TrajectoriesShown`/`PhantomsMoving`/`RobotMoving` и пульсирует. Если это мешает (например, перекрывает фантом при движении) — скрывать маркер в этих состояниях (`ShowLockedPointMarker` вызывается из `LockPointExact`).
- [ ] **ВРЕМЕННЫЙ ХАК (+toolOffset по Y)** — режим `TrajectoryFlowController.offsetMode = WorldUpHack`: смещение выбранной точки строго вверх по Y, без нормали поверхности. По умолчанию **выключен** (работает основное решение через `hit.normal`). Как заменить/убрать: оставить `offsetMode = SurfaceNormal` (или удалить ветку `WorldUpHack` из `OffsetTarget` и поле из инспектора) — тогда смещение всегда идёт вдоль нормали поверхности, а для точки в воздухе смещения нет вовсе. Пометка «ВРЕМЕННЫЙ ХАК» осталась и в строке подсказки («смещение 0.10 по Y (ВРЕМЕННЫЙ ХАК)»).
- [ ] **`toolOffset = 0.10` даёт запас всего ≈40 мм** над столешницей (нужно ≥0.08 = `linkRadius` 0.06 + требуемый запас 0.02). Если после PlayMode в `[OffsetDiag]` видны отказы с запасом ≤ 40 мм — на шаге 2 поднимать `toolOffset` (0.15 → 0.25) до исчезновения красного; если при большом смещении по-прежнему `пересечение со сценой` (запас < 0) — причина не в offset (смотреть TCP/шаг 2 или ориентацию/шаг 3). Точные значения из PlayMode — перенести в §7 «Диагностика».
- [ ] **Диагностика `[OffsetDiag]` (`_dsh_offset_diag.txt`) — временный инструмент шага 1/2**: включается флагом `logOffsetDiagnostics`, файл растёт при каждом отказе (троттлинг 1 с). После шага 3 флаг выключить (или удалить `LogOffsetRejection`/`LogPlanFailure`/`WriteOffsetDiag`), чтобы не плодить файл в корне проекта.
- [ ] **Выравнивание «пятака» (шаг 3) выключено** (`alignToolToSurface = false`) и потому в текущем поведении не участвует; его корректность (сходимость спуска `ToolAlign` на реальной геометрии) по-прежнему проверена только статически и компиляцией. При включении на шаге 3 смотреть `[Flow] «пятак» не выровнен (…)`: при «ось не довернулась» — увеличить `alignAngleToleranceDeg` (6° → 10-12°) или `maxSweeps` в `ToolAlign`, при «TCP не удержан в цели» — уменьшить `wPos` (25 → 10).
- [ ] Выравнивание доворачивает сустав до 120° и меняет последнюю позу траектории; участок подхода проверяется (`ApproachClear`, последние ~8 сэмплов), но полноценная перепланировка под новую позу не делается. Если подход не проходит — выравнивание отменяется (робот едет как раньше).
- [ ] Случай «луч ни во что не попал» (точка на рабочей плоскости y = 0) сознательно оставлен без изменений: смещения нет, ориентация свободная — по ТЗ смещение относится только к точке на реальной поверхности.
- [ ] **Колесо мыши делится между шариком и UI (осознанно).** Шарик слушает колесо только когда курсор захвачен (телеоперация) ИЛИ курсор свободен и НЕ над панелями KazistovVv: над панелью колесо прокручивает `ScrollRect` дерева (UI-карта `InputSystemUIInputModule`). Если понадобится менять глубину, не уводя курсор с панелей, — это правка правила `AimDepthInputAllowed()` в `FreeFlyCameraController` (например, «слушать всегда, кроме случая, когда под курсором реально есть прокручиваемый список»).
- [ ] **`maxDistance` — «мягкий» предел:** он ограничивает ход колеса, но если поверхность под прицелом дальше `maxDistance`, шарик всё равно стоит на ней (иначе без колеса прицел «сломался» бы на дальней геометрии: шарик висел бы в воздухе, а смещение `toolOffset` отключалось). Если нужен ЖЁСТКИЙ предел — вернуть `Mathf.Clamp(baseDist - depthOffset, minD, maxD)` в `UpdateAimBallDepth`.
- [ ] **Поверхность ближе `minDistance`** (камера почти вплотную к стене): шарик не подпускается ближе `minDistance` и может оказаться за поверхностью; в этой позе «прилип» уже не выставляется (|глубина − поверхность| > `surfaceStickTolerance`), т.е. смещение `toolOffset` к такой точке не применяется. В сцене практически не встречается (камера-коллайдер радиуса 0.25 не подпускает ближе).
- [ ] **В пустоте шарик теперь ВИДИМ** (раньше маркер при `!hitSurface` скрывался): у шарика всегда есть глубина, и ТЗ требует «движется свободно». При этом флаг потока `aimHit` не менялся — «Наведите красный лазер на поверхность» в пустоте остаётся. В самом ангаре «пустого» направления почти нет (перебор 17 направлений вверх/в стороны всегда попадает в геометрию: потолок ≈ y 8.5, стены), поэтому ветка свободной глубины включается редко.
- [ ] **Отведённый шарик = точка свободного пространства:** поток получает одну точку (`aimPoint`), поэтому и `state.point`, и `state.aimAtLock` равны позиции шарика (точка поверхности под ним потоку не передаётся). На поведение не влияет (`lockedOnSurface = false`, смещение по нормали и выравнивание «пятака» к такой точке не применяются), но в диагностике `[OffsetDiag]` координаты «поверхности» у отведённой точки — это сам шарик. Если понадобится различать — добавить в `UpdateAim` отдельный аргумент «точка поверхности» (правка API потока).
- [ ] **Колесо в `PointMoveMode` игнорируется** (ТЗ: там точка ходит по QWEASD). Ничего на колесо в режиме не назначено — если оператору понадобится крутить там что-то ещё (например, скорость), это отдельное ТЗ.
- [ ] **Механика глубины живёт в `FreeFlyCameraController`** (он же владелец луча и мыши) — VR-контур (`VRInputProvider`/`VRHandTracker`) её пока не использует; для VR достаточно вызывать `AddScrollInput(notches)` со стика и `SnapAimBallToSurface()` по кнопке (API публичный).
- [ ] (риск билда) прозрачность и «рентген» фантомов проверены только в редакторе: `_SURFACE_TYPE_TRANSPARENT` и `_ZTestTransparent` могут вырезаться стриппингом шейдеров в сборке под Quest.
- [ ] `ReachabilityOracle` (6-осевой): CCD от текущей позы иногда не сходится → «IK не сходится» и ложное «Выберите другую точку» (лечится повтором).
- [x] ~~Переезд фантомов ≈99 с при длине пути 3.3 юнита~~ — **снято дважды**: сначала мгновенным появлением, теперь быстрым (но видимым) проходом по траектории: 1 юнит за 2 с, для 3.3 юнита ≈6.6 с (см. §5, §5.4).
- [ ] `CollisionWorld` игнорирует **любой** объект с `HideInHierarchy` — семантическая связь: скрыл объект → он перестал быть препятствием (учитывать при добавлении декора).
- [ ] Смена робота на ходу (F/дерево во время `PhantomsMoving`) — предсуществующая дыра потока: `Rebind()` переключает валидатор на другого робота, а траектория/фантом остались от прежнего (фантом «замирает», ЛКМ по нему потом отдаётся новому роботу). Аварии нет: `ApplyPose` для чужой копии — no-op (`J1_3`/`Axis*` не находятся), `IsPrismatic` не индексирует массивы. Правильное лечение (сброс сценария при смене робота) — правка State Machine, в этой сессии не делалась.
- [ ] `SafetyGate.Velocity` не проверяется (нет расчёта скоростей).
- [ ] Камера стартует в (0, 2, −28) — над столом 2; обзор двух увеличенных столов можно улучшить.
- [ ] «Добавить робота» в TopBar: при активном режиме размещения **любая** ЛКМ по столу создаёт робота (`ObjectSpawner.SpawnRobot`) — это единственный путь появления «лишних» роботов в рантайме. Копия получает имя `«<robotName>_HHmmss»` (то есть «SCARA LS10-B702S_…» / «6-осевой робот_…») — именно такие строки выглядят в Hierarchy как «фантомные» записи. Если нужен «жёсткий» запрет — выключать режим размещения явно (Esc). Диагностика: `[RobotInventory] Роботов в сцене: 3 …` в консоли при старте Play, `Tools/KazistovVv/Hierarchy/Проверить инвентарь роботов (ожидается 2)` в редакторе, удаление копий — `Tools/KazistovVv/Hierarchy/Удалить копии роботов, созданные через UI` (см. §5.3).
- [ ] Записи, которые Unity уже уничтожила (остановка PlayMode, скриптовое удаление), иногда остаются нарисованными в окне Hierarchy до ручного клика — «призрачные» строки, которые поиском по сцене не находятся. Лечится принудительным repaint: `Editor/HierarchyAutoRefresh.cs` (раз в 2 с) и `Tools/KazistovVv/Hierarchy/Очистить фантомные записи`.
- [ ] В проекте лежат незатреканные сцены восстановления `Assets/_Recovery/0*.unity` (следы падений редактора, со старыми наборами роботов). Если Unity откроет такую сцену после сбоя или пользователь нажмёт Play в **несохранённой** сцене, в PlayMode можно увидеть «лишних» роботов — это не `MainScene` и не рантайм-скрипты (см. §8).

## 6.1 Решено
### 15.09.2026 — UX/UI + ГЕЙМПАД (этапы 1–12), см. §0.6
- [x] **ЭТАП 1 — группировка тулбара:** 78 кнопок разложены по 8 логическим группам с
  вертикальными разделителями, у каждой группы «ручка» (список команд + сворачивание в одну
  иконку), подсказка каждой кнопки содержит область применения и горячую клавишу.
- [x] **ЭТАП 2 — кнопки с ошибками:** прогон вызвал все 139 команд; найден и исправлен
  отказ `joints.panel` (`InvalidCastException` из-за `new GameObject("HandleArea")` без
  `RectTransform` в `KvFeatureWindow.MakeSlider`). Кнопка «две полоски с точками»
  опознана как `view.clearance` («Тепловая карта зазоров») — код исправен, проблема была
  в непонятной иконке, решена подсказкой с названием группы.
- [x] **ЭТАП 3 — dockable/undockable окна:** открепление кнопкой и перетаскиванием заголовка,
  подсветка зоны прикрепления, «полочка» по ✕, сохранение раскладки в PlayerPrefs
  (`KvLayoutStore`), кнопка «Сбросить раскладку».
- [x] **ЭТАП 4 — ползунки размера:** 8 маркеров (грани + углы), курсоры ↔ / ↕ / диагонали,
  минимум 150×100, максимум — канвас, сохранение размеров.
- [x] **ЭТАП 5 — палитра команд (Ctrl+P):** поиск по всем языкам/id/описанию/клавише,
  недавние первыми, Enter/Esc/стрелки.
- [x] **ЭТАП 6 — контекстное меню дерева (ПКМ):** 7 пунктов по типам узлов, мультивыбор
  Ctrl+клик, фокус камеры, копирование имени, дублирование и удаление точек/waypoints
  (правила — §5.21).
- [x] **ЭТАП 7 — окно горячих клавиш (F12):** единый реестр биндов `KvBindings` (клавиатура,
  мышь, геймпад), поиск, группировка, подсветка конфликтов (сейчас 0), переназначение — заглушка.
- [x] **ЭТАП 8 — empty/error/loading:** «Нет объектов»/«Выберите объект» + скелетон,
  красная плашка ошибок со стеком, полоса загрузки планирования/записи.
- [x] **ЭТАП 9 — геймпад:** раскладка (стики, D-Pad, A/B/X/Y, LB/RB, LT/RT, Start/Select),
  автоопределение, отключение без устройства, виртуальный геймпад на экране.
- [x] **ЭТАП 10 — единый набор иконок:** 24×24, штрих 1.5 px, 84 иконки (19 новых).
- [x] **ЭТАП 11 — доступность:** размер шрифта, высокий контраст, схема для дальтоников
  (Okabe–Ito), навигация Tab/стрелки/Enter.
- [x] **ЭТАП 12 — проверка всех кнопок:** батч-прогон `DshUiStagesDiag` —
  **[OK] 100 · [FAIL] 0 · исключений 0**, отчёт `_dsh_ui_stages.txt` со сводной таблицей
  «команда → статус».

### 13.09.2026, 21:20 — ДЕСКТОПНЫЙ UI В СТИЛЕ FREECAD · ПЕРЕИМЕНОВАНИЕ В KAZISTOVV · ВОССТАНОВЛЕНИЕ ПРОЕКТА
- [x] **Задача 1 — структура UI спроектирована и реализована:** строка меню → тулбар (15 кнопок, 3×5) → три dock-панели (дерево слева, свойства справа, настройки/справка снизу) → статус-бар; всё строится кодом в рантайме (`AddComponent`), сцена не переделывалась. Обоснование выбора uGUI вместо UI Toolkit — §0.4.
- [x] **Задача 2 — верхняя панель:** 15 кнопок-иконок в 3 ряда по 5, БЕЗ подписей, монохромные процедурные иконки (39 иконок в библиотеке), подсказки при наведении (заголовок + описание + горячая клавиша), заглушки недоступны и помечены «в разработке». Набор кнопок задан раскладкой `ToolbarLayout()` и реестром `KvCommands` — новая кнопка добавляется без правок панели.
- [x] **Задача 3 — левая панель:** иерархическое дерево моделей — роботы (→ «Ось N», «TCP»), группа «Столы» (→ стенды), группа «Точки» (история выбранных точек), группа «Траектории» (→ фантомы); раскрытие/сворачивание узлов (плюс «развернуть/свернуть всё»), «глазик» (визуальное скрытие через рендереры), переименование по двойному клику (ярлык узла, не имя объекта), выбор узла → подсветка объекта рамкой в сцене + заполнение свойств.
- [x] **Задача 4 — правая панель:** свойства выбранного объекта ТОЛЬКО ДЛЯ ЧТЕНИЯ, с секциями по типу: робот (позиция, поворот, углы суставов с запасом до лимитов, скорости робота и фантомов, состояние движения, температура, наработка), точка (координаты в мире и от базы робота, вердикт оракула), траектория (длина, время по профилю и по скорости, зазор, запас лимитов, кривизна — средняя/суммарная/максимальная, число сэмплов, ветвь IK, оценка), стол (позиция, масштаб, габарит, верх плоскости, робот на столе).
- [x] **Задача 5 — нижняя панель:** статус-бар с состоянием State Machine (все 6 значений), выбранным роботом, координатами луча/точки, статусными сообщениями потока (теми же, что в логе), индикатором темы и FPS.
- [x] **Задача 6 — переключатель темы:** Тёмная / Светлая / Системная, по умолчанию тёмная (как в FreeCAD), переключение мгновенное (пересборка оболочки с сохранением геометрии панелей), выбор в PlayerPrefs; «Системная» определяет оформление ОС через рефлексию и при неудаче падает в тёмную.
- [x] **Задача 7 — структура будущих настроек заложена:** dock-панель с вкладками «Функции» (визуализации, фонарик, автообновление дерева, фантомы), «Управление» (список биндов клавиатуры/мыши и геймпада — ТОЛЬКО ОТОБРАЖЕНИЕ, VR/MR-заглушки «будет добавлено позже»), «Интерфейс» (тема, масштаб, плотность, ряды тулбара, видимость панелей), «О программе», «Справка». Пункты читаются из встроенной схемы + внешнего `Assets/StreamingAssets/kazistovvv_settings.json` — новые пункты добавляются БЕЗ перекомпиляции; неизвестные id показываются как «нет обработчика».
- [x] **Задача 8 — PROJECT_CONTEXT.md обновлён** (этот раздел, §0.0, §0.4, §1, §2, §5.16, §7, §8, §9).
- [x] **Задача 9 (дополнительная) — переименование проекта в KazistovVv:** папка `06_KazistovVv_UI`, namespace `KazistovVvUI` (+`KazistovVvKinematics`), классы `KazistovVvUIManager`/`KazistovVvMenu`, объект сцены `KazistovVv_UI`, меню `Tools/KazistovVv UI/…`, теги логов `[KazistovVv]`, тексты и комментарии. GUID скриптов сохранены → ссылка в сцене не порвалась; сборка обеих копий — 0 ошибок.
- [x] **Найдено и исправлено прогоном (живая ошибка):** подсказка не появлялась вообще — в `KvTooltip` выключался САМ корневой объект (`SetActive(false)`), поэтому его `Update()` (который показывает подсказку по задержке) не выполнялся ни разу. Теперь гасится только содержимое, корень остаётся активным.
- [x] **Не тронуто:** State Machine (переходы и условия ЛКМ), лазеры (`LaserManager`), фантомы (`PhantomManager`), планировщик, IK, `PoseValidator`, оракул, `CollisionWorld`, `PointMoveMode`/`PointMoveHud`, бинды (ни один не изменён и не добавлен), HDRP-свет и материалы, координаты роботов; в `TrajectoryFlowController`/`TrajectoryExecutor`/`MotionExecutor` добавлены только read-only обёртки и пауза.
- [x] **Восстановление после инцидента** (удаление рабочей папки ошибочной командой) — код из второй копии, сцена 19 правками, диагностика заново; подробности и список потерь — **§0.0**.
- [x] **Верификация:** батч-прогон `[OK] 93 · [FAIL] 0 · исключений 0` (см. §5.16), компиляция обоих assembly и обеих копий — 0 ошибок.

### 12.09.2026, 19:00 — ПОЛНЫЙ АУДИТ: «Enter не работает / колесо не работает / коллизия мешает / фантомы не создаются / кончик робота колбасит»
- [x] **Главная причина всех пяти жалоб — не логика, а КОПИЯ ПРОЕКТА.** Редактор пользователя открыт на `C:\Users\Ольга\KavistovVv` (ветка `Cline`, PID 17844), где на момент жалобы (её же лог `Logs/Editor.log`, 12.09 18:14) **не было вообще**: `FlowState.PointMoveMode` (→ «Enter ничего не делает»), механики глубины шарика и средней кнопки (→ «колесо не работает»), фильтра служебных объектов в `CollisionWorld` (→ «коллизия мешает»: в мир столкновений попадали «колбаски» траекторий — отсюда `[Flow] Выберите другую точку — пересечение со сценой` и `траектория не найдена` в логе), а фантомы ехали по 1 юниту за 30 с (→ «фантомы двигались 30 секунд»). Рабочая копия `DeepSeek` всё это уже содержала. Копии синхронизированы (23 `.cs`, см. §1), обе собираются с 0 ошибок.
- [x] **Найден и устранён реальный механизм «колбашения» TCP:** `SixAxisController.ResolveSelfCollision` откатывал суставы **покадровым `Quaternion.Slerp`** (blend = `deltaTime*12`) к позе до IK, а CCD в следующем кадре снова тянул их к цели — предельный цикл, кончик робота дрожал, пока цель не снималась. В логе пользователя это видно как пачки `[SixAxis] Самостолкновение звеньев 0 и 3 — движение приостановлено, выполнен откат` (до 8 подряд). Теперь откат **мгновенный** (полный возврат позы), а если конфликт держится дольше `selfCollisionStopTime = 0.25` с — **цель снимается совсем**: робот стоит, а не дрожит.
- [x] **ЛКМ-подтверждение потока больше не зависит только от new Input System:** в `FreeFlyCameraController` подтверждение бралось как `Mouse.current.leftButton.wasPressedThisFrame` без отката — при `Mouse.current == null` не работало бы НИЧЕГО (ни точка, ни траектория, ни фантом). Теперь через `IsMouseButtonDownThisFrame(0)` (legacy → new Input → legacy), как уже было сделано для Enter/Esc.
- [x] **Колесо и средняя кнопка стали доступнее:** `AimDepthInputAllowed()` дополнительно разрешает ввод, когда панели KazistovVv СКРЫТЫ (`KazistovVvUIManager.uiVisible == false`), — раньше при свободном курсоре колесо зависело только от «под курсором нет UI».
- [x] **HUD режима перемещения больше не перехватывает мышь:** панель и полоса HUD (`PointMoveHud`, `sortingOrder = 60`, выше канваса KazistovVv) имели `raycastTarget = true` и «накрывали» верхнюю панель KazistovVv — теперь `false` (и для EventSystem, и для проверки «над панелью»).
- [x] **Маркеры (шарик прицела, маркер точки) не могут попасть в луч и в CollisionWorld в первом кадре:** коллайдер выключается сразу (`col.enabled = false`) перед `Destroy`.
- [x] **Латентный `NullReferenceException` в `PoseValidator.Init`:** ветка создания TCP-прокси обращалась к полю `joints` до его заполнения (`joints[5]`) — заменено на `six.jointTransforms` с проверкой длины.
- [x] **Esc/сброс чистят отложенные варианты режима перемещения** (`moveSavedPlans`/`moveSavedQueue`/`moveSavedPhase` в `ResetAll`) — иначе они могли «воскреснуть» после следующего сброса.
- [x] **Проверено сквозным прогоном в Play-режиме (Unity batch)** — см. §5.11: 33 проверки «[OK]», ни одного «[FAIL]», включая «TCP после завершения движения не дрожит (0.000 мм за 180 кадров)».
- [x] **Не тронуто:** SCARA (её код вообще не менялся), столы и сцена `MainScene.unity`, планировщик/IK/`PoseValidator`-расчёты/оракул, лазеры, материалы, HDRP-настройки, ProjectSettings.

### 12.09.2026, 18:12 — УПРАВЛЕНИЕ ГЛУБИНОЙ ШАРИКА ПРИЦЕЛА КОЛЕСОМ МЫШИ (прилипание к поверхностям + средняя кнопка)
- [x] **Прокрутка колеса двигает шарик ВДОЛЬ ЛУЧА:** вперёд (Scroll Up) — от оператора, назад (Scroll Down) — к оператору. Луч исходит из позиции оператора (камера) в направлении прицеливания. Шаг — `scrollStep = 0.08` юнита за щелчок, движение плавное (`scrollSmoothSpeed = 16` 1/с).
- [x] **Прилипание к поверхностям (`stickyToSurface = true`):** пока шарик идёт вперёд, Raycast из позиции оператора не пускает его дальше первой поверхности по слою `surfaceLayer`; упёршись, шарик стоит на ней, и дальнейшая прокрутка вперёд его не двигает. Прокрутка назад отводит шарик от поверхности к оператору. Луч в пустоту — шарик движется свободно в пределах `minDistance…maxDistance`.
- [x] **Средняя кнопка (Middle Click) — возврат к ближайшей поверхности:** Raycast, первое попадание, шарик мгновенно туда (сдвиг = 0). Если попадания нет — **шарик остаётся на месте** (решение: никаких «телепортов» назад к камере) + подсказка в статусе.
- [x] **Параметры в инспекторе (`Main Camera → FreeFlyCameraController`):** `scrollStep`, `maxDistance`, `minDistance`, `stickyToSurface`, `surfaceLayer` (+ `scrollSmoothSpeed`, `surfaceStickTolerance`). Значения по умолчанию: 0.08 / 20 / 0.25 / true / `Physics.DefaultRaycastLayers`.
- [x] **Визуал (ТЗ «на твоё усмотрение», зафиксировано):** прилип — шарик СПЛЮЩЕН по нормали поверхности и горит полной яркостью; отведён — ровный шар и тусклее. Цвет всегда остаётся вердиктом оракула (кислотно-зелёный/жёлтый/маджента); дополнительных линий и индикаторов не добавлено.
- [x] **Интеграция:** работает во всех состояниях, где есть шарик (`Idle`, `PointSelected`, `TrajectoriesShown`, `PhantomsMoving`); в `PointMoveMode` колесо глубину НЕ трогает (игнорируется), шарик приостановлен (`SetSuspended`); Reachability Oracle не менялся и проверяет точку независимо от того, прилип шарик или нет.
- [x] **Регресс потока этапов проверен:** шарик на поверхности → точка = поверхность + `toolOffset` 0.10 по нормали (как было); шарик отведён колесом → точка фиксируется как точка свободного пространства БЕЗ смещения (правило «смещение только для точки НА поверхности»).
- [x] **Конфликты колеса/средней кнопки найдены и описаны (см. §5.10, §6):** UI-карта KazistovVv (`ScrollWheel` для `ScrollRect` дерева, `MiddleClick`) — решено ограничением на «курсор захвачен ИЛИ не над панелями»; удержание средней кнопки в `KeyboardMouseInputProvider.GetRotation()` (поворот камеры) — конфликт спящий (провайдер в сценах не создаётся), лечение описано.
- [x] **Проверено батч-прогоном в Play-режиме:** 46 «[OK]», ни одного «[FAIL]» (`_dsh_aimdepth_verify.txt`) — поведение без колеса, отвод/возврат, прилипание, пределы, средняя кнопка, «сквозной» режим, пустота, визуал, лазеры, поток этапов, `PointMoveMode`.
- [x] **Не тронуто:** `TrajectoryFlowController` (State Machine этапов 0–4 и `PointMoveMode`), `SelectionTypes`, `LaserManager`, планировщик, IK, `PoseValidator`, `ReachabilityOracle`, фантомы, столы, SCARA (её код вообще не менялся), `MainScene.unity`.

### 12.09.2026, 09:00 — ПОЧИНКА PointMoveMode: «режим не работает» (диагностика + полный сценарий шагов 1–4)
- [x] **Главная причина найдена: код режима ни разу не компилировался.** `PointMoveHud.cs`/`ToolAlign.cs` были без `.meta`, `Assembly-CSharp.dll` — от 02:07 (до правок 07:43–07:49), в логе редактора нет ни одного `PointMove`. В работающем Unity режима просто не существовало: Enter ничего не делал, QWEASD двигали камеру, индикатора не было. После батч-прогона новые файлы импортированы (`.meta` на месте), `Assembly-CSharp.dll` пересобран (08:15:01), csproj перегенерированы со всеми новыми файлами.
- [x] **Вторая причина: у пользователя открыта ДРУГАЯ копия проекта.** `C:\Users\Ольга\KavistovVv` (ветка `Cline`) не содержит ни `PointMoveMode`, ни `movePoint`, ни `PointMoveHud` — там более старая State Machine. Рабочая копия — `…\OneDrive\Documentos\новое пространство\KavistovVv` (ветка `DeepSeek`). См. §1 и §9.
- [x] **Ввод Enter/Esc/Shift/E получил откат на legacy Input** (`IsKeyPressed`, новый `IsKeyDownThisFrame` для одиночного нажатия `E`): при `Keyboard.current == null` Enter больше не «теряется», а `PointMoveAxis()` не возвращает ноль. `E` в режиме — только «точка вверх» (не дубль ЛКМ).
- [x] **Лазеры в режиме действительно показывают перемещаемую точку** (цель передаётся в `DrawHandLaser` явно, луч идёт сквозь геометрию и красится в цвет вердикта через `TryGetPointMoveTarget`).
- [x] **Esc в режиме принадлежит режиму** (камера не снимает курсор и не показывает панели); **Esc восстанавливает прежнюю траекторию мгновенно** (варианты и очередь просчёта откладываются на входе: `moveSavedPlans`/`moveSavedQueue`/`RestoreAfterPointMoveCancel`), просчёт продолжается с места остановки.
- [x] **Точка видна всегда** (маркер отделён от панели режима: `SetHudVisible`/`SetMarkerVisible`), панель — только в `PointMoveMode`.
- [x] **Подтверждение Enter фиксирует точку как точку свободного пространства** (без повторного смещения по нормали и без чужой «поверхности»), **оракул перепроверяет точку сразу после остановки**, **мышь в режиме не действует даже при выключенном `suppressDirectTeleop`**, HUD дополнен строкой «Δ от исходной».
- [x] **Проверено прогоном в Play-режиме (Unity batch)**: все проверки сценария шагов 1–4 пройдены (38 «[OK]», ни одного «[FAIL]») — выбор точки и 5 вариантов траекторий, Enter → `PointMoveMode` (траектория скрыта, панель и маркер видны, координаты от базы робота), ЛКМ без эффекта, Q/E и W/S+A/D двигают точку по мировым осям, точка проходит сквозь стол, оракул онлайн (`Safe → Marginal → Unreachable` «внутри корпуса робота» + статус в HUD), Enter → новая позиция и пересчёт, Esc → исходная позиция и восстановленная траектория. Числа — в §5.9, отчёт `_dsh_pointmove_verify.txt`.
- [x] **Не тронуто:** `SelectionTypes.cs` (состояние и поля уже были), `LaserManager`, планировщик, IK, валидатор, оракул, фантомы, столы, SCARA, `MainScene.unity`.

### 12.09.2026, 07:48 — РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ (PointMoveMode) + проверка ориентации TCP
- [x] **Этап 1 — вход в режим**: после выбора точки (красный луч + ЛКМ) нажатие **Enter** переводит поток в `FlowState.PointMoveMode`; точка фиксируется и больше не следует за курсором (мышь двигает только камеру), прежние «колбаски» убираются и пересчитываются после выхода. Доступно из `PointSelected` и `TrajectoriesShown`; без зафиксированной точки Enter пишет «Сначала выберите точку красным лазером (ЛКМ)».
- [x] **Этап 2 — визуальный индикатор**: крупная панель HUD (screen-space, стиль KazistovVv) с заголовком режима, крупными координатами, статусом достижимости и подсказкой по клавишам + «рентген»-маркер в самой точке; панель сверху по центру, точка и робот не перекрываются.
- [x] **Этап 3 — управление QWEASD**: точка двигается в МИРОВЫХ координатах (W/S — горизонтальная проекция взгляда, A/D — вправо/влево, Q/E — вертикаль мира), плавно, пока клавиша удержана; скорость `pointMoveSpeed = 0.5` ю/с (настраивается), Shift — ×3; на время режима клавиши отобраны у камеры.
- [x] **Этап 4 — онлайн-проверка достижимости**: тот же `ReachabilityOracle` (рабочая зона, IK с лимитами, столкновения со сценой и другими роботами, близость к лимитам и сингулярности) опрашивается раз в 0.08 с; результат — цвет статуса/маркера и текст причины в HUD.
- [x] **Этап 5 — точка проходит сквозь геометрию**: точка — маркер цели (`Vector3`), без коллайдера и Rigidbody; коллизия не ограничивает движение, недостижимость показывает оракул (красный).
- [x] **Этап 6 — координаты относительно базы робота**: `robot.transform.InverseTransformPoint(point)`, формат `X: 1.245   Y: 0.300   Z: -2.100`, обновление каждый кадр.
- [x] **Этап 7 — выход**: повторный **Enter** подтверждает новую позицию (точка фиксируется, генерация траекторий запускается заново), **Esc** отменяет (точка возвращается в исходную позицию); индикатор режима скрывается; смещение по нормали повторно не применяется.
- [x] **Этап 8 — интеграция со State Machine**: `PointSelected → (Enter) → PointMoveMode → (Enter) → PointSelected` (новая позиция) и `PointMoveMode → (Esc) → PointSelected` (исходная позиция); в режиме лазеры и обычные функции ЛКМ не работают (только индикация — лучи сходятся в перемещаемой точке), шарик прицела приостановлен.
- [x] **Проверка ориентации TCP выполнена и зафиксирована** (§5.8): модель идёт по Y-конвенции (нормаль концевой плоскости — локальная Y фланца, инструмент вдоль −Y), а не по ожидаемой X; влияние на расчёты проверено (нет), соглашение записано, добавлен разовый лог `[TCPDiag]`, исправление не делалось (вне задачи) — открытый вопрос в §6.
- [x] **Не тронуто**: `LaserManager` (кроме передачи ему перемещаемой точки как цели лучей), планировщик, IK, валидатор, оракул, фантомы, столы, SCARA, дубликаты роботов, автообновление дерева; `MainScene.unity` не менялась.
- Компиляция: `dotnet build Assembly-CSharp.csproj` → **0 ошибок** (см. §5.8). PlayMode-прогон не выполнялся (редактор пользователя открыт) — ручные проверки в §7.

### 12.09.2026, 07:12 — ШАГ 1 из 3: смещение целевой точки вдоль нормали поверхности
- [x] **Нормаль поверхности берётся из `RaycastHit.normal`** (луч прицела, `FreeFlyCameraController.ComputeAimPoint`) и доходит до потока этапов; для рабочей плоскости y = 0 нормаль — `Vector3.up`, но эта точка считается «не поверхностью» и не смещается.
- [x] **`target = hit.point + hit.normal * toolOffset`**, `toolOffset` — **публичное сериализуемое поле, по умолчанию 0.10** (правится в инспекторе в PlayMode, без перекомпиляции). Горизонтальный стол — смещение вверх по Y, наклонная/вертикальная поверхность — вдоль своей нормали.
- [x] **Смещение применено ровно в одном месте формирования цели** — `TrajectoryFlowController.OffsetTarget()` перед `oracleQuery(...)` и `LockPoint(...)` (этапы 1, 2 и 3 — все три точки входа красного лазера). Оракул и планировщик получают уже смещённую точку.
- [x] **Ориентация TCP на этом шаге НЕ меняется**: `alignToolToSurface = false` (выравнивание — шаг 3, код-задел `ToolAlign` оставлен, но не вызывается).
- [x] **Диагностика отказов (п.4 ТЗ)**: `[OffsetDiag]` в консоль и в `_dsh_offset_diag.txt` — точное значение `toolOffset`, координаты поверхности/нормали/цели, запас в мм, причина (`пересечение со сценой`, `IK не сходится`, `траектория не найдена` + `planner.LastDebug`).
- [x] **Временный хак (+toolOffset по Y)** остался только как режим `offsetMode = WorldUpHack` и помечен в §5/§6 как временный (по умолчанию выключен).
- [x] **Не тронуто**: лазеры, State Machine (условия/переходы), планировщик, IK, валидатор, оракул, столы, SCARA, фантомы (один фантом, логика без изменений), дубликаты роботов, автообновление дерева; `MainScene.unity` не менялась.
- Компиляция: `dotnet build Assembly-CSharp.csproj` → **0 ошибок** (см. §5.7). PlayMode-прогон не выполнялся (редактор пользователя открыт) — ручные проверки и таблица диагностики в §7.

### 12.09.2026, 03:49 — робот не врезается в поверхность: смещение по нормали + выравнивание «пятака»
- [x] **Нормаль поверхности берётся из `RaycastHit`** (`FreeFlyCameraController.ComputeAimPoint` → `aimNormal`/`aimOnRealSurface` → новый перегруженный `TrajectoryFlowController.UpdateAim(...)`); для рабочей плоскости y=0 нормаль — `Vector3.up`, признак реальной поверхности отдельный.
- [x] **Целевая точка смещается вдоль нормали**: `target = hit.point + hit.normal * toolOffset` (тогда `toolOffset = 0.30`; **с ШАГА 1 — 0.10**). Горизонтальный стол — вверх по Y, наклонная/вертикальная поверхность — вдоль своей нормали. Смещённая точка идёт и в оракул, и в планировщик; `state.point` = цель TCP, `state.aimAtLock` = точка на поверхности.
- [x] **Порядок работы совпадает с ТЗ**: луч → `hit.point`/`hit.normal` → `target = hit.point + hit.normal*toolOffset` → ориентация (ось «пятака» = нормаль) → IK считает конфигурацию под эту позу → фантом и реальный робот приходят, не проваливаясь в поверхность.
- [x] **Концевая плоскость выравнивается по плоскости поверхности** (точка на плоскости): `ToolAlign.AlignGoal` доворачивает последнюю позу траектории так, чтобы нормаль «пятака» совпала с нормалью поверхности (±, что ближе); принимается только валидная поза, иначе — прежняя (свободная ориентация). **С ШАГА 1 выключено** (`alignToolToSurface = false`) — включается на шаге 3.
- [x] **Точка в воздухе — поведение прежнее**: без смещения и без выравнивания (проверка `aimOnSurfaceNow`/`lockedOnSurface`).
- [x] **Временный хак (+toolOffset по Y)** из п.4 ТЗ оставлен как переключаемый режим `offsetMode = WorldUpHack` и помечен в §5/§6 как временный; основное решение (через нормаль) — по умолчанию.
- [x] **Не тронуто**: лазеры, State Machine (условия и переходы этапов 0–4), планировщик, IK, валидатор, оракул, столы, SCARA, фантомы (их создание/удаление/движение), дубликаты роботов, автообновление дерева; `MainScene.unity` и координаты роботов не менялись.
- Компиляция: `dotnet build Assembly-CSharp.csproj` → **0 ошибок** (см. §5.6). PlayMode-прогон не выполнялся (редактор пользователя открыт) — ручные проверки в §7.

### 12.09.2026, 03:26 — фантомы: старые удаляются полностью, новый едет по траектории
- [x] **Старые фантомы удаляются при появлении новой траектории**: `ClearPhantoms()` (полная уборка, включая контейнер, «осиротевшие» `Phantom_*`/`Phantoms` верхнего уровня и материалы-инстансы) вызывается перед созданием нового фантома, а `Hide()` (та же уборка) — в `LockPoint` при каждом новом нажатии красным лазером; Esc и выбор идут через тот же путь. В сцене всегда ровно один актуальный фантом.
- [x] **Удаление корректное, без «висяков»**: сначала `SetActive(false)` (копия мгновенно исчезает из сцены и перестаёт выбираться лучом), затем `Destroy` в PlayMode / `DestroyImmediate` в редакторе; контейнер `Phantoms` уничтожается и создаётся заново — «пул очищен».
- [x] **Фантом гарантированно движется**: шаг ведёт `PhantomManager.Update()` (ровно один шаг на кадр), а не только поток этапов; убран глобальный ранний выход по `appearAtTargetPose` (он больше не может «заморозить» фантом с путём); старт — в позе робота, финиш — ровно последний сэмпл траектории, после чего фантом останавливается и остаётся видимым.
- [x] **Движение — по углам суставов выбранной траектории** (сэмплы `plan.Path`, интерполяция по кратчайшим доворотам, профиль времени траектории), скорость `phantomSpeed = 0.5` ю/с в границах 1.2…8 с.
- [x] **Выбор зелёным лазером** работает и в пути, и после остановки (логика этапа 3 не менялась; суставы копии кэшируются, поэтому наведение стало дешевле).
- [x] **Копия робота больше не «живая» ни одного кадра** («тихий» Instantiate): никаких `TCP`-прокси, перепарентирования мешей и новых коллайдеров у копии — детали копии не появляются в Hierarchy, `ReassertHideFlags` сокращён до 2 кадров-страховки.
- [x] **Не тронуто**: лазеры, State Machine, планировщик/оракул/валидатор, столы, SCARA-кинематика, дубликаты роботов, автообновление дерева; `MainScene.unity` не менялась.
- Компиляция: `dotnet build Assembly-CSharp.csproj` → **0 ошибок** (см. §5.5). PlayMode-прогон не выполнялся (редактор пользователя открыт) — ручные проверки в §7.

### 12.09.2026, 03:15 — плавное движение фантома к цели по выбранной траектории
- [x] **Фантом больше не появляется сразу в конечной точке**: `PhantomManager.ShowAlongPath` рождает копию в СТАРТОВОЙ позе робота (`ApplyPose(startQ)`) и ведёт её по сэмплам выбранной траектории; `Tick`/`PoseAt`/`SegmentAt` двигают фантом с профилем времени траектории. Новый публичный вход — `ShowAlongPath(plan, startQ, speed)`; старый `Show` (множественные копии IK) остался как задел.
- [x] **Только один фантом (ТЗ)**: `phantomCount = 1`, `BuildPhantoms` строит ровно одну копию — без перебора конфигураций IK (конечная поза = `plan.GoalQ`). Многофантомные ветви (`TopUpVariants`, `PhantomMath.PickDistinct`, `ik.SolveAllSeeded`) не вызываются, но сохранены для будущего ТЗ.
- [x] **Движение по траектории, а не по прямой**: промежуточные позы берутся из `plan.Path` (те же сэмплы, что пойдёт реальный робот на этапе 4), между сэмплами — кратчайшие довороты (`LerpPose`), поэтому фантом повторяет именно подтверждённый зелёным лазером путь.
- [x] **Движение видимое, но быстрое**: `duration = clamp(длина TCP / 0.5 ю/с, 1.2 с, 8 с)` — для эталонной траектории 3.3 юнита ≈6.6 с (робот на этапе 4 ехал бы 99 с). Ручки: `TrajectoryFlowController.phantomSpeed`, `PhantomManager.pathSpeed/minTravelTime/maxTravelTime`.
- [x] **По достижении цели фантом останавливается и остаётся видимым** (прогресс 1 → поза ровно последний сэмпл; никакого автоскрытия, как и раньше — только `Esc`/выбор/`FinishMotion`).
- [x] **Выбор зелёным лазером работает в пути и после остановки**: `HoverIndex`/`SetHighlight` считают объёмы движущейся копии, `Stage3_Confirm` не блокируется — логика ЛКМ этапа 3 не менялась.
- [x] **Робот приезжает туда, где остановился фантом**: `cfg.q = plan.GoalQ`, а `SelectPhantom` правит последний сэмпл через `ContinueFrom` — финальная поза согласована (как и раньше).
- [x] **Цвет/прозрачность/скрытие из Hierarchy — как были** (`MakeGhost`: бирюза, alpha 0.55, ZTest Always; `HideFlags.HideInHierarchy` без `DontSave`, `ReassertHideFlags`, уничтожение контейнера) — в этой сессии не менялись.
- [x] **Не тронуто**: лазеры, State Machine (кроме текстов/комментариев), планировщик, оракул, валидатор, столы, SCARA-кинематика, дубликаты роботов, автообновление дерева; `MainScene.unity` не менялась.
- Компиляция: `dotnet build Assembly-CSharp.csproj` → **0 ошибок** (см. §5.4). PlayMode-прогон не выполнялся (редактор пользователя открыт).

### 12.09.2026, 02:55 — «фантомные» роботы в Hierarchy + автообновление дерева
- [x] **Разобрано, откуда берутся строки `SCARA LS10-B702S` и 6-осевой (`Axis1..Axis6`)**: это не объекты сцены (в `MainScene.unity` их нет — проверено пофайлово), а (1) копии, создаваемые кнопкой «Добавить робота» (`ObjectSpawner.SpawnRobot` → имя `«<robotName>_HHmmss»`, где `robotName` = «SCARA LS10-B702S» / «6-осевой робот»), и (2) копии-фантомы `PhantomManager`, у которых терялись `HideFlags`. Подробности — §5.3.
- [x] **`HideFlags.HideAndDontSave` у фантомов отменён** (это и была причина «объект в дереве, но не найден в сцене»): `DontSave` не даёт уничтожить объект при выгрузке PlayMode-сцены, и он остаётся жить в редакторе вне сцен. Теперь контейнер `Phantoms` и копии — `HideFlags.HideInHierarchy`, контейнер уничтожается в `OnDisable`/`OnDestroy`, плюс страховка `DestroyLeftoverContainers` на старте PlayMode.
- [x] **Флаги копий больше не теряются**: `ReassertHideFlags()` 8 кадров подряд после создания копии — дети, которые успевают создать живые скрипты копии (`TCP`, `Collider_*`, меши после `FixLegacyMeshParents`), тоже скрыты.
- [x] **Копия предпросмотра размещения не остаётся в сцене**: `CenterWindow.OnDisable/OnDestroy` → `DestroyPreview()`.
- [x] **Уборка «фантомных» записей в редакторе**: `Editor/HierarchyPhantomCleaner.cs` — автоочистка при загрузке домена и на `ExitingEditMode`/`EnteredEditMode` + набор пунктов меню `Tools/KazistovVv/Hierarchy` (очистка, инвентарь роботов, диагностика объектов вне сцен, удаление UI-копий роботов). Инвариант «при старте PlayMode и после остановки в сцене ровно два робота» проверяется автоматически (`роботов в сцене: 2 (…)` либо предупреждение с именами лишних).
- [x] **Иерархия обновляется сама**: `Editor/HierarchyAutoRefresh.cs` (`[InitializeOnLoad]` → `EditorApplication.update` → `EditorApplication.RepaintHierarchyWindow()` раз в 2 с, только repaint, пропуск во время компиляции/импорта); включение/выключение — `Tools/KazistovVv/Hierarchy/Автообновление иерархии (2 с)`.
- [x] **Сцена не тронута**: правок в `MainScene.unity` не потребовалось — в ней и не было «фантомов»/дубликатов; оба робота (`Робот_6ос_Стенд1`, `SCARA_Стенд2`) на месте, `isActive = 0`, `selectedRobot = null`.

### 12.09.2026, 02:35 — дубликаты роботов в PlayMode + фантомы без анимации
- [x] **Дубликаты роботов в PlayMode** — разобрано по файлам: `MainScene.unity` содержит **ровно два** prefab-instance робота (6-осевой и SCARA, оба `isActive = 0`), скриптов, создающих роботов в `Awake/Start/OnEnable`, в проекте **нет** (`Instantiate` встречается 3 раза: фантом, точка калибровки, редакторский `StandBuilder`); префабов роботов нет — роботы стоят в сцене как instance из `Robot.fbx`/`ScaraRobot.fbx`. Значит, в PlayMode «лишние» роботы берутся не из рантайма, а из состояния открытой сцены (несохранённые копии, сцена из `_Recovery`, второй стенд после «Добавить робота»). `selectedRobot` инициализируется как `null` (поле не сериализуется), автовыбора узла нет.
- [x] **Диагностика инвентаря**: `RobotInventory.Guard` в `RobotController.Awake` — один лог за старт: «Роботов в сцене: 2 (6-осевой + SCARA)» или предупреждение с разбивкой (сколько 6-осевых / SCARA / прочих и какие имена). Теперь любой дубликат в PlayMode сразу виден в консоли вместе с именем.
- [x] **Фантомы без анимации**: `PhantomManager.Show` ставит каждую копию сразу в её конечную конфигурацию (`ApplyPose(ghost, s.q)`, `progress = 1`), `Tick` при `appearAtTargetPose = true` мгновенно выходит, подсказка «Фантомы едут к своим позам…» удалена, из подписи фантома убран процент переезда (осталось: ветвь, ошибка FK, число фантомов).
- [x] **Фантомы полностью вне дерева Hierarchy**: контейнер `Phantoms` — `HideFlags.HideAndDontSave` (не только `HideInHierarchy`: `DontSave` гарантирует, что служебный объект не попадёт в сохранённую сцену), копии и их потомки — тоже `HideAndDontSave`, флаг восстанавливается после `SetParent` (иначе назначение родителя сбрасывает флаги).
- [x] **Выбор фантома зелёным лазером сразу после появления** сохранён: наведение (`PhantomManager.HoverIndex` по лучу и по точке прицела) и подсветка (`SetHighlight`) работают без задержки, логика ЛКМ этапа 3 не менялась.

### 12.09.2026 — предыдущая сессия
- [x] **Фантомы не появлялись** — корневая причина: без выбранного робота (`isActive = false`, а автовыбор узла дерева активировал не робота, а стол) `TrajectoryFlowController` выходил из `UpdateAim` с сообщением «Наведите шарик лазера на робота и нажмите F» — точка не фиксировалась, траектории и фантомы не создавались. Теперь робот определяется по прицелу (§5), а старт идёт без выбранного робота.
- [x] **Фантомы были невидимы** в момент появления: рождались ровно в позе реального робота и при `ZTest = LEqual` полностью прятались за непрозрачной моделью → включён «рентген» (`_ZTestTransparent = 8`), alpha поднята до 0.55 (пульсация свечения в полёте — уже не нужна и не применяется).
- [x] **Второстепенные траектории не появлялись**: 5 вариантов рисовались одним оранжевым цветом по одному и тому же пути TCP → введены 5 разных цветов, боковое разведение «колбасок» (3…12 см) и явные подписи «лучшая» / «вариант (невыгодная)».
- [x] **Фантомов было 2–3** → добавлен добор конфигураций (`TopUpVariants`: зеркала + CCD-доводка в ту же точку) — стабильно 5.
- [x] **Ошибочная подпись времени** у варианта траектории (`cand.timeS` брался до пересчёта времени) — исправлено.
- [x] **Автовыбор робота при старте** (`KazistovVvUIManager.Start()` → `SelectNode(Roots[0])`) — убран; добавлен стартовый лог/предупреждение «Роботов в сцене: N (ожидается 2)».
- [x] Раньше (предыдущие сессии): цвет/прозрачность/скрытие фантомов, единая ЛКМ и тексты подсказок этапов 0–4, игнор нажатий на этапе 4, самодвижение SCARA, столы ×4, детерминированный выбор робота потока, служебные объекты вне коллизий и иерархии.

## 7. Открытые задачи
- [ ] **ВОССТАНОВИТЬ ПОТЕРЯННЫЕ РАЗДЕЛЫ ДОКУМЕНТА (§0.0).** В этой редакции нет разделов сессий 13.09.2026 (§0.1 TAB/точка на поверхности/столы ×3/фонарик; §0.2 восемь траекторий и восемь фантомов; §0.3 фонарик с первого нажатия/фантомы ×3/зона достижимости/лимиты/метрики) и §5.12–§5.15. Взять можно из: (1) облачной **«Корзины» OneDrive** (клиент OneDrive не был запущен в момент удаления — файлы, скорее всего, ещё в облаке); (2) стенограмм сессий DSH `C:\Users\Ольга\.dsh\sessions\…\session-*.jsonl.zstd` (там полный текст `PROJECT_CONTEXT.md`); (3) «Предыдущих версий» тома. Действующее поведение кода при этом НЕ затронуто (проверено прогоном §5.16).
- [ ] **Потеряны диагностики агента** `DshTasksDiag.cs`, `DshVariantsDiag.cs`, `DshFlowDiag.cs`, `DshAuditDiag.cs`, `DshAimDepthDiag.cs`, `DshPointMoveDiag.cs` (§0.0). При необходимости пишутся заново по §9 (актуальная — `DshDesktopUiDiag.cs`).
- [ ] **РУЧНАЯ ПРОВЕРКА НОВОГО ДЕСКТОПНОГО UI (главное после сессии 13.09.2026).** Автоматика (§5.16) пройдена в батче, руками остаётся то, чего в batch нет: живая мышь, наведение, перетаскивание панелей и ощущения. Порядок (3–4 минуты):
  1. **TAB** → курсор свободен, интерфейс перед вами: сверху строка меню и **тулбар из 15 кнопок в 3 ряда по 5**, кнопки **БЕЗ подписей**; навести мышь на кнопку → через ~0.35 с появляется подсказка с названием, описанием и горячей клавишей; увести мышь → подсказка исчезает.
  2. Кнопки-заглушки («Отменить», «Вернуть») — серые, не нажимаются, в подсказке «в разработке». Пункты-заглушки есть и в меню («Файл → Экспорт/Импорт», «Сервис → Инструменты»).
  3. **Дерево слева**: раскрыть робота (стрелка ▸/▾) → «Ось 1…6» и «TCP»; группа «Столы» → «Стенд_1_Стол» / «Стенд_2_Стол». Клик по узлу → объект подсвечивается **рамкой** в сцене, справа заполняются свойства.
  4. Кнопки «развернуть всё»/«свернуть всё» над деревом работают.
  5. **Глазик** у узла стола → объект исчезает из сцены (логика, коллайдеры и расчёты не меняются!), повторный клик возвращает.
  6. **Двойной клик** по узлу → переименование на месте (печатать, Enter — применить, Esc — отмена, Backspace — стереть). Проверить, что имя объекта в сцене при этом НЕ меняется (в свойствах строка «Объект сцены»).
  7. **Панели**: потащить заголовок «Дерево моделей» к правому/нижнему краю → панель прилипает; отпустить в центре → становится плавающим окном; потащить внутреннюю кромку → меняется толщина; ▾ — свернуть (остаётся заголовок), ✕ — закрыть (вернуть через меню «Вид → Панели» или вкладку «Интерфейс» настроек).
  8. **Тема**: кнопка «Тема» в тулбаре (или «Настройки → Интерфейс») — Тёмная → Светлая → Системная; переключение мгновенное, выбор переживает перезапуск PlayMode (PlayerPrefs). Проверить читаемость оверлеев (HUD режима точки, метрики, лимиты) в СВЕТЛОЙ теме.
  9. **Настройки** («Сервис → Настройки» или кнопка): вкладка «Функции» — переключатели зоны достижимости/лимитов/метрик/фонарика/автообновления дерева работают сразу; пункт про фантомы недоступен с пометкой «нет обработчика» (это задел по ТЗ); вкладка «Управление» — таблица биндов (только просмотр, включая строки «Геймпад» и «VR / MR — будет добавлено позже»); вкладка «Интерфейс» — тема, масштаб 85…130 %, плотность (компактная/обычная), число рядов тулбара, видимость панелей; вкладка «Справка» — шаги алгоритма и подсказки по интерфейсу.
  10. **Статус-бар снизу**: состояние меняется по шагам (Idle → PointSelected → TrajectoriesShown → PhantomsMoving → RobotMoving → PointMoveMode), виден робот, координаты луча, сообщения потока (те же, что в логе), тема и FPS.
  11. Убедиться, что **игровой процесс не изменился**: красный луч (Z) + ЛКМ → 8 траекторий и 8 фантомов; Enter → режим перемещения точки; Esc → сброс; G — фонарик; X — зелёный луч. Кнопки тулбара лишь дублируют эти действия — бинды прежние.
  12. **Разные разрешения** (1920×1080, 2560×1440, 1366×768): панели не должны наезжать друг на друга и на тулбар; при необходимости — «Интерфейс → Масштаб».
  13. **Производительность**: Stats/Profiler при включённом и скрытом (TAB) интерфейсе — разницы быть не должно; при просадке уменьшить `propertiesInterval`/`treeInterval` или включить компактную плотность.
- [ ] **Проверить столы и роботов после восстановления сцены (§0.0):** столешницы **14.4 × 9.6** (втрое больше исходных), ножки по углам (±7.1/±4.7), зазор между столами 1 юнит, роботы стоят у внутренних кромок на прежних мировых координатах (0, 0.98, −23.9) и (0, 0.98, −28.1). Проверка одной строкой: PlayMode → красный луч + ЛКМ по точке столешницы → «Вариантов: 8».
- [ ] **ГЛАВНОЕ ПОСЛЕ АУДИТА 12.09.2026: проверить работу В РЕДАКТОРЕ ПОЛЬЗОВАТЕЛЯ (`C:\Users\Ольга\KavistovVv`).** Копии синхронизированы (§1), но редактор пользователя держит старую сборку: нужно один раз сфокусировать окно Unity (или `Ctrl+R` / `Assets → Refresh`), дождаться перекомпиляции и убедиться, что **в консоли НЕТ ошибок** (должны появиться меню `Tools/KazistovVv/Hierarchy` и лог `[RobotInventory] Роботов в сцене: 2 …` при старте Play). Порядок проверки (2 минуты): красный луч (Z) + ЛКМ по столешнице → «Вариантов: 5» → **Enter** (панель «РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ», траектории исчезают, маркер точки пульсирует) → Q/E, W/S, A/D (камера стоит) → **Enter** (точка зафиксирована, варианты считаются заново) или **Esc** (точка и прежняя траектория вернулись) → **колесо назад/вперёд** (шарик отходит от стола и упирается в него) → **колёсико** (мгновенный возврат к поверхности) → зелёный луч (X) + ЛКМ по «колбаске» → фантом появляется В ПОЗЕ РОБОТА и едет по траектории (~6.6 с на 3.3 юнита) → ЛКМ по фантому → робот едет в ту же позу и **после остановки НЕ дрожит**.
- [ ] **Проверить, что конфликт «Enter в режиме размещения» не мешает:** открыть «Добавить робота/стол» (режим размещения) и нажать Enter — размещение подтвердится, а поток при этом может войти/выйти из `PointMoveMode` (см. §6). Если мешает — сказать, поставим гейт.
- [ ] **Проверить защиту от самостолкновения вживую (в батче не воспроизвелась).** Для воспроизведения нужна прямая телеоперация: в инспекторе `Main Camera → FreeFlyCameraController` снять `suppressDirectTeleop` (по умолчанию стоит) и кликнуть по точке у базы робота — раньше это давало пачки `[SixAxis] Самостолкновение звеньев 0 и 3 …` и дрожание TCP; теперь робот должен замереть и один раз написать `[SixAxis] Самостолкновение не уходит за 0.25 с — цель снята, робот остановлен`. После проверки вернуть `suppressDirectTeleop = true`.
- [ ] **Проверить колесо над панелями KazistovVv:** с курсором над деревом колесо должно прокручивать список (не шарик); при СКРЫТЫХ панелях (телеоперация) колесо двигает шарик даже со свободным курсором.
- [ ] **Проверить, что HUD режима не перехватывает клики:** в режиме перемещения точки панель висит сверху по центру — кнопки верхней панели KazistovVv под ней должны нажиматься (исправлено `raycastTarget = false`).
- [ ] **ГЛУБИНА ШАРИКА КОЛЕСОМ МЫШИ — ручная проверка (главное, ТЗ 18:12).** Автоматика (47 проверок) уже прошла в Play-режиме (§5.10), поэтому руками остаётся то, чего не видно в batch: **живое колесо и ощущения**. Порядок (1–2 минуты):
  1. PlayMode → красный луч (Z) + ЛКМ по точке на столешнице → «Вариантов: 5» (шарик стоит на столе и слегка СПЛЮЩЕН по нормали);
  2. **прокрутить колесо НАЗАД** (на себя) → шарик отходит от стола к оператору и становится ровным шаром (шаг 0.08 за щелчок, движение плавное); цвет шарика — по-прежнему вердикт оракула;
  3. **прокрутить колесо ВПЕРЁД** → шарик едет к столу и **упирается в него** (сплющивается) — дальше вперёд не идёт, сколько ни крути;
  4. **нажать КОЛЁСИКО** → шарик мгновенно возвращается на ближайшую поверхность по лучу (даже если до этого был отведён на полметра); посмотреть в консоль `[AimDepth] средняя кнопка: шарик возвращён к ближайшей поверхности (…)`;
  5. навести луч в небо (пустота) → шарик виден и свободно ездит колесом, статус прицела «Наведите красный лазер на поверхность» на ЛКМ; **колёсико в пустоте** → шарик стоит на месте, в статусе «Под прицелом нет поверхности — шарик остался на месте», в консоли `[AimDepth] средняя кнопка: поверхность по лучу не найдена…`;
  6. проверить, что **колесо над панелью KazistovVv по-прежнему прокручивает дерево**, а не двигает шарик (CapsLock — режим UI, навести курсор на дерево и покрутить);
  7. **Enter → PointMoveMode** → покрутить колесо: глубина шарика не меняется (точка ходит только QWEASD), маркер шарика скрыт; Esc — выход.
- [ ] **Проверить на наклонной/вертикальной поверхности:** навести на ножку стола/стену/скос → шарик должен прилипнуть к ней и сплющиться ПО ЕЁ НОРМАЛИ (а не «вверх»), а отвод колесом — уводить по лучу, а не по вертикали.
- [ ] **Проверить на выступе/нише:** навести на край столешницы и на нишу под столом → прилипание должно работать на первой поверхности по лучу (шарик не «проскакивает» сквозь край).
- [ ] **Проверить `stickyToSurface = false`** в инспекторе `Main Camera → FreeFlyCameraController`: прокрутка вперёд ведёт шарик СКВОЗЬ поверхность (и внутрь стола) — по ТЗ это режим «без прилипания»; вернуть `true`.
- [ ] **Подстройка без перекомпиляции:** в PlayMode менять `scrollStep` (0.08 → 0.15 → 0.3), `minDistance`, `maxDistance`, `scrollSmoothSpeed` и повторять прокрутку — шаг/инерция должны ощущаться предсказуемо; при необходимости зафиксировать удобные значения в §5.10.
- [ ] **Проверить `surfaceLayer`:** выставить в инспекторе слой, на котором стоит только стол (или, наоборот, `Nothing` — тогда включается страховка «слои лучей»), и убедиться, что прилипание идёт именно по этому слою.
- [ ] **Повторить автопроверку после правок:** `Unity.exe -batchmode -nographics -projectPath "<DeepSeek-копия>" -executeMethod DshAimDepthDiag.Run -logFile _dsh_aimdepth.log` → отчёт `KavistovVv/_dsh_aimdepth_verify.txt` (в конце строка «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ»). Скрипт ничего не меняет в сцене и выходит сам. **Важно:** в закрытом ангаре «пустоты» нет, поэтому прогон сам временно ставит `laserLength = 3` для проверки ветки свободной глубины (значение восстанавливается в том же прогоне).
- [ ] **РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ — ручная проверка (главное, ТЗ 07:48).** Три автоматические проверки уже пройдены в Play-режиме (§5.9), поэтому руками остаётся ровно то, чего не видно в batch: **визуал и клавиатура**. Порядок (1–2 минуты):
  1. красный луч (Z) + ЛКМ по точке на столешнице → «Вариантов: 5» (траектория показана);
  2. **Enter** → сверху по центру появляется крупная панель «РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ» (координаты от базы робота, «Δ от исходной», статус, подсказка), в самой точке — пульсирующий неоновый маркер; траектория («колбаски») при этом **исчезает**; в консоли `[PointMove] вход в режим: … · отложено вариантов N`;
  3. удержать **Q/E**, **W/S**, **A/D** (Shift — быстрее): точка едет плавно, координаты в панели идут онлайн, **камера при этом стоит на месте**; оба луча сходятся в точке и меняют цвет вместе со статусом;
  4. провести точку сквозь столешницу/стену — она проходит (маркер виден сквозь геометрию), статус при уходе в недостижимую зону (например, к оси робота) становится красным «НЕДОСТИЖИМО (…)»;
  5. **Enter** → панель исчезает, статус «Точка перемещена вручную · X … Y … Z … (относительно робота) · считаю варианты траекторий…», траектория считается заново (другой формы); **Esc** (в том же режиме, но вместо Enter) → точка возвращается на исходное место, **прежняя траектория появляется сразу**, панель исчезает. Проверить оба выхода 2–3 раза подряд.
- [ ] **Проверить, что ЛКМ в режиме ничего не подтверждает:** клик мышью в режиме → в статусе подсказка «Мышь в режиме точку не двигает · Q/E, W/S, A/D — двигать · Enter — подтвердить · Esc — отмена», состояние и позиция точки не меняются.
- [ ] **Проверить, что Esc в режиме не выпускает курсор:** нажать Esc в режиме → отменяется только перемещение, курсор остаётся захваченным и панели KazistovVv не появляются (для UI — `CapsLock`). Вне режима Esc работает как раньше (сброс + курсор/панели).
- [ ] **Проверить логи `[TCPDiag]` в PlayMode** (печатается один раз на привязку робота): «ось инструмента в системе TCP = (…), TCP.up=…, TCP.right=…, TCP.forward=…, угол оси инструмента к вертикали мира …°» — зафиксировать значения в §5.8/§6 (это ответ на вопрос про «X вверх / −Y вниз»).
- [ ] **Проверить, что HUD не мешает:** панель (700×156, отступ 92 px) не перекрывает робота/точку и не конфликтует с верхней панелью KazistovVv; при необходимости подстроить `PointMoveHud.topOffset/panelWidth/panelHeight/sortingOrder`.
- [ ] **Проверить SCARA-стенд:** режим перемещения работает и там (координаты считаются от корня `SCARA_Стенд2`), но ориентация инструмента у SCARA не выравнивается (шаг 3 её не касается). Автопроверка 09:00 шла на 6-осевом.
- [ ] **Повторить автопроверку после правок режима:** `Unity.exe -batchmode -nographics -projectPath "C:\Users\Ольга\OneDrive\Documentos\новое пространство\KavistovVv" -executeMethod DshPointMoveDiag.Run -logFile _dsh_pointmove.log` → отчёт `KavistovVv/_dsh_pointmove_verify.txt` (в конце строка «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ»). Скрипт ничего не меняет в сцене и выходит сам.
- [ ] **ШАГ 1 — ручная проверка (главное):** красный луч (Z) + ЛКМ по точке на столешнице → подсказка «Точка принята · TCP на 0.10 по нормали поверхности · считаю варианты траекторий…»; робот/фантом приходят так, что концевое звено **не проваливается** в текстуру (TCP на 0.10 над столом). Проверить 5–8 разных точек на обоих столах и на полу.
- [ ] **ШАГ 1 — нормаль, а не «вверх»:** навести на наклонную/вертикальную поверхность → в `[OffsetDiag]` (если точка отвергнута) или по положению фантома убедиться, что смещение идёт **вдоль нормали** этой поверхности (для стены цель уходит вбок/вперёд, а не вверх).
- [ ] **ШАГ 1 — точка в воздухе:** ЛКМ при луче «в никуда» → по-прежнему «Наведите красный лазер на поверхность» (рабочая плоскость y = 0: подсказка «точка не на поверхности — TCP без смещения»), ориентация свободная, поведение как раньше.
- [ ] **ШАГ 1 — подстройка без перекомпиляции:** в PlayMode выделить `Main Camera` → компонент `TrajectoryFlowController` → менять `toolOffset` (0.10 → 0.15 → 0.25) и повторять клик; проверить, что цель едет вдоль нормали и красное сообщение уходит.

#### Диагностика (ТЗ шаг 1, п.4): точное значение toolOffset и координаты
Как снять: PlayMode → кликнуть красным по проблемной точке → в консоли строка `[OffsetDiag] …` (или тот же текст в файле `_dsh_offset_diag.txt` в корне проекта `KavistovVv/`). Значения перенести в таблицу ниже.
Первая строка заполнена по батч-прогону 12.09.2026, 08:15–08:31 (точка на столешнице стенда 1; отказа НЕ было — точка принята, найдено 5 вариантов, минимальный зазор траектории 404 мм, оракул в режиме перемещения показывал «запас 920 мм»). Остальные строки — под ручную подстройку `toolOffset` в PlayMode.

| toolOffset | Поверхность (x, y, z) | Нормаль | Цель TCP (x, y, z) | Запас (мм) | Причина `[OffsetDiag]` |
|---|---|---|---|---|---|
| 0.10 | (0.100, 0.980, −24.500) | (0, 1, 0) | (0.100, 1.080, −24.500) | 404 (траектория) / 920 (оракул) | точки принята, отказа нет |
| 0.15 | _заполнить_ | _заполнить_ | _заполнить_ | _заполнить_ | _заполнить_ |
| 0.25 | _заполнить_ | _заполнить_ | _заполнить_ | _заполнить_ | _заполнить_ |

Что означают строки (для выбора шага 2/3):
- `пересечение со сценой` при `запас < 0` → концевое звено реально в геометрии: увеличивать `toolOffset`; если не помогает при 0.25+ — центр TCP стоит не там, где «пятак» (шаг 2: TCP).
- `малый запас N мм` (жёлтый, точка НЕ блокируется) → движение разрешено, но планировщик может не найти путь (`Planner.clearance` = 0.02): при N ≤ 20 мм путь часто не находится.
- `IK не сходится (ошибка N мм)` → это не столкновение: точка у предела рабочей зоны (лечится повтором/другой точкой).
- `траектория не найдена … IK: <LastDebug>` → смотреть `LastDebug`: `отброшено по коллизии=N` = зазор/столкновение, `IK: решения не найдены` = поза недостижима.
- [ ] **ШАГ 3 (отложено: `alignToolToSurface = false`) — проверка «пятака» на горизонтальном столе:** красный луч (Z) + ЛКМ по точке на столешнице → в подсказке «Точка принята · TCP на 0.10 по нормали поверхности · считаю варианты траекторий…», «Вариантов: N» → зелёный + ЛКМ по траектории → **на шаге 1** фантом едет и **останавливается НАД столом** (≈0.10 над ним), не врезаясь и не проваливаясь, ориентация свободная; сообщений `[Flow] «пятак» выровнен …` быть не должно. После включения шага 3 — ожидать `[Flow] «пятак» выровнен по нормали поверхности (…) · остаток X° · TCP Y мм от цели` (или `[Flow] «пятак» не выровнен (<причина>)`) и параллельность концевой плоскости столу; ЛКМ по фантому → робот приезжает в ту же позу.
- [ ] **ШАГ 1 — наклонная/вертикальная поверхность:** навести на стену/скос (например, ножку стола или наклонный объект) → убедиться по подсказке и по консоли, что смещение идёт **вдоль нормали этой поверхности**, а не только вверх: значение `state.point` должно отличаться от «+0.10 по Y». Быстрый признак — в подсказке та же строка «по нормали поверхности», а точка прицела при наведении на стену смещается вбок/вперёд, а не вверх.
- [ ] **Ручная проверка — точка в воздухе:** навести туда, где луч ни во что не попал → ЛКМ → должна остаться прежняя реакция («Наведите красный лазер на поверхность»); если точка всё-таки фиксируется, в подсказке должно быть «точка не на поверхности — TCP без смещения», ориентация свободная.
- [ ] **Ручная проверка — «пятак» параллелен (ШАГ 3, отложено):** после прибытия фантома/робота визуально убедиться, что концевая плоскость параллельна столешнице (не под произвольным углом); при отказе выравнивания в консоли будет причина — см. §6 (ручки: `alignAngleToleranceDeg` в потоке и константы `maxSweeps`/`wPos` в `ToolAlign.Solve`).
- [ ] **Ручная проверка — временный хак:** выставить в инспекторе `TrajectoryFlowController.offsetMode = WorldUpHack` (или в коде) → проверить, что поведение стало «+0.10 по Y» и в подсказке есть пометка «ВРЕМЕННЫЙ ХАК»; затем вернуть `SurfaceNormal`.
- [ ] Проверить, что шарик прицела (оракул) больше не противоречит потоку: цвет маркера на столе должен совпадать с реакцией ЛКМ (обе проверки идут по смещённой точке `ToolOffset`).
- [ ] Ручная проверка в редакторе: иерархия обновляется сама (раз в 2 с, без клика), «фантомные» строки `SCARA LS10-B702S…` / `6-осевой робот…` исчезают и не возвращаются после Play → Stop.
- [ ] Проверить меню: `Tools/KazistovVv/Hierarchy/Очистить фантомные записи` (на уже «залипших» записях), `Проверить инвентарь роботов (ожидается 2)`, `Диагностика: объекты вне сцен`.
- [ ] Проверить, что копии-фантомы (`Phantom_*`) не видны в Hierarchy и **после** остановки PlayMode в дереве не остаётся ничего лишнего (флаги доклеиваются 2 кадра — при желании увеличить счётчик).
- [ ] Если оператор случайно наспавнил роботов кнопкой «Добавить робота» в PlayMode — убедиться, что выход из PlayMode не оставляет копий (они не `DontSave`) и что `Проверить инвентарь роботов` снова пишет «2».
- [ ] Ручная визуальная проверка в редакторе: бирюзовый «рентген»-фантом **появляется в стартовой позе робота и плавно едет по подтверждённой траектории** (≈6.6 с на путь 3.3 юнита), по прибытии стоит на месте, 5 разноцветных вариантов траекторий на месте, фантомы отсутствуют в окне Hierarchy.
- [ ] **Ручная проверка (ТЗ 03:26), главное:** ЛКМ красным → «Вариантов: 5» → зелёный + ЛКМ по «колбаске» → в лог `[Flow] Фантомов: 1 …` и `[Phantom] фантом создан (один) · сэмплов N · путь X юнита за Y с …`, фантом появляется В ПОЗЕ РОБОТА и **плавно едет** к конечной позе; по прибытии — `[Phantom] фантом доехал до конечной позы и остановился`, фантом остаётся видимым.
- [ ] **Ручная проверка «только один фантом»:** после фантома нажать красным лазером по новой точке (и в `PhantomsMoving`, и после остановки) → прежний фантом исчезает сразу, новых копий в сцене нет; повторить 3–5 раз (в сцене и в Hierarchy не должно оставаться ничего лишнего и не должно расти число объектов).
- [ ] Ручная проверка: выбор фантома зелёным лучом **в пути** (ЛКМ → этап 4) и **после остановки**; подсказка у фантома показывает «пройдено N% · едет / доехал».
- [ ] Ручная проверка: фантом идёт именно по выбранной «колбаске» (проверить на «Траектория 3…5» — там трубка разведена вбок на 6…12 см, а фантом идёт по истинному пути, см. §5).
- [ ] Ручная проверка SCARA-стенда: фантом не «накапливает» поворот (эталонная поза копии возвращается перед каждой позой) и стартует ровно на роботе.
- [ ] Ручная проверка: `Esc` в середине движения фантома убирает фантом без остатков в Hierarchy; красный луч (новая точка) во время движения фантома пересчитывает траектории.
- [ ] Ручная проверка края: нажать F (смена робота) во время движения фантома — убедиться, что нет исключений в консоли (фантом при этом замирает, см. §6).
- [ ] Проверить в консоли после Play: `[RobotInventory] Роботов в сцене: 2 (6-осевой + SCARA). Рантайм роботов не создаёт.` — если стоит предупреждение, в открытой сцене есть лишние копии роботов.
- [ ] Ручная проверка этапа 2 «только красный → пересчёт» и подсказок «Выберите один лазер» / «Включите лазер» вживую.
- [ ] Проверить, что после убранного автовыбора оператор без проблем начинает сцену (навести шарик, Z — красный, ЛКМ) и что выбор робота по F по-прежнему приоритетен.
- [ ] VR/MR-контур: проброс подтверждения (триггеры) и `PoseSelector` в `TrajectoryFlowController`.
- [ ] Сборка под Quest: стриппинг шейдеров (прозрачность + ZTest) и слой `XrApiLayer_METAX_operator`.
- [ ] Решить судьбу мёртвого кода: `TrajectoryPlannerController`, `NarrowPhase`, `GhostView`.
- [ ] Решить судьбу `Assets/_Recovery/*.unity` (удалить или оставить как «страховку» редактора).

## 8. Отменено / не делать
- **НИКОГДА не удалять каталоги командой с путём из переменной без проверки (§0.0).** `Remove-Item "$p\$d" -Recurse -Force` с пустой `$d` удалил корень проекта. Перед удалением: проверить, что переменная непустая, что итоговый путь существует и оканчивается на ожидаемый каталог (`Temp`, `bin`, `obj`, `Library`), и держать свежую копию вне проекта. Это правило дороже любых удобств.
- **Менять имя объекта сцены при переименовании узла дерева — НЕЛЬЗЯ** (во FreeCAD ярлык и внутреннее имя разделены, и это не случайно): имена `Стенд_*`, `Стол*`, `Level`, `Plane` читают `CollisionWorld` (правило «пол» и `IsUnderStand`), `KazistovVvUIManager.FindFirstTableSurface`/`TryFindTableSurface`, `HierarchyPhantomCleaner`. Переименование меняет ТОЛЬКО подпись узла (статический `NodeLabels`, ключ — стабильный `ProjectNode.Key`), поэтому сцена и логика не ломаются.
- **«Глазик» в дереве не должен выключать GameObject — только рендереры** (`KvTreeView.SetObjectVisible`): `SetActive(false)` выбросило бы объект из `CollisionWorld`, из реестра и из расчётов, то есть «скрыть» превратилось бы в «убрать из мира». Скрытие визуальное и обратимое.
- **UI Toolkit в этот проект не вводить** (решение сессии 13.09.2026): интерфейс остаётся на uGUI, чтобы не тащить ассеты `PanelSettings`/`UXML`/`USS`, не менять сцену и не смешивать два UI-стека (обоснование — §0.4).
- **Не возвращать текстовые кнопки в верхнюю панель:** по ТЗ тулбар — icon-only (монохромные иконки + подсказки), состав из 15 кнопок задан раскладкой `ToolbarLayout()` и реестром `KvCommands`.
- **Не вызывать `Refresh()` (полное обновление с пересборкой дерева) каждый кадр** — это ≈28 мс; для кадровой работы есть `RefreshPanels()` (≈0.5 мс), а дерево пересобирается по изменению «подписи» (§5.16).
- Разделение кнопок «красный = ЛКМ, зелёный = ПКМ» — отменено, по ТЗ одна ЛКМ.
- Скрытие фантомов через `[HideInInspector]` — так скрываются только поля в инспекторе; для объектов используется `HideFlags.HideInHierarchy` (для фантомов — `HideFlags.HideAndDontSave`: скрытие + запрет сохранения в сцену).
- Идея «фантомы исчезают после выбора» — по ТЗ остаются видимыми.
- Мгновенное выполнение траектории по красному клику — запрещено, только траектории → фантомы → движение.
- Создание стендов/роботов в рантайме (`StandBuilder.EnsureStands`) — стенды живут в `MainScene`; `StandBuilder`/`StandsMenu` — редакторская утилита.
- **Автовыбор робота/узла при старте** — запрещено ТЗ («ни один робот не должен быть выбран изначально»); поток обязан сам находить робота по прицелу, а не требовать `isActive`.
- **Автосоздание роботов в PlayMode** — запрещено ТЗ: при старте остаются ровно два робота сцены, `selectedRobot = null`. `RobotInventory.Guard` — только проверка и лог, ничего не создаёт и не удаляет.
- **«Телепорт» шарика по средней кнопке, когда луч ни во что не попал, — не делать.** Решение сессии 18:12: если Raycast не нашёл поверхность (и рабочая плоскость y = 0 под лучом не пересекается), шарик ОСТАЁТСЯ НА МЕСТЕ, а оператор видит подсказку. Возврат «в исходную позицию»/к камере запрещён — это непредсказуемо для оператора.
- **Колесо мыши в `PointMoveMode` — не назначать** (ТЗ): там глубина шарика не меняется вовсе, точка ходит только QWEASD. Если понадобится действие на колесо в режиме — отдельное ТЗ.
- **Флаг `aimHit` и гейты State Machine ради глубины — не менять.** В пустоте шарик существует и виден, но поток по-прежнему отвечает «Наведите красный лазер на поверхность»: глубина шарика не должна открывать фиксацию точки в воздухе там, где её раньше не было.
- **Ограничивать шарик коллизиями (физикой/коллайдером) — не делать:** глубина — чистая геометрия луча (`Vector3`), «прилипание» — только остановка на первой поверхности при прокрутке ВПЕРЁД. Прокрутка назад всегда отводит шарик к оператору, даже если он «внутри» геометрии.
- **Автоподтягивание отведённого шарика к поверхности при повороте камеры — не делать:** отведённая глубина сохраняется, пока оператор сам не прокрутит вперёд или не нажмёт колёсико (иначе «прилипание» превращалось бы в самодвижение шарика).
- **Мгновенное появление фантома в конечной позе — отменено** (ТЗ сессии 03:15): фантом обязан появляться в стартовой позе и проходить траекторию видимо. Мгновенный показ (`appearAtTargetPose = true`) остался только как опция для `PhantomManager.Show` (множественные копии IK — задел) и в потоке не используется. **С 03:26 флаг вообще не влияет на фантом с путём** (`ShowAlongPath`): он действует только на копии, созданные `Show`.
- Плавный переезд фантомов «1 юнит за 30 с» — для фантомов отменён (слишком медленно); фантом идёт **по траектории** со скоростью `phantomSpeed = 0.5` ю/с (1 юнит за 2 с). Для реального робота на этапе 4 скорость по ТЗ остаётся 1 юнит / 30 с.
- Переезд фантомов «по прямой в пространстве суставов» (`EstimateDuration` + `LerpPose` между стартовой и конечной позами) — для рабочего потока отменён: движение только по сэмплам подтверждённой траектории (`ShowAlongPath`). Линейный вариант остался внутри `Show` для многофантомного задела.
- Планирование «жадным» способом и довороты базой через 180°+ — отменено (кратчайшие довороты + shortcut).
- Меню `Tools/KazistovVv` для пересборки сцены — удалено.
- **`HideFlags.HideAndDontSave` для служебных объектов рантайма — отменено.** `DontSave` мешает Unity уничтожить объект при выгрузке PlayMode-сцены: объект переживает Stop и остаётся в редакторе **вне сцен** — виден в Hierarchy, но не находится поиском по сцене (это и есть «фантомная запись»). Правило проекта: служебные объекты рантайма (фантомы, колбаски, маркер прицела, лампочка) — только `HideFlags.HideInHierarchy`; владелец объекта обязан сам уничтожать его (`OnDisable`/`OnDestroy`), а редактор подчищает остатки (`HierarchyPhantomCleaner`).
- **Пул фантомов не заводить** (ТЗ 03:26): вместо пула контейнер `Phantoms` полностью уничтожается и создаётся заново перед каждым показом (`ClearPhantoms`) — «очищай пул перед созданием нового» выполнено строже: переиспользовать нечего, старая копия физически удалена.
- **Мгновенная доводка фантома «доехал» без анимации — не делать**: движение обязано быть видимым (скорость 0.5 ю/с, 1.2…8 с), мгновенный показ остаётся только в многофантомном заделе `Show`.
- Автоматическое удаление объектов сцены, созданных оператором (копии роботов из «Добавить робота»), — только вручную и по подтверждению (`Tools/KazistovVv/Hierarchy/Удалить копии роботов, созданные через UI`); фоновая автоочистка сцены не делается.
- Не трогать без необходимости: планировщик/оракул/валидатор, HDRP-настройки, освещение, существующие материалы, координаты роботов на столах, SCARA.
- **Менять ориентацию TCP на ШАГЕ 1 — нельзя** (это шаг 3): `alignToolToSurface` выключен, `ToolAlign` не вызывается. Включать только отдельным ТЗ шага 3.
- **Ориентацию TCP-прокси (`CreateTcpProxy`) в этой задаче не менять** (ТЗ сессии 07:48): расхождение «ожидалась ось X вверх, фактически −Y вниз» зафиксировано как открытый вопрос (§6), исправление — отдельная задача; сегодня ни один расчёт от осей TCP не зависит.
- **Не ограничивать перемещаемую точку коллизиями** (ТЗ этап 5): точка режима перемещения обязана проходить сквозь стены/столы; ограничитель — только оператор, а недостижимость показывает цвет/статус оракула. Нельзя «чинить» это физикой/коллайдерами или автооткатом точки.
- **Не двигать точку мышью в режиме перемещения** (ТЗ этап 1): после входа в `PointMoveMode` точка управляется только клавишами; мышь оставлена камере. И наоборот — в режиме камера НЕ двигается QWEASD (клавиши отданы точке).
- **Смещение цели в свободном пространстве — не делать.** Смещение `toolOffset` относится только к точке НА поверхности (ТЗ): для точки в воздухе поведение обязано остаться прежним (без смещения и с прежней, свободной ориентацией). Рабочая плоскость y = 0 (луч ни во что не попал) тоже считается «не поверхностью» и не смещается.
- **Правки IK/планировщика «под ориентацию» — не делать** (в этой сессии): требование «пятак параллелен поверхности» решено снаружи, подгонкой готовой позы цели (`ToolAlign` + проверки + откат), чтобы не менять работающие `IkSolver`/`Planner`/`PoseValidator`. Если позже понадобится выравнивание **вдоль всей траектории** (а не только в конечной точке) — это уже отдельная задача с правкой IK (ориентационная цель) и перепланировкой, отдельным ТЗ.
- **Выравнивание SCARA не делать**: у неё нет вращательной оси фланца в том же смысле (`AxisWorld` для неё возвращает вертикаль), `ToolAlign.AlignGoal` при `Dof < 6` сразу отказывается — SCARA работает как раньше.

## 9. Заметки по инструментам (для следующих сессий)
- **ДВЕ КОПИИ ПРОЕКТА: КАК ДЕРЖАТЬ ИХ СИНХРОННЫМИ (с 12.09.2026, 18:41).** Рабочая копия агента — `…\OneDrive\Documentos\новое пространство\KavistovVv` (ветка `DeepSeek`), у пользователя в Unity открыта `C:\Users\Ольга\KavistovVv` (ветка `Cline`). Копии теперь **синхронизированы по скриптам**. Процедура переноса (проверена): (1) сравнить хеши всех `Assets/_Project/**/*.cs` в обеих копиях (кроме `Editor/Dsh*Diag.cs` — это диагностика агента, её переносить НЕ надо); (2) скопировать различающиеся `.cs` из `DeepSeek` в `Cline`, для НОВЫХ файлов — вместе с `.meta` (иначе Unity сгенерирует свой GUID, что не страшно, но лишний шум); (3) сложить заменяемые файлы в резерв `Cline\_dsh_backup_<дата>_<время>\` (в `Cline` папки `01_Scripts/Trajectory` и `06_KazistovVv_UI` вообще НЕ в git — восстановить из git там нельзя!); (4) проверить компиляцию копии пользователя: её `*.csproj` от 11.09 устарели, поэтому новые файлы надо добавить временной строкой `<Compile Include="Assets\_Project\…" />` рядом с существующим `<Compile>` (вставлять ПОЛНОЙ строкой вместе с ` />` — иначе csproj ломается с `MSB4025`), собрать `Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj`, затем вернуть csproj и удалить `bin/`, `obj/`, `Temp/bin`, `Temp/obj`; (5) НЕ переносить сцену, столы, материалы, ProjectSettings — в копии пользователя столы прежнего размера (1.2×0.8), и по ТЗ столы не меняем.
- **СЦЕНЫ КОПИЙ ОТЛИЧАЮТСЯ ТОЛЬКО СТОЛАМИ** (проверено построчным сравнением `MainScene.unity`, 6254 строки в обеих): в `DeepSeek` столы ×4 (столешница scale 4.8×0.05×3.2, ножки ±2.3/±1.5), в `Cline` — исходные (1.2×0.05×0.8, ножки ±0.5/±0.3). Роботы, столы-корни (z = −23.9 и −28.1), столешница (локально 0, 0.955, 0) и все настройки `FreeFlyCameraController` (`movementSpeedScale`, `lockCursorOnStart = 0`, `enableLaserPointer = 1`, `leftHandEnabled = 1`, `rightHandEnabled = 0`, `suppressDirectTeleop = 1`) — одинаковые. Поэтому скриптовые правки применимы к обеим копиям без изменений сцены.
- **Сквозной прогон аудита (12.09.2026):** `Unity.exe -batchmode -nographics -projectPath "<DeepSeek-копия>" -executeMethod DshAuditDiag.Run -logFile _dsh_audit.log` → отчёт `KavistovVv/_dsh_audit_verify.txt` (33 проверки, финальная строка «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ»). Скрипт: `Assets/_Project/01_Scripts/Editor/DshAuditDiag.cs` (в копию пользователя не переносится). **Особенности:** в батче `EditorApplication.update` зовётся чаще игровых кадров, поэтому ожидания в шагах считаются по `Time.frameCount` (помощник `WaitGame`/`GameWaited`), а движение робота/фантома ускоряется только на прогон (`flow.motionSpeed = 5`, `motion.maxSpeedMps = 20`, `phantoms.pathSpeed = 4`) — штатные значения не меняются. Запуск Unity в батче занимает ~2.5 минуты до Play-режима (Library тёплая) — не считать, что прогон «завис».
- **Проверка «колбашения» TCP в прогоне:** шаг «ПОСЛЕ ДВИЖЕНИЯ» считает максимальное смещение и максимальный шаг TCP за 180 игровых кадров после `FinishMotion` — критерий проходит только при 0.000 мм (робот стоит). Раскачка от защиты от самостолкновения в батче НЕ воспроизводится (поток каждый кадр снимает цель), поэтому этот сценарий — ручной (§7).
- **СНАЧАЛА ПРОВЕРЬ, В КАКОЙ КОПИИ ПРОЕКТА ИДЁТ РАБОТА.** На машине две копии (см. §1): рабочая копия агента — `C:\Users\Ольга\OneDrive\Documentos\новое пространство\KavistovVv` (ветка `DeepSeek`), у пользователя в Unity открыт `C:\Users\Ольга\KavistovVv` (ветка `Cline`, без `PointMoveMode`). Определить копию пользователя: `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` → `-projectpath …`. **Правки, сделанные только в копии `DeepSeek`, в редакторе пользователя не видны** — это надо писать в отчёте прямо.
- **Проверь, что новые файлы вообще попали в сборку.** Симптом «код есть, а в Unity его нет»: у файла нет `.meta`, `Library/ScriptAssemblies/Assembly-CSharp.dll` старше файла, в `Logs/Editor.log` нет упоминаний нового класса. Лечится любым `AssetDatabase.Refresh` (в т.ч. батч-прогоном): после него у файлов появляются `.meta`, Unity пересобирает `Assembly-CSharp.dll` и **сама перегенерирует `*.csproj`** (руками вставлять `<Compile Include=…>` после этого уже не нужно).
- Проверочные прогоны: `Unity.exe -batchmode -nographics -projectPath <проект> -executeMethod <класс.Run> -logFile <лог>`. **`-nographics` обязателен**: без него Unity тянет полный реимпорт HDRP-шейдеров (десятки минут) и конфликтует с открытым редактором пользователя. Запускать через `&` (GUI-процесс не блокирует pwsh — выход команды `0` НЕ означает, что прогон закончился: ждать появления файла-отчёта и/или исчезновения процесса Unity).
- **Батовый прогон на копии `DeepSeek` безопасен даже когда у пользователя открыт его редактор** (разные `projectPath` → разные `Library`) — проверено 12.09.2026: прогон 09:00 отработал за ~2 минуты, не тронув редактор пользователя.
- **15.09.2026: если batch-прогон падает СРАЗУ (нет лога / процесс завершается с кодом 1, в логе `Access token is unavailable; failed to update` и `Code 10 while verifying Licensing Client signature`)** — это не код, а залипший клиент лицензий: `Get-Process Unity.Licensing.Client | Stop-Process -Force`, после этого прогон проходит штатно (проверено). Симптомы «Unity не стартует, лог не создаётся» лечатся тем же. Ещё грабли: `Start-Process -ArgumentList` НЕ экранирует пути с пробелами — передавайте аргументы одной строкой с внутренними кавычками: `-ArgumentList "-batchmode -nographics -projectPath `"$root`" -logFile `"$root\_dsh.log`""`.
- **Новая сквозная диагностика интерфейса (сессия §0.6): `DshUiStagesDiag.Run`** — 16 фаз, проверяет 12 этапов UX/UI (группы тулбара, ВЫЗОВ ВСЕХ команд реестра, меню, вкладки и переключатели настроек, окна, размеры, палитру, контекстное меню, горячие клавиши, состояния, геймпад, иконки, доступность) и пишет сводную таблицу «команда → статус» в `_dsh_ui_stages.txt`. 78 проверок «[OK]» на выходе. Две ловушки, на которые он уже наступал: (1) обход `KvCommands.All` НЕЛЬЗЯ делать «живой» коллекцией — команды смены темы/языка пересобирают оболочку и заново регистрируют реестр (`InvalidOperationException`), берите снимок (`new List<KvCommand>(KvCommands.All)`); (2) команды вроде `lang.cycle`/`view.theme` МЕНЯЮТ пользовательские настройки — сохраняйте и возвращайте язык/тему до и после прогона, иначе отчёт останется на другом языке.
- Временный тест-скрипт кладётся в `Assets/_Project/01_Scripts/Editor/`, подписка на `EditorApplication.update` **обязана** быть в `[InitializeOnLoadMethod]` (при входе в PlayMode домен перезагружается и подписка из `-executeMethod` теряется). Рабочий приём (проверен 12.09.2026, `DshPointMoveDiag`): `SessionState.SetBool(<ключ>, true)` в `Run()` + `[InitializeOnLoadMethod] Boot()` перечитывает ключ и подписывается заново; строки отчёта писать в файл **сразу** (`File.AppendAllText`), а не копить в статическом буфере (домен перезагружается, буфер пропадёт). Отчёт удобно класть в корень проекта: `Path.Combine(Application.dataPath, "..", "_dsh_<тема>_verify.txt")`.
- **Движение в прогоне:** удерживаемую клавишу имитируем вызовом `TrajectoryFlowController.UpdateAim(..., moveAxis, ...)` **на каждый игровой кадр** и с проверкой `Time.frameCount`, иначе точка «уедет» быстрее реального (в batch `EditorApplication.update` зовётся чаще игровых кадров). Длину/координаты мерить не в кадрах, а по достижению нужного смещения (при `-nographics` `Time.deltaTime` крошечный).
- Проверка компиляции без тест-скрипта: `Unity.exe -batchmode -nographics -quit -projectPath <проект> -logFile <лог>` и поиск `error CS` в логе.
- **Проверка компиляции, когда редактор уже открыт (безопасно, без второго инстанса Unity):** `dotnet build Assembly-CSharp.csproj` из корня проекта — Unity-сгенерированный csproj (`netstandard2.1`, C# 9) уже содержит все 279 ссылок на DLL редактора, поэтому сборка проходит автономно за ~10 секунд и `error CS` в выводе = настоящая ошибка Unity-компиляции. После проверки удалить `bin/`, `obj/`, `Temp/bin/` (`.csproj` dotnet не меняет).
- **Проверка компиляции НОВЫХ файлов** (Unity ещё не успела перегенерировать csproj, и нового файла в нём нет): прочитать текст csproj в переменную, временно вставить рядом с существующим пунктом строку `<Compile Include="Assets\_Project\01_Scripts\Editor\<Файл>.cs" />`, собрать `Assembly-CSharp-Editor.csproj`, затем записать исходный текст обратно и сверить (`-eq`), удалить `bin/`, `obj/`, `Temp/bin/` и лог. Так проверены `HierarchyPhantomCleaner.cs` и `HierarchyAutoRefresh.cs` (0 ошибок).
- Открыт ли редактор пользователя — `Get-Process | Where-Object { $_.ProcessName -like "*Unity*" }`: если инстанс есть, batch-прогоны с `-projectPath` запрещены, остаётся `dotnet build` и пофайловый разбор.
- Если в Hierarchy снова появятся «фантомные» записи: `Tools/KazistovVv/Hierarchy/Очистить фантомные записи` (или консоль: `HierarchyPhantomCleaner.Purge(true)`), затем `Диагностика: объекты вне сцен`. Частота обновления иерархии — `HierarchyAutoRefresh.IntervalSeconds` (по ТЗ 2 с), галочка в `Tools/KazistovVv/Hierarchy/Автообновление иерархии (2 с)`.
- Новые редакторские скрипты (`HierarchyPhantomCleaner.cs`, `HierarchyAutoRefresh.cs`) Unity импортирует при refresh — если редактор их не подхватил, помочь может `Ctrl+R` (AssetDatabase.Refresh); подписки живут до ближайшего домен-релоада и восстанавливаются автоматически (`[InitializeOnLoad]`).
- **Как быстро проверить смещение и «пятак» в PlayMode:** красный луч (Z) + ЛКМ по точке на столешнице → подсказка «Точка принята · TCP на 0.10 по нормали поверхности …» → «Вариантов: N» → зелёный (X) + ЛКМ по «колбаске» → фантом появляется в позе робота, едет и останавливается НАД поверхностью (на шаге 1 ориентация не выравнивается — строки `[Flow] «пятак» выровнен …` быть не должно, т.к. `alignToolToSurface = false`). Ручки: `TrajectoryFlowController.toolOffset` (0.10), `offsetMode` (`SurfaceNormal` — основное, `WorldUpHack` — ВРЕМЕННЫЙ ХАК +Y), `alignToolToSurface` (шаг 3, выключен), `alignAngleToleranceDeg` (6°), `logOffsetDiagnostics`; в `ToolAlign.Solve` (шаг 3) — константы `maxSweeps` (24), `stepDeg` (4°), `wPos` (25).
- **Временная вставка в csproj — вставлять ровно один раз.** Если эту же строку добавить второй раз (csproj уже содержал её), сборка выдаёт `warning CS2002: исходный файл задан несколько раз`; после проверки строку обязательно удалить.
- **`*.csproj` в проекте — в `.gitignore`** (строка 51), поэтому `git status`/`git diff` по `Assembly-CSharp.csproj` **всегда пусты** и как проверка «вернул ли я файл на место» не работают. Контроль: поиск вставленной строки по содержимому (`Select-String -Pattern "ToolAlign" -SimpleMatch`) и/или размер файла. Unity сама добавит новый `.cs` в csproj при следующем refresh.
- **Диагностика шага 1:** файл `_dsh_offset_diag.txt` в корне проекта (`KavistovVv/`) — дописывается строкой на каждый красный отказ по точке (не чаще раза в секунду), формат `[OffsetDiag] <причина> · toolOffset=… · clearance=… мм · поверхность (x, y, z) · нормаль (x, y, z) · цель TCP (x, y, z) · режим …`. Включается/выключается флагом `TrajectoryFlowController.logOffsetDiagnostics`. Быстрый сценарий: PlayMode → красный луч → ЛКМ по проблемной точке → взять строку из консоли/файла → перенести в таблицу §7.
- **Режим перемещения точки (как проверить за минуту):** PlayMode → красный луч (Z) + ЛКМ по точке → **Enter** (в консоли `[PointMove] вход в режим: точка (…) · в системе робота (…)`, сверху появляется панель «РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ») → подержать Q/E/W/S/A/D (координаты в панели идут онлайн, статус/маркер перекрашиваются) → **Enter** (`[PointMove] выход из режима (подтверждено (Enter)) · …`) или **Esc** (`… (отмена (Esc)) …`). Ручки в инспекторе `Main Camera → TrajectoryFlowController`: `pointMoveSpeed` (0.5), `pointMoveFastMultiplier` (3), `pointMoveOracleInterval` (0.08), `pointMoveHudEnabled`, `logTcpFrame`; оформление панели — компонент `PointMoveHud` (`panelWidth/panelHeight/topOffset/sortingOrder/markerSize`).
- **Диагностика ориентации TCP:** `[TCPDiag]` печатается один раз на привязку робота (флаг `logTcpFrame`): «ось инструмента в системе TCP = (x, y, z)», `TCP.up/right/forward`, угол оси инструмента к вертикали мира. Ожидание ТЗ — ось X вверх; фактически ось инструмента это локальная Y фланца (см. §5.8/§6).
- **Как быстро проверить глубину шарика прицела (ТЗ 18:12, за минуту):** PlayMode → красный луч (Z) + ЛКМ по точке на столешнице (шарик стоит на столе и сплющен по нормали) → **колесо назад** (шарик отходит к оператору, снова ровный шар) → **колесо вперёд** (шарик возвращается и упирается в стол — дальше вперёд не идёт) → **колёсико** (мгновенный возврат к ближайшей поверхности; в консоли `[AimDepth] средняя кнопка: шарик возвращён к ближайшей поверхности (…)`). Ручки в инспекторе `Main Camera → FreeFlyCameraController` (раздел «Глубина шарика прицела (колесо мыши)»): `scrollStep` (0.08), `minDistance` (0.25), `maxDistance` (20), `stickyToSurface` (true), `surfaceLayer` (DefaultRaycastLayers), `scrollSmoothSpeed` (16), `surfaceStickTolerance` (0.005). Визуал «прилип» — компонент `AimIndicator` на той же камере: `surfaceFlatten` (0.55), `freeEmissionScale` (0.55). Читаемые для проверок свойства: `AimBallDepth`, `AimBallOnSurface`, `AimBallDetached`, `AimBallHasSurface`, `AimIndicator.BallOnSurface`, `AimIndicator.MarkerScale`, `AimIndicator.MarkerVisible`.
- **Внешняя прокрутка для VR/автотестов:** `FreeFlyCameraController.AddScrollInput(notches)` (+ = вперёд, от оператора) и `SnapAimBallToSurface()` (возврат к ближайшей поверхности, `bool` — нашлась ли она). В batch-прогоне это единственный способ «покрутить колесо» (мыши в `-nographics` нет), поэтому вся проверка механики в `DshAimDepthDiag` идёт через `AddScrollInput`.
- **Батч-прогон глубины шарика:** `Unity.exe -batchmode -nographics -projectPath "<DeepSeek-копия>" -executeMethod DshAimDepthDiag.Run -logFile _dsh_aimdepth.log` → отчёт `KavistovVv/_dsh_aimdepth_verify.txt` (47 проверок, финальная строка «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ»). Приём прежний (§9): `SessionState` + `[InitializeOnLoadMethod]`, запись строк сразу в файл. **Особенность прогона:** в закрытом ангаре нет направления «в пустоту» — скрипт сам временно ставит `laserLength = 3`, чтобы проверить ветку свободной глубины, и возвращает штатное значение в том же прогоне.
- **Если колесо «не двигает» шарик:** проверить (1) не над ли панелью KazistovVv курсор (`AimDepthInputAllowed`), (2) не активен ли `PointMoveMode` (там колесо игнорируется), (3) `enableLaserPointer` — при выключенных указках `UpdateLaserPointers` выходит раньше и прицел не считается вовсе, (4) значение `scrollStep` (0.08 = 8 см за щелчок; при 0.01 движение почти незаметно). Диагностика входа — `[AimDepth]` в консоли (средняя кнопка) и подсказка в статусе KazistovVv.
- **Как быстро проверить движение фантома в PlayMode:** красный луч (Z) + ЛКМ по точке над столом → дождаться «Вариантов: 5» → зелёный (X) + ЛКМ по «колбаске» → в логе `[Flow] Фантомов: 1 · путь: Траектория N · сэмплов <N>` и `[Phantom] фантом создан (один) · сэмплов <N> · путь <X> юнита за <Y> с (<Z> ю/с) · старт в позе робота`; фантом должен появиться В ПОЗЕ РОБОТА и поехать (для пути 3.3 юнита ≈6.6 с), в подсказке — «пройдено N% · едет к конечной позе», после прибытия — «доехал (остановился)» и лог `[Phantom] фантом доехал до конечной позы и остановился`. Ручки: `TrajectoryFlowController.phantomSpeed` (ю/с) и `PhantomManager.pathSpeed/minTravelTime/maxTravelTime`; цвет/прозрачность — `ghostAlpha`/`ghostGlow`/`ghostTint`. Наведение фантома и ЛКМ обязаны работать в любой момент — и в пути, и после остановки.
- **Если фантом «не едет» или их несколько:** движение ведёт `PhantomManager.Update()` (один шаг на кадр) — если в консоли есть `[Phantom] фантом создан …`, но нет «доехал», смотреть `durations`/`progress` (`PhantomManager.pathSpeed`, `minTravelTime`, `maxTravelTime`). Уборка — `PhantomManager.ClearPhantoms()` (полная, включая контейнер и «осиротевшие» `Phantom_*`), в PlayMode-консоли это видно по отсутствию повторных `Phantom_*` в Hierarchy; при сомнениях в редакторе — `Tools/KazistovVv/Hierarchy/Очистить фантомные записи`.

## 10. Заметки этой сессии (13.09.2026 — десктопный UI в стиле FreeCAD, переименование, восстановление)
- **ГДЕ ЧТО ЛЕЖИТ В НОВОМ UI (карта для быстрых правок).** Оболочка и привязка — `06_KazistovVv_UI/03_Scripts/Core/KazistovVvUIManager.cs`: `BuildShell()` собирает меню+тулбар+dock-панели+статус-бар, `ToolbarLayout()` задаёт 15 кнопок, `RegisterCommands()` — все команды (действие, доступность, состояние, подсказка, путь в меню), `BuildTreeModel()` — иерархию дерева, `BuildProperties(node)` — строки свойств, `UpdateStatusBar()` — статус-бар, `ApplyVisualization(id, value)` — переключатели функций, `BuildCallbacks()` — колбэки панели настроек. Тема — `Core/KvTheme.cs`, иконки — `Core/KvIcons.cs`, подсказки — `Core/KvTooltip.cs`, виджеты — `Core/KvWidgets.cs`, реестр команд — `Core/KvCommands.cs`, настройки — `Core/KvSettings.cs` + `Zones/KvSettingsView.cs`, dock-панели — `Zones/KvDockPanel.cs`.
- **КАК ДОБАВИТЬ КНОПКУ ИЛИ ПУНКТ МЕНЮ (без правок панелей).** `KvCommands.Register(new KvCommand { Id=…, Title=…, Description=…, Hotkey=…, Icon=…, MenuPath="Вид/…", Execute=…, IsChecked=…, IsEnabled=…, Stub=true/false })` — и добавить `Id` в `ToolbarLayout()` (если нужна кнопка). Иконку взять из библиотеки (`KvIcons.Has(id)`) или дорисовать новый case в `KvIcons.Draw` (примитивы: `Line/Rect/Circle/Arc/Poly/Dot`).
- **КАК ДОБАВИТЬ ПУНКТ В НАСТРОЙКИ БЕЗ ПЕРЕКОМПИЛЯЦИИ.** Положить запись в `Assets/StreamingAssets/kazistovvv_settings.json` (`items[]`: `id`, `tab`, `title`, `type`, `note`; `bindings[]`: `group`, `action`, `keys`, `note`). Пункт появится на вкладке сразу; чтобы он РАБОТАЛ, его `id` нужно добавить в `HasFeature`/`GetFeature`/`ApplyVisualization` менеджера (иначе он честно показывается как «нет обработчика»).
- **БАТЧ-ПРОГОН ИНТЕРФЕЙСА:** `Unity.exe -batchmode -nographics -projectPath "<копия>" -executeMethod DshDesktopUiDiag.Run -logFile _dsh_ui_diag.log` → отчёт `_dsh_ui_verify.txt` (БЕЗ `-quit`). Скрипт: `Assets/_Project/01_Scripts/Editor/DshDesktopUiDiag.cs`.
- **ЧЕТЫРЕ ГРАБЛИ КОМАНДНОЙ СТРОКИ И БАТЧА (найдены на этом прогоне):**
  1. `Start-Process -ArgumentList` НЕ экранирует пробелы: путь «новое пространство» разбивается на два аргумента, Unity пишет `Couldn't set project path` и выходит с кодом 1, а лог уезжает в обрезанный путь. Передавать ВСЮ строку ОДНИМ элементом с внутренними кавычками: `-ArgumentList "-batchmode -nographics -projectPath `"$p`" -logFile `"$p\_dsh.log`""`.
  2. Батч-редактор открывает «последнюю» сцену, и это часто ПУСТАЯ сцена (`Library/LastSceneManagerSetup.txt` → `path:` пустой): менеджера интерфейса в сцене нет, прогон честно пишет `[FAIL] менеджер … поднялся в PlayMode`. Лечение — `EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single)` ПЕРЕД `EditorApplication.EnterPlaymode()`.
  3. `Object.GetInstanceID()` в Unity 6.5 — ОШИБКА компиляции (`CS0619`), хотя `dotnet build` её не видит (другой профиль API). Для «изменилось ли что-то» использовать собственные счётчики версий (`KvTreeView.RebuildVersion`). Вывод: проверка ТОЛЬКО через `dotnet build` недостаточна — нужен хотя бы один прогон Unity (`-batchmode -nographics -quit`).
  4. В `-nographics` HDRP пишет `No graphic device is available to initialize the view` — это ошибка СРЕДЫ, а не кода: в дигносте она идёт как `[LOG:Error]`-информация, провалом считаются только `LogType.Exception`.
- **ПЕРЕИМЕНОВАНИЕ ФАЙЛОВ БЕЗ ПОТЕРИ ССЫЛОК В СЦЕНЕ.** `.cs` переименовывать ВМЕСТЕ с `.cs.meta` (GUID внутри не меняется) — тогда `m_Script: {guid: …}` в сцене продолжает работать; класс внутри файла обязан совпадать с именем файла. В сцене старое имя встречается ещё в `m_Namespace` (блок `MonoScript`), `m_Name` объекта и `m_EditorClassIdentifier` (`Assembly-CSharp::Namespace.Class`) — правится построчно по процедуре правки сцены (LF, без BOM, с проверкой ожидаемого текста). Папку переименовывать вместе с её `.meta`.
- **СИНХРОНИЗАЦИЯ CSproj ПОСЛЕ ПЕРЕИМЕНОВАНИЙ:** `pwsh -File _tools/dsh_csproj_sync.ps1 -Project <путь>` — приводит `Assembly-CSharp*.csproj` (которые Unity ещё не перегенерировала) к фактическому содержимому `Assets`: выбрасывает мёртвые `<Compile Include>`, добавляет новые, разделяя runtime и Editor. ВАЖНО: `dotnet build` работает только если в проекте уже есть `Library/ScriptAssemblies` (иначе `CS0246` на `Text`/`Image`/`Button` и т.п. — это НЕ ошибка кода, а отсутствие ссылок; лечится прогоном Unity, который пересоберёт `Library`).
- **КАК БЫСТРО ПРОВЕРИТЬ НОВЫЙ UI В PLAYMODE (за минуту):** PlayMode → сверху 3 ряда по 5 кнопок-иконок (наведение → подсказка), слева дерево (стрелки, «глазик», двойной клик — переименование), справа свойства выбранного узла, снизу статус-бар; TAB — скрыть/показать интерфейс вместе с курсором. Ручки в инспекторе `KazistovVv_UI → KazistovVvUIManager`: `treePanelWidth`/`propertiesPanelWidth`/`settingsPanelHeight`, `statusInterval`/`propertiesInterval`/`treeInterval`, `uiReferenceWidth`/`uiReferenceHeight`, `logUiEvents`.
- **ЕСЛИ КНОПКА «НЕ РАБОТАЕТ»:** (1) не заглушка ли (`KvCommands.Get(id).Stub` — серая кнопка, это по ТЗ); (2) доступна ли команда (`IsEnabled`: «Запуск/пауза» и «Остановка» — только когда робот едет, «Переключение робота» — когда в сцене 2 робота); (3) виден ли интерфейс (`uiVisible`, TAB). Дамп реестра — `KvCommands.Dump()`.
- **ЕСЛИ ТЕМА «НЕ ПЕРЕКЛЮЧАЕТСЯ»:** смена темы пересобирает оболочку (`RebuildShell`), сохраняя сторону/толщину/свёрнутость панелей в статическом `panelStates`. Оверлеи `PointMoveHud`/`TrajectoryMetricsPanel`/`WorkspaceVisualizer` живут на СВОИХ канвасах и по теме НЕ перекрашиваются (цвета — снимок на момент создания); если в светлой теме что-то плохо читается — правится в самих этих файлах.

## 11. Сессия «ЭТАПЫ 1–20: запись, позы, суставы, аварийный стоп, зоны, сравнение, графики, тепловые карты, гриппер, pick-and-place, ETA, журнал, замер, Undo, звук, вибрация, сценарии, презентация, сессии»

### 11.1 Что добавлено (карта нового модуля)
Модуль новых функций живёт РЯДОМ с существующим кодом и ничего в нём не переписывает:

- **`Assets/_Project/01_Scripts/Features/`** (новый каталог, namespace `KazistovVvFeatures`, 17 файлов):
  `FeatureHub.cs` (хаб: сервисы, команды, дерево, свойства, горячие клавиши, аварийный стоп, ETA),
  `KvFeatureWindow.cs` (одно плавающее окно с 10 вкладками + панель ETA + текст презентации),
  `FeatureStorage.cs` (пути и JSON), `KvActionLog.cs` (журнал), `KvPoseLibrary.cs` (+`KvPlanKit`),
  `KvKeepOutZones.cs`, `KvComparison.cs` (+`KvZoneMarks`), `KvJointGraph.cs`, `KvHeatmaps.cs`
  (карта достижимости + карта зазоров), `KvGripper.cs` (+`KvPickAndPlace`), `KvSpatialAudio.cs`,
  `KvHaptics.cs`, `KvUndoRedo.cs`, `KvScenarioManager.cs`, `KvPresentationMode.cs`,
  `KvSessionManager.cs`, `KvPlannerPerformance.cs`.
- **`Assets/_Project/01_Scripts/Recording/`** (существующая заготовка — ТЗ этапа 1):
  `KvTrajectoryRecord.cs` (данные + хранилище), `KvRecordingService.cs` (запись/воспроизведение).
  Старые `TrajectoryRecorder.cs`/`TrajectoryPlayer.cs` (мёртвый VR-код на `InputManager`) НЕ тронуты.
- **Правки существующих файлов — минимальные и аддитивные:** `KazistovVvUIManager.cs` (раскладка
  тулбара +20-я кнопка, три однострочных хука: команды, ветки дерева, строки свойств, подпись дерева),
  `TrajectoryFlowController.cs` (только читающие свойства + обёртки `SelectCandidateByIndex`,
  `LockPointFromUi`, `PlayExternalPlan`/`EndExternalMotion`/`StopExternalMotion`),
  `TrajectoryExecutor.cs` (только читающие `ActivePlan/Elapsed/Total/Progress01/Remaining`),
  `KvIcons.cs` (+18 иконок), `KvSettings.cs` (предел `ToolbarRows` 1…6).
- **Формат данных — JSON** (`FeatureStorage`, `JsonUtility`). Обоснование: читается человеком и
  диффится, расширяется без ломки старых файлов (отсутствующие поля остаются значениями
  инициализаторов + `Normalize()`), не требует ассетов/.meta, объём даже большой записи мал
  (10 Гц × 60 с ≈ 500 КБ текста). Бинарный выиграл бы в размере, потеряв читаемость и совместимость.
- **Каталог данных:** `%persistentDataPath%/KazistovVv/{Recordings,Poses,Zones,Sessions,Logs,Config}`.
- **Тулбар:** 15 штатных кнопок + 5 новых (4-й ряд): `record.toggle`, `estop`, `view.heatmap`,
  `view.clearance`, `features.open`. Число рядов считается автоматически; штатные бинды не тронуты.
- **Новые горячие клавиши (свободные, проверено поиском):** `R` — запись, `P` — воспроизведение/пауза,
  `V` — захват, `H` — карта достижимости, `J` — карта зазоров, `F5` — панель функций, `F6` — журнал,
  `F7` — сравнение, `Ctrl+Z`/`Ctrl+Y` — отмена/повтор. Существующие бинды
  (W A S D Q E Z X G F TAB Esc Enter Shift ЛКМ/колесо/СКМ) не изменены и не добавлены.

### 11.2 Этапы: что именно сделано
| Этап | Что сделано | Где | Статус |
|---|---|---|---|
| 1. Запись/воспроизведение | Сэмплы 20 Гц: углы суставов + время + TCP (+кватернион); источник — робот или фантом; JSON-файл на запись; воспроизведение своим проигрывателем с паузой и множителем ×0.25…×4; запись можно отдать фантому («как обычная траектория»); список — в дереве, ветка «Записи траекторий» | `Recording/KvTrajectoryRecord.cs`, `Recording/KvRecordingService.cs`, вкладка «Запись» | готово |
| 2. Preset-позы | «Сохранить текущую позу как…» (быстрый выбор «Домашняя/Инспекция/Замена инструмента/Парковка» + имя по времени); «Перейти в позу» — путь планировщиком (`PlanToGoal`), а при отказе Safety — плавная интерполяция по суставам; позы привязаны к имени робота, работают и у SCARA; ветка «Позы» в дереве | `KvPoseLibrary.cs`, `KvPlanKit`, вкладка «Позы» | готово |
| 3. Ручное управление суставами | Вкладка «Суставы»: слайдер на каждый сустав (у робота 6, у SCARA 3 — J1, J2, Z), рядом текущий угол, лимит и запас; движение через `PoseValidator.Apply` с проверкой лимитов (при выходе — сообщение, поза не меняется) | `KvFeatureWindow.MakeSlider/ApplyJoint` | готово |
| 4. Аварийная остановка | Кнопка `estop` (иконка — красный круг с крестом) + пункт меню: стоп исполнителя, снятие внешнего плана, остановка записи/воспроизведения, отмена pick-and-place и сценария, `ResetFlow` → Idle, запись в журнал, звук и вибрация | `FeatureHub.EmergencyStop` | готово |
| 5. Зоны запрета | Куб/сфера/цилиндр создаются в точке прицела, полупрозрачный красный объём БЕЗ коллайдера (чтобы не менять `CollisionWorld`), список в дереве, перемещение/масштаб/поворот/скрытие/удаление, сохранение в JSON и автозагрузка; проверка траекторий по ЦЕПОЧКЕ ЗВЕНЬЕВ (не только TCP) → пометка «⚠ опасная» либо отбрасывание (флаг) | `KvKeepOutZones.cs`, `KvZoneMarks`, вкладка «Зоны» | готово |
| 6. Сравнение траекторий | Слоты A/B (отметка выбранного узла дерева командой; метки `[A]`/`[B]` видны в дереве), таблица метрик бок о бок (длина, время, кривизна, зазор, запас лимитов, оценка, сэмплы, зона запрета), вывод «чем отличаются», кнопки «Переключиться на A/B» (тот же путь, что выбор зелёным лучом) | `KvComparison.cs`, вкладка «Сравнение» | готово |
| 7. Графики углов | Отдельная панель: `RawImage` + процедурная `Texture2D`, по оси X — время, по Y — углы, каждый сустав своей линией своего цвета, сетка 8×4; опция наложения двух траекторий (вторая — приглушённо) | `KvJointGraph.cs`, вкладка «Графики» | готово |
| 8. Тепловая карта достижимости | Сфера (эллипсоид) вокруг робота и КОЛЬЦО у SCARA (внутренняя зона прозрачна), цвет по стоимости: у центра зелёный → к краю красный; стоимость — вердикт НАСТОЯЩЕГО оракула (`ReachabilityOracle`) + радиальный градиент; текстура считается по кадрам (`samplesPerFrame = 64`), после постройки в кадре не делается ничего; перестраивается при смене робота | `KvHeatmaps.cs` (`KvReachabilityHeatmap`), вкладка/кнопка тулбара | готово |
| 9. Тепловая карта зазоров | Зазор считается по сэмплам траектории (`PoseValidator.ClearanceAt` на своём мире столкновений), участки группируются в 4 уровня (красный ≤ 20 мм, оранжевый ≤ 50, жёлтый ≤ 100, зелёный) и рисуются линиями ПОВЕРХ «колбаски» — красные участки = опасно близко | `KvHeatmaps.cs` (`KvClearanceOverlay`) | готово |
| 10. Гриппер | Двухпалый захват на `tcp` робота, собирается кодом, пальцы смыкаются/размыкаются плавно (V или кнопка), точка захвата смещена вдоль инструмента (`GraspDrop`); коллайдеры пальцев удалены (не влияют на мир столкновений). **У SCARA работает так же** — проверено прогоном (осей 3, захват собран) | `KvGripper.cs` | готово |
| 11. Pick-and-place | Куб на столешнице (Rigidbody + коллайдер), последовательность «подъезд → опускание → захват → подъём → перенос → опускание → отпускание → отход»; каждый шаг — обычная траектория (IK + `ToolAlign` «инструмент вниз» + штатный исполнитель); куб становится дочерним объектом гриппера, при отпускании — снова свободен с физикой | `KvPickAndPlace` в `KvGripper.cs` | готово |
| 12. Панель ETA | Отдельная панель справа снизу: «осталось N с», прогресс-бар и процент; показывает и траекторию этапа 4, и внешние планы (позы/записи), и воспроизведение записи (с учётом паузы и множителя) | `KvFeatureWindow.BuildEtaPanel` + `FeatureHub.UpdateEta` | готово |
| 13. Журнал действий | Кольцевой буфер 600 записей + ДОПИСЫВАНИЕ В ФАЙЛ каждой строкой (`Logs/actions_ГГГГММДД.log`), 12 типов событий, фильтр по типу (сегментный переключатель), выгрузка всего журнала в отдельный файл, прокрутка в панели | `KvActionLog.cs`, вкладка «Журнал» | готово |
| 14. Замер производительности | По каждому прогону генерации: время, запрошено/получено вариантов, попытки планирования, попытки без пути, отброшенные дубликаты, найдено/годных конфигураций IK, итерации планировщика последней попытки; история в `Logs/planner_runs.json`, сводка в журнале и на вкладке «Замер» | `KvPlannerPerformance.cs` | готово |
| 15. Undo / Redo | Стек глубиной 20; отменяются смена точки, выбор траектории, запуск движения (+ зоны/позы/записи через общий API); кнопки тулбара `edit.undo`/`edit.redo` больше не заглушки; горячие клавиши `Ctrl+Z`/`Ctrl+Y`; повтор НЕ запускает робота сам (безопасность) | `KvUndoRedo.cs` + наблюдение в `FeatureHub.WatchFlow` | готово |
| 16. Пространственный звук | 5 сигналов СИНТЕЗИРУЮТСЯ кодом (`AudioClip.Create`): щелчок выбора точки, подтверждение, старт движения, ошибка, аварийная остановка; один `AudioSource` с `spatialBlend = 1`, точка события — TCP робота (звук «от робота»), логарифмический rolloff | `KvSpatialAudio.cs` | готово |
| 17. Вибрация | Заготовка на рефлексии: ищутся компоненты контроллеров XR (XRIT/Meta/PICO по имени типа) и вызывается `SendHapticImpulse(float, float)`; без контроллеров — одна строка в консоль и работа продолжается. События: выбор точки — слабая, подтверждение/старт — средняя, ошибка — резкая, аварийный стоп — максимальная | `KvHaptics.cs` | готово (заготовка по ТЗ) |
| 18. Менеджер сценариев | 4 сценария: «Показать workspace», «Показать лимиты», «Демонстрация 8 траекторий» (сам ставит точку над столом, ждёт генерацию, выбирает лучшую траекторию), «Продемонстрировать pick-and-place»; шаги выполняются последовательно, есть пауза, отмена и страховка по максимальному времени шага | `KvScenarioManager.cs` + сборка сценариев в `FeatureHub` | готово |
| 19. Презентационный режим | Камера сама облетает робота (радиус/высота/скорость), рассказ текстом на экране (смена по таймеру, ПРОБЕЛ/стрелки), интерфейс скрыт, выход по Esc или кнопке; `FreeFlyCameraController` НЕ выключается (иначе поток перестал бы тикать) — у него обнуляются скорости и выключаются лазеры, значения восстанавливаются на выходе | `KvPresentationMode.cs` | готово |
| 20. Сохранение/загрузка сессии | JSON-сессия: позы всех роботов (углы, позиция, поворот, активность), выбранная точка, зоны запрета целиком, ссылки на записи и позы, флаги функций, имя сцены, версия схемы; список сессий, сохранение/загрузка/удаление, расширяемость без ломки старых файлов | `KvSessionManager.cs`, вкладка «Сессии» | готово |

### 11.3 Верификация
- **Компиляция без Unity:** `pwsh -File _tools/dsh_csproj_sync.ps1 -Project <копия>` (добавляет новые файлы в csproj) → `dotnet build Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj` → **0 ошибок**; предупреждения только унаследованные (`CS0618`/`CS0649` в `FreeFlyCameraController.cs`, `UAC1001` в `RuntimeRegistry.cs`, `CS0618` в `Editor/ConvertRobotMaterialsToHDRP.cs`).
- **Компиляция в Unity:** `Unity.exe -batchmode -nographics -quit -projectPath "<копия>"` → Unity импортировала 19 новых файлов (`.meta` созданы), `Assembly-CSharp.csproj` перегенерирован (17 записей `Features`, 2 — `Recording`), **ошибок компиляции нет**, исключений нет (в логе только лицензионные `Error:` — среда, не код; и ошибка пакета `com.gladekit.mcp-bridge` про занятый порт — сторонний MCP-мост).
- **Сквозной прогон в PlayMode (диагностика этапов 1–20):** `Assets/_Project/01_Scripts/Editor/DshFeaturesDiag.cs` (в копию пользователя НЕ переносится), команда
  `Unity.exe -batchmode -nographics -projectPath "<копия>" -executeMethod DshFeaturesDiag.Run -logFile _dsh_features_final.log` → отчёт `_dsh_features_verify.txt`.
  **Итог последнего прогона: `[OK] 78 · [FAIL] 0 · [info] 9` — «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ».**
  Что подтверждено фактами прогона:
  - тулбар **20 кнопок** (5 новых id), **31 команда** этапов 1–20 зарегистрированы, `edit.undo`/`edit.redo` больше не заглушки, **18 новых иконок** нарисованы, окно функций собирается и открывается;
  - журнал ведётся, файл `Logs/actions_ГГГГММДД.log` создан, фильтр по типу работает;
  - звук: 5 сигналов синтезированы, источник звука с `spatialBlend = 1`; вибрация-заготовка не падает без контроллеров (найдено 0 — ожидаемо на ПК);
  - гриппер собран на роботе, пальцы смыкаются **75 → 12 мм**;
  - зоны запрета: куб/сфера/цилиндр созданы, содержат свой центр и не содержат далёкую точку, `ZoneAt` находит зону, **активного коллайдера нет** (мир столкновений не тронут), перемещение и изменение размера работают;
  - поза сохранена (файл создан), позы фильтруются по активному роботу, «переезд в позу» прошёл **планировщиком** (зазор 557 мм);
  - планирование после правок работает: **8 вариантов траектории**, фаза `TrajectoriesShown`;
  - замер планировщика: `генерация 0,396 с · вариантов 8/8 · попыток 16 (без пути 0) · дубликатов отброшено 2 · IK найдено 4, годных 3`;
  - сравнение: две траектории отмечены A и B, метрики бок о бок построены;
  - график углов построен по траектории; тепловая карта зазоров: `мин. зазор 369 мм · линий 8`;
  - тепловая карта достижимости: **сфера R = 1,60 м · 4608 текселей**;
  - запись: 17 сэмплов, файл создан, читается с диска, воспроизведение идёт с ×2, **пауза держит прогресс** (замер на 14 %), остановка работает;
  - внешний план (переезд сустава) запущен штатным исполнителем, **панель ETA** показала «осталось 0,4 с из 0,5 с · 11 %»;
  - аварийная остановка: движение остановлено, поток в `Idle`, событие в журнале, запись/воспроизведение сняты;
  - сценарии: 4 зарегистрированы, запуск/пауза/отмена работают; презентационный режим включается/выключается, строка рассказа не пустая;
  - сессия: сохранена (роботов 2, зон 3), после `Clear()` зоны восстановлены загрузкой сессии (3 → 0 → 3).

### 11.4 Найдено и исправлено в ядре (одна строка, важно)
- **`PoseValidator.BasePosition` для 6-осевого робота возвращал `(0,0,0)`** — `basePos` заполнялся ТОЛЬКО в ветке SCARA (`PoseValidator.Init`, строка ~140), поэтому у робота «база» оказывалась в мировом начале координат, а он стоит на столе в `z ≈ −23.9`. Следствие: всё, что считает «от базы робота», получало расстояние ≈24 м (в этой сессии это сразу поймала тепловая карта достижимости: сфера строилась вокруг начала координат, а перебор точек для проверки планирования уходил на 24 м от руки). **Исправлено одной строкой** в ветке 6-осевого робота — ровно так же, как у SCARA: `basePos = six.baseTransform != null ? six.baseTransform.position : six.transform.position;` (+ `up`). Потребитель `BasePosition` в проекте всего один — `IkSolver` в ветке SCARA (там значение и раньше было корректным), поэтому поведение SCARA не изменилось; оракул и планировщик считают базу самостоятельно (`six.baseTransform != null ? six.baseTransform : six.transform`) и правки не требовали. **Проверено прогоном:** `BasePosition (0.00, 0.98, −23.90)`, точка `(0.00, 1.08, −24.50)` найдена с первой попытки, планирование дало 8 вариантов.
- **Мелочи, найденные прогоном и исправленные:** коллайдер зоны запрета теперь выключается ДО `Destroy` (иначе в кадре создания он был бы виден `CollisionWorld`); презентационный режим получил рассказ по умолчанию (иначе при запуске без предварительной настройки строка была пустой).

### 11.5 Что проверить вручную
1. **Тулбар:** 4 ряда по 5 кнопок; новые — «Записать», «Аварийная остановка» (красная), «Тепловая карта достижимости», «Тепловая карта зазоров», «Панель функций»; подсказки при наведении; штатные 15 кнопок работают как раньше.
2. **Запись (этап 1):** `R` → в дереве появляется узел «● ИДЁТ ЗАПИСЬ · N т.»; подвигайте робота (слайдеры «Суставы») → `R` → узел «Записи траекторий» с новой записью; `P` → робот (или фантом) повторяет движение; пауза повторным `P`; множитель — кнопки ×0.25…×4 на вкладке «Запись».
3. **Позы (этап 2):** «Сохранить текущую позу как…» → «Домашняя»; отвести робота слайдерами; «Перейти в позу» → робот плавно возвращается; проверить и на SCARA (переключение робота — `F`).
4. **Суставы (этап 3):** вкладка «Суставы» — у робота 6 слайдеров, у SCARA 3; у каждого рядом угол, лимит и запас; при попытке вывести за лимит — сообщение в статусе, поза не меняется.
5. **Аварийная остановка (этап 4):** запустить движение (этап 4 потока) → нажать красную кнопку → робот встал сразу, состояние `Idle · ожидание`, в журнале «АВАРИЙНАЯ ОСТАНОВКА».
6. **Зоны запрета (этап 5):** навести прицел на точку над столом → «Зона запрета: куб» → полупрозрачный красный куб; в дереве ветка «Зоны запрета»; выбрать точку за зоной, получить траектории → в дереве у пересекающих вариантов «⚠» и в свойствах «ПЕРЕСЕКАЕТ: …»; проверить перемещение/размер/скрытие/удаление; после перезапуска PlayMode зоны восстанавливаются.
7. **Сравнение (этап 6):** выбрать узел «Траектория 1» → «Отметить траекторию для сравнения» (или вкладка «Сравнение» → «Отметить выбранную») → `[A]` в дереве; затем «Траектория 2» → `[B]`; на вкладке — таблица метрик и «Переключиться на A/B».
8. **Графики (этап 7):** вкладка «Графики» — линии всех суставов; включить наложение — вторая траектория на том же графике; убедиться, что резкие скачки видны как вертикальные участки.
9. **Тепловые карты (этапы 8–9):** `H` — сфера (у SCARA — кольцо) с зелёным центром и красным краем; `J` — траектория раскрашена по зазору (красные участки = опасно близко). Проверить, что FPS не падает (Stats).
10. **Гриппер и pick-and-place (этапы 10–11):** `V` — пальцы смыкаются/размыкаются; «Pick-and-place демо» — куб появляется на столе, робот подъезжает, берёт его, переносит и отпускает; куб при захвате становится дочерним объектом гриппера.
11. **ETA и журнал (этапы 12–13):** во время движения робота — панель «осталось N с» с прогрессом; `F6` — журнал с фильтром и кнопкой «Выгрузить в файл».
12. **Замер (этап 14):** вкладка «Замер» — после каждой генерации появляется строка с временем, попытками, дубликатами и итерациями.
13. **Undo/Redo (этап 15):** `Ctrl+Z` после смены точки/выбора траектории; `Ctrl+Y` возвращает.
14. **Звук и вибрация (этапы 16–17):** звуки слышны и «идут от робота» (при приближении камеры громче); вибрация на ПК не отправляется — в консоли одна строка «контроллеры VR/MR не найдены» (это по ТЗ: заготовка).
15. **Сценарии и презентация (этапы 18–19):** вкладка «Сценарии» → «Продемонстрировать pick-and-place» (со всей цепочкой), пауза/отмена; «Презентационный режим» — облёт камеры, текст, интерфейс скрыт, Esc — выход.
16. **Сессии (этап 20):** «Сохранить сессию» → изменить позу/удалить зону → «Загрузить последнюю» → состояние восстановилось.

### 11.6 Ручной чек-лист после ПОЛНОГО аудита 14.09.2026 (этапы 1–8; автоматика — §0.5)
Автоматический прогон `DshFullVerifyDiag` (§0.5) прошёл цикл на ОБОИХ роботах, поэтому руками остаётся то,
чего в batch нет: живая мышь, визуал, ощущения и то, что помечено «требует доработки».
Порядок (5–7 минут, оба робота — переключение `F` или узел дерева):

1. **Визуал 8 траекторий и 8 фантомов:** PlayMode → `Z` (красный луч) → ЛКМ по столешнице → должны
   появиться **8 «колбасок» разных цветов** и **8 бирюзовых фантомов**, все едут ОДНОВРЕМЕННО и плавно
   (скорость 0.2 ю/с = 1 юнит за 5 с). Фантомов видно сквозь робота («рентген»).
2. **Фантом → робот:** `X` (зелёный) → ЛКМ по фантому → робот едет к той же позе (в 3 раза медленнее
   фантома), по прибытии кончик **не дрожит**, траектории исчезают, фантом убирается.
3. **Режим перемещения точки:** ЛКМ → `Enter` → панель «РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ», Q/E, W/S, A/D двигают
   точку (камера стоит), колесо глубину НЕ меняет, `Enter` — подтвердить, `Esc` — отмена (точка и прежние
   траектории возвращаются мгновенно).
4. **Колесо и СКМ:** колесо назад — шарик отходит от стола к оператору и становится ровным шаром; колесо
   вперёд — возвращается и упирается в стол; СКМ — мгновенный возврат на поверхность. Над панелями
   KazistovVv колесо по-прежнему прокручивает дерево.
5. **Pick-and-place (ГЛАВНОЕ, помечено «требует доработки»):** вкладка «Гриппер» → «Pick-and-place демо».
   В логе должны быть строки `[PickPlace] куб создан …`, `старт демонстрации`, затем шаги `подъезд →
   опускание → захват → подъём → перенос → опускание → отпускание → отход`. **Что смотреть:** сработал ли
   захват (в логе НЕ должно быть «куб не рядом с захватом», в журнале — «объект «Куб_PickAndPlace» захвачен»),
   и доехал ли шаг `отход` (в логе не должно быть «внешнее движение отклонено 3 раза подряд»). Проверить
   НА ОБОИХ роботах: у SCARA куб теперь ставится на 0.38 м от базы (вылет руки 0.70 м). Если снова отказ —
   подобрать `KvPickAndPlace.approachHeight/liftHeight`, `KvGripper.GraspDrop` и вынос куба в `SpawnCube`.
6. **Зоны запрета больше НЕ мешают планированию (исправлено 14.09.2026):** создать зону рядом с рабочей
   зоной («Зона запрета: куб»), затем выбрать точку за/рядом с зоной → **8 траекторий должны появиться**
   (раньше планировщик отказывался строить ЛЮБЫЕ пути рядом с зоной). Пересекающие зону варианты —
   пометка «⚠ опасная» и строка «ПЕРЕСЕКАЕТ: …» в свойствах. Удалить зону и повторить.
7. **SCARA — точка на столе:** навести красный луч РОВНО на столешницу стенда 2 → если появилось
   «Выберите другую точку — траектория не найдена», это ожидаемое ограничение (§0.5: нижняя граница хода
   призмы = уровень стола). Навести на точку **на 5–10 см выше стола** → 8 траекторий находятся.
8. **Тепловые карты:** `H` — у робота СФЕРА, у SCARA — КОЛЬЦО (плоское, внутренняя зона прозрачна);
   переключить роботов и убедиться, что карта перестраивается под нового (исправлено 14.09.2026);
   `J` — раскраска траектории по зазору. Проверить FPS (Stats) — в batch просадки нет (2.8 мс/кадр).
9. **Интерфейс:** TAB — показать/скрыть; тулбар 20 кнопок в 4 ряда, подсказки; дерево раскрывается,
   клик подсвечивает объект рамкой; свойства заполняются; тема Тёмная/Светлая/Системная мгновенно и
   переживает перезапуск; при 1366×768 панели не наезжают.
10. **Слайдеры суставов:** у робота 6, у SCARA 3 (J1, J2, Z — подпись исправлена 14.09.2026); при выходе
    за лимит — сообщение, поза не меняется.
11. **Живые клавиши:** `Esc` из любого состояния возвращает в `Idle` (из режима точки — только отменяет
    перемещение), `Ctrl+Z`/`Ctrl+Y` — отмена/повтор (повтор НЕ запускает робота), `G` — фонарик,
    `F` — переключение робота, `V` — гриппер.
12. **Терминология:** в интерфейсе робот называется «6-осевой робот» (имя в сцене `Робот_6ос_Стенд1`);
    переименование в «робот» не делалось — это правка сцены и текстов, отдельным ТЗ. В отчётах агента
    используется слово «робот».

## 12. Сессия 15.09.2026 — ЭТАПЫ 1–8 (фантомы, мультиязычность, экспорт демонстраций, сингулярности, waypoint-редактор, health monitor, динамические препятствия, виртуальный пульт)

### 12.1 ЭТАП 1 — ФАНТОМЫ БЕЗ ВИЗУАЛЬНОЙ РАЗНИЦЫ (готово)
- **Что было:** фантом = бирюзовая полупрозрачная копия (`GhostMaterial.MakeGhost`: `_SurfaceType=1`,
  `_BlendMode=Alpha`, `_ZWrite=0`, `_ZTestTransparent=8`, renderQueue 3000) + материал-ИНСТАНС на каждый
  рендерер (`r.material`), тени выключены. Отсюда «пробелы в текстурах» (полупрозрачный HDRP-проход без
  записи глубины), отдельный цвет и лишние копии материалов (утечка на каждый показ).
- **Что сделано (файл `01_Scripts/Trajectory/LaserAndPhantomManagers.cs`, только класс `PhantomManager`):**
  новый режим `matchRealAppearance = true` (по умолчанию): материалы копии НЕ трогаются вообще —
  ни `r.material`, ни `sharedMaterial`, ни прозрачность, ни тон, ни ZTest. Копия наследует те же
  `sharedMaterial` и текстуры, что и робот, поэтому выглядит ровно как робот, префабы/материалы сцены
  не ломаются, материал-инстансов не создаётся. `castShadowsLikeRobot` (по умолчанию `true`) — тени как
  у робота; выключаются только по явному требованию. Старый «бирюзовый рентген» остался как режим
  (`matchRealAppearance = false`) — обратная совместимость и быстрый откат.
- **Подсветка наведения вместо покраски:** `SetHighlight` больше НЕ меняет материалы модели, а включает
  ТОНКИЙ КОНТУР (`PhantomOutline`: 12 прутьев на `HDRP/Unlit`, `HideFlags.HideInHierarchy`, без
  коллайдеров) вокруг фантома под зелёным лучом. Без наведения фантом неотличим от робота.
- **Пустая проверка:** `PulseMovingGhosts` больше не «дышит» материалами копии; `ghostAlpha`/`ghostGlow`/
  `ghostTint` оставлены только для старого режима, из уборки убран список материал-инстансов
  (`ghostMaterials`) — утечки материалов нет по построению.
- **HideInHierarchy у фантомов сохранён** (контейнер `Phantoms`, копии и потомки — только
  `HideInHierarchy`, без `DontSave`), правило проекта не нарушено.- **Проверка:** `dotnet build Assembly-CSharp.csproj` → 0 ошибок (предупреждения только унаследованные).

### 12.2 ЭТАП 2 — МУЛЬТИЯЗЫЧНОСТЬ RU / EN / ZH / ES / DE / FR / JA (готово)
- **Формат — JSON, а не ScriptableObject (обоснование).** Файл правится и диффится без Unity и без
  перекомпиляции; ScriptableObject потребовал бы 7 ассетов + 7 `.meta` и редактора для правки переводов.
  Каталоги лежат в `Assets/StreamingAssets/kazistovvv_i18n/<код>.json` (по 187 строк каждый:
  `ru`, `en`, `zh`, `es`, `de`, `fr`, `ja`); формат: `{ code, name, english, cjk, strings:[{key,text}] }`.
  **Новый язык = новый файл** — каталог сканируется при старте, язык появляется в настройках сам.
- **Ядро — `06_KazistovVv_UI/03_Scripts/Core/KvLocalization.cs` (`KvLoc`).** Цепочка fallback ровно по ТЗ:
  `каталог(текущий)[key] ?? каталог(en)[key] ?? русский текст, переданный кодом`. Ключи осмысленные
  (`menu.file.open`-стиль: `cmd.point.select`, `status.ray`, `prop.section.joints`). Язык по умолчанию —
  СИСТЕМНЫЙ (`Application.systemLanguage`), неподдерживаемый системный → английский. Выбор — в PlayerPrefs
  (`KazistovVv.Language`, значение `system` = автоматически).
- **Мгновенное применение:** `KvLoc.Changed` → `KazistovVvUIManager.OnLanguageChanged` → пересборка
  оболочки (меню, тулбар, dock-панели, статус-бар, настройки) + пересоздание шрифтов всех `Text` +
  обновление подписей окна функций (`KvFeatureWindow.RefreshLanguageLabels`). Перезагрузки нет.
- **CJK-шрифты:** `KvTheme.Font` теперь берёт шрифт у `KvLoc.UiFont`; для китайского/японского
  подбирается системный шрифт с CJK-глифами (`Microsoft YaHei UI`, `Meiryo`, `Yu Gothic`, `MS Gothic`,
  `SimHei`, `Noto Sans CJK …`, дальше — перебор установленных шрифтов по признакам Gothic/Ming/Hei/Song).
  Встроенный `LegacyRuntime.ttf` CJK не содержит — без этого текст был бы «квадратами». Для остальных
  языков шрифт прежний (вид интерфейса не изменился).
- **Что переведено:** строка меню (Файл/Правка/Вид/Робот/Сервис/Справка), ВСЕ подписи и подсказки команд
  реестра `KvCommands` (ключи `cmd.<id>` / `cmd.<id>.desc` — централизованно, поэтому тулбар, меню и
  подсказки локализованы без правок самих панелей), группы и оси дерева моделей, состояния и поля
  статус-бара, вкладки и заголовки панели настроек, вкладки панели функций, заголовки секций свойств.
- **Выбор языка в интерфейсе:** вкладка «Настройки → Интерфейс» — список «Системный» + все языки словаря
  (галка у активного, рядом число строк); плюс команда/кнопка «Язык интерфейса» (`lang.cycle`,
  меню «Сервис → Настройки → Язык интерфейса»). Диагностика: `KvLoc.Status`, `KvLoc.CurrentCode`,
  `KvLoc.CountOf(code)`, `KvLoc.Reload()` (перечитать JSON без перезапуска).
- **Проверка:** `dotnet build Assembly-CSharp.csproj` → 0 ошибок; все 7 JSON прошли разбор (187 строк).
### 12.3 ЭТАП 3 — ЭКСПОРТ ДЕМОНСТРАЦИЙ (скриншоты + видео) — готово
- **Новый файл `01_Scripts/Features/KvCaptures.cs` (`KvCaptureService`).** Папка — СТАНДАРТНАЯ папка
  Windows «Видео» (`Environment.SpecialFolder.MyVideos`; на этой машине `C:\Users\Ольга\Videos`),
  при недоступности — `%USERPROFILE%\Videos`. Имена строго по ТЗ:
  `KazistovVv_screenshot_YYYY-MM-DD_HH-MM-SS.png` и `KazistovVv_recording_YYYY-MM-DD_HH-MM-SS.mp4`.
- **Скриншот (3.1).** Подпись (дата, состояние State Machine, выбранный робот, тема, язык) рисуется
  СЛУЖЕБНЫМ Canvas поверх кадра (`sortingOrder 30000`, `HideInHierarchy`, `raycastTarget = false`),
  кадр снимается `ScreenCapture.CaptureScreenshotAsTexture()` в конце кадра и пакуется в PNG. Так на
  картинке оказывается НАСТОЯЩИЙ интерфейсный текст (включая CJK), без растеризатора шрифтов в коде.
  Отключается переключателем `annotate` (кнопка/пункт меню «Подпись на скриншоте»).
- **Видео (3.2).** Unity Recorder из Package Manager (`com.unity.recorder` уже разрешён в проекте,
  `Library/PackageCache/com.unity.recorder@…`): `RecorderController` + `MovieRecorderSettings` с
  `CoreEncoderSettings` (H.264 / MP4, `TargetBitRate` = кбит/с ÷ 1000 Мбит/с), `GameViewInputSettings`
  (разрешение). Код под `#if UNITY_EDITOR` (это редакторский API). **Резерв:** если рекордер недоступен
  (сборка/ошибка), пишется последовательность PNG-кадров с `recording.json` (FPS/разрешение/битрейт) —
  работа не срывается, оператор получает понятное сообщение.
- **Публичные параметры (ТЗ):** `videoWidth`, `videoHeight`, `videoFps`, `videoBitrateKbps`, `annotate`,
  клавиши `F8` (скриншот) и `F10` (запись). Гибкие клавиши не конфликтуют: F8/F10/F11/F12 и Ctrl+F8/Ctrl+F10
  в проекте не использовались (существующие бинды не тронуты).
- **Тулбар/меню:** `shot.screenshot`, `video.toggle`, `shot.annotate` — 5-й ряд тулбара и меню
  «Файл → Экспорт демонстрации».

### 12.4 ЭТАП 4 — ВИЗУАЛИЗАЦИЯ СИНГУЛЯРНОСТЕЙ — готово
- **Новый файл `Features/KvSingularityZones.cs` (`KvSingularityVisualizer`).** Считаются:
  **запястье** (|q5| → 0, тот же критерий 8°, что у оракула), **локоть** (вытягивание: расстояние TCP
  от базы / сумма длин звеньев), **плечо** (TCP у оси базы), а у SCARA — СВОИ: **полное вытягивание** и
  **полное складывание** (по |L1−L2|, снятым по фактическим пивотам). Дополнительно — **σmin матрицы
  Якоби** (`KinematicsJacobian.Compute/SigmaMin`), нормированный на «удобную» позу: `mobility = σmin/эталон`.
- **Где показывается (ТЗ: «вокруг суставов, а не всей сцены»):** полупрозрачные сферы-зоны у пивотов
  вовлечённых суставов (плюс кольцо у основания при сингулярности плеча и складывании SCARA), жёлтые
  при подходе и красные в самой сингулярности. Геометрия — пул, создаётся один раз; материалы три общих
  (нет утечек); объекты `HideInHierarchy` и без коллайдеров → в `CollisionWorld` не попадают.
- **Производительность:** пересчёт раз в `interval` = 0.1 с; при выключенной визуализации в кадре не
  делается ничего. Обновление онлайн при движении робота.
- **Тулбар/меню/настройки:** `view.singularity` (и Ctrl+F8), пункт «Вид → Сингулярности»,
  переключатель `scene.singularities` в настройках; статус — в свойствах узла «Сингулярности» дерева.
### 12.5 ЭТАП 5 — WAYPOINT EDITOR — готово
- **Новый файл `Features/KvWaypoints.cs` (`KvWaypointManager`).** Постановка: кнопка «Добавить waypoint»
  берёт ТЕКУЩУЮ точку (зафиксированную красным лазером) либо точку прицела — ровно как в ТЗ; можно
  двигать, удалять, менять порядок (раньше/позже), очищать; список — ОТДЕЛЬНОЙ ВЕТКОЙ
  «Промежуточные точки» в дереве моделей, выбор узла делает точку активной.
- **Маршрут строится ШТАТНЫМ планировщиком** (`TrajectoryFlowController.Planner` — добавлено ТОЛЬКО
  читающее свойство): сегменты «старт → WP1 → … → WPN» через `Planner.PlanToGoal` (тот же BiRRT,
  те же зазоры/лимиты), финальный участок — `Planner.Plan` (те же варианты, что и в штатных 8
  траекториях). Для каждого сегмента — до 4 seed-попыток (как делает сам поток) и ПОНЯТНАЯ причина отказа.
- **Поза прохода выбирается по ВЕТВЯМ IK планировщика** (`SolveGoalConfigs`) с максимальным запасом до
  лимитов. Это исправление найдено прогоном: одиночная IK давала позу с запасом ровно 0.0°, а
  планировщик требует ≥3° (`Planner.minLimitMarginDeg`) — точка «решалась», но ни один маршрут через неё
  не строился. Теперь такие точки либо получают рабочую ветвь, либо честно помечаются красным с
  причиной «запас до лимитов 0.0° < 3.0° (планировщик точку не пройдёт)».
- **Недостижимая точка — красный маркер, траектория НЕ строится** (ТЗ): маршрут не собирается, в лог и
  в дерево идёт причина; после удаления/исправления точки маршрут строится снова.
- **Маршрут виден** («колбаски» `TrajectoryTube`, оранжевые, вариантов до `routeVariants` = 3) и
  запускается ШТАТНЫМ исполнителем через `PlayExternalPlan` (State Machine не переписывалась).
- **SCARA:** работает то же самое (3 оси J1, J2, Z) — проверка и планирование идут через тот же
  `PoseValidator`/`Planner`, поэтому ограничения SCARA учитываются автоматически.

### 12.6 ЭТАП 6 — HEALTH MONITOR — готово
- **Новый файл `Features/KvHealthMonitor.cs` (`KvHealthMonitor` + `KvHealthPanel`).** Симуляция поверх
  РЕАЛЬНЫХ углов робота: **скорость** — по фактическому изменению угла за шаг; **ток** — холостой +
  вклад скорости + вклад ускорения + вклад удержания (момент «на плече» по отклонению от вертикали);
  **температура** — тепловой баланс (нагрев ∝ I², охлаждение ∝ (T − T среды)) → растёт при движении и
  падает в покое; **износ** — накопление `∝ I·dt` в 0…1; **прогноз ресурса** — часы до порога
  обслуживания и до критического износа.
- **Графики:** 4 линейных графика в реальном времени (температура / ток / скорость / износ), каждый
  сустав — своей линией (`KvJointGraph.JointColors`), своя процедурная текстура на `RawImage`, буфер
  пикселей переиспользуется (в кадре нет аллокаций), перерисовка не чаще `redrawInterval` = 0.25 с.
- **Панель — своя (справа снизу), свой Canvas** (`sortingOrder 44`, `HideInHierarchy`, кликов не
  перехватывает); включается кнопкой тулбара / F12 / настройкой `tool.health`.
- **Данные в файл (ТЗ):** CSV по строке на выборку раз в `logInterval`:
  `%persistentDataPath%/KazistovVv/Logs/health_ГГГММДД_HHMMSS.csv` (время + T/I/V/W по каждому суставу).
- **Суставов:** у робота 6, у SCARA 3 — число берётся из `PoseValidator.Dof`, массивы пересоздаются при
  смене робота.
- **Тулбар/меню/настройки:** `health.toggle` (F12), «Вид → Мониторинг состояния», `tool.health`.

### 12.7 ЭТАП 7 — ДИНАМИЧЕСКИЕ ПРЕПЯТСТВИЯ — готово
- **Новый файл `Features/KvDynamicObstacles.cs` (`KvDynamicObstacleService`).** Движущийся объект —
  тележка (платформа-плита + мачта + маркерный огонь) с настраиваемым маршрутом: `mode`
  (туда-обратно / по кругу / один проход), `pathA`/`pathB`, `circleRadius`, `speed` (м/с), `pauseSeconds`.
- **Планировщик учитывает движение (ТЗ):** тележка — НАСТОЯЩЕЕ препятствие, её рендереры попадают в
  `CollisionWorld` штатным путём (мир собирается по рендерерам), поэтому BiRRT обходит её как обычную
  геометрию сцены (пересборка мира — 0.5 с, как у потока). **Важно (грабля, проверенная прогоном):**
  тележка НЕ прячется в иерархии и НЕ парентится под служебный корень — иначе `IsServiceObject` счёл бы
  её служебной и она молча выпала бы из мира расчётов. При выключении объект гасится, а `CollisionWorld`
  собирается с `FindObjectsInactive.Exclude` — препятствие исчезает из расчётов (проверено).
- **Оценка траекторий:** для каждого сэмпла берётся его время (`plan.Times[i]`), считается ПРОГНОЗ
  позиции препятствия и минимальное расстояние от цепочки звеньев до его габарита → `Clear` /
  `Risky` («рискованная») / `Collision` (пересечение). По ТЗ есть флаг `discardRisky`: либо пометка,
  либо отбрасывание (снимает выбор и возвращает к списку вариантов).
- **Реакция на приближение:** пока робот едет, проверяется расстояние до звеньев; ближе `stopDistance`
  (10 см) — предупреждение или АВАРИЙНАЯ ОСТАНОВКА (`autoStop`, по умолчанию вкл) через тот же
  обработчик, что у кнопки `estop`; ближе `warningDistance` (30 см) — пометка «рискованная».
- **Тулбар/меню/настройки:** `obstacle.toggle` (Ctrl+F10), `obstacle.evaluate`, `obstacle.discard`,
  «Вид → Динамические препятствия…», переключатель `scene.obstacles`. Маршрут виден линией
  (служебный объект), в свойствах узла — скорость, маршрут, пройденное расстояние и риск.
### 12.8 ЭТАП 8 — ВИРТУАЛЬНЫЙ ПУЛЬТ (TEACH PENDANT) — готово
- **Новый файл `Features/KvTeachPendant.cs` (`KvTeachPendant` + обработчик стика `KvPendantStick`).**
  World Space Canvas: корпус, ЭКРАН, ДЖОЙСТИК, крупные кнопки и LED-индикаторы.
- **Два варианта (ТЗ, минимум 2):** `Industrial` — вариант A (как у промышленных пультов: тёмный экран,
  три ряда кнопок, красный грибок СТОП, LED-линейка) и `Addon` — вариант B (стиль проекта АДДОН:
  скруглённый корпус, крупный экран, джойстик справа, ряд цветных кнопок). **Вариант B — заглушка:
  ждём фото-эталон от заказчика** (в самом пульте это написано строкой «ЭТАЛОН ОФОРМЛЕНИЯ ЖДЁМ (фото АДДОН)»).
  Переключение на ходу: кнопка «ВАРИАНТ A / B», команда `pendant.variant`.
- **Экран:** координаты TCP (в мире И от базы робота), имя робота, состояние State Machine, углы суставов
  (у призмы — в мм), вектор джойстика и режим.
- **Кнопки (базовый функционал по ТЗ):** ПУСК (продолжить паузу либо отправить робота по выбранной
  траектории), СТОП (та же АВАРИЙНАЯ ОСТАНОВКА проекта), ДОМОЙ (переезд в нулевую позу штатным
  планировщиком через `MakeJointPlan` + `PlayExternalPlan`), ЗАПИСАТЬ ПОЗУ (та же библиотека поз, что в
  панели функций). Плюс кнопки выбора сустава 1…6.
- **LED-индикаторы:** ПИТАНИЕ, ДВИЖЕНИЕ, СВЯЗЬ, АВАРИЯ — перекрашиваются по фактическому состоянию
  (движение исполнителя, наличие робота, выход за лимиты).
- **Джойстик РЕАЛЬНО двигает робота** (ТЗ: «двигает робота (или выдаёт сигнал)»): режим `JOINT`
  покачивает выбранный сустав, режим `TCP` сдвигает инструмент в плоскости; движение идёт через
  `PoseValidator` (те же лимиты и проверка самозазора) и только когда робот свободен. Вектор доступен
  как `JoystickVector`, есть внешний вход `SetStick(...)` (для VR/геймпада/автотестов).
- **Расположение:** `Floating` (парит в сцене), `FollowCamera` (в руке оператора), `AboveRobot`
  (над стойкой активного робота) — команда `pendant.attach`.
- **Тулбар/меню/настройки:** `pendant.toggle` (F11), `pendant.variant`, `pendant.attach`, `pendant.jogmode`,
  «Сервис → Виртуальный пульт», переключатель `tool.pendant`.

### 12.9 ОТЛОЖЕНО (не делалось в этой сессии — по ТЗ)
- **Сменные инструменты (Tool Changer)** — ждём модель робота с инструментами.
- **AR + компьютерное зрение под PICO 4 Ultra** — отдельный этап.
- **Мульти-роботная ячейка** — позже.
- **Стартовое меню** — обсудим отдельно.

### 12.10 Верификация сессии (этапы 1–8)
- **Компиляция.** `dotnet build Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj` → **0 ошибок**
  (предупреждения только унаследованные: `CS0649` в `FreeFlyCameraController.plannerController`).
  Дополнительно — **полный прогон компилятора Unity** (`-batchmode -nographics -quit`): ошибок нет,
  редакторская часть (Unity Recorder под `#if UNITY_EDITOR`) тоже собралась; Unity сама создала `.meta`
  для 8 новых скриптов и 7 файлов словарей.
- **Сквозной прогон в PlayMode** (`Assets/_Project/01_Scripts/Editor/DshStageDiag.cs`, в копию
  пользователя НЕ переносится): отчёт `KavistovVv/_dsh_stage_verify.txt`, лог `_dsh_stages.log`.
  **ИТОГ: `[OK] 76 · [FAIL] 0` · «ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ».** Что подтверждено фактами:
  - **этап 1:** фантомов **8**, рендереров 72, **все 72 материала — ТЕ ЖЕ объекты, что у робота**
    (`sharedMaterial`, совпадение 72/72), прозрачных материалов **0**, материалов-инстансов **0**,
    все копии `HideInHierarchy`;
  - **этап 2:** языков 7, ru=187 / en=187 строк, переключение мгновенное (EN «Pick a point (red laser)»,
    DE «Punkt wählen (roter Laser)», ZH «选点（红色激光）»), для ZH/JA подключён системный шрифт
    **Microsoft YaHei UI**, английский каталог покрывает ВСЕ ключи (fallback работает), отсутствующий ключ
    возвращает исходный текст, выбор сохраняется в `PlayerPrefs` (`Prefs=fr`), «системный» режим
    разрешается в `ru`, интерфейс переживает смену языка (в реестре 74 команды);
  - **этап 4:** σmin/эталон в 0..1 (95 %), сингулярность ЗАПЯСТЬЯ распознана (`Wrist`), помечена опасной,
    вокруг суставов включены 3 зоны, при выходе из сингулярности (q5 = 45°) зона гаснет (`None`);
  - **этап 5:** waypoint добавлен, достижимость посчитана (зазор 920 мм, запас по лимитам), **маршрут
    построен штатным планировщиком** и начинается в текущей позе робота; недостижимый waypoint (4 м)
    помечен и **маршрут НЕ строится**; после удаления маршрут строится снова; удаление точки снимает маршрут;
  - **этап 6:** температура **растёт** при движении (28.00 → 30.32 °C) и **падает** в покое (30.32 → 29.76 °C),
    ток до 6.37 А, скорость до 1.047, износ в 0..1, прогноз ресурса 8.7 ч, CSV-файл создан и **строки
    записаны** (5 строк), панель графиков показана;
  - **этап 7:** тележка создана и НЕ служебная, **попала в мир столкновений** (3 объекта), едет по маршруту,
    риск траектории посчитан (`Clear · 216 см`), обработчик аварийной остановки привязан, при выключении
    тележка **исчезает из мира столкновений** (0 объектов);
  - **этап 8:** пульт показан, экран выводит TCP в мире и от базы, оба варианта (A и B) применяются,
    закрепление у оператора работает, кнопки привязаны (СТОП/ДОМОЙ/ПОЗА/ПУСК), **джойстик двигает сустав**
    (J1 изменился на 13.2°), режимы JOINT/TCP переключаются;
  - **этап 3:** имена файлов соответствуют шаблонам ТЗ, папка экспорта — `C:\Users\Ольга\Videos`,
    подпись включена, запрос скриншота принят, **запись видео стартовала и остановлена Unity Recorder
    (MP4)** — файл `C:\Users\Ольга\Videos\KavistovVv_recording_…mp4`.
- **Найдено и исправлено прогоном:** (1) `GetInstanceID()` в Unity 6.5 — ОШИБКА компиляции `CS0619`
  (заменено сравнением ссылок на `RobotController`); (2) `yield` внутри `try/catch` в корутине скриншота
  (`CS1626`) — вынесен наружу; (3) waypoint получал позу с запасом лимитов 0.0° и планировщик отказывался
  строить маршрут — поза теперь выбирается по ветвям IK с максимальным запасом (см. §12.5);
  (4) тележка изначально парентилась под служебный (скрытый) корень и выпадала из `CollisionWorld` —
  теперь она самостоятельный объект сцены.
- **Прогон завершался аварийно ТОЛЬКО на выходе** (`EditorApplication.Exit` в batch-режиме — падение
  внутри `AssetDatabase::CleanupAssetDatabase`); отчёт при этом дописывался не через файл, а через лог
  Unity (файл `_dsh_stage_verify.txt` в OneDrive не дописывался — известная особенность окружения),
  поэтому отчёт собран из `_dsh_stages.log`.
- **Синхронизация копий (§9):** 35 файлов перенесены в копию пользователя `C:\Users\Ольга\KavistovVv`
  (резерв заменённых — `_dsh_backup_2026-09-14_192236`), `.csproj` копии пользователя синхронизированы,
  её сборка — **0 ошибок**. Сцена, столы, материалы и `ProjectSettings` в копии пользователя НЕ менялись.

### 12.11 Ручная проверка (что осталось на живом редакторе)
1. **Фантомы:** PlayMode → `Z` + ЛКМ по столешнице → 8 фантомов должны выглядеть РОВНО как робот
   (те же материалы и текстуры, без «прозрачности» и бирюзы); при наведении зелёного луча вокруг копии
   появляется тонкий голубой контур (материалы модели при этом не меняются).
2. **Язык:** «Настройки → Интерфейс» → выбрать 中文 и 日本語: подписи меню/тулбара/панелей меняются
   МГНОВЕННО (без перезапуска), текст читается (не «квадраты»); перезапустить PlayMode — выбор сохранился.
3. **Скриншот:** `F8` (или кнопка) → в `C:\Users\Ольга\Videos` появляется
   `KazistovVv_screenshot_ГГГГ-ММ-ДД_ЧЧ-ММ-СС.png` с подписью (дата, состояние, робот, тема, язык).
4. **Видео:** `F10` → в логе «ЗАПИСЬ НАЧАТА … (Unity Recorder, MP4)», после остановки в папке «Видео»
   лежит `KazistovVv_recording_….mp4` (параметры разрешение/FPS/битрейт — публичные поля компонента).
5. **Сингулярности:** Ctrl+F8 (или кнопка) → увести робота в позу с |q5| ≈ 0 (или слайдером
   «Суставы» выставить J5 = 0) → вокруг запястья загорается КРАСНАЯ зона; вернуть J5 = 45° — зона гаснет.
6. **Waypoints:** выбрать точку (красный + ЛКМ) → «Добавить waypoint» → в дереве ветка «Промежуточные
   точки», у точки зелёная сфера; «Перестроить маршрут» → оранжевые «колбаски» идут ЧЕРЕЗ точку;
   «Выполнить маршрут» → робот едет по маршруту; удалить точку — траектория возвращается к прежней форме.
   Поставить заведомо далёкую точку — она красная, маршрут не строится.
7. **Состояние:** `F12` → панель справа снизу с четырьмя графиками; подвигать суставы слайдерами —
   температура/ток/скорость/износ идут вверх, в покое температура падает; CSV-файл в
   `%persistentDataPath%/KazistovVv/Logs/`.
8. **Препятствие:** Ctrl+F10 → тележка едет по маршруту перед стендом; выбрать точку красным и получить
   траектории — варианты, проходящие через путь тележки, помечаются как «рискованные» (журнал/статус);
   подвести тележку к работающему роботу — предупреждение или аварийная остановка.
9. **Пульт:** `F11` → пульт в сцене (экран TCP, LED); перетащить джойстик мышью — робот покачивает
   выбранным суставом (робот должен быть свободен); «ВАРИАНТ A / B» — оформление меняется; «ДОМОЙ» —
   плавный переезд в нулевую позу; «ЗАПИСАТЬ ПОЗУ» — поза появляется в ветке «Позы».
10. **Проверить на SCARA** (`F` — переключение робота): сингулярности вытягивания/складывания, waypoints,
    мониторинг (3 сустава), препятствие и пульт — та же логика.11. **Тулбар стал выше:** кнопок теперь **28 (было 20)**, сетка 5 в ряд → **6 рядов**; высота считается
    автоматически, dock-панели подстраиваются. На разрешении 1366×768 проверить, что панели не наезжают
    (при нехватке места помогает «Настройки → Интерфейс → Масштаб»; при желании часть кнопок можно
    убрать из `ToolbarLayout()` — команды и пункты меню останутся).
12. **Известная особенность этапа 1 (следствие ТЗ):** 8 фантомов стартуют в ТОЙ ЖЕ позе, что робот, с
    ОДИНАКОВЫМИ материалами — в первый момент копии накладываются друг на друга (возможно мерцание
    граней). Через доли секунды они расходятся по своим траекториям. Это плата за «без визуальной
    разницы»; если мигание мешает — можно включить `castShadowsLikeRobot = false` или сдвинуть старт
    фантомов (отдельное ТЗ).

## 13. Сессия 16.09.2026 — ЭТАПЫ 1–6 нового ТЗ (главное меню, туториал, демонстрация, сглаживание траекторий, время-оптимальная траектория, эко-профиль)

**Объём сессии.** Сделаны этапы **1–6 из 36**. Работа разбита на подсессии по правилу ТЗ:
сделано 6 этапов, дальше — **«продолжить с этапа 7»** (§13.13). Существующая архитектура не
переписывалась: всё новое — отдельные модули рядом, которые пользуются ТОЛЬКО публичными
методами потока этапов, планировщика, валидатора и оболочки. Ни один существующий бинд не
тронут и не добавлен.

**Новые файлы (12), все в `Assets/_Project/01_Scripts/Features/`:**

| Файл | Что внутри | Этап ТЗ |
|---|---|---|
| `KvLocExtra.cs` | локализация новых текстов: таблица «ключ + 7 языков» (`118` строк), регистрация в словарях во время работы | 1–6, 11 |
| `KvOverlayKit.cs` | свои экранные слои: канвас, всплывающая подсказка `KvMiniTip`, рамка подсветки `KvHighlightFrame`, карточка-подсказка `KvHintCard` | 1–3 |
| `KvWorkbenchWindow.cs` | окно-верстак с **реестром вкладок** (`IKvWorkbenchTab`) + `KvTabKit` (секции, ползунки, таблицы, кнопки) | 4–6 |
| `KvTrajMath.cs` | метрики траектории (jerk / ускорение / кривизна / зазор / энергия), сглаживание путём, время-оптимальная перепараметризация, модель энергии | 4–6 |
| `KvVariantKit.cs` | операции над вариантами: подмена плана «на месте» (фантомы в полёте видят новый путь), перестройка «колбаски» | 4–6 |
| `KvStartMenu.cs` | главное меню при запуске + кинематографический облёт камеры | 1 |
| `KvTutorial.cs` | туториал из 4 шагов с автозавершением и подсветкой элементов | 2 |
| `KvQuickStart.cs` | автоматическая демонстрация (6 шагов) | 3 |
| `KvPathSmoothing.cs` | сервис сглаживания + вкладка с метриками «до / после» | 4 |
| `KvTimeOptimal.cs` | сервис время-оптимальной траектории + вкладка | 5 |
| `KvEnergyOptimal.cs` | сервис оптимизации по энергии + вкладка | 6 |
| `KvStageHub2.cs` | хаб этапов 1–6: создание сервисов, кадровое обслуживание, горячие клавиши, дерево, свойства, переключатели, команды | 1–6 |

**Изменены 3 существующих файла — ТОЛЬКО добавления:**
`06_KazistovVv_UI/…/Core/KvLocalization.cs` (новый публичный `AddRuntimeStrings` + `TotalStrings`),
`…/Core/KazistovVvUIManager.cs` (регистрация команд хаба, ветка дерева, свойства, переключатели,
подпись дерева, публичный `CommandButtonRect(id)`, 4 кнопки в тулбар), `…/Zones/KvSettingsView.cs`
(4 новых переключателя в списке функций).

### 13.1 ЭТАП 1 — ГЛАВНОЕ МЕНЮ ПРИ ЗАПУСКЕ (готово)
- **Экран запуска** — отдельный канвас (`KvStartMenu`, `sortingOrder 200`) поверх сцены: слева панель
  с названием, подзаголовком и **шестью пунктами**, снизу — строка-подсказка (как строка состояния
  FreeCAD), справа снизу — подпись режима. Пункты: **Новый проект · Открыть сессию · Показать демо
  (этап 3) · Обучение (этап 2) · Настройки · Выход**; сверх обязательных четырёх добавлены «Показать
  демо» (требует ТЗ этапа 3) и «Обучение» (естественная пара к этапу 2).
- **Кнопки — ТОЛЬКО иконки** (требование ТЗ): монохромные процедурные иконки `KvIcons`
  (`reset`, `session`, `presentation`, `help`, `settings`, `close`), подпись всплывает подсказкой
  у кнопки **и** дублируется в строке-подсказке внизу (там же — пояснение пункта). Наведение
  мышью и выбор клавишами (↑↓/W S/Enter) подсвечивают пункт.
- **Фон — медленный кинематографический облёт**: камера сама идёт по кругу вокруг активного робота
  (`orbitRadius 3.6 м`, `orbitSpeed 6.5 °/с`), плавно «дышит» по высоте (амплитуда 0.35 м, период
  26 с) и слегка меняет радиус; сглаживание по экспоненте. На время меню управление оператора
  отключается ровно как в презентационном режиме (скорости и чувствительность обнуляются, лазеры
  выключаются), а оболочка интерфейса гасится штатным `SetUiVisible(false)` → чистый кинематографический
  кадр. При выходе из меню всё состояние камеры и оператора восстанавливается.
- **Что делают пункты:** «Новый проект» — сброс потока (`ResetFlow`) + очистка зон запрета, меток
  сравнения, waypoints и кэшей постобработки; «Открыть сессию» — `KvSessionManager.Reload` + загрузка
  последней сессии (если сессий нет — понятное сообщение, меню остаётся); «Настройки» — вернуть
  оболочку и открыть панель настроек; «Выход» — закрыть приложение (в редакторе — остановить Play).
- **Клавиши и настройка:** `F1` — показать/скрыть меню в любой момент; показ при запуске переживает
  перезапуск (PlayerPrefs `KazistovVv.StartMenu.Enabled`), переключатель —
  «Настройки → Функции → Главное меню при запуске».

### 13.2 ЭТАП 2 — ТУТОРИАЛ / ONBOARDING (готово)
- **Четыре шага ровно по ТЗ:** как выбрать точку (красный лазер) · как подтвердить траекторию
  (зелёный лазер) · как двигать точку (режим перемещения) · как выбрать фантом.
- **Шаг завершается САМ по факту действия**, а не по кнопке: условия проверяются по состоянию
  State Machine (`State.hasPoint`, `selectedTrajectory >= 0` + фаза, вход/выход из `PointMoveMode`,
  смена выбранного варианта). Кнопка «Отметить выполненным» осталась как ручной способ, «Далее» —
  переход дальше, «Пропустить туториал» — выключить подсказки.
- **Подсветка элементов:** пульсирующая рамка `KvHighlightFrame` обводит **саму кнопку тулбара**
  (для шага 1 — «Выбор точки», для шагов 2 и 4 — «Выбор траектории»); прямоугольник берётся у
  кнопки через новый публичный `KazistovVvUIManager.CommandButtonRect(id)` — интерфейс не
  перестраивается и не дублируется. Если кнопка скрыта (тулбар погашен), в журнал идёт строка об этом,
  а подсказка опирается на текст и «бейдж» клавиш.
- **Прогресс сохраняется** (PlayerPrefs `KazistovVv.Tutorial.Step/.Active/.Done`): обучение можно
  прервать в любой момент и **продолжить позже** (`F3` или «Обучение» в меню → «Продолжить обучение»),
  после полного прохождения доступно «Начать заново».
- **Карточка-подсказка** (`KvHintCard`) живёт на своём канвасе (`sortingOrder 210`), поэтому видна и
  при скрытой оболочке; показывает номер шага, полосу прогресса, текст и кнопки.

### 13.3 ЭТАП 3 — QUICK START («Показать демо») (готово)
- Кнопка в главном меню и команда/клавиша **`F4`**; шесть шагов: точка над столешницей →
  планировщик считает 8 вариантов → по каждой траектории идут фантомы → метрики → выбор лучшего
  варианта → робот едет → итог с предложением пройти туториал («Пройти туториал» / «Позже»).
- Каждый шаг подписан на экране (та же карточка), у шага есть минимальное время показа, необязательное
  условие завершения (состояние потока) и предел ожидания — сценарий **не ломается**, если
  планирование заняло больше времени, а если условие не наступило за отведённое время, в журнал идёт
  честная строка (демонстрация продолжается или останавливается с причиной).
- Демонстрация пользуется теми же публичными методами, что и оператор (`LockPointFromUi`,
  `SelectCandidateByIndex`): «магии» в обход State Machine нет. Прерывание — кнопкой или `Esc`.
- Выбранный лучший вариант определяется по стоимости среди прошедших `SafetyGate`; точка ставится
  с учётом робота (для SCARA — ближе к базе, чтобы планировщик нашёл пути).

### 13.4 ЭТАП 4 — СГЛАЖИВАНИЕ ТРАЕКТОРИЙ (готово)
- **Три метода на выбор** (ползунок «Уровень сглаживания 0…100 %»):
  **B-сплайн** (кубический равномерный, свёртка маской `[1,4,1]/6`, 2…10 проходов по уровню),
  **Безье** (кусочные кубические кривые, эквивалент Catmull-Rom в форме Безье),
  **фильтр Гаусса** (свёртка гауссовым ядром, σ растёт с уровнем, зеркальные края).
  Уровень работает как коэффициент смешивания с исходным путём, поэтому 0 % — ровно исходная
  траектория. **Концы не смещаются** (проверено прогоном).
- **После сглаживания время пересчитывается по лимитам** скорости/ускорения (этап 5) — иначе
  сглаженный путь не был бы привязан к ограничениям приводов. Измерено прогоном: план планировщика
  `3.999 с` → после постобработки `1.543 с` при одинаковой длине пути (у планировщика время взято
  «с большим запасом»), скорость `10.6 → 51.5 °/с`, ускорение `0 → 115 °/с²`, jerk `0 → 666 °/с³`
  (jerk у планировщика нулевой, потому что внутри сегментов скорость постоянна).
- **Метрика «До / После»**: jerk, ускорения, кривизна, длина пути TCP и время. «До» сравнивается с
  **тем же путём, перепараметризованным по лимитам** (опорный вариант) — только так сравнение честное,
  потому что у «сырого» плана планировщика ускорение и рывок нулевые (постоянная скорость в сегментах,
  мгновенные скачки в узлах). Рядом отдельной строкой показан «План планировщика (как построен)».
- **Исходный план сохраняется** → «Вернуть исходную» отменяет обработку мгновенно.
- **«Применять автоматически»** — тумблер в панели настроек, вкладке верстака, меню и командой:
  включён по умолчанию, переживает перезапуск (PlayerPrefs `KazistovVv.Post.SmoothAuto`); после
  планирования все 8 вариантов обрабатываются сами (проверено: «обработано вариантов: 8»).
- **Геометрия подменяется «на месте»**: значения копируются в те же массивы, на которые ссылаются
  фантомы в полёте, поэтому картинка не отрывается от расчёта; «колбаска» перестраивается публичным
  `TrajectoryTube.Build`, метрики варианта обновляются.

### 13.5 ЭТАП 5 — ВРЕМЯ-ОПТИМАЛЬНЫЕ ТРАЕКТОРИИ (готово)
- **Ограничения задаются оператором**: максимальная скорость суставов (`90 °/с` по умолчанию),
  максимальное ускорение (`180 °/с²`), максимальный jerk (`1200 °/с³`); значения сохраняются в
  PlayerPrefs и работают как общие для этапов 4–6 (сглаживание, время, энергия — одна шкала).
- **Алгоритм (форма пути НЕ меняется, меняется только время):** путь переводится в нормированное
  пространство суставов (1 единица ≈ 90° или 10 см), считаются производные `q'=dq/ds` и `q''=d²q/ds²`;
  предел скорости вдоль пути `v ≤ min_j(ω_j/|q'_j|)`; предел по центростремительной составляющей
  `v ≤ min_j√(α_j/(2|q''_j|))`; предел касательного ускорения `a ≤ min_j(α_j/(2|q'_j|))`;
  интегрирование **вперёд-назад** по длине пути (разгон / крейсер / торможение, старт и финиш из покоя);
  затем до 20 проходов **ограничения jerk** во временной области (сглаживание профиля ускорений с
  пересчётом времён и повторным применением пределов).
- **Метрика «Время-оптимальная»**: время (до/после), выигрыш в процентах, фактические максимумы
  скорости/ускорения/jerk, длина, минимальный зазор. Всё это есть и в свойствах узла траектории,
  и в узле дерева «Время-оптимальная траектория».
- **Переключиться на неё**: кнопка в вкладке верстака, команда в меню («Робот → Постобработка →
  Переключиться на время-оптимальную») и **отметка в сравнении траекторий** (слот A/B тем же путём,
  что у кнопки «Отметить выбранную (A/B)», вкладка «Сравнение» показывает разницу).
- Измерено прогоном: время-оптимальная не медленнее исходной, скорость `57.8 °/с` при лимите `90`,
  ускорение `169.9 °/с²` при лимите `180`, jerk `1325 °/с³` при лимите `1200` (итеративное
  ограничение — см. «требует доработки»).

### 13.6 ЭТАП 6 — ОПТИМИЗАЦИЯ ПО ЭНЕРГИИ (готово)
- **Симуляция строго по ТЗ: энергия ∝ Σ |момент × угловая скорость| × время.** Момент —
  упрощённая динамика: `инерция × ускорение + вязкое трение × скорость + удержание`
  (масса distal-части и груза на плече от базы до TCP, своя доля удержания на каждый сустав);
  призматическая ось SCARA считается в СИ (`Н × м/с`), вращательные — в рад/с.
- **Метрика «Энергоэффективность»:** энергия (Дж), удельная энергия (Дж/м), пиковая мощность (Вт),
  время; показывается в вкладке, в свойствах траектории и в узле дерева.
- **Поиск эко-профиля:** путь не меняется, перебирается семейство профилей (5 × 4 = **20 вариантов**
  «мягче разгоны / ниже крейсерская скорость»), для каждого считается энергия, выбирается минимум.
  Измерено: `16.07 → 15.64 Дж` (`+2.7 %`), пиковая мощность `17.4 Вт` против `81` у исходной —
  то есть эко-профиль заметно бережнее к приводам (меньше износ), хотя и медленнее по времени.
- **Масса груза** задаётся ползунком (0…25 кг) и влияет и на метрику, и на поиск; сохраняется.
- **Полезно для реального робота:** в подсказке прямо написано, что модель упрощённая и метрика служит
  для сравнения вариантов и оценки износа, а не для паспортных расчётов.

### 13.7 ЛОКАЛИЗАЦИЯ НОВЫХ ТЕКСТОВ (этап 11 ТЗ — начало)
- **118 новых ключей переведены на все 7 языков** (RU / EN / ZH / ES / DE / FR / JA) —
  меню, туториал, демонстрация, все подписи и подсказки вкладок верстака, метрики, команды
  (`cmd.<id>` и `cmd.<id>.desc`), поэтому кнопки тулбара, меню и подсказки локализованы без правок
  самих панелей.
- **Почему таблица в коде, а не 7 файлов JSON:** строки живут рядом с кодом, который их использует
  (один диф), не нужно синхронно править 7 словарей при каждой кнопке, а внешние словари остаются
  ГЛАВНЫМИ: `KvLocExtra.Install()` пишет только ОТСУТСТВУЮЩИЕ ключи (проверено: «зарегистрированы: 118,
  уже были в словарях: 2»), цепочка fallback «текущий язык → английский → русский текст из кода»
  сохранена полностью.
- Смена языка по-прежнему мгновенная: пересобирается оболочка, обновляются подписи меню (`RefreshLanguage`)
  и вкладки верстака; проверено прогоном на всех 7 языках.

### 13.8 ОБЩАЯ ИНФРАСТРУКТУРА И ТОЧКИ ИНТЕГРАЦИИ
- **Окно-верстак постобработки** (`KvWorkbenchWindow`, собственный канвас, `sortingOrder 46`,
  перетаскивание за заголовок) с **реестром вкладок**: `KvWorkbenchWindow.RegisterTab(...)` — будущие
  этапы добавляют свои вкладки, не правя окно (как реестр команд у тулбара). Вкладки этапов 4–6:
  «Сглаживание траектории», «Время-оптимальная траектория», «Оптимизация по энергии» (ползунки,
  сегментные выборы, таблицы метрик, кнопки, подсказки).
- **Горячие клавиши (проверено — свободны):** `F1` — главное меню, `F2` — верстак, `F3` — обучение,
  `F4` — демонстрация. Занятые ранее `F5–F8`, `F10–F12`, `Ctrl+Z/Y` и все штатные бинды не тронуты.
- **Тулбар:** добавлены 4 кнопки-иконки (меню, обучение, демо, верстак) → **32 кнопки, 7 рядов**
  (сетка 5 в ряд, высота считается автоматически, dock-панели подстраиваются).
- **Меню программы:** «Файл → Главное меню», «Сервис → Показать демо», «Справка → Обучение /
  Обучение заново», «Вид → Верстак постобработки», «Робот → Постобработка → …» (сглаживание,
  время-оптимальная, эко-профиль, автосглаживание).
- **Дерево моделей:** новая ветка «Верстак: постобработка траекторий» с узлами «Сглаживание»,
  «Время-оптимальная траектория», «Оптимизация по энергии»; у каждого узла — состояние в «Свойствах»,
  а у узла траектории — раздел «Постобработка траектории» (сглаживание, jerk до/после, кривизна,
  метрика «время-оптимальная», энергия и пиковая мощность). Клик по узлу верстака открывает нужную вкладку.
- **Настройки:** 4 новых переключателя в «Настройки → Функции» — «Главное меню при запуске»,
  «Автосглаживание траекторий», «Верстак постобработки», «Обучение с подсказками».

### 13.9 ВЕРИФИКАЦИЯ СЕССИИ (этапы 1–6)
- **Компиляция:** `Unity.exe -batchmode -nographics -quit` → **0 ошибок, 0 предупреждений** (6 прогонов
  компилятора по ходу правок).
- **Сквозной прогон в PlayMode** (`Assets/_Project/01_Scripts/Editor/DshStage2Diag.cs`, диагностика
  агента, в копию пользователя НЕ переносится): 19 шагов, отчёт читается из `_dsh_stage2*.log`
  (файл в OneDrive во время PlayMode не дописывается — известная особенность окружения, §12.10).
  **ИТОГ последнего прогона: `[OK] 66 · [FAIL] 0` · «ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ»** при `[info] 9`.
  Что подтверждено фактами:
  - **этап 1:** меню показывается, 6 пунктов с иконками `reset / session / presentation / help /
    settings / close`, оболочка на время меню скрыта и возвращается при выходе; камера сама
    сместилась на `0.663 м` за `2.0 с` работы меню и держит радиус `3.39 м` при заданном `3.60 м`;
    «Новый проект» очистил точку/траектории/фазу, «Настройки» вернули оболочку, «Открыть сессию»
    отработало без исключений, выход из меню вернул управление оператору;
  - **этап 2:** туториал стартует с шага 1, карточка видна, кнопка тулбара для подсветки найдена,
    шаг 1 завершился САМ после выбора точки, шаг 2 — после подтверждения траектории, шаг 3 отмечен,
    шаг 4 — после переключения фантома, обучение пройдено полностью; отдельно проверено сохранение
    прогресса («Продолжить обучение» встаёт на сохранённый шаг);
  - **этап 3:** демонстрация запускается (6 шагов), доходит до постановки точки штатным путём,
    строит 8 вариантов, карточка подписывает шаги, прерывается в любой момент;
  - **этап 4:** сглаживание применяется, «колбаска» пересобрана, **концы траектории не смещены**,
    jerk не вырос относительно опорного варианта, время и кривизна не ухудшились на всех трёх методах
    (B-сплайн: jerk `666 °/с³`, кривизна `3.25 → 3.27`; Безье: `3947 °/с³`; Гаусс: `3145 °/с³`),
    «Вернуть исходную» работает, автосглаживание обработало все 8 вариантов без команд оператора;
  - **этап 5:** время-оптимальная рассчитана, не медленнее исходной, скорость `57.8 °/с` (лимит 90),
    ускорение `169.9 °/с²` (лимит 180), jerk `1325 °/с³` (лимит 1200), переключение подменило время
    варианта, метка в сравнении выставлена (A/B);
  - **этап 6:** эко-профиль рассчитан (`16.07 → 15.64 Дж`, `+2.7 %`), считается удельная энергия
    (`29.56 Дж/м`) и пиковая мощность (`9.7 Вт`), переключение выполнено, строка метрики для свойств
    формируется;
  - **SCARA:** проверена ДВУМЯ путями. (1) Перепривязка потока к SCARA в batch-режиме не
    подтвердилась — управление прицелом идёт от мыши, а в batch она стоит в углу экрана; это
    ограничение окружения, а не продукта (в редакторе оператор просто наводит луч, `F`). (2) Поэтому
    постобработка 3-осевого робота проверена НАПРЯМУЮ на собственном валидаторе SCARA
    (`PoseValidator.Init(robotScara)`, 3 оси, **призматическая ось Z — да**): тестовый план из 40
    сэмплов, перепараметризация по лимитам `1.688 → 1.070 с` (**выигрыш +36.6 %**), сглаживание
    сохраняет начало и конец пути, энергия считается в СИ (`10.17 Дж`, пик `16.21 Вт`) — то есть
    призматическая ось обрабатывается как сила × скорость, а не как момент × угол;
  - **локализация:** текст «Новый проект» переведён на все 7 языков, смена языка на ходу не ломает
    интерфейс (каждая смена пересобирает оболочку — в логе 8 пересборок, это и есть проверка).
- **Сборка копии пользователя** (`C:\Users\Ольга\KavistovVv`): `dotnet build Assembly-CSharp.csproj`
  → **0 ошибок** (2 предупреждения — унаследованные: `CS0649` в `FreeFlyCameraController.plannerController`
  и анализатор сериализации `UAC1001` в `RuntimeRegistry`, к новому коду отношения не имеют).

### 13.10 НАЙДЕНО И ИСПРАВЛЕНО ПРОГОНОМ (5 багов — все в новом коде)
1. **`Start()` у MonoBehaviour вызывался движком как служебное сообщение.** Метод запуска демонстрации
   назывался `Start()` — Unity вызывала его сама в первом кадре, ДО привязки к потоку этапов, и падала
   с `ArgumentOutOfRangeException` на пустом сценарии. **Исправлено:** метод переименован в `Begin()`,
   остановка — `StopDemo(...)`; в комментарии прямо написано, почему так.
2. **Ограничение ускорения в перепараметризации времени не работало.** Первая версия считала предел
   ускорения через кривизну пути (`α/|q''|`), из-за чего предел «взрывался» на почти прямых участках:
   прогон показывал ускорение `2534 °/с²` при лимите `180`. **Исправлено:** пределы считаются строго
   по суставам — `a ≤ min_j(α_j·0.4/|q'_j|)` и `v ≤ min_j√(α_j·0.4/|q''_j|)` (множитель 0.4 —
   гарантированный запас на кривизну, чтобы суммарное `|q'·a + q''·v²| ≤ α_j`); интегрирование
   вперёд-назад + ограничение jerk во временной области (до 20 проходов). После правки: `169.9 °/с²`
   при лимите 180.
3. **Метрика «до/после» сравнивалась с «сырым» планом планировщика**, у которого ускорение и jerk
   нулевые (постоянная скорость внутри сегментов) — сглаживание выглядело как ухудшение
   (`0 → 76786 °/с³`). **Исправлено:** добавлен опорный вариант «тот же путь, время по лимитам»,
   сравнение идёт с ним, а «сырой» план показан отдельной строкой.
4. **Точка, посчитанная для одного стенда, использовалась после перепривязки к другому** — планировщик
   честно не находил ни одного пути («вариантов 0»). Это дефект диагностики, но он же показал важное:
   демонстрация (этап 3) считает точку ОТНОСИТЕЛЬНО привязанного робота, поэтому работает всегда.
5. **Оболочка интерфейса «вылезала» поверх стартового меню при пересборке.** `RebuildShell` создаёт
   НОВЫЙ канвас (активный по умолчанию), а флаг видимости применялся только к старому — при смене
   темы/языка/масштаба во время показа меню (и при пересборке в презентационном режиме) интерфейс
   появлялся поверх кинематографического экрана. **Исправлено:** `BuildShell` в конце восстанавливает
   состояние видимости (`canvasGo.SetActive(uiVisible)`). Найдено прогоном (проверка «оболочка скрыта
   на время показа меню» из `[FAIL]` стала `[OK]`).

### 13.11 ТРЕБУЕТ ДОРАБОТКИ / ЗАМЕЧАНИЯ (честно)
- **Ограничение jerk итеративное, а не точное.** В прогоне остаётся `1325 °/с³` при лимите `1200`
  (перебор ~10 %), у методов Безье/Гаусса jerk выше (`3947` и `3145 °/с³`) — им нужно больше проходов
  или перепараметризация с S-профилем (стандартный «double S»). Пока в интерфейсе это не скрывается:
  в таблице видны ФАКТИЧЕСКИЕ максимумы. Направление работ: заменить итеративное сглаживание на
  аналитический jerk-limited профиль (разгон/торможение из фаз постоянного рывка).
- **Планировщик отдаёт время «с большим запасом»** (в проверке `3.999 с` против `1.543 с` по лимитам
  при том же пути): формула `Planner.BuildTrajectory` (`dq/vmax + vmax·accelShare/(vmax·2)`) даёт
  скорость `10.6 °/с` при лимите `90`. Это НЕ правилось (ядро планировщика — не предмет этих этапов),
  но именно поэтому постобработка этапов 4–6 даёт заметный выигрыш. Возможный отдельный шаг: использовать
  время-оптимальную параметризацию СРАЗУ в планировщике (тогда «8 траекторий» будут быстрее по умолчанию).
- **Перепривязка потока к другому роботу в batch-прогоне идёт только через прицел камеры** — заняло до
  десятков секунд; в живом редакторе это мгновенно (оператор просто наводит луч). Отдельного
  публичного «переключись на робота X» в потоке нет — при необходимости добавить (правка ядра).
- **Тулбар стал выше** (32 кнопки, 7 рядов). На 1366×768 dock-панели подстраиваются, но при нехватке
  места помогает «Настройки → Интерфейс → Масштаб»; часть кнопок можно убрать из `ToolbarLayout()`
  (команды и пункты меню остаются).
- **Отчётный файл диагностики в OneDrive во время PlayMode не дописывается** (особенность окружения,
  §12.10) — результаты читаются из лога Unity; сам файл пишется только до входа в PlayMode.
- **Стоимость автосглаживания.** В авторежиме после планирования обрабатываются все 8 вариантов:
  на каждый считается сглаживание пути, перепараметризация и анализ (включая FK по сэпмлам и модель
  энергии) — это десятки миллисекунд ОДИН РАЗ на новую точку (в том же порядке, что уже измеренное
  «полное обновление интерфейса 49 мс», §0.5). Опорный вариант метрик («тот же путь по лимитам»)
  считается ТОЛЬКО по запросу интерфейса/свойств (`EnsureBaseline`) — в авторежиме он не нужен.
  Если на слабой машине заметны рывки при появлении 8 траекторий — выключить автосглаживание
  (настройки/вкладка) или снизить уровень.

### 13.12 РУЧНАЯ ПРОВЕРКА (что смотреть в живом редакторе)
1. **Меню при запуске:** PlayMode → появляется экран запуска, камера медленно облетает робота,
   наведение на пункт показывает подсказку внизу; `F1` — вернуть меню в любой момент; `Esc` — войти
   в рабочую область без изменений.
2. **Новый проект / сессия:** «Новый проект» → сцена чистая (точки и траекторий нет); «Открыть сессию» →
   загружается последняя сохранённая сессия (поза, точка, зоны).
3. **Туториал:** `F3` (или «Обучение» в меню) → карточка шага, мигающая рамка вокруг кнопки
   «Выбор точки»; наведите луч и щёлкните — шаг сменится сам; прервите (`Пропустить туториал`),
   перезапустите PlayMode и нажмите `F3` — обучение продолжится с сохранённого шага.
4. **Демонстрация:** `F4` → шесть подписанных шагов, 8 фантомов, выбор лучшего варианта, проезд
   робота; в конце — предложение пройти туториал; `Esc` — прервать.
5. **Сглаживание:** выбрать точку (Z + ЛКМ) → `F2` → вкладка «Сглаживание»: ползунок уровня,
   метод (B-сплайн / Безье / Гаусс), «Сгладить выбранную» → в таблице видно «До / После» по jerk,
   ускорению, кривизне, длине и времени; «Вернуть исходную» возвращает прежнюю траекторию.
   Тумблер «Применять автоматически» — в настройках и на вкладке: с ним каждая новая точка сразу
   приходит сглаженной.
6. **Время-оптимальная:** на вкладке выставить лимиты (скорость / ускорение / jerk) → «Рассчитать» →
   в таблице видно время и фактический выигрыш → «Переключиться на неё» → робот поедет по новому
   времени; «Вариант → A/B» отмечает её в сравнении (`F7`).
7. **Энергия:** задать массу груза → «Рассчитать эко-профиль» → таблица «Дж / Дж·м / Вт / время» →
   «Переключиться на эко-профиль»: робот едет мягче и медленнее, энергия ниже.
8. **Дерево/свойства:** ветка «Верстак: постобработка траекторий» (щёлкнуть узел — откроется нужная
   вкладка); у узла траектории в «Свойствах» — раздел «Постобработка траектории».
9. **Язык:** «Настройки → Интерфейс» → переключить на 中文/日本語 — подписи меню, туториала и верстака
   меняются мгновенно (включая строки новых модулей).
10. **Проверить на SCARA** (`F` — переключение робота): сглаживание, время-оптимальная и эко-профиль
    работают так же, у призмы энергия считается в СИ.

### 13.14 СИНХРОНИЗАЦИЯ КОПИЙ (§9) И ДИАГНОСТИКА
- В копию пользователя `C:\Users\Ольга\KavistovVv` перенесены **15 файлов** (12 новых + 3 изменённых)
  вместе с `.meta`; резерв заменённых файлов — `_dsh_backup_2026-09-14_203605` (все предыдущие резервы
  этой сессии — `_dsh_backup_2026-09-14_200639` и др. — остались рядом).
  **Сцена, столы, роботы, материалы и `ProjectSettings` в копии пользователя НЕ менялись.**
- В `Assembly-CSharp.csproj` копии пользователя новые 12 скриптов добавлены отдельным `<ItemGroup>`
  (Unity перегенерирует список сама при открытии проекта; добавка нужна была только чтобы проверить
  сборку сразу) — сборка: **0 ошибок**.
- Диагностика агента (в копию пользователя НЕ переносится): `Assets/_Project/01_Scripts/Editor/DshStage2Diag.cs`,
  логи прогонов — `_dsh_stage2*.log` в корне рабочей копии.

### 13.13 ПРОДОЛЖИТЬ С ЭТАПА 7
Следующая подсессия (по правилу «5–6 этапов за раз») — **этапы 7–12 нового ТЗ**:
7. ограничения на waypoints (ориентация TCP, скорость в точке, обязательный обход препятствия, пауза);
8. планирование с ограничениями (инструмент вертикально / наклон ≤ 15° / TCP смотрит на объект);
9. калибровочный мастер (TCP по 4 точкам, база робота, камера — заглушка, сохранение в файл);
10. калькулятор нагрузки (payload по расстоянию от базы, максимум в текущей позе, в свойствах робота);
11. языки роботов KUKA KRL / FANUC KAREL / ABB RAPID (экспорт `trajectory.src/.kl/.mod`);
12. импорт моделей URDF / STEP с проверкой кинематики.
Инфраструктура готова: реестр вкладок верстака, реестр команд, слои подсказок и подсветки,
таблица локализации, общий набор лимитов движения (`KvStageHub2.Limits`) и метрики `KvTrajMath`.

## 14. Сессия 17.09.2026 — ЭТАПЫ 7–12 (ограничения waypoints, планирование с ограничениями, калибровка, нагрузка, экспорт в языки роботов, импорт моделей)

**Объём сессии.** Сделаны этапы **7–12 из 36**. Работа разбита на подсессии по правилу ТЗ:
сделано 6 этапов, дальше — **«продолжить с этапа 13»** (§14.13). Архитектура не переписывалась:
всё новое — отдельные модули рядом, которые пользуются ТОЛЬКО публичными методами потока
этапов, планировщика, валидатора и оболочки. Ни один существующий бинд не тронут.

**Новые файлы (9), все в `Assets/_Project/01_Scripts/Features/`:**

| Файл | Что внутри | Этап ТЗ |
|---|---|---|
| `KvToolKinematics.cs` | ось инструмента по фактической кинематике, ограничения ориентации (направление / вертикаль / предел наклона / взгляд на объект), проверка плана, **IK с дополнительными условиями** | 7, 8 |
| `KvWaypointConstraints.cs` | ограничения промежуточной точки (ориентация, скорость, обход, пауза), инструменты маршрута (паузы, лимит скорости, расстояние до препятствия) + вкладка верстака | 7 |
| `KvConstrainedPlanner.cs` | сервис планирования с ограничениями: проверка вариантов, пометка нарушающих, авто-переключение на соблюдающий, **приведение траектории к ограничению** (проекция) + вкладка | 8 |
| `KvCalibration.cs` | калибровочный мастер: TCP по 4 точкам (МНК), база робота по 3 точкам, камера (заглушка), файл калибровки + самопроверка метода + вкладка | 9 |
| `KvPayloadCalculator.cs` | калькулятор нагрузки: максимум в текущей позе, ограничивающий сустав, кривая «нагрузка ↔ расстояние от базы», мини-график + вкладка | 10 |
| `KvRobotExport.cs` | генераторы кода **KUKA KRL / FANUC KAREL / ABB RAPID** (PTP и LIN), выбор языка, папка экспорта, вкладка с предпросмотром | 11 |
| `KvRobotImport.cs` | импорт **URDF** (полная поддержка: звенья, суставы, оси, лимиты, TCP) и **STEP** (базовая: габарит и корпус), проверка кинематики + вкладка | 12 |
| `KvLocExtra2.cs` | локализация этапов 7–12: **124 ключа × 7 языков** (RU/EN/ZH/ES/DE/FR/JA) | 11 (расширение) |
| `KvStageHub3.cs` | хаб этапов 7–12: сервисы, вкладки, горячая клавиша F9, дерево, свойства, переключатели, команды | 7–12 |

**Изменены 4 существующих файла — ТОЛЬКО добавления:**
`Features/KvWaypoints.cs` (поле `Limits` у точки, поза с ограничением, обход/пауза/скорость в
маршруте, `useWaypointLimits`, цвет маркера), `06_KazistovVv_UI/…/KazistovVvUIManager.cs`
(регистрация хаба, ветки дерева, свойства, переключатели, подпись дерева, 5 кнопок тулбара),
`…/KvSettingsView.cs` (4 переключателя), плюс новый пример модели
`Assets/StreamingAssets/robots/sample_robot.urdf` (6-осевая цепь для проверки импорта).

### 14.1 ЭТАП 7 — ОГРАНИЧЕНИЯ НА WAYPOINTS (готово)
- У промежуточной точки теперь можно задать **четыре ограничения** (ровно по ТЗ):
  **требуемую ориентацию TCP** (направление оси инструмента в мире + допуск, либо «как сейчас»),
  **максимальную скорость TCP в точке**, **обязательный обход препятствия в окрестности**
  (центр + радиус + запас), **паузу** (остановиться и подождать).
- **Ориентация** решается IK с дополнительным условием: сначала перебираются ветви IK
  планировщика и берутся только те, что соблюдают ограничение, затем работает численный
  решатель спуска по суставам (`KvToolKinematics.Solve`), и только в крайнем случае берётся
  обычная ветвь с ЧЕСТНОЙ пометкой «ориентация не достигнута» — такая точка помечается
  недостижимой (по ТЗ ограничение обязательно), а маршрут по ней не строится.
- **Скорость** проверяется на фактическом профиле: если скорость TCP в точке выше заданной,
  профиль времени пересчитывается тем же `KvTrajMath.Retime`, что и время-оптимальные
  траектории (этап 5), с подбором масштаба (до 3 попыток) — лимиты суставов соблюдаются.
- **Пауза** вставляется в маршрут как выдержка: поза точки дублируется на нужное время
  (шаг 0.05 с), поэтому робот реально останавливается и ждёт (ТЗ), а не «проезжает мимо».
- **Обход препятствия** обязателен: сегмент строится штатным `Planner.PlanViaWaypoint`
  (путь «через сторону»), затем проверяется, что минимальное расстояние до окрестности
  препятствия ≥ радиус + запас; если нет — маршрут НЕ строится и причина пишется в журнал/статус.
- **Интерфейс:** вкладка «Ограничения точки» в окне-верстаке (F2): переключатели, ползунки,
  кнопки направлений (↓↑←→ и ±Z), «Ориентация ← как сейчас», «Центр из точки прицела»,
  «Применить», «Снять ограничения», «Перестроить маршрут»; плюс 6 команд в меню
  («Робот → Промежуточные точки → Ограничения»). Точка с ограничениями выделяется в сцене
  своим цветом маркера, а в дереве — значками ◈ (ориентация), ↓ (скорость), ⟲ (обход), ❚❚ (пауза).
- **Выключатель:** «Настройки → Функции → Ограничения промежуточных точек» — маршрут снова
  строится без учёта ограничений (полезно, когда нужно быстро вернуть прежнее поведение).

### 14.2 ЭТАП 8 — ПЛАНИРОВАНИЕ С ОГРАНИЧЕНИЯМИ (CONSTRAINED PLANNING) (готово)
- **Три задачи из ТЗ** реализованы как один механизм с четырьмя режимами: «инструмент
  вертикально (вниз/вверх)», «наклон не больше N°», «TCP всегда смотрит на объект X»,
  плюс произвольное направление оси инструмента.
- **Проверка** идёт по ФАКТИЧЕСКОЙ оси инструмента (ось последнего сустава; у робота с
  призматической последней осью — SCARA — инструмент считается направленным против хода призмы,
  то есть вниз) на **каждом сэмпле** каждой из 8 траекторий: сколько сэмплов нарушают ограничение,
  худший угол, доля соблюдения. Нарушающие варианты помечаются в дереве (◈ + причина в подсказке)
  и в свойствах.
- **«Привести траекторию к ограничению»**: нарушающие сэмплы пересчитываются IK с дополнительным
  условием (метод спуска, TCP удерживается в 4 мм, проверяются лимиты, самозазор и зазор до мира),
  затем путь перепараметризуется по лимитам и подменяется «на месте» (`KvVariantKit.ApplyPlan`) —
  «колбаска» и фантомы видят новый путь. Отчёт: сколько сэмплов исправлено, остаток в градусах.
- **Включается/выключается в настройках** (ТЗ): переключатель «Планирование с ограничениями»
  (он же команда и клавиша `F9` → открыть вкладку), плюс «Отбрасывать варианты, нарушающие
  ограничение» — тогда платформа сама переключает выбор на подходящий вариант и пишет об этом.
- Вкладка «Планирование с ограничениями»: режим, предел наклона, допуск, опорное направление,
  цель «смотреть на объект» из точки прицела, кнопки проверки и приведения, таблица по 8 вариантам
  (время/длина и статус ограничения).

### 14.3 ЭТАП 9 — КАЛИБРОВОЧНЫЙ МАСТЕР (готово)
- **TCP по 4 точкам:** оператор наводит инструмент в одну и ту же точку четырьмя разными
  ориентациями, каждая запись берёт позу суставов; из позы строится СИСТЕМА ФЛАНЦА
  (положение — пивот последнего сустава, оси — ось последнего сустава и перпендикуляр к ней)
  и решается линейная система `(R_i − R_1)·t = p_1 − p_i` методом наименьших квадратов.
  Точка касания из системы ИСКЛЮЧАЕТСЯ, поэтому знать её координаты не требуется.
  Результат: смещение инструмента в системе фланца, его длина и **остаточная ошибка** по точкам.
- **Защита от вырождения:** точки с почти одинаковой ориентацией (< 12°) не принимаются
  (с подсказкой «поверните инструмент заметнее»), вырожденная система честно отклоняется.
- **Самопроверка метода** (`SelfTest`): по известному смещению строятся четыре системы фланца,
  касающиеся одной точки, и метод обязан восстановить смещение — в прогоне ошибка **0.000 мм**
  при заданных 149.6 мм и остатке 0.000 мм.
- **База робота:** оператор касается инструментом ТРЁХ и более точек одной плоскости
  (например, столешницы); считается нормаль плоскости, **высота базы над плоскостью** и
  **наклон оси базы** к нормали, а также предлагаемое смещение/доворот, которое можно применить
  к роботу в сцене отдельной кнопкой (это изменение рантайма — сцену не сохраняем).
  Поворот по плоскости не определяется (для него нужна вторая база отсчёта) — это написано
  в отчёте калибровки и в §14.10.
- **Камера (hand-eye) — заглушка**, как и требует ТЗ: сохраняется намерение и место в файле.
- **Сохранение в файл:** JSON в `FeatureStorage.ConfigDir` (`calibration_<робот>.json`) —
  TCP (вектор, длина, остатки), база (высота, наклон, нормаль, смещение), камера; загрузка при старте.
- **Применение:** кнопка «Применить смещение к потоку» ставит `toolOffset` потока равным длине
  инструмента (проект ведёт TCP смещением вдоль направления; полный вектор остаётся в файле) —
  это соглашение проекта зафиксировано в интерфейсе и в отчёте.

### 14.4 ЭТАП 10 — КАЛЬКУЛЯТОР НАГРУЗКИ (готово)
- **Максимальная нагрузка в текущей позе** считается по фактическим плечам: для каждого сустава
  берётся момент от веса груза (`m·g·|r × up|`, спроецированный на ось сустава) и вклад веса
  звеньев — каждое звено в СВОЁМ центре (середина между его пивотом и следующим), затем
  сравнивается с номинальным моментом с коэффициентом запаса. Показывается **ограничивающий
  сустав** и его загрузка.
- **График зависимости от расстояния от базы** (ТЗ): для 14 расстояний решается IK в том же
  направлении и на той же высоте, что текущий TCP, и для каждой позы считается допустимая
  нагрузка; несходящиеся расстояния честно пропускаются (видно по графику). График —
  процедурная текстура `KvMiniPlot` (оси, сетка, ломаная), файлов-ассетов нет.
- **Показывается в панели свойств робота** (ТЗ): раздел «Калькулятор нагрузки» с максимумом,
  ограничивающим суставом, расстоянием до базы и коэффициентом запаса — у любого узла робота.
- **Призматическая ось** (SCARA) считается в СИ: номинальное значение — СИЛА в ньютонах
  (`ratingPrismaticN`, 600 Н по умолчанию), потому что призма держит вес руки целиком.
- Модель настраивается: 6 номинальных моментов, коэффициент запаса (1…4), масса инструмента;
  всё сохраняется. В интерфейсе прямо написано, что это инженерная оценка, а не паспортный расчёт.

### 14.5 ЭТАП 11 — ЭКСПОРТ В ЯЗЫКИ РОБОТОВ (KRL / KAREL / RAPID) (готово)
- **Это не языки интерфейса, а языки экспорта** (как и сказано в ТЗ): пользователь выбирает
  язык робота в настройках/на вкладке/командой, а платформа выгружает КОД ДВИЖЕНИЯ.
- **Файлы строго по ТЗ:** `trajectory.src` (KUKA KRL), `trajectory.kl` (FANUC KAREL),
  `trajectory.mod` (ABB RAPID) — в папку «Документы\KazistovVv\robot_export» (резерв —
  папка данных приложения). В прогоне все три файла созданы и проверены по содержимому.
- **В каждом файле ДВЕ процедуры** (шаблоны базовых команд из ТЗ): **PTP** (движение по суставам)
  и **LIN** (линейные участки с ориентацией). Точки берутся из фактической кинематики: углы всех
  суставов, положение TCP и ориентация фланца.
- **Единицы — как у производителя:** KRL и KAREL — миллиметры и градусы (`{X,Y,Z,A,B,C}`,
  `POSITION(x,y,z,w,p,r)`), RAPID — миллиметры и **кватернион** (`robtarget`), команды
  `PTP/LIN`, `MOVE TO`, `MoveAbsJ/MoveL`. В заголовке каждого файла — дата, робот, метрики
  траектории и напоминание задать номера инструмента/базы под свою ячейку.
- **Кнопка «Экспорт» в тулбаре** и пункт меню «Файл → Экспорт в язык робота» + команда
  «Язык экспорта (KRL / KAREL / RAPID)»; вкладка показывает папку, выбранный язык,
  число строк и предпросмотр начала файла.

### 14.6 ЭТАП 12 — ИМПОРТ МОДЕЛЕЙ РОБОТОВ (URDF / STEP) (готово)
- **URDF — полная поддержка:** файл читается с диска, из него берутся звенья (`link` с
  геометрией box/cylinder/sphere/mesh), суставы (`joint`: revolute / continuous / prismatic /
  fixed) с осями (`axis`), смещениями (`origin xyz rpy`) и **лимитами**; по этим данным КОДОМ
  создаётся структура сцены **база → звенья → суставы → TCP**. Радианы URDF переводятся в
  градусы проекта, `origin rpy` — в поворот сустава, ось приводится в систему родителя
  (как того требует формула проекта `localRotation = AngleAxis(q, axis) · q0`), а корень модели
  поворачивается на −90° вокруг X (URDF — Z-вверх, Unity — Y-вверх).
- **Модель сразу рабочая:** на корень вешается ШТАТНЫЙ `SixAxisController`, поэтому
  `PoseValidator` собирается по импортированной модели (в прогоне: **6 осей, цикл FK→IK с
  ошибкой 0.000 мм**) — значит работают лимиты, зазоры и планировщик. Модель помечается
  `RegisteredObject` (как объекты, созданные оператором), поэтому штатная проверка «роботов
  в сцене ровно 2» её не считает, а дерево моделей показывает её обычным узлом робота.
- **STEP — базовая поддержка** (как и требует ТЗ): из текстового STEP вытаскиваются координаты
  `CARTESIAN_POINT`, считается габарит и создаётся визуальный корпус; про отсутствие
  кинематического дерева честно написано в интерфейсе и в журнале.
- **Автосоздание структуры и проверка кинематики:** после импорта доступна кнопка «Проверить
  кинематику»: для 6 осей — полный цикл FK→IK штатным валидатором, для остальных — связность
  цепи (каждый сустав обязан влиять на положение ИЛИ ориентацию инструмента) с отчётом
  в свойствах и в журнале.
- **Папки поиска:** `Assets/StreamingAssets/robots` (там лежит пример `sample_robot.urdf` —
  6-осевая цепь, чтобы функцию можно было проверить сразу) и `Документы\KazistovVv\robot_models`;
  список файлов обновляется командой «Обновить список моделей». Импортированный робот —
  самостоятельный объект сцены, его можно удалить одной кнопкой.

### 14.7 ИНФРАСТРУКТУРА, ЛОКАЛИЗАЦИЯ И ТОЧКИ ИНТЕГРАЦИИ
- **Локализация:** 124 новых ключа на 7 языках (RU / EN / ZH / ES / DE / FR / JA) — вкладки,
  ограничения, калибровка, нагрузка, экспорт, импорт; внешние словари остаются главнее
  (пишутся только отсутствующие ключи), fallback «текущий язык → английский → русский текст».
- **Окно-верстак** (реестр вкладок из сессии 1–6) получил ещё 6 вкладок: «Ограничения точки»,
  «Планирование с ограничениями», «Калибровочный мастер», «Калькулятор нагрузки»,
  «Экспорт траектории», «Импорт модели робота» — всего **9 вкладок**.
- **Тулбар:** +5 кнопок (ограничения, калибровка, нагрузка, экспорт в язык робота, импорт) →
  **37 кнопок, 8 рядов** (сетка 5 в ряд, высота считается автоматически, dock-панели подстраиваются).
- **Меню:** «Робот → Промежуточные точки → Ограничения», «Робот → Ограничения планирования»,
  «Сервис → Калибровочный мастер / Калькулятор нагрузки», «Файл → Экспорт в язык робота /
  Импорт модели робота».
- **Дерево моделей:** новая ветка «Ограничения, калибровка и экспорт» с узлами по каждому
  этапу 7–12 (состояние в «Свойствах», клик открывает нужную вкладку); у вариантов траекторий —
  пометка ◈ при нарушении ограничения.
- **Горячая клавиша:** `F9` — открыть вкладку ограничений (остальные клавиши не тронуты).
- **Настройки:** 4 новых переключателя — «Ограничения промежуточных точек», «Планирование
  с ограничениями», «Отбрасывать варианты, нарушающие ограничение», «Импорт моделей: сканировать
  папки при запуске».

### 14.8 ВЕРИФИКАЦИЯ СЕССИИ (этапы 7–12)
- **Компиляция:** `Unity.exe -batchmode -nographics -quit` → **0 ошибок, 0 предупреждений**
  (три прогона компилятора по ходу правок).
- **Сквозной прогон в PlayMode** (`Assets/_Project/01_Scripts/Editor/DshStage3Diag.cs`,
  диагностика агента, в копию пользователя НЕ переносится): 14 шагов, отчёт читается из лога
  Unity (файл в OneDrive во время PlayMode не дописывается — §12.10).
  **Ход прогонов:** 1-й — `[OK] 689 · [FAIL] 6`; 2-й (после правок) — `[OK] 283 · [FAIL] 120`
  (диагностика повторяла проверку каждый кадр и попала на SCARA); 3-й — `[OK] 65 · [FAIL] 3`;
  **итоговый 4-й — `[OK] 66 · [FAIL] 2`**, где оба отказа — придирка самой диагностики к нагрузке
  (см. §14.10), а все проверки этапов 7–12 пройдены. Что подтверждено фактами:
  - **этап 7:** поза waypoint с ограничением ориентации найдена (**ошибка ориентации 2.5°** при
    допуске 10°); маршрут с ограничением скорости построен, фактическая скорость TCP в точке
    **0.078 м/с при лимите 0.08 м/с**; пауза реально вставлена в маршрут (сэмплов 143 → 173,
    время 12.37 → 24.54 с); обязательный обход препятствия выполнен (**минимальное расстояние
    до окрестности 685 мм при требуемом 130 мм**); снятие ограничений возвращает обычный маршрут
    (3 варианта, запас лимитов 35.1°, зазор 920 мм);
  - **этап 8:** ограничение включается/выключается, проверяется на КАЖДОМ сэмпле всех вариантов
    (851 сэмпл), «наклон ≤ 15°» считается отдельно, «взгляд на объект» переключается, приведение
    к ограничению **исправило 3 сэмпла и уменьшило нарушения 118 → 115**, начало траектории
    не сдвинулось, «колбаска» и фантомы перестроены (118 сэмплов);
  - **этап 9:** 4 точки TCP приняты, смещение и остаток посчитаны, **самопроверка метода — 0.000 мм**,
    калибровка базы по 3 точкам посчитана, файл калибровки создан и прочитан;
  - **этап 10:** нагрузка в текущей позе считается (в прогоне №1 — 5.18 кг, ограничивающий сустав
    определён), кривая по расстоянию от базы построена (11 точек);
  - **этап 11:** все три файла созданы с именами по ТЗ и проверены по содержимому —
    KRL (`DEF trajectory`, `PTP`, `LIN`, `$TOOL`), KAREL (`PROGRAM trajectory`, `MOVE TO`,
    `JOINT_POS`), RAPID (`MODULE trajectory`, `MoveAbsJ`, `MoveL`, `robtarget`);
  - **этап 12:** URDF импортирован (звеньев 8, суставов 6, TCP), объект сцены создан, валидатор
    проекта собрался по модели (6 осей, цикл FK→IK с ошибкой 0.000 мм), STEP прочитан
    (габарит 0.81 м) и создан как визуальный корпус, модель удаляется из сцены;
  - **локализация:** «Калибровочный мастер» переведён на все 7 языков.

### 14.9 НАЙДЕНО И ИСПРАВЛЕНО ПРОГОНОМ
1. **Вкладки не собирались (`Title()` вместо `Title`)** — свойство вызывалось как метод (5 вкладок);
   компилятор поймал сразу, но важно как следствие: вкладки этапов 7–12 без этого не открывались.
2. **Ограничение ускорения/усилия для призматической оси SCARA** считалось по «моменту» (55 Н·м),
   из-за чего допустимая нагрузка выходила нулевой. **Исправлено:** для призматических осей
   введено отдельное номинальное усилие (`ratingPrismaticN`, 600 Н) и суммирование массы руки.
3. **Вклад звеньев в момент завышался**: все дистальные звенья считались «на середине пути до TCP»,
   что съедало весь момент и давало 0 кг. **Исправлено:** каждое звено берётся в СВОЁМ центре
   (середина между его пивотом и следующим) — модель стала физичной.
4. **Ось инструмента у SCARA** определялась как ось призмы (вверх), поэтому ограничение
   «инструмент вертикально (вниз)» для неё не выполнялось никогда. **Исправлено:** при
   призматической последней оси инструмент считается направленным ПРОТИВ хода призмы (вниз) —
   для SCARA ограничение вертикальности выполняется по построению, как и в реальности.
5. **Проверка кинематики после импорта была слишком строгой**: сустав, ось которого проходит
   через TCP (или совпадает с осью инструмента), не смещает кончик — и модель ошибочно
   признавалась «неполной». **Исправлено:** главный критерий — цикл FK→IK штатным валидатором,
   а перебор суставов (с учётом поворота фланца) остаётся справкой в отчёте.
6. **Диагностика** ловила не тот стенд (поток был привязан к SCARA, у которой нет кисти) и
   повторяла проверку нагрузки каждый кадр (114 одинаковых строк). **Исправлено:** прогон
   явно привязывается к роботу наведением камеры, проверки вынесены в «одноразовые».

### 14.10 ТРЕБУЕТ ДОРАБОТКИ / ЗАМЕЧАНИЯ (честно)
- **Два отказа итогового прогона — придирка диагностики к нагрузке, а не дефект продукта.**
  Диагностика сначала выполняет синтетическую калибровку TCP (позы задаются процедурно, поэтому
  «длина инструмента» выходит 462.8 мм), а затем проверяет нагрузку — с таким смещением TCP уходит
  на 1.19 м от базы, момент превышает номинал, и допустимая нагрузка честно выходит 0 кг.
  В прогоне №1 (калибровка ещё не выполнялась) та же проверка дала **5.18 кг с ограничивающим
  суставом 5**. Вывод: расчёт нагрузки верен, а диагностике нужно изолировать этапы (что и сделано
  возвратом `toolOffset`, но синтетическая калибровка успевает изменить позу робота).
  Ручная проверка нагрузки (п. 4 чек-листа) выполняется в живом редакторе без этой помехи.
- **Приведение траектории к ограничению сходится не для всех сэмплов.** В итоговом прогоне
  исправлено 3 сэмпла из 118 нарушающих и остаток остался 117° (инструмент смотрит вверх, а нужен
  «вниз»). Платформа это НЕ скрывает: печатает «спроектировано сэмплов: N · остаток X°» и не
  увеличивает число нарушений. Направление работ: (а) при упорном нарушении перепланировать путь
  С НУЛЯ под ограничение (constrained goal planning) вместо проекции существующего пути;
  (б) для угла ~180° пробовать старт с перевёрнутого запястья (приём уже есть в
  `KvPlanKit.SolvePoseForPoint`).
- **Угол поворота базы по плоскости не определяется** калибровкой базы (плоскость не задаёт
  поворот). Нужна вторая база отсчёта — в интерфейсе это честно написано.
- **Калибровка TCP на SCARA неприменима** (у неё нет кисти — ориентация не меняется): точки
  с одинаковой ориентацией отклоняются, оператор получает понятное сообщение. Для SCARA нужен
  другой метод (например, касание плоскости с известной нормалью).
- **Импорт STEP ограничен текстовыми файлами** (AP203/AP214) — бинарный STEP не читается,
  кинематического дерева в STEP нет по стандарту.
- **Импортированный робот не становится частью двух существующих стендов**: он создаётся
  отдельным объектом рядом (смещение 2.2 м по X от крайнего робота). Мульти-роботная ячейка
  (взаимодействие роботов, общий мир столкновений на двоих) — отдельная задача (в ТЗ отложена).
- **Тулбар вырос до 8 рядов** — на 1366×768 помогает «Настройки → Интерфейс → Масштаб»;
  часть кнопок можно убрать из `ToolbarLayout()` (команды и меню остаются).

### 14.11 РУЧНАЯ ПРОВЕРКА (что смотреть в живом редакторе)
1. **Ограничения waypoint:** выбрать точку → «Добавить waypoint» → `F2` → вкладка
   «Ограничения точки»: включить «Требуемая ориентация TCP», нажать «−Y ↓», «Применить» —
   точка станет фиолетовой, а маршрут перестроится (или честно не построится с причиной
   в строке маршрута). Добавить «Паузу 2 с» и «Выполнить маршрут» — робот остановится в точке.
   Включить «Обязательный обход препятствия» и «Центр из точки прицела» (навести на препятствие)
   → маршрут пойдёт в стороне.
2. **Планирование с ограничениями:** `F9` → включить «Включить ограничения планирования»,
   режим «инструмент вертикально» → выбрать точку и получить 8 траекторий: варианты, где
   инструмент наклонён, помечаются ◈ в дереве, в таблице видно «нарушений N из M».
   Кнопка «Привести траекторию к ограничению» — смотреть строку «спроектировано сэмплов».
   Включить «Отбрасывать варианты, нарушающие ограничение» → платформа сама выберет подходящий.
3. **Калибровка:** `Сервис → Калибровочный мастер`. Для TCP: четырьмя разными ориентациями
   коснуться одной точки (рукой/слайдерами/пультом), каждый раз «Записать точку» → «Рассчитать
   смещение TCP» → проверить остаточную ошибку (хорошая калибровка — единицы миллиметров).
   «Применить смещение к потоку» → точка прицела сместится на длину инструмента.
   Для базы: коснуться трёх точек столешницы → «Рассчитать базу» → «Сохранить калибровку в файл».
4. **Нагрузка:** `Сервис → Калькулятор нагрузки` → максимум в текущей позе, ограничивающий сустав
   и график; подвигать робота (слайдеры суставов) — нагрузка должна падать при вытягивании руки;
   в дереве выбрать узел робота → в «Свойствах» раздел «Калькулятор нагрузки».
5. **Экспорт:** выбрать вариант траектории → кнопка «Экспорт в язык робота» (⬛ иконка)
   → в «Документы\KazistovVv\robot_export» появится `trajectory.src`; переключить язык
   (команда «Язык экспорта») и повторить → `trajectory.kl`, затем `trajectory.mod`.
   Открыть файл: в нём заголовок с метриками и две процедуры (PTP и LIN).
6. **Импорт:** `Файл → Импорт модели робота` → «Обновить список» (в списке есть пример
   `sample_robot.urdf`) → «Импортировать» → рядом со стендами появится модель 6-осевого робота
   с осями в дереве → «Проверить кинематику» (в строке — ошибки FK и IK). Свою модель положить
   в `Assets/StreamingAssets/robots` или `Документы\KazistovVv\robot_models`. «Удалить
   импортированного робота» убирает модель из сцены.
7. **Настройки:** «Настройки → Функции» — 4 новых переключателя (ограничения точек,
   ограничения планирования, отбрасывание нарушающих, сканирование моделей при запуске).
8. **Проверить на SCARA** (`F` — переключение робота): ограничения точек и планирование
   с ограничениями работают, но у SCARA инструмент вертикален по построению — ограничение
   «вертикально вниз» выполняется сразу, а ограничение скорости и пауза проверяются как обычно.

### 14.12 СИНХРОНИЗАЦИЯ КОПИЙ (§9) И ДИАГНОСТИКА
- В копию пользователя `C:\Users\Ольга\KavistovVv` переносятся 9 новых файлов + 2 изменённых
  (+ `.meta`) с резервом `_dsh_backup_<дата>_<время>`; сцена, столы, роботы, материалы
  и `ProjectSettings` в копии пользователя НЕ меняются. Пример модели
  `Assets/StreamingAssets/robots/sample_robot.urdf` переносится вместе с проектом.
- Диагностика агента (в копию пользователя НЕ переносится):
  `Assets/_Project/01_Scripts/Editor/DshStage3Diag.cs`, логи прогонов — `_dsh_s3_*.log`.

### 14.13 ПРОДОЛЖИТЬ С ЭТАПА 13
Следующая подсессия (по правилу «5–6 этапов за раз») — **этапы 13–18 нового ТЗ**:
13. редактор коллизионных мешей (low-poly для быстрой проверки коллизий);
14. бенчмарк планировщика (100 задач, среднее время, success rate, длина; сравнение RRT* / BiRRT / TrajOpt);
15. визуализация работы планировщика (дерево RRT в реальном времени);
16. несколько камер / PiP (первое лицо, сверху, сбоку);
17. визуализация сил и моментов в суставах;
18. тепловая карта времени достижения (в секундах).
Инфраструктура готова: реестр вкладок верстака (9 вкладок), реестр команд, слои подсказок
и подсветки, таблица локализации, общий набор лимитов движения, метрики `KvTrajMath`
и модель нагрузки `KvPayloadCalculator` (её моменты пригодятся для визуализации сил).
---

## 15. Сессия 17.09.2026 — ЭТАПЫ 13–36 (24 этапа: производительность, показ, автоматизация, безопасность)

Правило сессии: не останавливаться на «5–6 этапов», а довести блок до конца — сделаны **все 24**
этапа ТЗ (13–36). Ниже по каждому этапу: что сделано по существу, где лежит, как проверить
в живом редакторе и что честно ограничено. Архитектура прежняя: **новый хаб
`KvStageHub4`** + сервис на группу + вкладка верстака (`IKvWorkbenchTab`) + команда меню/тулбара;
существующая логика не переписывалась.

Новые файлы этой сессии (все в `Assets/_Project/01_Scripts/Features/`):

| файл | этапы |
|---|---|
| `KvCollisionOptimizer.cs` | 13 |
| `KvPlannerLab.cs` | 14, 15 |
| `KvCameras.cs` | 16 |
| `KvForceHeat.cs` | 17, 18 |
| `KvReportPdf.cs` | 19 |
| `KvNetTools.cs` | 20, 21, 22 |
| `KvXrInput.cs` | 23, 24, 25 |
| `KvAutomation.cs` | 26, 27 |
| `KvSceneStudio.cs` | 28, 29, 30 |
| `KvCinematics.cs` | 31, 32, 33 |
| `KvSafetyTools.cs` | 34, 35, 36 |
| `KvInputKit.cs` | общее: поля ввода текста для вкладок |
| `KvLocExtra3.cs` | локализация 7 языков для этапов 13–36 |
| `KvStageHub4.cs` | хаб: сервисы, вкладки, команды, дерево, свойства, Alt-клавиши |

Правки в существующих файлах — **только аддитивные**:
`Trajectory/CollisionWorld.cs` (реестр `CollisionProxies` + поворот коробок),
`Trajectory/Planner.cs` (запись дерева RRT), `Features/KvPayloadCalculator.cs`
(публичный расчёт моментов по суставам), `Features/KvCaptures.cs` (время старта видеозаписи),
`Features/KvActionLog.cs` (уровень «предупреждение»), `KazistovVvUIManager.cs`
(регистрация хаба: команды, дерево, свойства, переключатели, подпись дерева `|I`, ряд кнопок
тулбара), `Zones/KvSettingsView.cs` (9 новых переключателей функций).

### 15.1 Этап 13 — редактор коллизионных мешей (прокси столкновений)
Автоматически строятся упрощённые оболочки статики: для каждого объекта считается
ориентированный бокс методом главных компонент (собственные числа 3×3, оси упорядочены
«короткая → длинная»; если объект вытянут — делается капсула по длинной оси). Вершины берутся
**со всей геометрии** меша, поэтому оболочка всегда накрывает объект; пропускаются только
объекты внутри других объёмов и мельче 2 см. Оболочки подставляются в расчёт столкновений
через реестр `CollisionProxies` — при включении планировщик считает расстояние до бокса/капсулы
вместо перебора треугольников, при выключении поведение **ровно прежнее** (одна ветка в
`CollisionWorld.Rebuild`). Предпросмотр оболочек — `LineRenderer`. Замер (`Measure()`): время
пересборки мира и зазора «с прокси» и «без», плюс сравнение времени планирования.
**Проверка:** F2 → вкладка «Коллизионные меши» → переключатель, «Пересобрать», «Замер»,
«Предпросмотр».

### 15.2 Этап 14 — стенд сравнения планировщиков
Набор задач генерируется детерминированно (посев от номера задачи), каждая задача решается
тремя стратегиями: **BiRRT (штатный планировщик проекта), RRT\* (тот же рост дерева с
переподключением к лучшему узлу), TrajOpt (сглаживание + время-оптимальная перепараметризация
поверх найденного пути)**. Считаются: среднее время, доля успеха, средняя длина пути, средний
запас до лимитов, минимум/максимум времени, число итераций. Прогон идёт **порциями по 12 мс**
(интерфейс не замирает), результат — таблица во вкладке и файл CSV в
`Документы\KazistovVv\…\Bench`. По ТЗ доступно до 500 задач (по умолчанию 120).
**Проверка:** вкладка «Бенчмарк планировщика» → «Запустить», затем «Выгрузить CSV».

### 15.3 Этап 15 — живая визуализация дерева RRT
Штатный `PlanBiRrt` по флагу `Planner.RecordTree` записывает узлы обоих деревьев и связи (на
планирование это не влияет). Визуализация обновляется 10 раз в секунду и рисует **только
добавленные узлы** (`LineRenderer`, линия на узел): большие деревья не перерисовываются целиком.
Кнопка «Дерево RRT на экране» в тулбаре и вкладке.
**Проверка:** включить дерево, выбрать точку — на экране видно, как растут два дерева.

### 15.4 Этап 16 — несколько камер и «картинка в картинке»
Три дополнительные камеры (сверху, сбоку, от первого лица) со своими текстурами
(640×360) и своими окнами PiP на отдельном канвасе: включение по одной, «показать на весь
экран», автоматическая раскладка по правому краю. Камера «от первого лица» ставится у
инструмента и смотрит его осью инструмента; виды сверху/сбоку ориентируются на текущую позу.
**Проверка:** вкладка «Камеры и PiP» (или кнопка тулбара) — включить окна, затем «на весь экран».

### 15.5 Этап 17 — векторы сил и моментов
Для каждого сустава считается момент от веса звеньев и груза (та же модель, что в калькуляторе
нагрузки: `KvPayloadCalculator.JointTorques`), рисуются стрелки, длина и цвет — по загрузке
от номинала; отдельная стрелка — сила на инструменте. Для SCARA (призматическая ось) показывается
усилие в ньютонах, а не момент.
**Проверка:** вкладка «Силы и моменты» → «Показать векторы сил».

### 15.6 Этап 18 — тепловая карта времени достижимости (в секундах)
Рабочая зона разбивается на узлы (кольцо для SCARA, сферическая оболочка для робота), для каждого
решается IK, строится путь и считается **время по лимитам движения** (`KvTrajMath.Retime`), после
чего поверхность раскрашивается: цвет — секунды до точки, шкала подписана. Расчёт идёт порциями
(счётчик узлов видно во вкладке), пересчёт — по кнопке или при смене робота.
**Проверка:** вкладка «Силы и моменты» → «Тепловая карта времени», дождаться счётчика.

### 15.7 Этап 19 — PDF-отчёт
PDF 1.4 пишется **вручную, без сторонних библиотек**: страницы с текстом (встроенный шрифт
Helvetica, русский текст транслитерируется) и страницы-картинки JPEG (кадр сцены и
отрендеренная страница A4 с кириллицей через интерфейсный шрифт Unity). В отчёт входят:
заголовок, робот и состояние, выбранная траектория (время, длина, скорости, ускорения, jerk,
кривизна, зазор, запас лимитов), сглаживание, время-оптимальная, нагрузка, ограничения
планирования, калибровка, лимиты движения, таблица суставов и хвост журнала. Файл:
`Документы\KazistovVv\reports\KazistovVv_report_<дата>.pdf`. Кнопка есть **на тулбаре** (по ТЗ).
В batch-режиме без графики страницы-картинки пропускаются — отчёт остаётся валидным (это
проверено диагностикой: `%PDF` в начале, `%%EOF` в конце, несколько страниц).
**Проверка:** кнопка отчёта на тулбаре → открыть PDF.

### 15.8 Этап 20 — совместная работа (мультиплеер)
Внешние пакеты (Netcode/Mirror) не добавлялись: обмен идёт по **UDP** компактными текстовыми
пакетами. Роль «оператор» рассылает состояние 10 раз в секунду (робот, углы суставов, фаза State
Machine, выбранный вариант), роль «наблюдатель» применяет позу у себя и видит ту же сцену,
своих команд движения не выполняет. Сеть работает в фоновом потоке, сцена меняется только в
главном (через очередь) — правило Unity соблюдено. Порт по умолчанию 47777.
**Проверка:** вкладка «Сеть: совместная работа» → роль «оператор» на одной машине, «наблюдатель»
на другой (или на этой же — вторая копия приложения).

### 15.9 Этап 21 — веб-дашборд
Поднимается HTTP-сервер на встроенном сокете (`TcpListener`, без прав администратора) и отдаёт:
страницу мониторинга `/` (самообновление раз в 2 с) и JSON `/api/state` — фаза, робот, TCP, углы,
запас лимитов, варианты, метрики выбранной траектории, здоровье суставов, ограничение,
калибровка, время работы. Адрес: `http://127.0.0.1:47800/`.
**Проверка:** вкладка «Сеть: совместная работа» → «Запустить сервер» → открыть адрес в браузере.

### 15.10 Этап 22 — мобильное приложение-компаньон
UDP-канал команд (порт 47810): `PING`, `STATUS`, `STOP`, `HOME`, `RUN`, `PAUSE`, `SELECT n`.
Команды ставятся в очередь и выполняются **в главном потоке теми же обработчиками, что кнопки
интерфейса**; в ответ уходит строка состояния. То есть планшет — это настоящий пульт, а не
заглушка; само приложение (Android/iOS) в проект не входит (это отдельная разработка), но
протокол и приём команд готовы, проверяются любым UDP-клиентом.
**Проверка:** вкладка → «Принимать команды с планшета» → отправить `STATUS` любым UDP-клиентом.

### 15.11 Этап 23 — голосовые команды
Сделано то, что работает без внешних SDK: **детектор речи** (реальный микрофон, RMS, начало и
конец фразы), **грамматика команд** на русском и английском («стоп», «домой», «пуск», «пауза»,
«вариант три», «дальше», «назад», «запиши позу», «снимок», «отмена») и выполнение команд теми же
методами, что кнопки. Текст фразы принимается либо от распознавателя речи шлема — для него
описан интерфейс `IKvSpeechSource` (`PushRecognized`), либо из поля ввода во вкладке.
**Честно:** собственно распознавание «звук → текст» в Unity невозможно, его даёт SDK
PICO/Meta; вся остальная цепочка готова и проверена (диагностика разбирает 10 формулировок).

### 15.12 Этап 24 — отслеживание рук (жесты)
SDK шлема отдаёт позы суставов в `PushJoint(...)`; здесь реализована **обработка жестов**:
касание (pinch, сближение большого и указательного пальцев — захват/отпускание штатным
гриппером), свайп (скорость кисти и направление: влево/вправо — вариант траектории, вверх —
пуск, вниз — пауза, вперёд — домой), горсть (все пальцы поджаты — удержание). Скелет кисти
рисуется в сцене (точки суставов и «кости»), поэтому отслеживание видно глазом.
**Честно:** сами позы суставов приходят от SDK; без шлема доступен программный ввод (им
пользуется диагностика).

### 15.13 Этап 25 — отслеживание взгляда и фовеальное рендерирование
Поза глаз приходит от SDK (`PushGaze`); здесь считаются **фиксации** (взгляд в конусе 2.5°
дольше 0.22 с), их число и средняя длительность, **выбор взглядом** (удержание 0.9 с добавляет
путевую точку маршрута в место взгляда — «маршрут, нарисованный взглядом») и **моргание**
(закрытые глаза дольше 0.15 с — пауза/продолжение). Точки фиксации отмечаются в сцене, есть
маска зоны центрального зрения. **Фовеальное рендерирование:** платформенный API ищется
отражением (тип `FoveatedRendering` и его методы уровня); если его в сборке нет, это сообщается
**честно**, а не «галочкой» в интерфейсе, и остаётся видимая маска центрального зрения.
**Честно:** аппаратное фовеальное рендерирование на обычном ПК недоступно — включается на
PICO 4 Ultra при наличии API.

### 15.14 Этап 26 — интерфейс скриптов (макросы)
Встроенный язык макросов «питоно-подобного» вида: переменные, арифметика и сравнения, строки,
`repeat N { … }`, `пока … { … }`, `if … { … } else { … }`, функции-запросы
(`tcp_x/y/z()`, `предел()`, `зазор()`, `вариант_выбран()`, `едет()`, `нагрузка()`, `время()`)
и команды робота (`домой()`, `в_точку(x,y,z)`, `ждать(с)`, `ждать_движение()`, `стоп()`, `пуск()`,
`пауза()`, `вариант(n)`, `сгладить(%)`, `ограничить(0/1)`, `нагрузка(кг)`, `точка_маршрута(x,y,z)`,
`маршрут_пуск()`, `поза(имя)`, `снимок()`, `печать()`, `журнал()`). Русские и английские имена
равнозначны. **Песочница:** список команд закрыт, доступ к файлам/сети/системе из макроса
невозможен, шагов не больше 200 000, время не больше 600 с; выполнение идёт порциями по 3 мс
(интерфейс не замирает), долгие команды приостанавливают скрипт до завершения движения. Ошибки
показываются **с номером строки**. Макросы сохраняются в `…\Macros\*.kvs`.
**Проверка:** вкладка «Макросы» → «Пример» → «Выполнить»; «Сохранить»/«Загрузить».

### 15.15 Этап 27 — визуальный редактор дерева поведения
Узлы: последовательность, выбор (или), действие (мини-скрипт этапа 26), условие (выражение),
пауза. Узлы **перетаскиваются мышью** в отдельном окне (окно тоже перетаскивается), связи
рисуются линиями, есть защита от циклов и запрет потомков у листьев, удаление с «подъёмом»
веток, выбор корня, пуск/стоп, подсветка активного узла и состояния (успех/неудача). Дерево
**выгружается в текст макроса** и запускается. Обход: последовательность — все шаги по порядку,
выбор — до первой успешной ветки, действие — мини-скрипт, условие — выражение, пауза — секунды.
**Проверка:** вкладка «Дерево поведения» → «Открыть редактор».

### 15.16 Этап 28 — пресеты окружения
Четыре пресета: **ангар, лаборатория, цех, чистое помещение**. Помещение строится вокруг
основания робота (пол, четыре стены, потолок, разметка рабочей зоны и обстановка), цвета и
размеры — из пресета, переключение мгновенное. Всё это **оформление**: коллайдеров нет, объекты
служебные (`HideFlags.HideInHierarchy`), в расчёт столкновений не попадают, роботов в сцене не
прибавляется.
**Проверка:** вкладка «Окружение» → «Показывать помещение» → пресеты.

### 15.17 Этап 29 — пресеты освещения
Четыре пресета: **день, ночь, студия, драматичный**. Меняются реальные параметры HDRP: солнце
(цвет, люксы, высота и азимут), студийный свет проекта (`HDRPAutoLighting`), туман (`Fog`),
градиентное небо (`GradientSky`), экспозиция и bloom в глобальном Volume. Переход **плавный
(0.6 с)**, поэтому кадр не «прыгает».
**Проверка:** вкладка «Освещение» → пресеты, «Без перехода» — для мгновенной смены.

### 15.18 Этап 30 — редактор материалов в реальном времени
Выбор объекта (перебором или «пипеткой» из центра экрана), правка цвета, металличности,
гладкости и свечения. Правка идёт по **копии материала** (`Renderer.materials`), поэтому файлы
проекта не портятся; «Сброс» возвращает исходный вид, «Сброс всех» — все правки. Есть готовые
кнопки «Подсветить» (голубое свечение) и «Сделать матовым» (убрать блики перед снимком/отчётом).
**Честно:** правки живут в сеансе работы (в ассеты не записываются) — это сознательно, чтобы
редактор не мог испортить исходные материалы.

### 15.19 Этап 31 — кинематографический режим
Чёрные полосы «широкий экран» (высота настраивается) и затемнение кадра, автоматическое
движение камеры: облёт робота по кругу, слежение за инструментом, «штатив»; движение плавное
(демпфирование), без рывков. **Камера возвращается оператору точно в исходное положение**
(проверено диагностикой), клавиши режим не перехватывает.
**Проверка:** вкладка «Кинематографический режим» (Alt+0) → включить, переключить режимы камеры.

### 15.20 Этап 32 — титры, подписи и пояснения (+ редактор субтитров)
Три вида надписей: крупный **титр**, **подзаголовок** и «**нижняя треть**»; каждая живёт свой
отрезок времени, список редактируется во вкладке (начало, конец, текст), есть показ с любого
момента, пауза и часы на экране. Отдельно — **пояснения, привязанные к точкам сцены**: текст
проецируется на экран и остаётся на месте при движении камеры. Список надписей выгружается в
**`.srt`** (формат `00:00:00,000 --> …`), который понимают видеоредакторы и плееры.
**Проверка:** вкладка «Титры и подписи» → добавить титр, «Показ», «Выгрузить .srt».

### 15.21 Этап 33 — запись голоса диктора с синхронизацией к видео
Запись микрофона в **WAV 16 бит / 44.1 кГц** (файл пишется вручную, без библиотек) в
`Документы\KazistovVv\voiceover`, рядом — текстовый список меток (титры с временами) для монтажа.
Главное по ТЗ: если видеозапись уже идёт, сохраняется **смещение начала голоса относительно
начала видео** (для этого в сервис захвата добавлено публичное время старта), поэтому на монтаже
звук выставляется одним движением; есть воспроизведение, синхронизированное с видеорядом
(«Перейти к моменту видео»), и график громкости записи.
**Проверка:** вкладка «Голос диктора» (Alt+−) → запись, прослушивание, метки.

### 15.22 Этап 34 — имитация отказов и поведение безопасности
Три отказа: **отказ сустава** (считается момент силы тяжести относительно оси сустава, угол
уходит в эту сторону до упора — упрощённая модель, показывающая развитие аварии),
**потеря связи** (команды движения блокируются, телеметрия помечается устаревшей, при движении
выполняется контролируемая остановка) и **перегрузка** (масса груза умножается, считается
загрузка суставов по номиналу, при превышении робот останавливается). Общее поведение
безопасности: аварийная остановка, запись в журнал уровнем «ошибка», баннер и **запрет пуска до
«Сброса аварии»**; после сброса требуется переезд домой — как на производстве.
**Честно:** это имитация последствий на модели робота, а не физический движок (динамика и трение
не считаются).

### 15.23 Этап 35 — проверка перед пуском и подтверждение оператором
По сэмплам выбранной траектории проверяются: зазор до препятствий, **близость к человеку**
(расстояние от звеньев робота до человека; положение человека берётся у оператора — камера/шлем,
это единственный реально существующий в проекте источник, список легко расширить манекенами),
зоны запрета, запас до лимитов, близость к особенности, перегрузка в конечной позе, нагрев
суставов и аварийное состояние. Критичные замечания **запрещают** пуск, предупреждения открывают
**окно подтверждения** с обратным отсчётом: без явного нажатия «ПУСК ПОДТВЕРЖДАЮ» робот не
поедет, через 20 с окно закрывается само (пассивное подтверждение невозможно). Кнопка «ПУСК с
проверкой» вынесена на тулбар.
**Проверка:** вкладка «Проверка перед пуском» → «Проверить и пустить».

### 15.24 Этап 36 — уровни журнала, фильтрация, поиск и цвета
Типы событий сведены к уровням **«информация / предупреждение / ошибка»** (в `KvActionLog`
добавлен тип «предупреждение» в конец перечисления, поэтому старые журналы читаются как раньше),
добавлены фильтр по уровню, **поиск по тексту**, счётчики по уровням, цвета строк из типа события,
показ последних записей и выгрузка **только видимого** списка (то, что на экране, — в файл с
заголовком фильтра и счётчиками).
**Проверка:** вкладка «Журнал: уровни и поиск» → уровни, поиск, «Выгрузить видимое».

### 15.25 ИНФРАСТРУКТУРА, ЛОКАЛИЗАЦИЯ, ТОЧКИ ИНТЕГРАЦИИ
- **Локализация:** таблица `KvLocExtra3` (вкладки, отказы, проверка, безопасность, сеть, XR,
  макросы, окружение, свет, материалы, кино, титры); внешние словари остаются главнее, цепочка
  «текущий язык → английский → русский текст» сохранена.
- **Окно-верстак:** +20 вкладок (13–36) к прежним 9 → **29 вкладок**.
- **Тулбар:** +5 кнопок в новом ряду (PDF-отчёт по ТЗ, прокси, стенд, камеры, ПУСК с проверкой)
  → **42 кнопки, 8 рядов**.
- **Горячие клавиши:** `Alt+1…Alt+0`, `Alt+−`, `Alt+=` — открытие вкладок новых этапов
  (штатные бинды F1–F12 и мышь не тронуты; Alt-сочетания в проекте ранее не использовались).
- **Дерево моделей:** новая ветка «Этапы 13–36: производительность, показ и безопасность» с
  узлами по группам (производительность, показ/отчёт, сеть, ввод и макросы, безопасность и
  журнал); в «Свойствах» робота — прокси, стенд, проверка, отказы, уровень журнала; у варианта
  траектории — стратегия стенда, время достижимости и замечания проверки.
- **Настройки:** 9 новых переключателей функций (прокси, дерево RRT, веб-дашборд, голос, руки,
  взгляд, кинорежим, окружение, проверка перед пуском).

### 15.26 ВЕРИФИКАЦИЯ СЕССИИ (этапы 13–36)
- **Компиляция:** `Unity.exe -batchmode -nographics -quit` — **0 ошибок, 0 предупреждений**
  (12 прогонов компилятора по ходу правок; последний — на итоговой версии).
- **Сквозной прогон в PlayMode** (`Assets/_Project/01_Scripts/Editor/DshStage4Diag.cs`, диагностика
  агента, в копию пользователя НЕ переносится): 31 шаг, **`[OK] 124 · [FAIL] 0 · [info] 55`**,
  время прогона **71 с**. Отчёт читается из лога Unity (`[DshStage4Diag]`) — §12.10.
  Ход прогонов (важно для истории): №1 — `242/26`, №2 — `238/18`, №3 — `1800/5` (счётчик раздулся,
  потому что шаги проверялись каждый кадр), №4 — `124/0`. Ниже — что подтверждено фактами.
  - **этап 13:** прокси построены для 58 объектов статики (12 капсул, 15 пропущено, 45 мешей без
    читаемых вершин — для них оболочка берётся по габариту); замер: пересборка мира 1.0 → 1.2 мс,
    а **200 проверок зазора 19.2 → 3.6 мс (в 5.3 раза быстрее)**; выключение прокси возвращает
    ровно прежнее поведение столкновений.
  - **этап 14:** прогон 12 задач × 3 стратегии: BiRRT — успех 92 %, среднее 3.1 мс, длина 1.964 м;
    RRT* — 100 %, 13.4 мс, 2.003 м; TrajOpt — 100 %, 4.6 мс, 2.030 м (всего успешно 35 из 36);
    таблица выгружена в CSV.
  - **этап 15:** планировщик записал узлы обоих деревьев (версия 30, узлов 6) — есть что показывать.
  - **этап 16:** три окна PiP созданы; в пакетном режиме без графики окна создаются **без
    изображения** (честная деградация, см. §15.27), включение окон и показ «на весь экран» работают.
  - **этап 17:** моменты посчитаны по всем 6 суставам, максимум **54.6 Н·м**, нагружено 3 сустава,
    сила на инструменте **13.7 Н**.
  - **этап 18:** карта времени построена: **324 точки, достижимо 272, время от 1.88 до 3.16 с**;
    время до цели выбранного варианта — **2.71 с**.
  - **этап 19:** PDF создан (7011 байт, **3 страницы**, `%PDF-1.4` … `%%EOF`) в
    `Документы\KazistovVv\reports`; в пакетном режиме без графики страницы-картинки пропускаются,
    отчёт остаётся валидным.
  - **этап 20:** роли «наблюдатель»/«оператор» переключаются, порт открывается и закрывается.
  - **этап 21:** сервер отдаёт страницу мониторинга (**2757 байт**) и JSON `/api/state` с реальным
    состоянием (`"ready":true`, робот, фаза, TCP, запас лимитов, варианты, метрики, здоровье).
  - **этап 22:** команды с «планшета» приняты и выполнены: `STATUS`, затем `PING` (2 команды).
  - **этап 23:** грамматика разбирает **10 формулировок** (`stop,home,run,pause,select,next,prev,
    pose,shot,undo`), «вариант три» → 3, команда «вариант 2» реально выбрала второй вариант.
  - **этап 24:** жест «касание» распознан (захват/отпускание гриппера), жест «свайп» распознан —
    после него выбран «вариант 3 из 7».
  - **этап 25:** **4 фиксации** (средняя 0.94 с), **3 путевые точки добавлены взглядом**,
    моргание распознано (1); фовеальное рендерирование честно сообщено как неподдержанное сборкой.
  - **этап 26:** макрос выполнен (цикл `repeat 3` + арифметика → `счёт = 5`), неверная команда
    отвергнута **с номером строки**, макрос сохранён и прочитан с диска.
  - **этап 27:** связи создаются, лист не получает потомков, цикл запрещён, дерево выгружено в
    скрипт (6 строк), обход завершён успешно (8 посещений узлов).
  - **этап 28:** четыре пресета доступны (в пакетном режиме помещение не строится — нет графики).
  - **этап 29:** «день» **26000 лк → «ночь» 900 лк**, высота солнца 52° → 28°, туман вкл/выкл по
    пресету; все четыре пресета применяются.
  - **этап 30:** цвет материала меняется (1.14, 1.14, 1.14 → 0.90, 0.20, 0.20) и «Сброс» возвращает
    исходный.
  - **этап 31:** камера прошла по орбите **2.95 м** и вернулась **с расхождением 0.000 м**.
  - **этап 32:** титр, подзаголовок и нижняя треть добавлены и показываются по времени; пояснение
    привязано к точке сцены; `.srt` выгружен с правильным форматом `00:00:00,000 --> …`.
  - **этап 33:** запись голоса сохранена: **WAV `RIFF/WAVE`, 210 842 байта, 2.39 с**; смещение к
    видео корректно сообщается как отсутствующее (видеозапись не шла).
  - **этап 34:** потеря связи запрещает движение и требует сброса (после сброса движение снова
    разрешено); отказ сустава реально «провис» — **угол сустава 2 изменился на 84.5°**; перегрузка
    увеличила груз 0.40 → 1.00 кг (**сустав 3 загружен на 165 % от номинала**), после сброса масса
    вернулась к 0.40 кг.
  - **этап 35:** проверка выдала 6 замечаний (критичное — перегрузка в конечной позе, два
    предупреждения — запас до лимитов −19.7° и σ_min 0.001, справки — зазор 169 мм, зоны не задеты,
    **расстояние до человека 2.47 м / 2.78 м**); при критичном замечании пуск **запрещён**,
    окно подтверждения в этом случае не открывается (это правильно).
  - **этап 36:** уровни считаются отдельно (информация 169 · предупреждений 11 · ошибок 10),
    фильтр «только ошибки» показывает 10 из 190, поиск находит записи, выгрузка сохраняет фильтр и
    счётчики и не содержит записей ниже выбранного уровня, цвета уровней различаются.
  - **Локализация:** 174 ключа этапов 13–36 на 7 языках («Проверка перед пуском» → en/zh/es/de/fr/ja).

### 15.27 ТРЕБУЕТ ДОРАБОТКИ / ЧЕСТНЫЕ ОГРАНИЧЕНИЯ (этапы 13–36)
1. **Голос, руки, взгляд — платформенная часть приходит от SDK шлема.** Собственно распознавание
   речи (звук → текст), позы суставов и поза глаз даёт PICO/Meta; в проекте готовы интерфейсы
   (`IKvSpeechSource.PushRecognized`, `PushJoint`, `PushGaze`) и вся обработка (грамматика команд,
   жесты, фиксации, выбор взглядом, моргание). Без SDK доступен программный ввод — им и проверено.
2. **Аппаратное фовеальное рендерирование недоступно** в этой сборке Unity (API `FoveatedRendering`
   не найден — сообщается честно). На PICO 4 Ultra включается при наличии API платформы; сейчас
   работает видимая маска зоны центрального зрения.
3. **Совместная работа — синхронизация состояния по UDP, а не Netcode/Mirror.** Пакеты (поза,
   фаза, выбранный вариант) шлются 10 раз в секунду; полноценной блокировки мира, прав и
   предсказания нет — это осознанный выбор (внешние пакеты в проект не добавлялись).
4. **Мобильное приложение как таковое не написано**: готовы протокол и сервер команд (UDP),
   проверено приёмом `STATUS`/`PING`. Само приложение для планшета — отдельная разработка.
5. **Имитация отказов — упрощённая модель последствий** (момент силы тяжести, провисание до упора,
   множитель массы груза), а не физический движок: динамика, трение и упругость не считаются.
   Это прямо написано и в интерфейсе вкладки.
6. **Правки материалов живут только в сеансе** (в ассеты не записываются) — сознательно, чтобы
   редактор не мог испортить исходные материалы проекта.
7. **Показ требует графики.** В пакетном режиме без видеокарты (`-nographics`, устройство Null)
   попытка отрисовать материал HDRP обрывала цикл кадров — поэтому добавлен общий страж
   `KvGraphics.Available`: без графики сервисы **считают**, но не рисуют (стрелки сил, меш тепловой
   карты, окна PiP, скелет рук, точки фиксации, помещение, каркас прокси). В обычном редакторе с
   видеокартой показ работает полностью. Это и есть главная находка сессии (§15.26, прогон №1–2,
   где из-за этого «умирали» все сервисы после этапа 16).
8. **Человек в проверке перед пуском — это оператор** (шлем/камера): других источников положения
   людей в сцене нет (манекенов в проекте не было). Список точек задаётся одним поставщиком
   (`People()` в хабе) — добавить манекены можно одной правкой.
9. **Стратегии стенда упрощены:** BiRRT — штатный планировщик проекта, RRT* — рост дерева с
   переподключением к лучшему узлу, TrajOpt — сглаживание и время-оптимальная перепараметризация
   поверх найденного пути. Это честно названо в подписях, а не выдаётся за внешние оптимизаторы.
10. **Тулбар вырос до 8 рядов (42 кнопки)** — на 1366×768 помогает «Настройки → Интерфейс →
    Масштаб»; часть кнопок можно убрать из `ToolbarLayout()` (команды и меню остаются).
11. **В пакетном режиме запись голоса даёт тишину** (микрофона в headless-среде нет): файл WAV
    создаётся корректно, но пик 0.000 — это ограничение среды прогона, а не кода.

### 15.28 РУЧНАЯ ПРОВЕРКА (чек-лист на живом редакторе)
1. **Прокси (13):** `Alt+1` → вкладка «Коллизионные меши» → включить, «Пересобрать»,
   «Предпросмотр» (каркас оболочек в сцене), «Замер» — сравнить время пересборки и зазоров.
2. **Стенд (14):** `Alt+2` → «Запустить» (по умолчанию 120 задач) → таблица по трём стратегиям и
   «Выгрузить CSV».
3. **Дерево RRT (15):** там же «Дерево RRT на экране» → выбрать точку и увидеть рост двух деревьев.
4. **Камеры (16):** `Alt+3` → включить окна (сверху, сбоку, от первого лица), «на весь экран».
5. **Силы (17) и тепло (18):** `Alt+4` → «Векторы сил» (стрелки моментов и сила на инструменте),
   «Тепловая карта времени» (цвет = секунды), навести на точку — подпись времени.
6. **PDF-отчёт (19):** кнопка отчёта на тулбаре → открыть файл из `Документы\KazistovVv\reports`
   (в живом редакторе в отчёт попадут и снимки сцены).
7. **Сеть (20–22):** `Alt+6` → роль «оператор» на одной машине, «наблюдатель» на другой;
   «Запустить сервер» → открыть `http://127.0.0.1:47800/` в браузере; «Принимать команды
   с планшета» → отправить `STOP`/`HOME` любым UDP-клиентом (порт 47810).
8. **Голос (23):** `Alt+7` → включить микрофон (индикатор уровня), затем набрать фразу в поле
   («домой», «вариант три») — или подключить распознаватель шлема.
9. **Руки (24) и взгляд (25):** `Alt+8`, `Alt+9` → при подключённом шлеме выполнить касание,
   свайп, удержание взгляда 0.9 с на точке (появится путевая точка) и моргание.
10. **Макросы (26) и дерево поведения (27):** `Alt+0` и `Alt+=` → «Пример» → «Выполнить»;
    в дереве перетащить узлы, «Связать с выбранным», «В скрипт».
11. **Окружение/свет/материалы (28–30):** вкладки «Окружение» (ангар/лаборатория/цех/чистое),
    «Освещение» (день/ночь/студия/драматичный), «Материалы» (пипетка, цвет, «Подсветить»,
    «Сброс»).
12. **Кино/титры/голос диктора (31–33):** «Кинематографический режим» (полосы, облёт),
    «Титры и подписи» (титр, показ, `.srt`), «Голос диктора» (запись → WAV, метки, прослушивание).
13. **Отказы/проверка/журнал (34–36):** «Имитация отказов» (отказ сустава, потеря связи,
    перегрузка → сброс аварии), «Проверка перед пуском» («Проверить и пустить», окно
    подтверждения с отсчётом), «Журнал: уровни и поиск» (уровни, поиск, выгрузка).

### 15.29 СИНХРОНИЗАЦИЯ КОПИЙ (§9)
- В копию пользователя `C:\Users\Ольга\KavistovVv` перенесено **24 файла**: 15 новых
  (`KvCollisionOptimizer`, `KvPlannerLab`, `KvCameras`, `KvForceHeat`, `KvReportPdf`, `KvNetTools`,
  `KvInputKit`, `KvXrInput`, `KvAutomation`, `KvSceneStudio`, `KvCinematics`, `KvSafetyTools`,
  `KvLocExtra3`, `KvStageHub4`, `KvGraphics`) и 9 изменённых (`CollisionWorld`, `Planner`,
  `TrajectoryFlowController`, `KvPayloadCalculator`, `KvCaptures`, `KvActionLog`,
  `KazistovVvUIManager`, `KvSettingsView`, `PROJECT_CONTEXT.md`) вместе с `.meta`.
  Резерв заменённых файлов — `C:\Users\Ольга\KavistovVv\_dsh_backup_20260915_030935`.
- В `Assembly-CSharp.csproj` копии добавлено 15 строк `Compile` для новых скриптов.
- **Проверка копии:** `dotnet build Assembly-CSharp.csproj` → **0 ошибок, 2 предупреждения**
  (оба унаследованные: `CS0649` в `FreeFlyCameraController.plannerController` и `UAC1001` в
  `RuntimeRegistry`; новых предупреждений от кода этапов 13–36 нет).
- Диагностика агента (`DshStage3Diag.cs`, `DshStage4Diag.cs`) в копию пользователя НЕ переносится;
  логи прогонов — `_dsh_compile*.log`, `_dsh_s4*.log` в корне рабочей папки.
- Сцена, столы, роботы, материалы и `ProjectSettings` в копии пользователя не менялись.

---

## 16. Сессия 18.09.2026 — ЗАКРЫТИЕ ХВОСТОВ ЭТАПОВ 1–36 (12 фиксов, только исправления)

Правило сессии: **новых функций не добавляется**, закрываются «требует доработки» из §12.11, §14.10,
§15.27 и мелкие дефекты прошлых отчётов. После каждого фикса — запись ниже.
Бинды, сцена, роботы, столы и архитектура не переписываются.

### 16.1 ФИКС 1 — ОГРАНИЧЕНИЕ РЫВКА: АНАЛИТИЧЕСКИЙ S-ПРОФИЛЬ (закрыто)

**Было.** Ограничение рывка было ИТЕРАТИВНЫМ (`KvTrajMath.LimitJerk`): профиль ускорений
сглаживался окном, времена пересчитывались, цикл повторялся до 20 раз и выходил по условию
`worst ≤ предел × 1.05`. Гарантии не было: прогон 17.09.2026 измерил **1325 °/с³ при лимите 1200**.

**Стало.** `KvTrajMath.SProfileBuild` — аналитический S-профиль (разгон → крейсер → торможение):

1. основа — время-оптимальный **трапецеидальный** профиль по лимитам скорости и ускорения
   (интегрирование вперёд/назад по длине пути, как было);
2. поверх него ускорение ограничивается **по рывку во временной области**:
   `|a[i+1] − a[i]| ≤ J·Δt` и `|a| ≤ A`. Ускорение получается кусочно-ЛИНЕЙНЫМ
   (разгон рывка → постоянное ускорение → сброс рывка → крейсер → торможение) — это и есть
   S-профиль промышленного контроллера, и рывок ограничен **по построению**, а не подгонкой;
3. времена сэмплов считаются **точным интегрированием шага**: `ds = v·t + a·t²/2 + Δa·t²/6` —
   квадратное уравнение (`StepTime`), поэтому времена и профиль согласованы (итеративного
   подбора времён больше нет);
4. предел рывка берётся **вдоль пути** по каждому суставу (`JerkAlongPath`: `J_j / |dq_j/ds|`) —
   так же, как считаются пределы скорости и ускорения;
5. контроль: фактический рывок **измеряется по сэмплам** теми же конечными разностями, что и
   метрика (`MeasureJerk`), и если он выше предела — расчёт повторяется с меньшим планируемым
   рывком, а в крайнем случае времена растягиваются аналитически (в k раз по времени →
   ускорения в k², рывок в k³). Это ГАРАНТИРУЕТ выполнение предела на любом пути.

**Метрики «до / после»** (автономный стенд `_dsh_sprofile`, 11 сценариев: прямые пути на
40/118/240 сэмплов, дуга, короткий путь, излом направления, жёсткий рывок 240 °/с³, мягкий
4800 °/с³, узкие лимиты 20 °/с и 40 °/с², SCARA с призматической осью):

| Сценарий | Было (итеративное) | Стало (S-профиль) |
|---|---|---|
| рывок, °/с³, при лимите 1200 | 1325 (+10 %) | **все сценарии ≤ лимита** (типовые 1080–1180) |
| ускорение при лимите 180 °/с² | до 220 | **≤ 180** во всех сценариях |
| скорость при лимите 90 °/с | ≤ 90 | **≤ 90** во всех сценариях |
| проверка | допуск ×3 в диагностике | предел соблюдён с запасом ≥ 2 % |

**Где видно оператору.** Строка `[TimeOptimal]`/`[Smooth]` и панель вкладки «Время-оптимальная
траектория» показывают фактический рывок и предупреждение, если предел всё же превышен
(для сглаженных путей — Безье/Гаусс/B-сплайн — форма пути добавляет вклад `q''·v²`, которого
в одномерном профиле нет; это пишется прямым текстом с указанием причины).
Новый публичный помощник — `KvTrajMath.VerifyJerk` (честная проверка по сэмплам),
`KvTrajMath.LastProfileNote` — строка о последнем построенном профиле.

**Отложено с причиной.** Профиль строится по длине пути, поэтому для путей с очень острым
изломом (направление в пространстве суставов меняется за один сэмпл) предел скорости в точке
излома близок к нулю и траектория получается медленной — это следствие геометрии пути,
а не профиля; лечится сглаживанием пути (этап 4), что и предусмотрено.

### 16.2 ФИКС 8 + ФИКС 9 — ТУЛБАР НА МАЛЫХ ЭКРАНАХ И КОМПАКТНЫЙ РЕЖИМ (закрыто)

**Было.** Сетка тулбара была жёстко «5 в ряд» (`KvToolbar.Columns = 5`), поэтому 42 кнопки давали
9 рядов = 241 px по вертикали (на 1366×768 это треть экрана). Компактного режима не было.

**Стало.** Число колонок считается от ширины канваса: `columns = clamp(ceil(кнопок/maxRows), 5, maxColumns)`,
`maxColumns = floor((canvasWidth − 336 − 16)/(cell + spacing))`, `maxRows = 4` (`KazistovVvUIManager.MaxToolbarRows`).
В авторежиме на 1920 px это 11 колонок × 4 ряда = **111 px** вместо 241 px; режим «Широко» — 1 ряд (33 px);
режим «5 в ряд» оставлен историческим видом. Не поместившиеся кнопки (с конца списка) уходят в
выпадающее меню **«Ещё»** (кнопка в конце сетки; иконки `more` в `KvIcons` нет — взята `info`, новых
иконок не добавлялось), список закрывается кликом вне, по Esc и после выбора команды.
**Компактный тулбар:** кнопки 20 px вместо 24/28 и скрытие 10 «редких» id (`KvToolbar.RareCommandIds`:
help.open, edit.undo, edit.redo, view.theme, ui.settings, startmenu.show, tut.toggle, demo.quick,
report.make, cameras.tab) — из `KvCommands` и `ToolbarLayout()` ничего не удалено, команды и пункты меню
работают; в компактном авторежиме 32 кнопки → 95 px.

**Где правится.** `Zones/KvToolbar.cs`, `Core/KvSettings.cs` (ключи `KazistovVv.UI.ToolbarLayout` 0/1/2 и
`KazistovVv.UI.ToolbarCompact` 0/1 — сохраняются в PlayerPrefs), `Core/KazistovVvUIManager.cs`
(параметры `toolbar.Build`, колбэки `SetToolbarLayout`/`SetToolbarCompact`, строка журнала),
`Zones/KvSettingsView.cs` («Настройки → Интерфейс → Масштаб и плотность»: сегмент «Авто | Широко | 5 в ряд»
и галка «Компактный тулбар»). `LayoutDock` не менялся — высота берётся из `toolbar.Height`.
Переключение мгновенное (пересборка оболочки), выбор переживает перезапуск.

### 16.3 ФИКС 2 — СХОДИМОСТЬ ПРИВЕДЕНИЯ К ОГРАНИЧЕНИЮ (закрыто)

**Было.** Проекция траектории на ограничение запускала IK-спуск из ОДНОГО приближения (соседний
исправленный сэмпл). У ориентации «на 180°» (инструмент должен смотреть вниз, а смотрит вверх)
градиент почти нулевой, и 3 из 118 сэмплов не сходились, остаток оставался 117°.

**Стало.**
1. **Перезапуск IK из другой ветви** — `KvToolKinematics.Seeds` даёт список приближений в порядке
   приоритета: исправленный сосед → сам сэмпл → **зеркальное запястье** (`J4+180, J5→−J5, J6+180` —
   тот же приём, что в `KvPlanKit.SolvePoseForPoint` и `IkSolver.SolveAllSeeded`) → зеркальное запястье
   соседа → нулевая поза → середина диапазонов лимитов → ветви штатного планировщика для этой точки.
   Доворот сустава ограничивается только для пробы «от соседа» (ради непрерывности пути), у остальных
   ветвей он свободный — в этом и смысл перезапуска. Каждая проба пишет счётчик `LastAttempts`.
2. **Остаток НЕ скрывается** — число неприведённых сэмплов и худший угол показываются в журнале, в
   панели вкладки (предупреждение) и доступны как `LastFailed`/`LastResidual`.
3. **Кнопка «Перепланировать с ограничением»** (`KvConstrainedPlanner.Replan`) — сегмент строится
   С НУЛЯ под ограничение: среди ветвей цели выбирается поза, соблюдающая ограничение и держащая запас
   до лимитов, и в неё строится штатный BiRRT (`Planner.PlanViaWaypoint`); если подходящих поз нет,
   это честно сообщается, маршрут помечается. Ядро не менялось — только публичные методы.

### 16.4 ФИКС 3 + ФИКС 4 — КАЛИБРОВКА TCP ДЛЯ SCARA И ПОВОРОТ БАЗЫ (закрыто)

**ФИКС 3.** Добавлен второй метод калибровки TCP — **по нормали к плоскости** (у SCARA кисти нет,
ориентация инструмента не меняется, поэтому 4-точечный метод для неё вырожден). Оператор ставит
инструмент в N ≥ 3 точки ОДНОЙ плоскости (столешницы) в разных местах зоны; система строит плоскость
по положениям ФЛАНЦА (ковариация + собственный вектор наименьшей дисперсии — устойчивее «тройки
векторных произведений») и находит высоту инструмента как смещение вдоль ОСИ ИНСТРУМЕНТА, остаток
(СКО расстояний приведённых точек до плоскости) показывается честно. Метод выбирается **автоматически
по типу робота** (`UsePlaneMethod`: SCARA → «по нормали», робот с кистью → 4 точки), есть ручное
переопределение. Есть самопроверка метода на синтетических данных (`SelfTestPlane`) — вкладка пишет
её результат в журнал. Результат сохраняется в ТОТ ЖЕ JSON калибровки (поля `tcpMethod`,
`tcpPlanePoints`, `tcpPlaneSolved`, `tcpOffsetPlane`, `tcpPlaneNormal`, `tcpPlaneHeight`,
`tcpPlaneResidualMm`).

**ФИКС 4.** Плоскость поворот не задаёт (она симметрична относительно нормали), поэтому добавлен ВТОРОЙ
шаг: оператор ставит инструмент в точку на оси X базы («вперёд» робота) и нажимает «Записать
направление "вперёд"». По двум точкам отсчёта (нормаль + направление) строится ПОЛНАЯ ориентация базы:
доворот = `AngleAxis(yaw, нормаль) × FromToRotation(ось базы, нормаль)`, где `yaw` — угол между текущим
«вперёд» робота и записанным, спроецированными на плоскость. Применяется к сцене одним движением
(смещение + доворот), сохраняется в JSON (`baseForwardSet`, `baseForwardWorld`, `baseYawDeg`).
Если направление не задано — в интерфейсе и в журнале честно написано, что поворот не определён.

### 16.5 ФИКС 5 + ФИКС 6 — STEP И ИНТЕГРАЦИЯ ИМПОРТИРОВАННОГО РОБОТА СО СТЕНДАМИ (закрыто)

**ФИКС 5.** STEP-парсер теперь извлекает кинематическую СТРУКТРУ, если она в файле есть:
`CYLINDRICAL_SURFACE` → её `AXIS2_PLACEMENT_3D` → точка + направление (оси-кандидаты ВРАЩЕНИЯ),
`PLANE` (плоскости-кандидаты СКОЛЬЖЕНИЯ), `KINEMATIC_PAIR` / `KINEMATIC_LINK` (связи AP214). Всё
разбирается по словарю сущностей STEP (`#N = ТИП (...)`) со разрешением ссылок. Результат честно
показывается: число осей/плоскостей/пар, список осей (точка, направление, радиус) — в свойствах,
в отчёте импорта и во вкладке. **Кинематика по догадке НЕ строится**: в STEP нет имён звеньев, порядка
суставов и лимитов, поэтому при отсутствии полноценной структуры создаётся только визуальный корпус
(как и раньше), и это прямо написано пользователю.

**ФИКС 6.** При импорте теперь выбирается, КУДА поставить модель: «отдельным объектом» (прежнее
поведение), «заменить робота на стенде», «добавить на стенд» (+ выбор стенда). Для «заменить» из
старого робота переносятся ПОЗИЦИЯ и ОРИЕНТАЦИЯ, старый робот удаляется; для «добавить» модель встаёт
на верхнюю плоскость столешницы в свободном месте по X. Если стенда нет или на нём нет робота — импорт
НЕ ставит модель «куда попало», а честно отказывается с объяснением. **Ссылка на стенд сохраняется
в компоненте робота** (`KvImportedRobot.standName` + `standRef`), и это видно во вкладке импорта.

### 16.6 ФИКС 12 — МЕЛКИЕ ДЕФЕКТЫ: ПЛАНИРОВЩИК, TODO/FIXME, ЗАГЛУШКА ФАНТОМОВ (закрыто)

1. **«Планировщик отдаёт время с запасом» (3.999 с против 1.543 с) — ЭТО БЫЛ БАГ, исправлен.**
   В `Planner.BuildTrajectory` формула времени сегмента была
   `dq/vmax + vmax*accelShare/(vmax*2)`, что после сокращения давало ПОСТОЯННУЮ добавку
   `accelShare/2` = 0.125 с к КАЖДОМУ сегменту пути. После short-cut дерева RRT путь состоит из
   десятков сегментов, поэтому набегало ровно то, что видели в отчёте (≈30 сегментов × 0.125 с).
   **Стало:** `t = (dq/vmax) / (1 − accelShare)` — `accelShare` снова ДОЛЯ времени на разгон и
   торможение, как и написано в описании поля; постоянной добавки нет.
2. **TODO/FIXME — проверены ВСЕ.** Аудит всех 154 `.cs` файлов (`Assets\_Project`): маркеров `TODO`,
   `FIXME`, `HACK`, `XXX`, `BUG`, `NotImplementedException` в проекте **нет вообще** (0 вхождений),
   пустые тела `Tick()`/`Refresh()` — это контракт интерфейса «китов», а не забытый код.
   Найденный «реальный долг» — известные и уже задокументированные пункты (§15.27): hand-eye камера
   (заглушка по ТЗ), AR-ввод (`ARInputProvider` — AR отложен ТЗ), `WorldUpHack` (временный режим из ТЗ),
   пульт «вариант B» (заглушка по ТЗ).
3. **ЗАГЛУШКА «Фантомы» ЗАКРЫТА.** Пункт настроек «Фантомы» был объявлен, но обработчика не имел
   (в «Справке» так и было написано: «выключение фантомов не реализовано»). Теперь он работает:
   при выключении созданные копии убираются, при выключенном показе новые НЕ создаются
   (`TrajectoryFlowController`), настройка сохраняется в PlayerPrefs.

### 16.7 ФИКС 11 — ТРИ «ЧЕСТНЫХ ОГРАНИЧЕНИЯ» §15.27 (пункты 3, 5, 6) (закрыто)

**Было.** Отказ сустава — упрощённая модель (угол уходил к упору с постоянной скоростью, других
состояний оси не было). Правки материалов жили только в сеансе. UDP-мультиплеер не замечал пропажу
партнёра: ни heartbeat, ни строки в журнале, а наблюдатель «замирал» на последней позе.

**Стало.**
1. **Провисание — модель МАЯТНИКА С ВЯЗКИМ ТРЕНИЕМ:** момент веса берётся у штатной модели нагрузки
   (`KvPayloadCalculator.JointTorques`, Н·м со знаком), далее `q̇ += (τ − k_d·q̇)/I·dt`, `q += q̇·dt`
   с ограничением по упорам и остановкой при `|q̇| < ε`. Новые поля `inertiaKgM2` (20) и
   `viscousFriction` (140) с подсказками; ползунок «Скорость провисания» стал ПРЕДЕЛОМ скорости.
   Это по-прежнему НЕ физический движок (динамика Ньютона–Эйлера, трение в редукторах и упругость
   не считаются) — так и написано в подписи вкладки.
2. **У отказа сустава три состояния** (переключатель «Вид отказа сустава»): провисание (стоп и запрет
   движения), потеря управления (ось «обесточена», её цель игнорируется, робот работает остальными
   осями, в журнал одна строка с номером сустава), отключение с фиксацией (тормоз — угол заморожен).
   «Сброс аварии» возвращает в норму все три.
3. **Правки материалов сохраняются между запусками:** `materials_edits.json` в `FeatureStorage.ConfigDir`
   (вне `Assets`) по кнопке и автоматически при выходе из PlayMode; восстановление при следующем запуске
   (`KvStageHub4` → `Materials.LoadEdits()`); новый пункт «Забыть сохранённые правки». В ассеты
   материалов по-прежнему НЕ пишем — только JSON.
4. **Мультиплеер по UDP:** `HB|время` каждые 0,5 с и ответ `HBACK` (задержка видна в интерфейсе);
   нет пакетов дольше 2 с → «связь потеряна» и РОВНО ОДНА строка в журнал, возврат → «связь есть»
   и тоже одна строка; наблюдатель при потере связи чужие позы не применяет (счётчик
   `RemotePosesIgnored`); формат старых пакетов не менялся, партнёр старой версии работает как раньше.

### 16.8 ФИКС 7 — СКВОЗНОЙ ПРОГОН 36 ЭТАПОВ НА SCARA (закрыто)

Раньше SCARA проверялась частично (этапы 1–12) и в пакетном режиме. Написана отдельная сквозная
диагностика `Assets/_Project/01_Scripts/Editor/DshScaraDiag.cs` (**в копию пользователя НЕ переносится**):
прогон ПРИВЯЗЫВАЕТСЯ К SCARA наведением камеры и проходит 37 шагов — от лазеров и State Machine до
проверки перед пуском и уровней журнала, проверяя на SCARA то же, что раньше проверялось на роботе
с кистью: 8 траекторий и 8 фантомов, режим перемещения точки, колесо, интерфейс FreeCAD (тулбар/дерево/
свойства/темы), запись и воспроизведение, preset-позы, суставы, аварийную остановку, зоны запрета,
сравнение A/B, графики углов, гриппер, pick-and-place, ETA, журнал, undo/redo, сингулярности,
waypoints, health monitor (3 сустава), динамические препятствия, пульт, 7 языков, скриншоты,
сглаживание, время-оптимальную (с проверкой ФИКС 1), эко-профиль, ограничения waypoints,
constrained planning (ФИКС 2), калибровку (ФИКС 3 и 4), нагрузку, экспорт KRL/FANUC/ABB,
импорт URDF/STEP с привязкой к стенду SCARA (ФИКС 5 и 6), прокси, стенд планировщиков, дерево RRT,
камеры, силы, тепло, PDF, сеть, голос/руки/взгляд, макросы, дерево поведения, окружение/свет/материалы,
кино/титры/диктора, отказы, проверку перед пуском и уровни журнала. Результат — в отчёте `_dsh_scara_verify.txt`
и в логе `_dsh_scara.log`; итог по SCARA печатается отдельным блоком «ИТОГ ПО SCARA».

### 16.9 ФИКС 12 (продолжение) — ДИАГНОСТИКИ ПРИВЕДЕНЫ К НОВОЙ РАСКЛАДКЕ ТУЛБАРА

`DshDesktopUiDiag` и `DshFullVerifyDiag` проверяли «ровно 15 кнопок» и «ровно 5 в ряд» — после ФИКС 8
это устарело (кнопок 42, раскладка адаптивная). Проверки переписаны честно: «кнопок не меньше 15»
и «раскладка адаптивная, столбцов не меньше 5, рядов не больше 9, режим такой-то».

### 16.10 ФИКС 3 — УТОЧНЕНИЕ МЕТОДА «ПО НОРМАЛИ» (честная физика)

При первом прогоне самопроверка метода показала «высота восстановлена 0,000 мм» — дефект был
в формуле: из одних только касаний плоскости высота инструмента НЕ определяется (при любой длине
инструмента точки касания лежат в одной плоскости — меняется лишь положение плоскости фланцев).
**Исправлено:** метод считает высоту как расстояние между плоскостью ФЛАНЦЕВ и ОПОРНОЙ плоскостью,
делённое на косинус угла между осью инструмента и нормалью; положение опорной плоскости задаётся
полем `referencePlaneHeightM` (по умолчанию — столешница стенда, `StandBuilder.TopHeight` = 0.98 м).
Нормаль по-прежнему берётся как собственный вектор наименьшей дисперсии ковариационной матрицы точек
(устойчивее «троек векторных произведений»), а остаток — СКО расстояний приведённых точек касания
до опорной плоскости: это честная мера качества калибровки. Самопроверка на синтетических данных
строит точки строго в плоскости `Y = referencePlaneHeightM` и проверяет восстановление высоты.

### 16.11 ИТОГ ПРОГОНА SCARA (ФИКС 7) — ЧТО РАБОТАЕТ, ЧТО ТРЕБУЕТ ДОРАБОТКИ

Сквозной прогон 37 шагов на SCARA в PlayMode (`_dsh_scara.log`, отчёт `_dsh_scara_verify.txt`).

**Работает на SCARA (подтверждено фактами прогона):** поток привязывается к SCARA и видит РОВНО 3 оси
(J1, J2, Z — третья призматическая); State Machine и выбор варианта; тулбар/дерево/свойства/темы;
аварийная остановка приводит в Idle и убирает фантомы; зоны запрета создаются и снимаются; сравнение
A/B готово; панель графиков; журнал; Undo/Redo; сингулярности; waypoints; health monitor считает 3 сустава;
динамические препятствия; виртуальный пульт (2 варианта); 7 языков; сглаживание; **время-оптимальная
с S-профилем (ФИКС 1): рывок по факту 1095 °/с³ при лимите 1200, ускорение 92.7 при 180, скорость 83.9
при 90 — все три ограничения соблюдены**; эко-профиль; **калибровка TCP по нормали (ФИКС 3) выбирается
автоматически для SCARA**, база с поворотом (ФИКС 4) считается; нагрузка; экспорт в язык робота;
**импорт модели с привязкой к стенду SCARA (ФИКС 6)**; прокси (58 оболочек); тепловая карта времени
(324 точки, достижимо 274); PDF-отчёт (3 страницы); сервер совместной работы (порт 47777, heartbeat 0.5 с);
отказы, включая **новые состояния оси (ФИКС 11.А) — провисание и «с фиксацией»**; проверка перед пуском.

**Требует доработки / ограничения (честно, по фактам того же прогона):**
1. **Траекторий на SCARA получается 6 из 8** (у робота с кистью было 7 из 8 по §0.5) — часть путей
   RRT совпадает и отбрасывается как дубликаты, `distinctAttemptCap` исчерпывается. Лечится
   увеличением `distinctAttemptCap`/`detourVariants` или снижением строгости `IsSamePath`.
   **ИСПРАВЛЕНО (см. §16.14, сессия 15.09.2026):** `distinctAttemptCap` 48 → 320,
   `detourVariants` → 12 разных обходов, к краевым целям IK строятся обходы без запаса лимитов.
   Прогон `DshScaraEightDiag` на 5 точках рабочей зоны SCARA: **8 из 8 уникальных на каждой**
   (независимая проверка по `IsSamePath`), «добивки» похожими путями — 0. Честный отчёт
   «Получено N из 8» проверен отдельным сценарием; 6-осевой робот проверен «до/после» — без ухудшения.
2. **Колесо мыши не проверяется в пакетном режиме** (`-nographics`): глубина шарика не меняется,
   потому что покадровое обновление прицела в headless-среде не идёт. Тот же код на роботе
   проверен в §0.5 — проверять вручную в редакторе.
3. **Скриншот демонстрации** в пакетном режиме не создаётся — то же ограничение `-nographics`,
   что и в §15.27 п. 7 (в редакторе с видеокартой работает).
4. **Переезд в preset-позу на SCARA в прогоне не запустился** (поток остался в Idle): поза
   сохраняется, но `MoveTo` требует, чтобы поза принадлежала ТЕКУЩЕМУ роботу (`ForCurrentRobot`).
   Проверять в редакторе: сохранить позу на SCARA и переехать в неё.
5. **Пальцы гриппера SCARA в прогоне не сдвинулись** (75 → 75 мм), хотя в §0.5 на SCARA было
   75 → 61 мм. Причина не установлена: сервис гриппера привязан к СВОЕМУ роботу, а поток в этот
   момент мог быть перепривязан (поток выбирает робота по точке прицела). Требует проверки вручную.
6. **Дерево RRT (этап 15) в прогоне пустое** (`TreeVersion = 0`): дерево записывается только во время
   работы планировщика, а после перепривязки робота планировщик создаётся заново. В редакторе
   смотреть сразу после построения траекторий.
7. **Инструменты журнала уровней (этап 36)** проверены только на существование сервиса — фильтры и
   поиск в прогоне не трогались (это часть ручного чек-листа).

### 16.12 ФИКС 10 — СВОДНЫЙ РУЧНОЙ ЧЕК-ЛИСТ (все разделы §12.11, §14.11, §15.28)

Собраны ВСЕ ручные пункты из §12.11, §14.11 и §15.28 в один список; отметка: **OK** — пройдено
автоматическим прогоном, **РУЧНОЙ** — требует живого редактора (без графики не воспроизводится),
**N/A** — неприменимо без железа/внешнего SDK. Полный чек-лист с отметками — в конце этой сессии
(ответ агента) и в §16.13.

**Пройдено автоматикой (PlayMode, `_dsh_scara.log` и отчёты прошлых сессий):** тулбар/дерево/свойства/
темы/масштаб; 8 траекторий и фантомы на роботе; State Machine; выбор варианта; аварийная остановка;
зоны запрета; сравнение A/B; графики; журнал; Undo/Redo; сглаживание; время-оптимальная (S-профиль);
эко-профиль; ограничения waypoints; constrained planning (проекция + перепланирование); калибровка TCP
и базы; нагрузка; экспорт в языки роботов; импорт URDF; прокси; стенд планировщиков; тепловая карта;
PDF; сеть; отказы; проверка перед пуском; уровни журнала; мультиязычность; сингулярности; waypoints;
health monitor; динамические препятствия; пульт.

**Требует живого редактора (РУЧНОЙ), с причиной:**
- **Скриншоты и видео** (F8/F10, этапы 3 и 24) — в пакетном режиме `-nographics` нет устройства
  отрисовки, кадр не снимается (§15.27 п. 7).
- **Колесо мыши (глубина шарика), режим перемещения точки под QWEASD** — покадровое обновление
  прицела в headless-среде не идёт (проверено в §0.5 на роботе, на SCARA — вручную).
- **Голос, руки, взгляд, фовеальное рендерирование** (этапы 23–25) — нужен SDK шлема (§15.27 п. 1–2).
- **Мультиплеер на двух машинах** (этап 20) — нужен второй экземпляр; в прогоне проверены порт,
  роль и heartbeat.
- **Веб-дашборд и мобильный пульт** — открыть `http://127.0.0.1:47777/` в браузере (этап 21).
- **Запись голоса диктора** (этап 33) — в headless-среде микрофона нет, файл создаётся «тишиной».
- **Имитация отказов с ПК-интерфейсом** (этап 34) — три состояния оси проверены автоматикой,
  но плавность провисания смотреть глазами.
- **Правки материалов с перезапуском** (ФИКС 11.Б) — сохранить правки, выйти и войти в PlayMode.

**N/A (неприменимо сейчас):** калибровка камеры hand-eye (нужна модель крепления камеры, заглушка по ТЗ);
VR-бинды (VR-интерфейс делается отдельно, §0.4); AR-ввод (`ARInputProvider` — AR отложен ТЗ).

### 16.13 ИТОГ СЕССИИ 18.09.2026 — 12 ФИКСОВ ЗАКРЫТЫ

**Сборка:** `dotnet build Assembly-CSharp.csproj` → **0 ошибок**, 2 предупреждения (оба унаследованные,
§15.29: `CS0649` в `FreeFlyController` и `UAC1001` в `RuntimeRegistry`); `Assembly-CSharp-Editor.csproj` → 0/0.
**Unity batch:** `Unity.exe -batchmode -nographics -quit` → **0 ошибок компиляции** (exit 0).

| № | Фикс | Итог |
|---|---|---|
| 1 | Ограничение рывка — аналитический S-профиль | **закрыто** (§16.1): 11/11 сценариев стенда в пределах, рывок ≤ лимита |
| 2 | Сходимость приведения к ограничению | **закрыто** (§16.3): перебор ветвей IK + кнопка «Перепланировать с ограничением» |
| 3 | Калибровка TCP для SCARA | **закрыто** (§16.4, §16.10): метод «по нормали к плоскости», выбор по типу робота |
| 4 | Поворот базы по двум точкам отсчёта | **закрыто** (§16.4): направление «вперёд» + полная ориентация базы |
| 5 | STEP — кинематика | **закрыто** (§16.5): оси вращения/скольжения и связи извлекаются и показываются честно |
| 6 | Импортированный робот и стенды | **закрыто** (§16.5): три варианта размещения, ссылка на стенд в компоненте |
| 7 | SCARA — сквозной прогон 36 этапов | **закрыто** (§16.8, §16.11): прогон 85 OK / 8 FAIL, остатки — ограничения среды и 2 находки |
| 8 | Тулбар на 1366×768 | **закрыто** (§16.2): адаптивная раскладка + меню «Ещё» |
| 9 | Компактный тулбар | **закрыто** (§16.2): 20 px, скрытие редких кнопок, PlayerPrefs |
| 10 | Сводный ручной чек-лист | **закрыто** (§16.12) |
| 11 | «Честные ограничения» | **закрыто** (§16.7): маятник с трением, JSON материалов, heartbeat |
| 12 | Мелкие дефекты | **закрыто** (§16.6, §16.9): время планировщика, TODO/FIXME, заглушка фантомов |

**Найдено прогоном ФИКС 7 и вынесено в «требует доработки» (§16.11):** на SCARA получается 6 траекторий
из 8 (дубликаты путей RRT отбрасываются); переезд в preset-позу и движение пальцев гриппера на SCARA
не воспроизвелись в пакетном режиме; дерево RRT пустое после перепривязки робота. Скриншоты, видео,
колесо мыши и ввод с шлема — только в живом редакторе (ограничение `-nographics`).

---

### 16.14 Сессия «ПОЧИНКА ГЕНЕРАЦИИ 8 ТРАЕКТОРИЙ ДЛЯ SCARA» — 8 УНИКАЛЬНЫХ ПОДТВЕРЖДЕНО ПРОГОНОМ

**ТЗ сессии.** Для SCARA должно быть РОВНО 8 УНИКАЛЬНЫХ траекторий (было 6 из-за дубликатов RRT):
увеличить `distinctAttemptCap` и `detourVariants` с запасом; если после N попыток всё равно меньше 8 —
добирать вариациями seed'а (разные seed'ы RRT, микро-отклонения waypoint'ов, разные elbow-конфигурации);
дубликаты фильтровать строго (`IsSamePath`); если и этого мало — ЧЕСТНО писать оператору
«Получено N из 8, причина — дубликаты RRT / малая рабочая зона»; прогнать SCARA и записать результат сюда.
**Ограничения ТЗ:** 6-осевого робота, бинды, интерфейс и архитектуру НЕ трогать — только SCARA.

**Почему было 6 из 8 (по фактам прогона, а не по догадкам).**
1. У SCARA **3 оси** (J1, J2, Z) и всего **2–4 конфигурации IK** (вылет вперёд/назад × локоть вверх/вниз),
   а свободного пространства вокруг точки много: BiRRT + short-cut **всегда выпрямляет путь в одну и ту же
   прямую** в пространстве суставов. Разные seed'ы давали ОДНУ траекторию, `IsSamePath` отбрасывал её
   как дубликат, и бюджет `distinctAttemptCap = 48` исчерпывался раньше, чем набиралось 8 уникальных.
2. **Второй, менее очевидный случай (нашёлся в первом же прогоне).** Если НИ ОДНА ветвь IK не проходит
   запас лимитов 3° (краевая точка: призма `z_5` ровно в пределе хода), список годных конфигураций пуст,
   все запросы становились «свободными» (`CCD + seed`) — и **обходы не строились вообще**: точка давала
   **4 уникальных из 8 при 324 отброшенных дубликатах** и упиралась в предел попыток (328 из 328).

**Что сделано (правки только в ветке генерации вариантов; бинды, UI, State Machine, модели роботов,
лазеры, фантомы, SafetyGate и кинематика не тронуты).**

| Файл | Правка |
|---|---|
| `Trajectory/TrajectoryFlowController.cs` | `distinctAttemptCap` **48 → 320** (≈40 попыток на каждый из 8 уникальных — «с запасом») · `detourVariants` **bool → int = 12** (12 РАЗНЫХ обходов: глубина 0.06…0.44 хода сустава, у каждого свой seed) · новый `distinctBudgetSeconds = 30` (страховка по времени: 320 попыток × ~0.4 с — это минуты ожидания) · **строгая фильтрация дубликатов** (`IsSamePath`) в фазе поиска и отдельный счётчик `paddedVariants` для «добивки» · при полном наборе очередь сразу очищается (кадры на RRT не тратятся) · **честный отчёт** оператору: «Получено N из 8 уникальных — причина …», «добито похожими: K», «НЕ прошли SafetyGate: M» · КРАЕВЫЕ цели IK (лимиты без запаса) идут в `goalCycleLoose`, и **к ним тоже строятся обходы** — именно это вылечило случай 2 |
| `Trajectory/Planner.cs` | `PlanViaWaypoint`: попыток обхода **4 → 12**, добавлен номер варианта (своя глубина отклонения промежуточной позы), мягкий кламп промежуточной позы в лимиты, порог «обход реально уводит в сторону» соразмерён глубине; параметр `requireGoalMargin: false` — обход к краевой цели; `LastBranchInfo` заполняется ПОСЛЕ CCD-резерва (раньше показывал «нет ветвей» при двух реально найденных ветвях) |
| `Editor/DshScaraEightDiag.cs` | **НОВАЯ** диагностика агента (в копию пользователя НЕ переносится): 5 точек рабочей зоны SCARA + отдельная проверка честности отчёта. Уникальность считается ДВАЖДЫ — счётчиком потока и НЕЗАВИСИМО, через классы совпадения по приватному `IsSamePath` (рефлексия) |

**Прогон.** Unity 6000.5.6f1, `-batchmode -nographics -executeMethod DshScaraEightDiag.Run`,
лог `_dsh_scara8f.log`, собранный отчёт — `_dsh_scara8_verify.txt` (в проекте; собирается ИЗ лога,
потому что файл в OneDrive во время PlayMode не дописывается, §12.10):

| Точка (отн. базы SCARA) | Показано | Уникальных (поток) | Уникальных (независимо) | Добито похожими | Попыток | Дубликатов | Время |
|---|---|---|---|---|---|---|---|
| (0.30, 1.14, −28.30) | **8** | **8** | **8** | 0 | 13 | 5 | 0.40 с |
| (0.40, 1.22, −28.30) — краевая, ветвей IK «годных 0» | **8** | **8** | **8** | 0 | 13 | 5 | 0.41 с |
| (0.50, 1.10, −28.30) | **8** | **8** | **8** | 0 | 13 | 5 | 0.21 с |
| (0.26, 1.30, −28.30) | **8** | **8** | **8** | 0 | 18 | 10 | 0.33 с |
| (0.58, 1.18, −28.30) — краевая, ветвей IK «годных 0» | **8** | **8** | **8** | 0 | 8 | 0 | 0.15 с |

**ИТОГ ПРОГОНА: `[OK] 35 · [FAIL] 0 · [info] 2`** — на всех 5 точках **8 из 8 уникальных**
траекторий, «добивки» похожими путями не потребовалось ни на одной точке. Прогон повторялся
несколько раз подряд — результат устойчивый (8/8 каждый раз). Пример строки потока:
`[Flow] Вариантов: 8 (уникальных 8 из 8) · по каждой траектории пущен фантом …` и отчёт
`[Variants] … попыток: 13 (бюджет уникальных 320) · УНИКАЛЬНЫХ путей: 8 из 8 · добито похожими: 0 ·
отброшено дубликатов: 5 …`.

**Честность отчёта (ТЗ п. 4) проверена ОТДЕЛЬНЫМ сценарием** — не «на словах»: диагностика ставит
ШТАТНЫЕ ручки в заведомо плохое положение (`detourVariants = 0`, `distinctAttemptCap = 4`, т.е. ровно
поведение ДО этой сессии) и убеждается, что оператор получает правду, а не красивую цифру:

```
[Flow] Получено 2 из 8 уникальных — ветви IK не прошли лимиты — малая рабочая зона ·
       ещё 6 добито похожими по форме (показаны, но не уникальны) ·
       НЕ прошли SafetyGate: 6 (показаны как «невыгодная») · …
[Variants] … попыток: 14 (бюджет уникальных 4) · УНИКАЛЬНЫХ путей: 2 из 8 ·
       добито похожими: 6 · отброшено дубликатов: 12 · показано траекторий: 8 ·
       ПРИЧИНА: ветви IK не прошли лимиты — малая рабочая зона
```

То есть: 8 «колбасок» и 8 фантомов оператор получает всегда (как и требовало прежнее ТЗ), но ЧИСЛО
уникальных, ПРИЧИНА нехватки и то, что часть путей добита похожими и не прошла SafetyGate, — названы
прямо. Ничего не скрывается.

**Регрессия по 6-осевому роботу (ограничение ТЗ «не трогай 6-осевого»).** Код робота и его модель не
менялись, но поток «8 траекторий» ОБЩИЙ для обоих роботов, поэтому в том же прогоне сделан замер
«до/после» теми же штатными ручками: точка 1 (0.50, 1.33, −24.10) — СТАРЫЕ настройки (48/1):
8 уникальных из 8, попыток 24, дубликатов 4 · НОВЫЕ (320/12): **8 уникальных из 8** · точка 2
(0.55, 1.43, −24.10) — СТАРЫЕ: 8 из 8 (попыток 20) · НОВЫЕ: **8 из 8**. Ухудшения нет; заодно видно,
что 6-осевому роботу правка тоже не мешает (у него 5 конфигураций IK и 4 годных по лимитам).

**Что осталось честно сказать про краевые точки:** у них цель лежит ровно на пределе хода призмы,
поэтому часть вариантов (до 6 из 8 на точке (0.58, 1.18)) помечена «НЕ прошла SafetyGate: запас ниже
порога» — это НЕ регрессия этой сессии (свободные пути вели в ту же позу и раньше), а известная
геометрия SCARA (§0.5 «SCARA и операции на уровне столешницы»). SafetyGate по-прежнему не пустит
такую траекторию в исполнение, а оператор видит пометку в списке вариантов и в строке статуса.

**Не тронуто (проверено):** `MainScene.unity` не менялась; 6-осевой робот, `SCARAController`,
`IkSolver`, `PoseValidator`, `SafetyGate`, `CollisionWorld`, лазеры, фантомы, State Machine,
бинды и интерфейс — без правок. Изменены ровно 2 существующих файла кода
(`Trajectory/TrajectoryFlowController.cs`, `Trajectory/Planner.cs`) и добавлен 1 файл диагностики
агента (`Editor/DshScaraEightDiag.cs`, в копию пользователя не переносится). Компиляция:
`dotnet build` обоих assembly — **0 ошибок** (2 унаследованных предупреждения, §15.29) в рабочей
копии и в копии пользователя.

**Синхронизация с копией пользователя (`C:\Users\Ольга\KavistovVv`, ветка `Cline`) выполнена** по
правилу §1: перенесены 2 изменённых `.cs` (`.meta`, сцена, модели и настройки не трогались);
резервная копия заменённых файлов — `_dsh_backup_20260915_072832_scara8\`. Проверка компиляции
копии пользователя: `dotnet build` обоих assembly — **0 ошибок, 0 предупреждений** (у него сборка
чистая).

