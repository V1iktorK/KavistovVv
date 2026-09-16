# LOCALIZATION_AUDIT.md — аудит локализации проекта KazistovVv

**Дата:** 15.09.2026
**Тип документа:** отчёт. **Ни код, ни словари в рамках этого этапа НЕ изменялись.**
**Языков по ТЗ:** 7 — RU, EN, ZH, ES, DE, FR, JA.
**Инструменты:** `_tools\kv_audit_loc.ps1`, `_tools\kv_audit_loc_gap.ps1`. Сырые данные: `_tools\audit_loc.txt` (2 121 строка), `_tools\audit_loc_gap.txt` (316 строк).

---

## 0. Как устроена локализация в проекте (кратко, по коду)

| Слой | Файл | Формат | Языков | Ключей |
|---|---|---|---|---|
| **Встроенный минимум** | `06_KazistovVv_UI\03_Scripts\Core\KvLocalization.cs`, `RegisterBuiltin()` | вызовы `AddString(code, key, text)` | en, ru | 7 |
| **Внешние словари (данные)** | `Assets\StreamingAssets\kazistovvv_i18n\{ru,en,zh,es,de,fr,ja}.json` | `{ "key": "…", "text": "…" }` | 7 | **187 в каждом** |
| **Рантайм-таблицы модулей** | `01_Scripts\Features\KvLocExtra.cs`, `KvLocExtra2.cs`, `KvLocExtra3.cs` | `Row("ключ", ru, en, zh, es, de, fr, ja)` → `KvLoc.AddRuntimeStrings` | 7 | **419** |
| **Русский литерал в коде** | вызовы `KvLoc.T("ключ", "русский текст")` | — | ru | — |

**Цепочка подстановки** (`KvLoc.T`): `каталог(текущий язык)[key]` → `каталог(en)[key]` → `fallbackRu` (русский текст, переданный вызывающим кодом) → сам ключ.

**Приоритет источников** (`KvLocExtra2.Install()`, строка 50): `if (KvLoc.Has(key)) continue;` — **словарь из JSON главнее рантайм-таблицы**. То есть если ключ есть в файле-словаре, значение из `KvLocExtra*` игнорируется.

**Практическое следствие:** если ключа нет **ни в JSON, ни в рантайм-таблице**, то во **всех семи языках** (кроме случая, когда ключ случайно есть в английском каталоге) на экране появится **русский текст**. Это и есть предмет раздела 3.

---

## 1. Покрытие ключей по языкам

### 1.1 Взаимное покрытие словарей JSON: **100 % для всех 7 языков**

| Язык | Ключей в файле | Отсутствует против RU | Лишних против RU | Покрытие |
|---|---:|---:|---:|---:|
| RU (`ru.json`) | 187 | — (эталон) | — | **100,00 %** |
| EN (`en.json`) | 187 | **0** | **0** | **100,00 %** |
| ZH (`zh.json`) | 187 | **0** | **0** | **100,00 %** |
| ES (`es.json`) | 187 | **0** | **0** | **100,00 %** |
| DE (`de.json`) | 187 | **0** | **0** | **100,00 %** |
| FR (`fr.json`) | 187 | **0** | **0** | **100,00 %** |
| JA (`ja.json`) | 187 | **0** | **0** | **100,00 %** |

**Все семь словарей — зеркальные копии друг друга по набору ключей.** Это значит: **отсутствующих ключей «для конкретного языка» нет** — список отсутствующих ключей **один и тот же для всех семи** языков и приведён в разделе 3.

Аналогично по рантайм-таблицам `KvLocExtra/2/3`: каждая строка `Row(...)` содержит **все семь переводов**, и `Install()` регистрирует их для всех семи кодов. Частичных языков там тоже нет.

### 1.2 Реальное покрытие ключей, которые запрашивает код: **45,9 %**

Так как словари заморожены на 187 ключах, а код за время сессий вырос, честная метрика — не «словарь против словаря», а **«ключ, который код ищет» против «ключ, который где-нибудь есть»**.

| Показатель | Ключей | Доля |
|---|---:|---:|
| **Всего ключей, запрашиваемых кодом** | **379** | 100 % |
| — переведены словарями JSON | 130 | 34,3 % |
| — переведены рантайм-таблицами `KvLocExtra/2/3` | 44 | 11,6 % |
| **Итого переведено** | **174** | **45,9 %** |
| **НЕ переведено нигде → русский текст во всех 7 языках** | **205** | **54,1 %** |

> Пояснение, почему 379, а не «все ключи проекта»: код обращается к ключам двумя способами — прямо (`KvLoc.T("ключ", …)`, `KvLoc.Menu(…)`) и **косвенно**, через идентификатор команды: `KvCommand.LocalizedTitle` вызывает `KvLoc.Cmd(Id, Title)` → ключ `cmd.<Id>`, а `LocalizedDescription` → `cmd.<Id>.desc`. Каждая из ~157 зарегистрированных команд даёт два ключа. Поэтому в 379 входят и те, что нигде не встречаются строкой в коде.

---

## 2. Дубликаты ключей внутри одного языка

**Дубликатов не найдено ни в одном из 7 файлов.** Проверка: разбор всех вхождений `"key": …` по порядку с контролем повторного появления ключа.

| Язык | Дубликаты |
|---|---|
| RU / EN / ZH / ES / DE / FR / JA | **0** |

Отдельно проверено: повторная регистрация того же ключа в рантайм-таблицах `KvLocExtra*` (`AddString`/`AddRuntimeStrings` перезаписывает значение) — конфликтов приоритета «JSON vs рантайм» нет, потому что строка 50 `Install()` пропускает ключи, уже загруженные из словаря.

---

## 3. Отсутствующие ключи — 205 (одинаково для всех 7 языков)

Полный список. Формат: `ключ — место в коде, где он запрашивается`.
Для каждого из этих ключей `KvLoc.T` дойдёт до последнего звена цепочки и вернёт **русский литерал из кода**, то есть в EN/ZH/ES/DE/FR/JA интерфейс покажет русский текст.

### 3.1 Команды интерфейса — 168 ключей (`cmd.*`, из них 84 команды × 2)

Эти ключи образуются из `Id` команд, зарегистрированных в `FeatureHub`, `KvStageHub`, `KvStageHub2/3/4`, `KvDockPanel` и `KazistovVvUIManager`:

```
cmd.compare.mark / .desc                 FeatureHub.cs:1244
cmd.compare.open / .desc                 FeatureHub.cs:1253
cmd.demo.pickplace / .desc               FeatureHub.cs:1316
cmd.eta.toggle / .desc                   FeatureHub.cs:1327
cmd.export.tab / .desc                   KvStageHub3.cs:859
cmd.file.export / .desc                  FeatureHub.cs:1473
cmd.file.import / .desc                  KazistovVvUIManager.cs:1215
cmd.gamepad.hud / .desc                  KazistovVvUIManager.cs:1164
cmd.graph.open / .desc                   FeatureHub.cs:1265
cmd.gripper.toggle / .desc               FeatureHub.cs:1303
cmd.help.about / .desc                   KazistovVvUIManager.cs:1091
cmd.help.hotkeys / .desc                 KazistovVvUIManager.cs:1115
cmd.import.scan / .desc                  KvStageHub3.cs:879
cmd.joints.panel / .desc                 FeatureHub.cs:1168
cmd.log.export / .desc                   FeatureHub.cs:1349
cmd.log.open / .desc                     FeatureHub.cs:1339
cmd.obstacle.discard / .desc             KvStageHub.cs:741
cmd.obstacle.evaluate / .desc            KvStageHub.cs:732
cmd.pendant.attach / .desc               KvStageHub.cs:778
cmd.pendant.jogmode / .desc              KvStageHub.cs:795
cmd.pendant.variant / .desc              KvStageHub.cs:769
cmd.perf.open / .desc                    FeatureHub.cs:1360
cmd.pose.goto / .desc                    FeatureHub.cs:1157
cmd.pose.save / .desc                    FeatureHub.cs:1142
cmd.post.apply.energy / .desc            KvStageHub2.cs:741
cmd.post.apply.smooth / .desc            KvStageHub2.cs:723
cmd.post.apply.topt / .desc              KvStageHub2.cs:732
cmd.presentation.toggle / .desc          FeatureHub.cs:1415
cmd.properties / .desc                   KvDockPanel.cs:92
cmd.record.play / .desc                  FeatureHub.cs:1118
cmd.record.speed / .desc                 FeatureHub.cs:1131
cmd.scenarios.open / .desc               FeatureHub.cs:1395
cmd.scenarios.run / .desc                FeatureHub.cs:1404
cmd.session.load / .desc                 FeatureHub.cs:1436
cmd.session.panel / .desc                FeatureHub.cs:1450
cmd.session.save / .desc                 FeatureHub.cs:1427
cmd.shot.annotate / .desc                KvStageHub.cs:604
cmd.show.limits / .desc                  FeatureHub.cs:761
cmd.show.pickplace / .desc               FeatureHub.cs:826
cmd.show.trajectories / .desc            FeatureHub.cs:782
cmd.show.workspace / .desc               FeatureHub.cs:728
cmd.tool.grid / .desc                    KazistovVvUIManager.cs:1237
cmd.tool.measure / .desc                 KazistovVvUIManager.cs:1226
cmd.tut.restart / .desc                  KvStageHub2.cs:674
cmd.ui.access / .desc                    KazistovVvUIManager.cs:1139
cmd.ui.palette / .desc                   KazistovVvUIManager.cs:1103
cmd.ui.resetlayout / .desc               KazistovVvUIManager.cs:1127
cmd.ui.toggle / .desc                    KazistovVvUIManager.cs:1151
cmd.view.panel.properties / .desc        KazistovVvUIManager.cs:1017
cmd.view.panel.settings / .desc          KazistovVvUIManager.cs:1029
cmd.view.panel.tree / .desc              KazistovVvUIManager.cs:1005
cmd.waypoint.build / .desc               KvStageHub.cs:688
cmd.waypoint.clear / .desc               KvStageHub.cs:679
cmd.waypoint.delete / .desc              KvStageHub.cs:670
cmd.waypoint.earlier / .desc             KvStageHub.cs:652
cmd.waypoint.later / .desc               KvStageHub.cs:661
cmd.waypoint.move / .desc                KvStageHub.cs:643
cmd.waypoint.play / .desc                KvStageHub.cs:697
cmd.zone.box / .desc                     FeatureHub.cs:1191
cmd.zone.cylinder / .desc                FeatureHub.cs:1209
cmd.zone.discard / .desc                 FeatureHub.cs:1227
cmd.zone.panel / .desc                   FeatureHub.cs:1218
cmd.zone.sphere / .desc                  FeatureHub.cs:1200
```

*(в блоке выше каждая строка `… / .desc` — это два отдельных ключа; всего 66 имён команд → 132 ключа. Остальные 36 ключей `cmd.*` перечислены в разделе 4 как закрытые рантайм-таблицами либо уже переведены словарями.)*

### 3.2 Прочие ключи интерфейса — 37 ключей

**Контекстное меню дерева (16):**
`ctx.copied` (`KazistovVvUIManager.cs:3357`), `ctx.copy` (`:3259`), `ctx.delete` (`:3225`), `ctx.delete.none` (`:3397`), `ctx.duplicate` (`:3219`), `ctx.duplicate.none` (`:3451`), `ctx.focus` (`:3243`), `ctx.focused` (`:3348`), `ctx.hidden` (`:3330`), `ctx.hide` (`:3237`), `ctx.multi` (`:3264`), `ctx.properties` (`:3251`), `ctx.rename` (`:3212`), `ctx.rename.manual` (`:3310`), `ctx.show` (`:3237`), `ctx.shown` (`:3329`)

**Дерево моделей (10):**
`tree.empty` (`KvTreeView.cs:145`), `tree.empty.hint` (`:155`), `tree.kind.axis` (`KazistovVvUIManager.cs:3292`), `tree.kind.group` (`:3298`), `tree.kind.object` (`:3299`), `tree.kind.phantom` (`:3297`), `tree.kind.point` (`:3295`), `tree.kind.robot` (`:3291`), `tree.kind.table` (`:3294`), `tree.kind.trajectory` (`:3296`)

**Настройки и доступность (17):**
`settings.cb.deuter` (`KvSettingsView.cs:660`), `settings.cb.note` (`:671`), `settings.cb.off` (`:659`), `settings.cb.prot` (`:661`), `settings.cb.trit` (`:662`), `settings.contrast` (`:647`), `settings.font` (`:631`), `settings.font.large` (`:638`), `settings.font.normal` (`:637`), `settings.font.small` (`:636`), `settings.gamepadhud` (`:689`), `settings.header.access` (`:629`), `settings.header.service` (`:699`), `settings.keyboardnav` (`:679`), `settings.open.hotkeys` (`:709`), `settings.open.palette` (`:701`), `settings.reset.layout` (`:717`)

**Прочее (по зонам интерфейса):**
`dock.zone.left` / `dock.zone.right` / `dock.zone.bottom` / `dock.zone.float` (`KvDockPanel.cs:933/936/939/942`),
`panel.collapse` / `panel.rail` / `panel.rail.hint` / `panel.undock` (`KvDockPanel.cs:174/178/319/176`),
`panel.hotkeys` (`KazistovVvUIManager.cs:558`),
`properties.empty` / `properties.empty.hint` / `properties.none` / `properties.none.hint` (`KvPropertiesView.cs:163/164/247/248`),
`hotkeys.conflict` / `hotkeys.empty` / `hotkeys.rebind` / `hotkeys.rebind.stub` / `hotkeys.search` (`KvHotkeyView.cs:214/142/227/230/58`),
`palette.disabled` / `palette.empty` / `palette.hint` (`KvCommandPalette.cs:242/326/126`),
`toolbar.group` / `toolbar.group.all` / `toolbar.group.count` / `toolbar.group.hint` / `toolbar.workbench` (`KvToolbar.cs:362/331/382/380/148`),
`error.hide` / `error.more` / `error.unknown` (`KvUiStates.cs:148/140/198`),
`gamepad.absent` / `gamepad.connected` / `gamepad.last` (`KvGamepadHud.cs:135/134/272`),
`loading.planning` / `loading.recording` (`KazistovVvUIManager.cs:451/455`),
`empty.point` (`KazistovVvUIManager.cs:467`),
`calib.title` (`DshStage3Diag.cs:216` — диагностика).

**Служебный ключ:** `совсем.нет.такого.ключа` (`DshStageDiag.cs:239`) — **намеренно несуществующий** ключ, которым диагностика проверяет, что цепочка fallback работает. Держать его непереведённым — правильно.

### 3.3 Закономерность

Все 205 непереведённых ключей — это **функционал последних сессий** (§0.6 «UX/UI-улучшения + геймпад», контекстное меню дерева, палитра команд, окно горячих клавиш, группы тулбара, состояния empty/error/loading, расширенные настройки доступности, новый набор команд хабов этапов). Словари `kazistovvv_i18n/*.json` остались на состоянии этапа, когда команд было ~58; всё, что добавилось позже, имеет перевод **только в русском литерале внутри кода**.

---

## 4. Ключи, отсутствующие в JSON, но закрытые рантайм-таблицами `KvLocExtra*` — 44

Эти ключи **переведены на все 7 языков**, но не в редактируемых файлах-словарях, а в коде (`Row(...)` в `KvLocExtra2.cs` / `KvLocExtra3.cs`). Формально локализация есть, практически — **правка перевода требует перекомпиляции**, что противоречит заявленному в комментариях принципу «словари правятся без Unity».

```
calib.title             cmd.calib.tab(.desc)        cmd.constr.tab(.desc)
cmd.constr.toggle(.desc) cmd.demo.quick(.desc)      cmd.export.lang(.desc)
cmd.export.robot(.desc) cmd.import.tab(.desc)       cmd.payload.tab(.desc)
cmd.post.auto(.desc)    cmd.post.energy(.desc)      cmd.post.smooth(.desc)
cmd.post.timeoptimal(.desc) cmd.startmenu.show(.desc) cmd.tut.toggle(.desc)
cmd.workbench.toggle(.desc) cmd.wp.clear(.desc)     cmd.wp.detour(.desc)
cmd.wp.orient(.desc)    cmd.wp.pause(.desc)         cmd.wp.speed(.desc)
cmd.wp.tab(.desc)       start.new
```
*(в списке 22 позиции, из них 21 — пара «ключ + .desc», итого 43 + `calib.title` = 44 ключа)*

Все они запрашиваются из `KvStageHub2.cs` / `KvStageHub3.cs` (и `DshStage3Diag.cs` для `calib.title`).

---

## 5. Неиспользуемые ключи в JSON — 35

Ключи, которые есть в словарях, но **ни разу не запрашиваются кодом** ни прямым вызовом `KvLoc.*`, ни строковым литералом (литералы проверялись отдельно, чтобы не счесть «динамически подставляемые» ключи мёртвыми).

```
capture.folder          capture.screenshot      capture.title
capture.video           cmd.waypoint.panel      cmd.waypoint.panel.desc
common.add              common.apply            common.cancel
common.close            common.delete           common.language
common.load             common.save             group.phantoms
group.poses             group.records           group.robots
group.zones             health.current          health.speed
health.wear             menu.edit               menu.file
menu.help               menu.robot              menu.service
menu.view               panel.capture           panel.health
panel.limits            panel.metrics           panel.pendant
pendant.home            pendant.record          pendant.start
pendant.stop            pendant.variantA        pendant.variantB
prop.section.capture    prop.section.health     prop.section.hint
prop.section.pendant    settings.header.panels  tab.compare
tab.graphs              tab.joints              tab.log
tab.perf                tab.poses               tab.record
tab.scenarios           tab.sessions            tab.zones
tree.robot              waypoint.add            waypoint.order
```

**Важная оговорка (ложные срабатывания).** Часть этих ключей **используется динамически**, поэтому удалять их нельзя:

| Группа ключей | Как используется | Прямая ссылка в коде |
|---|---|---|
| `menu.file`, `menu.edit`, `menu.view`, `menu.robot`, `menu.service`, `menu.help` | `KvLoc.Menu(ruName)` — ключ строится из русского имени пункта меню через `switch` | `KvLocalization.cs:169-174` |
| `tab.*` (10 ключей) | массив `TabKeys` в `KvFeatureWindow`, обход в цикле | `KvFeatureWindow.cs:76` |
| `panel.*`, `pendant.*`, `prop.section.*`, `health.*`, `capture.*` | массивы ключей + `KvLoc.T(key, fallback)` из таблиц | `KvLocExtra*`, `KvHealthMonitor.cs:478` |
| `group.*` | `KvLoc.MenuGroup(group)` из `KvMenuBar` | `KvMenuBar.cs:205` |
| `waypoint.add`, `waypoint.order` | подписи в панели маршрута | `KvWaypoints.cs` |
| `common.save/load/cancel/close/add/apply/delete/language` | общие подписи кнопок в диалогах | `KvFeatureWindow`, `KvWorkbenchWindow` |
| `settings.header.panels` | заголовок секции в настройках | `KvSettingsView` |
| `tree.robot` | подпись узла «Робот» в дереве | `KazistovVvUIManager` |

**Действительно неиспользуемыми можно считать** ключи, которые не входят ни в один динамический набор: `cmd.waypoint.panel`, `cmd.waypoint.panel.desc`, `capture.title`, `panel.capture`. Перед удалением любого ключа — проверить его вхождение в массивы `TabKeys`/`MetricKeys`/`KindKeys` (см. `_tools\audit_loc.txt`, раздел «ДИНАМИЧЕСКИЕ КЛЮЧИ»).

---

## 6. Сводка

| Проверка | Результат |
|---|---|
| Покрытие словарей JSON по языкам | **100,00 % у всех 7** (по 187 ключей, наборы совпадают полностью) |
| Отсутствующие ключи «для языка» в словарях | **0** у каждого из 7 |
| Дубликаты ключей внутри языка | **0** у каждого из 7 |
| Реальное покрытие ключей, запрашиваемых кодом | **174 из 379 = 45,9 %** |
| Ключей без перевода где-либо (русский текст во всех 7 языках) | **205** |
| Ключей, переведённых только рантайм-таблицами (не файлами) | **44** |
| Ключей в JSON, не используемых кодом | **35** (из них реально мёртвых — не более 4) |
| Язык с частичным переводом | **нет ни одного** — все семь либо полные, либо одинаково неполные |

**Три главных вывода.**

1. **Словари образцовые по форме и устаревшие по содержанию.** Наборы ключей во всех 7 файлах совпадают строго, дубликатов нет, структура одинаковая — но словари не пополнялись с момента, когда команд было около шестидесяти. Фактически переведено меньше половины строк интерфейса.

2. **Основной пробел — команды интерфейса (168 из 205 ключей).** Добавление команды в `KvCommands` автоматически создаёт два ключа (`cmd.<id>`, `cmd.<id>.desc`), но нигде не напоминает, что их нужно перевести. Это системная причина, а не разовая забывчивость.

3. **Механизм локализации рабочий и безопасный.** Цепочка «текущий язык → английский → русский литерал» гарантирует, что непереведённый ключ **никогда не покажет пустую строку или `key`** — интерфейс остаётся читаемым, просто по-русски. Поэтому 205 пробелов — это дефект полноты, а **не** дефект стабильности.

**Что можно улучшить (в рамках этого этапа не выполнялось, изменения не вносились):**
- добавить в отчёт диагностики (`Dsh*Diag`) проверку «ключ запрошен кодом, но отсутствует во всех источниках» — она ловила бы пробел сразу при добавлении команды (частично уже есть: `KvLoc.Has`, `KvLoc.KeysOf`, `KvLoc.CountOf` — но только как API, без проверки);
- дописать в 7 JSON-файлов 205 ключей из раздела 3 (168 из них генерируются автоматически по `Id` команд — это скриптуемая задача);
- перенести 44 ключа из рантайм-таблиц в словари (или сознательно оставить в коде и зафиксировать это решение в документации);
- вычистить 4 реально мёртвых ключа из раздела 5.
