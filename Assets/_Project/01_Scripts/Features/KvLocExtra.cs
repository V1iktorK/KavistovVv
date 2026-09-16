using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЛОКАЛИЗАЦИЯ НОВЫХ ФУНКЦИЙ (этапы 1–6 этой сессии: стартовое меню, туториал,
    /// демонстрация, сглаживание, время-оптимальные и эко-траектории).
    ///
    /// ПОЧЕМУ СТРОКИ ЗДЕСЬ, А НЕ В ФАЙЛАХ-СЛОВАРЯХ: словари в
    /// `StreamingAssets/kazistovvv_i18n/*.json` — это данные интерфейса, которые правят
    /// без Unity; новые модули функций добавляют свои строки ВО ВРЕМЯ РАБОТЫ через
    /// `KvLoc.AddRuntimeStrings`, поэтому:
    ///   * не нужно синхронно править 7 JSON-файлов при каждой новой кнопке;
    ///   * строки живут рядом с кодом, который их использует (их видно в одном диффе);
    ///   * цепочка fallback остаётся штатной: текущий язык → английский → русский текст
    ///     из кода, поэтому неполный перевод НИЧЕГО не ломает;
    ///   * внешние словари по-прежнему главнее: если ключ есть в JSON, он перекроет
    ///     значение отсюда (порядок загрузки: файлы читаются после регистрации встроенных,
    ///     а этот модуль ставится ещё позже и пишет только ОТСУТСТВУЮЩИЕ ключи).
    ///
    /// Формат строки таблицы: { ключ, ru, en, zh, es, de, fr, ja } — семь языков ТЗ.
    /// </summary>
    public static class KvLocExtra
    {
        /// <summary>Порядок языков в таблице (совпадает с кодами каталогов).</summary>
        public static readonly string[] Codes = { "ru", "en", "zh", "es", "de", "fr", "ja" };

        private static bool installed;
        private static int registered;

        /// <summary>Сколько строк зарегистрировано модулем (диагностика).</summary>
        public static int RegisteredCount { get { return registered; } }

        /// <summary>Перевод по ключу (fallback — русский текст из кода).</summary>
        public static string T(string key, string fallback)
        {
            return KvLoc.T(key, fallback);
        }

        /// <summary>Перевод с подстановкой: {0}, {1} — как в string.Format.</summary>
        public static string F(string key, string fallback, params object[] args)
        {
            string pattern = KvLoc.T(key, fallback);
            try { return string.Format(pattern, args); }
            catch { return pattern; }
        }

        /// <summary>Зарегистрировать строки модуля (вызывается хабом этапов один раз).</summary>
        public static void Install()
        {
            if (installed) return;
            installed = true;

            int count = 0;
            int skipped = 0;
            for (int r = 0; r < Rows.Length; r++)
            {
                string[] row = Rows[r];
                if (row == null || row.Length < 1 + Codes.Length) continue;
                string key = row[0];
                if (string.IsNullOrEmpty(key)) continue;

                // Ключ уже есть в словаре (JSON с диска или другой модуль) — не перекрываем:
                // словарь интерфейса остаётся ГЛАВНЫМ источником текста.
                if (KvLoc.Has(key)) { skipped++; continue; }

                bool wrote = false;
                for (int c = 0; c < Codes.Length; c++)
                {
                    string text = row[c + 1];
                    if (string.IsNullOrEmpty(text)) continue;
                    KvLoc.AddRuntimeStrings(Codes[c], key, text);
                    wrote = true;
                }
                if (wrote) count++;
            }
            registered = count;
            UnityEngine.Debug.Log("[Loc] строки новых модулей зарегистрированы: " + count +
                                  " (уже были в словарях: " + skipped + ")" +
                                  " · всего в словарях: " + KvLoc.TotalStrings);
        }

        // ================================================================== таблица строк

        private static readonly string[][] Rows =
        {
            // ---------------------------------------------------------- ЭТАП 1: главное меню
            Row("start.title",
                "KazistovVv", "KazistovVv", "KazistovVv", "KazistovVv", "KazistovVv", "KazistovVv", "KazistovVv"),
            Row("start.subtitle",
                "Платформа управления роботами · VR / MR / ПК",
                "Robot control platform · VR / MR / PC",
                "机器人控制平台 · VR / MR / PC",
                "Plataforma de control de robots · VR / MR / PC",
                "Robotersteuerungsplattform · VR / MR / PC",
                "Plateforme de commande de robots · VR / MR / PC",
                "ロボット制御プラットフォーム · VR / MR / PC"),
            Row("start.new",
                "Новый проект", "New project", "新建项目", "Nuevo proyecto",
                "Neues Projekt", "Nouveau projet", "新規プロジェクト"),
            Row("start.new.desc",
                "Очистить рабочую область: точка, траектории, зоны запрета и маршрут",
                "Clear the workspace: point, trajectories, keep-out zones and route",
                "清空工作区：点、轨迹、禁入区与路线",
                "Limpiar el espacio de trabajo: punto, trayectorias, zonas prohibidas y ruta",
                "Arbeitsbereich leeren: Punkt, Bahnen, Sperrzonen und Route",
                "Vider l'espace de travail : point, trajectoires, zones interdites et itinéraire",
                "ワークスペースを初期化：点・軌道・禁止ゾーン・ルート"),
            Row("start.open",
                "Открыть сессию", "Open session", "打开会话", "Abrir sesión",
                "Sitzung öffnen", "Ouvrir une session", "セッションを開く"),
            Row("start.open.desc",
                "Загрузить последнюю сохранённую сессию: поза робота, точка, зоны, позы",
                "Load the last saved session: robot pose, point, zones, presets",
                "加载上次保存的会话：机器人姿态、点、区域、姿势",
                "Cargar la última sesión guardada: pose, punto, zonas y preajustes",
                "Letzte gespeicherte Sitzung laden: Pose, Punkt, Zonen, Voreinstellungen",
                "Charger la dernière session : pose, point, zones, préréglages",
                "最後に保存したセッションを読み込む：姿勢・点・ゾーン・プリセット"),
            Row("start.demo",
                "Показать демо", "Show demo", "播放演示", "Mostrar demo",
                "Demo zeigen", "Afficher la démo", "デモを表示"),
            Row("start.demo.desc",
                "Автоматический сценарий: точка → 8 траекторий → фантомы → движение робота",
                "Automatic scenario: point → 8 trajectories → phantoms → robot motion",
                "自动演示：点 → 8 条轨迹 → 幻影 → 机器人运动",
                "Escenario automático: punto → 8 trayectorias → fantasmas → movimiento del robot",
                "Automatisches Szenario: Punkt → 8 Bahnen → Phantome → Roboterbewegung",
                "Scénario automatique : point → 8 trajectoires → fantômes → mouvement du robot",
                "自動シナリオ：点 → 8 軌道 → ファントム → ロボット動作"),
            Row("start.tutorial",
                "Обучение", "Tutorial", "教程", "Tutorial", "Tutorial", "Tutoriel", "チュートリアル"),
            Row("start.tutorial.desc",
                "Пошаговое обучение: выбор точки, подтверждение траектории, перемещение точки, фантом",
                "Step-by-step guide: pick a point, confirm a trajectory, move the point, phantom",
                "分步教学：选点、确认轨迹、移动点、幻影",
                "Guía paso a paso: elegir punto, confirmar trayectoria, mover el punto, fantasma",
                "Schritt-für-Schritt: Punkt wählen, Bahn bestätigen, Punkt verschieben, Phantom",
                "Guide pas à pas : choisir un point, confirmer une trajectoire, déplacer le point, fantôme",
                "ステップ別ガイド：点の選択・軌道の確定・点の移動・ファントム"),
            Row("start.settings",
                "Настройки", "Settings", "设置", "Ajustes", "Einstellungen", "Paramètres", "設定"),
            Row("start.settings.desc",
                "Функции, управление, интерфейс, язык интерфейса",
                "Features, controls, interface, UI language",
                "功能、控制、界面、界面语言",
                "Funciones, controles, interfaz, idioma",
                "Funktionen, Steuerung, Oberfläche, Sprache",
                "Fonctions, commandes, interface, langue",
                "機能・操作・インターフェース・表示言語"),
            Row("start.exit",
                "Выход", "Exit", "退出", "Salir", "Beenden", "Quitter", "終了"),
            Row("start.exit.desc",
                "Закрыть приложение (в редакторе Unity — остановить Play)",
                "Close the application (in the Unity editor — stop Play mode)",
                "关闭程序（在 Unity 编辑器中为停止播放）",
                "Cerrar la aplicación (en el editor de Unity: detener Play)",
                "Anwendung schließen (im Unity-Editor: Play beenden)",
                "Fermer l'application (dans l'éditeur Unity : arrêter Play)",
                "アプリを終了（Unity エディターでは Play 停止）"),
            Row("start.hint",
                "Наведите курсор на кнопку — подсказка появится в этой строке",
                "Hover a button — its tooltip appears in this line",
                "将光标移到按钮上，提示将显示在此处",
                "Pase el cursor por un botón: la ayuda aparece en esta línea",
                "Mit der Maus über eine Schaltfläche: Hinweis erscheint hier",
                "Survolez un bouton : l'infobulle apparaît sur cette ligne",
                "ボタンにカーソルを合わせると、ここに説明が表示されます"),
            Row("start.cinematic",
                "Кинематографический облёт стенда · облёт идёт фоном",
                "Cinematic fly-around of the workcell · running in the background",
                "工作单元的影视化环绕 · 作为背景播放",
                "Órbita cinematográfica de la celda · en segundo plano",
                "Kinematografischer Rundflug der Zelle · läuft im Hintergrund",
                "Survol cinématographique de la cellule · en arrière-plan",
                "セルのシネマティック周回 · 背景で再生中"),
            Row("start.esc",
                "Esc — войти в рабочую область без изменений",
                "Esc — enter the workspace without changes",
                "Esc — 直接进入工作区（不做更改）",
                "Esc — entrar al área de trabajo sin cambios",
                "Esc — Arbeitsbereich ohne Änderungen öffnen",
                "Esc — entrer dans l'espace de travail sans modification",
                "Esc — 変更せずワークスペースへ"),
            Row("start.booted",
                "стартовое меню показано при запуске",
                "start menu shown on launch",
                "启动时显示开始菜单",
                "menú inicial mostrado al arrancar",
                "Startmenü beim Start angezeigt",
                "menu de démarrage affiché au lancement",
                "起動時にスタートメニューを表示"),
            Row("start.closed",
                "стартовое меню закрыто — рабочая область",
                "start menu closed — workspace",
                "开始菜单已关闭 — 工作区",
                "menú inicial cerrado — área de trabajo",
                "Startmenü geschlossen — Arbeitsbereich",
                "menu de démarrage fermé — espace de travail",
                "スタートメニューを閉じました — ワークスペース"),

            // ---------------------------------------------------------- ЭТАП 2: туториал
            Row("tut.title",
                "Обучение", "Tutorial", "教程", "Tutorial", "Tutorial", "Tutoriel", "チュートリアル"),
            Row("tut.step",
                "Шаг {0} из {1}", "Step {0} of {1}", "第 {0} 步，共 {1} 步",
                "Paso {0} de {1}", "Schritt {0} von {1}", "Étape {0} sur {1}", "ステップ {0} / {1}"),
            Row("tut.next",
                "Далее", "Next", "下一步", "Siguiente", "Weiter", "Suivant", "次へ"),
            Row("tut.skip",
                "Пропустить туториал", "Skip tutorial", "跳过教程", "Omitir tutorial",
                "Tutorial überspringen", "Passer le tutoriel", "チュートリアルをスキップ"),
            Row("tut.resume",
                "Продолжить обучение", "Resume tutorial", "继续教程", "Continuar tutorial",
                "Tutorial fortsetzen", "Reprendre le tutoriel", "チュートリアルを再開"),
            Row("tut.restart",
                "Начать заново", "Start over", "重新开始", "Empezar de nuevo",
                "Neu starten", "Recommencer", "最初からやり直す"),
            Row("tut.done",
                "Обучение пройдено — все шаги выполнены",
                "Tutorial completed — all steps done",
                "教程已完成 — 所有步骤均已完成",
                "Tutorial completado — todos los pasos hechos",
                "Tutorial abgeschlossen — alle Schritte erledigt",
                "Tutoriel terminé — toutes les étapes faites",
                "チュートリアル完了 — すべてのステップを実行しました"),
            Row("tut.waiting",
                "Выполните действие в сцене — шаг завершится сам",
                "Do the action in the scene — the step completes by itself",
                "在场景中执行操作 — 该步骤会自动完成",
                "Realice la acción en la escena: el paso se completa solo",
                "Führen Sie die Aktion in der Szene aus — der Schritt endet automatisch",
                "Effectuez l'action dans la scène — l'étape se termine toute seule",
                "シーンで操作すると、このステップは自動で完了します"),
            Row("tut.manual",
                "Отметить выполненным", "Mark as done", "标记为完成", "Marcar como hecho",
                "Als erledigt markieren", "Marquer comme fait", "完了にする"),
            Row("tut.progress",
                "Прогресс сохраняется автоматически — обучение можно продолжить позже",
                "Progress is saved automatically — you can continue later",
                "进度会自动保存 — 之后可以继续",
                "El progreso se guarda automáticamente: puede continuar después",
                "Fortschritt wird automatisch gespeichert — später fortsetzbar",
                "La progression est enregistrée automatiquement — reprise possible plus tard",
                "進捗は自動保存されます — 後で続きから再開できます"),
            Row("tut.highlight",
                "Подсвечен нужный элемент интерфейса",
                "The required UI element is highlighted",
                "已高亮所需的界面元素",
                "El elemento de interfaz requerido está resaltado",
                "Das benötigte Bedienelement ist hervorgehoben",
                "L'élément d'interface requis est mis en surbrillance",
                "必要な UI 要素をハイライトしています"),
            Row("tut.step1.title",
                "Как выбрать точку (красный лазер)",
                "How to pick a point (red laser)",
                "如何选点（红色激光）",
                "Cómo elegir un punto (láser rojo)",
                "Punkt wählen (roter Laser)",
                "Choisir un point (laser rouge)",
                "点の選び方（赤色レーザー）"),
            Row("tut.step1.text",
                "Нажмите Z — включится красный лазер. Наведите его на стол или объект и нажмите ЛКМ: " +
                "точка зафиксируется, и планировщик начнёт считать варианты траектории.",
                "Press Z to enable the red laser. Aim it at the table or an object and click LMB: " +
                "the point is locked and the planner starts computing trajectory variants.",
                "按 Z 打开红色激光。将其对准桌面或物体并单击左键：点被固定，规划器开始计算轨迹方案。",
                "Pulse Z para activar el láser rojo. Apúntelo a la mesa o a un objeto y haga clic: " +
                "el punto se fija y el planificador calcula las variantes de trayectoria.",
                "Z drücken — der rote Laser ist aktiv. Auf Tisch oder Objekt zielen und LMB klicken: " +
                "der Punkt wird fixiert, der Planer berechnet Bahnvarianten.",
                "Appuyez sur Z pour activer le laser rouge. Visez la table ou un objet puis cliquez : " +
                "le point est fixé et le planificateur calcule les variantes.",
                "Z を押すと赤色レーザーが有効になります。テーブルや物体に合わせて左クリックすると、" +
                "点が固定され、プランナが軌道候補を計算します。"),
            Row("tut.step2.title",
                "Как подтвердить траекторию (зелёный лазер)",
                "How to confirm a trajectory (green laser)",
                "如何确认轨迹（绿色激光）",
                "Cómo confirmar una trayectoria (láser verde)",
                "Bahn bestätigen (grüner Laser)",
                "Confirmer une trajectoire (laser vert)",
                "軌道の確定方法（緑色レーザー）"),
            Row("tut.step2.text",
                "Восемь «колбасок» — это восемь вариантов. Нажмите X — включится зелёный лазер; " +
                "наведите его на «колбаску» и нажмите ЛКМ: вариант подтверждён, по нему пойдёт фантом.",
                "The eight tubes are eight variants. Press X to enable the green laser, aim at a tube " +
                "and click LMB: the variant is confirmed and a phantom moves along it.",
                "八条“管道”就是八个方案。按 X 打开绿色激光，对准某条管道并左键单击：方案确认，幻影沿其运动。",
                "Los ocho tubos son ocho variantes. Pulse X para el láser verde, apunte a un tubo y haga " +
                "clic: la variante se confirma y un fantasma recorre la trayectoria.",
                "Die acht Röhren sind acht Varianten. X drücken (grüner Laser), auf eine Röhre zielen und " +
                "LMB klicken: Variante bestätigt, ein Phantom fährt entlang.",
                "Les huit tubes sont huit variantes. Appuyez sur X (laser vert), visez un tube et cliquez : " +
                "la variante est confirmée, un fantôme la parcourt.",
                "8 本のチューブが 8 つの候補です。X で緑色レーザーを有効にし、チューブに合わせて左クリックすると" +
                "候補が確定し、ファントムがその軌道を進みます。"),
            Row("tut.step3.title",
                "Как двигать точку (режим перемещения)",
                "How to move the point (move mode)",
                "如何移动点（移动模式）",
                "Cómo mover el punto (modo de movimiento)",
                "Punkt verschieben (Verschiebemodus)",
                "Déplacer le point (mode déplacement)",
                "点の移動方法（移動モード）"),
            Row("tut.step3.text",
                "Нажмите Enter — включится режим перемещения точки. QWEASD двигают точку в мире, " +
                "Shift — быстрее, Enter — зафиксировать новую точку, Esc — отменить и вернуть прежнюю.",
                "Press Enter to enter point-move mode. QWEASD move the point in the world, Shift makes it " +
                "faster, Enter locks the new point, Esc cancels and restores the previous one.",
                "按 Enter 进入点移动模式。QWEASD 在世界中移动该点，Shift 加速，Enter 固定新点，Esc 取消并恢复原点。",
                "Pulse Enter para el modo de movimiento. QWEASD mueven el punto, Shift acelera, Enter fija " +
                "el nuevo punto, Esc cancela y restaura el anterior.",
                "Enter startet den Verschiebemodus. QWEASD bewegen den Punkt, Shift beschleunigt, Enter " +
                "fixiert den neuen Punkt, Esc bricht ab und stellt den alten wieder her.",
                "Entrée active le mode déplacement. QWEASD déplacent le point, Shift accélère, Entrée fixe " +
                "le nouveau point, Échap annule et restaure l'ancien.",
                "Enter で移動モードに入ります。QWEASD で点を移動、Shift で高速化、Enter で新しい点を確定、" +
                "Esc でキャンセルして元に戻します。"),
            Row("tut.step4.title",
                "Как выбрать фантом",
                "How to choose a phantom",
                "如何选择幻影",
                "Cómo elegir un fantasma",
                "Phantom wählen",
                "Choisir un fantôme",
                "ファントムの選び方"),
            Row("tut.step4.text",
                "После подтверждения по траектории едет фантом — копия робота. Если вариант не подходит, " +
                "наведите зелёный лазер на другую «колбаску» и щёлкните ЛКМ: выбор переключится, " +
                "не запуская реального робота.",
                "After confirmation a phantom — a robot copy — travels the trajectory. If the variant does " +
                "not fit, aim the green laser at another tube and click LMB: the selection switches without " +
                "starting the real robot.",
                "确认后，机器人副本（幻影）会沿轨迹运动。如果方案不合适，用绿色激光对准另一条管道并左键单击：" +
                "选择会切换，而不会启动真实机器人。",
                "Tras confirmar, un fantasma (copia del robot) recorre la trayectoria. Si la variante no " +
                "sirve, apunte el láser verde a otro tubo y haga clic: la selección cambia sin mover el robot.",
                "Nach der Bestätigung fährt ein Phantom (Roboter-Kopie) die Bahn. Passt die Variante nicht, " +
                "mit dem grünen Laser auf eine andere Röhre zielen und LMB klicken: die Auswahl wechselt, " +
                "ohne den echten Roboter zu starten.",
                "Après confirmation, un fantôme (copie du robot) parcourt la trajectoire. Si la variante ne " +
                "convient pas, visez un autre tube au laser vert et cliquez : la sélection change sans " +
                "démarrer le robot réel.",
                "確定後はファントム（ロボットのコピー）が軌道を移動します。候補が合わない場合は緑色レーザーを" +
                "別のチューブに合わせて左クリックすると、実機を動かさずに選択を切り替えられます。"),

            // ---------------------------------------------------------- ЭТАП 3: демонстрация
            Row("demo.title",
                "Демонстрация работы", "Live demonstration", "运行演示", "Demostración",
                "Live-Demonstration", "Démonstration", "デモンストレーション"),
            Row("demo.running",
                "Демонстрация идёт — наблюдать можно, ничего нажимать не нужно",
                "Demo is running — just watch, no input required",
                "演示进行中 — 只需观看，无需操作",
                "La demostración está en marcha: solo observe",
                "Die Demo läuft — einfach zusehen",
                "Démonstration en cours — observez simplement",
                "デモ実行中 — 見るだけでOKです"),
            Row("demo.stop",
                "Остановить демонстрацию", "Stop demo", "停止演示", "Detener demostración",
                "Demo stoppen", "Arrêter la démo", "デモを停止"),
            Row("demo.offer",
                "Демонстрация завершена. Пройти туториал, чтобы делать это самому?",
                "Demo finished. Take the tutorial to do it yourself?",
                "演示结束。是否通过教程学会自己操作？",
                "Demostración finalizada. ¿Quiere hacer el tutorial para hacerlo usted mismo?",
                "Demo beendet. Tutorial starten, um es selbst zu machen?",
                "Démonstration terminée. Suivre le tutoriel pour le faire vous-même ?",
                "デモ終了。自分で操作するためにチュートリアルを受けますか？"),
            Row("demo.yes",
                "Пройти туториал", "Take the tutorial", "开始教程", "Hacer el tutorial",
                "Tutorial starten", "Suivre le tutoriel", "チュートリアルを開始"),
            Row("demo.later",
                "Позже", "Later", "稍后", "Más tarde", "Später", "Plus tard", "後で"),
            Row("demo.line1",
                "1 · Ставлю точку над столешницей",
                "1 · Placing a point above the table",
                "1 · 在桌面上方设置一个点",
                "1 · Coloco un punto sobre la mesa",
                "1 · Punkt über dem Tisch setzen",
                "1 · Pose d'un point au-dessus de la table",
                "1 · テーブル上に点を設定"),
            Row("demo.line2",
                "2 · Планировщик BiRRT считает 8 вариантов траектории",
                "2 · The BiRRT planner computes 8 trajectory variants",
                "2 · BiRRT 规划器计算 8 条轨迹方案",
                "2 · El planificador BiRRT calcula 8 variantes",
                "2 · Der BiRRT-Planer berechnet 8 Varianten",
                "2 · Le planificateur BiRRT calcule 8 variantes",
                "2 · BiRRT プランナが 8 つの軌道候補を計算"),
            Row("demo.line3",
                "3 · По каждой траектории идёт свой фантом — копия робота",
                "3 · A phantom (robot copy) travels each trajectory",
                "3 · 每条轨迹上都有一个幻影（机器人副本）",
                "3 · Un fantasma recorre cada trayectoria",
                "3 · Je ein Phantom fährt jede Bahn",
                "3 · Un fantôme parcourt chaque trajectoire",
                "3 · 各軌道をファントムが移動"),
            Row("demo.line4",
                "4 · Метрики: время, длина, кривизна, зазор и запас до лимитов",
                "4 · Metrics: time, length, curvature, clearance and limit margin",
                "4 · 指标：时间、长度、曲率、间隙与限位余量",
                "4 · Métricas: tiempo, longitud, curvatura, holgura y margen",
                "4 · Metriken: Zeit, Länge, Krümmung, Abstand und Grenzreserve",
                "4 · Métriques : temps, longueur, courbure, dégagement, marge",
                "4 · 指標：時間・長さ・曲率・クリアランス・限界余裕"),
            Row("demo.line5",
                "5 · Выбираю лучший вариант — робот едет по траектории",
                "5 · Selecting the best variant — the robot moves along it",
                "5 · 选择最佳方案 — 机器人沿轨迹运动",
                "5 · Selecciono la mejor variante: el robot se mueve",
                "5 · Beste Variante gewählt — der Roboter fährt",
                "5 · Je choisis la meilleure variante — le robot se déplace",
                "5 · 最良の候補を選択 — ロボットが移動"),
            Row("demo.line6",
                "6 · Готово: точка, траектории и движение показаны полностью",
                "6 · Done: point, trajectories and motion fully demonstrated",
                "6 · 完成：点、轨迹与运动已完整演示",
                "6 · Listo: punto, trayectorias y movimiento demostrados",
                "6 · Fertig: Punkt, Bahnen und Bewegung vollständig gezeigt",
                "6 · Terminé : point, trajectoires et mouvement démontrés",
                "6 · 完了：点・軌道・動作をすべて実演"),
            Row("demo.needrobot",
                "Робот не определён: наведите красный лазер (Z) на стенд с роботом",
                "No robot bound: aim the red laser (Z) at a robot stand",
                "未绑定机器人：请用红色激光（Z）对准机器人工作台",
                "Sin robot: apunte el láser rojo (Z) al puesto del robot",
                "Kein Roboter gebunden: roten Laser (Z) auf den Roboterstand richten",
                "Aucun robot : visez le poste du robot au laser rouge (Z)",
                "ロボット未接続：赤色レーザー（Z）をロボットスタンドに合わせてください"),

            // ---------------------------------------------------------- ЭТАП 4: сглаживание
            Row("smooth.title",
                "Сглаживание траектории", "Trajectory smoothing", "轨迹平滑",
                "Suavizado de trayectoria", "Bahn-Glättung", "Lissage de trajectoire", "軌道のスムージング"),
            Row("smooth.level",
                "Уровень сглаживания, %", "Smoothing level, %", "平滑程度，%",
                "Nivel de suavizado, %", "Glättungsgrad, %", "Niveau de lissage, %", "平滑レベル %"),
            Row("smooth.method",
                "Метод", "Method", "方法", "Método", "Verfahren", "Méthode", "手法"),
            Row("smooth.method.bspline",
                "B-сплайн", "B-spline", "B 样条", "B-spline", "B-Spline", "B-spline", "B スプライン"),
            Row("smooth.method.bezier",
                "Безье", "Bézier", "贝塞尔", "Bézier", "Bézier", "Bézier", "ベジェ"),
            Row("smooth.method.gauss",
                "Фильтр Гаусса", "Gaussian filter", "高斯滤波", "Filtro gaussiano",
                "Gauß-Filter", "Filtre gaussien", "ガウスフィルタ"),
            Row("smooth.auto",
                "Применять автоматически после планирования",
                "Apply automatically after planning",
                "规划后自动应用",
                "Aplicar automáticamente tras planificar",
                "Nach der Planung automatisch anwenden",
                "Appliquer automatiquement après planification",
                "計画後に自動適用"),
            Row("smooth.apply",
                "Сгладить выбранную", "Smooth selected", "平滑所选轨迹", "Suavizar la seleccionada",
                "Ausgewählte glätten", "Lisser la sélection", "選択した軌道を平滑化"),
            Row("smooth.reset",
                "Вернуть исходную", "Restore original", "恢复原始轨迹", "Restaurar original",
                "Original wiederherstellen", "Restaurer l'original", "元に戻す"),
            Row("smooth.before", "До", "Before", "之前", "Antes", "Vorher", "Avant", "前"),
            Row("smooth.after", "После", "After", "之后", "Después", "Nachher", "Après", "後"),
            Row("smooth.planner",
                "План планировщика (как построен)", "Planner output (as built)",
                "规划器输出（原始）", "Salida del planificador (original)",
                "Planer-Ausgabe (unbearbeitet)", "Sortie du planificateur (brute)",
                "プランナ出力（そのまま）"),
            Row("smooth.jerk",
                "Jerk (рывок), °/с³", "Jerk, °/s³", "加加速度 °/s³", "Jerk, °/s³",
                "Ruck, °/s³", "Jerk, °/s³", "ジャーク °/s³"),
            Row("smooth.accel",
                "Ускорение, °/с²", "Acceleration, °/s²", "加速度 °/s²", "Aceleración, °/s²",
                "Beschleunigung, °/s²", "Accélération, °/s²", "加速度 °/s²"),
            Row("smooth.curvature",
                "Кривизна, 1/м", "Curvature, 1/m", "曲率 1/m", "Curvatura, 1/m",
                "Krümmung, 1/m", "Courbure, 1/m", "曲率 1/m"),
            Row("smooth.length",
                "Длина пути TCP, м", "TCP path length, m", "TCP 路径长度 m", "Longitud TCP, m",
                "TCP-Weglänge, m", "Longueur TCP, m", "TCP 経路長 m"),
            Row("smooth.time",
                "Время, с", "Time, s", "时间 s", "Tiempo, s", "Zeit, s", "Temps, s", "時間 s"),
            Row("smooth.noplan",
                "Нет траектории: выберите точку (Z + ЛКМ) и подтвердите вариант (X + ЛКМ)",
                "No trajectory: pick a point (Z + LMB) and confirm a variant (X + LMB)",
                "无轨迹：请选点（Z + 左键）并确认方案（X + 左键）",
                "Sin trayectoria: elija un punto (Z + clic) y confirme una variante (X + clic)",
                "Keine Bahn: Punkt wählen (Z + LMB) und Variante bestätigen (X + LMB)",
                "Aucune trajectoire : choisissez un point (Z + clic) et confirmez une variante (X + clic)",
                "軌道がありません：点を選択（Z + 左クリック）し候補を確定（X + 左クリック）してください"),
            Row("smooth.pickone",
                "Выберите вариант в списке справа (индекс 1…8)",
                "Select a variant in the list (index 1…8)",
                "请在列表中选择方案（序号 1…8）",
                "Seleccione una variante (índice 1…8)",
                "Variante wählen (Index 1…8)",
                "Sélectionnez une variante (indice 1…8)",
                "候補を選択してください（番号 1…8）"),
            Row("smooth.done",
                "сглажено", "smoothed", "已平滑", "suavizada", "geglättet", "lissée", "平滑化済み"),
            Row("smooth.auto.on",
                "Автоматическое сглаживание включено: новая точка — сразу сглаженные варианты",
                "Auto smoothing on: new points yield smoothed variants immediately",
                "已开启自动平滑：新点直接生成平滑后的方案",
                "Suavizado automático activo: los puntos nuevos ya salen suavizados",
                "Auto-Glättung aktiv: neue Punkte ergeben sofort geglättete Varianten",
                "Lissage auto actif : les nouveaux points donnent des variantes lissées",
                "自動平滑が有効：新しい点は平滑化された候補になります"),
            Row("smooth.info",
                "Постобработка после планирования: путь сглаживается в пространстве суставов " +
                "(B-сплайн / Безье / фильтр Гаусса), затем время пересчитывается по лимитам " +
                "скорости и ускорения. Начало и конец траектории не смещаются.",
                "Post-processing after planning: the joint-space path is smoothed (B-spline / Bézier / " +
                "Gaussian), then retimed by velocity and acceleration limits. Start and end points stay put.",
                "规划后处理：在关节空间平滑路径（B 样条 / 贝塞尔 / 高斯），再按速度与加速度限制重新计时。" +
                "轨迹的起点与终点不变。",
                "Posproceso tras planificar: la ruta se suaviza en el espacio de articulaciones " +
                "(B-spline / Bézier / Gauss) y luego se recalcula el tiempo según los límites. " +
                "El inicio y el final no se mueven.",
                "Nachbearbeitung nach der Planung: der Gelenkraum-Pfad wird geglättet (B-Spline / Bézier / " +
                "Gauß), danach wird die Zeit nach Geschwindigkeits- und Beschleunigungsgrenzen neu berechnet. " +
                "Start und Ende bleiben erhalten.",
                "Post-traitement après planification : le chemin est lissé dans l'espace articulaire " +
                "(B-spline / Bézier / Gauss), puis recalculé selon les limites de vitesse et d'accélération. " +
                "Le début et la fin ne bougent pas.",
                "計画後の後処理：関節空間で経路を平滑化（B スプライン／ベジェ／ガウス）し、" +
                "速度・加速度制限に従って時間を再計算します。始点と終点は移動しません。"),
            Row("smooth.retime.note",
                "Время пересчитывается по лимитам: у планировщика оно с большим запасом " +
                "(в проверке — 3.999 с против 1.543 с), поэтому после постобработки траектория " +
                "становится заметно быстрее.",
                "Timing is recomputed by the limits: the planner's own timing is very conservative " +
                "(3.999 s vs 1.543 s in the check), so trajectories get much faster after processing.",
                "时间按限制重新计算：规划器自身的时间非常保守（检查中为 3.999 秒 对 1.543 秒），" +
                "因此后处理后轨迹明显更快。",
                "El tiempo se recalcula según los límites: el del planificador es muy conservador " +
                "(3,999 s frente a 1,543 s), por lo que la trayectoria se acelera tras el proceso.",
                "Die Zeit wird nach den Grenzen neu berechnet: die des Planers ist sehr konservativ " +
                "(3,999 s gegen 1,543 s), daher wird die Bahn danach deutlich schneller.",
                "Le temps est recalculé selon les limites : celui du planificateur est très conservateur " +
                "(3,999 s contre 1,543 s), la trajectoire devient donc plus rapide.",
                "時間は制限に従って再計算されます。プランナ自身の時間は非常に保守的（3.999 秒 対 1.543 秒）" +
                "のため、後処理後に軌道は大幅に高速化します。"),

            // ---------------------------------------------------------- ЭТАП 5: время-оптимальная
            Row("topt.title",
                "Время-оптимальная траектория", "Time-optimal trajectory", "时间最优轨迹",
                "Trayectoria óptima en tiempo", "Zeitoptimale Bahn", "Trajectoire temps-optimal", "時間最適軌道"),
            Row("topt.vel",
                "Макс. скорость суставов, °/с", "Max joint velocity, °/s", "关节最大速度 °/s",
                "Velocidad máx. de ejes, °/s", "Max. Gelenkgeschwindigkeit, °/s",
                "Vitesse max. des axes, °/s", "関節最大速度 °/s"),
            Row("topt.acc",
                "Макс. ускорение, °/с²", "Max acceleration, °/s²", "最大加速度 °/s²",
                "Aceleración máx., °/s²", "Max. Beschleunigung, °/s²",
                "Accélération max., °/s²", "最大加速度 °/s²"),
            Row("topt.jerk",
                "Макс. jerk, °/с³", "Max jerk, °/s³", "最大加加速度 °/s³",
                "Jerk máx., °/s³", "Max. Ruck, °/s³", "Jerk max., °/s³", "最大ジャーク °/s³"),
            Row("topt.compute",
                "Рассчитать время-оптимальную", "Compute time-optimal", "计算时间最优轨迹",
                "Calcular óptima en tiempo", "Zeitoptimale berechnen",
                "Calculer la temps-optimale", "時間最適を計算"),
            Row("topt.apply",
                "Переключиться на неё", "Switch to it", "切换到该轨迹", "Cambiar a ella",
                "Dazu umschalten", "Basculer dessus", "こちらに切り替え"),
            Row("topt.metric",
                "Метрика «время-оптимальная»", "Metric “time-optimal”", "指标“时间最优”",
                "Métrica «óptima en tiempo»", "Metrik „zeitoptimal“",
                "Métrique « temps-optimal »", "指標「時間最適」"),
            Row("topt.faster",
                "быстрее на {0} %", "{0} % faster", "快 {0} %", "{0} % más rápida",
                "{0} % schneller", "{0} % plus rapide", "{0} % 高速"),
            Row("topt.gain",
                "Выигрыш по времени", "Time saving", "时间节省", "Ahorro de tiempo",
                "Zeitersparnis", "Gain de temps", "時間短縮"),
            Row("topt.clears",
                "Мин. зазор, м", "Min clearance, m", "最小间隙 m", "Holgura mínima, m",
                "Min. Abstand, m", "Dégagement min., m", "最小クリアランス m"),
            Row("energy.peak.short",
                "Пиковая мощность", "Peak power", "峰值功率", "Potencia pico",
                "Spitzenleistung", "Puissance de pointe", "ピーク電力"),
            Row("topt.info",
                "Скорость вдоль пути ограничена лимитами суставов (скорость, ускорение, jerk): " +
                "профиль считается интегрированием вперёд-назад по длине пути, поэтому ни один сустав " +
                "не выходит за пределы. Форма пути НЕ меняется — меняется только распределение времени.",
                "Path speed is bounded by the joint limits (velocity, acceleration, jerk): the profile is " +
                "computed by forward-backward integration along the path, so no joint exceeds its limits. " +
                "The path shape is unchanged — only the timing changes.",
                "路径速度受关节限制（速度、加速度、加加速度）约束：沿路径做前向-后向积分得到速度曲线，" +
                "因此任何关节都不会超限。路径形状不变，仅时间分布改变。",
                "La velocidad se limita por los ejes (velocidad, aceleración, jerk): el perfil se calcula " +
                "por integración hacia delante y atrás, sin exceder límites. La forma no cambia, solo el tiempo.",
                "Die Bahngeschwindigkeit ist durch Gelenkgrenzen (Geschwindigkeit, Beschleunigung, Ruck) " +
                "begrenzt: das Profil wird vorwärts/rückwärts integriert, kein Gelenk überschreitet die Grenzen. " +
                "Die Form bleibt gleich — nur die Zeitverteilung ändert sich.",
                "La vitesse est bornée par les limites d'axes (vitesse, accélération, jerk) : le profil est " +
                "calculé par intégration avant/arrière, aucun axe ne dépasse. La forme ne change pas, " +
                "seule la répartition du temps.",
                "経路速度は関節制限（速度・加速度・ジャーク）で制限されます。前方向・後方向の積分で" +
                "プロファイルを求めるため、どの関節も制限を超えません。経路形状は変わらず、時間配分のみ変化します。"),

            // ---------------------------------------------------------- ЭТАП 6: энергия
            Row("energy.title",
                "Оптимизация по энергии", "Energy optimization", "能耗优化",
                "Optimización energética", "Energieoptimierung", "Optimisation énergétique", "エネルギー最適化"),
            Row("energy.payload",
                "Масса груза, кг", "Payload mass, kg", "负载质量 kg", "Masa de carga, kg",
                "Nutzlast, kg", "Masse de charge, kg", "負荷質量 kg"),
            Row("energy.compute",
                "Рассчитать эко-профиль", "Compute eco profile", "计算节能曲线",
                "Calcular perfil eco", "Öko-Profil berechnen", "Calculer le profil éco", "エコプロファイルを計算"),
            Row("energy.apply",
                "Переключиться на эко-профиль", "Switch to eco profile", "切换到节能曲线",
                "Cambiar al perfil eco", "Auf Öko-Profil umschalten",
                "Basculer sur le profil éco", "エコプロファイルに切替"),
            Row("energy.value",
                "Энергия, Дж", "Energy, J", "能耗 J", "Energía, J", "Energie, J", "Énergie, J", "エネルギー J"),
            Row("energy.per.meter",
                "Удельная энергия, Дж/м", "Specific energy, J/m", "单位能耗 J/m",
                "Energía específica, J/m", "Spezifische Energie, J/m",
                "Énergie spécifique, J/m", "比エネルギー J/m"),
            Row("energy.peak",
                "Пиковая мощность, Вт", "Peak power, W", "峰值功率 W", "Potencia pico, W",
                "Spitzenleistung, W", "Puissance de pointe, W", "ピーク電力 W"),
            Row("energy.savings",
                "Экономия", "Saving", "节省", "Ahorro", "Einsparung", "Économie", "削減"),
            Row("energy.info",
                "Симуляция по ТЗ: энергия ∝ Σ |момент × угловая скорость| × время. Момент считается " +
                "по упрощённой динамике (инерция + вязкое трение + удержание груза), поэтому метрика " +
                "годится для сравнения вариантов и оценки износа, а не для паспортных расчётов.",
                "Simulation per spec: energy ∝ Σ |torque × angular velocity| × time. Torque uses simplified " +
                "dynamics (inertia + viscous friction + payload holding), so the metric suits variant " +
                "comparison and wear estimation, not datasheet-grade calculations.",
                "按规范仿真：能耗 ∝ Σ |力矩 × 角速度| × 时间。力矩采用简化动力学（惯性 + 粘性摩擦 + 负载保持），" +
                "适用于方案对比与磨损评估，不用于出厂级计算。",
                "Simulación según especificación: energía ∝ Σ |par × velocidad angular| × tiempo. El par usa " +
                "dinámica simplificada (inercia + fricción + carga), sirve para comparar variantes y estimar " +
                "desgaste, no para cálculos de catálogo.",
                "Simulation laut Vorgabe: Energie ∝ Σ |Moment × Winkelgeschwindigkeit| × Zeit. Das Moment nutzt " +
                "eine vereinfachte Dynamik (Trägheit + viskose Reibung + Lashaltung) — geeignet für Varianten" +
                "vergleich und Verschleißabschätzung, nicht für Datenblattwerte.",
                "Simulation selon spécification : énergie ∝ Σ |couple × vitesse angulaire| × temps. Le couple " +
                "utilise une dynamique simplifiée (inertie + frottement + charge) : utile pour comparer les " +
                "variantes et estimer l'usure, pas pour des calculs de catalogue.",
                "仕様に基づくシミュレーション：エネルギー ∝ Σ |トルク × 角速度| × 時間。トルクは簡易動力学" +
                "（慣性＋粘性摩擦＋負荷保持）で計算するため、候補比較や摩耗評価向けであり、" +
                "カタログ値の計算には適しません。"),
            Row("energy.eco",
                "эко-профиль", "eco profile", "节能曲线", "perfil eco", "Öko-Profil", "profil éco", "エコプロファイル"),
            Row("energy.lower",
                "ниже на {0} %", "{0} % lower", "低 {0} %", "{0} % menos",
                "{0} % niedriger", "{0} % de moins", "{0} % 低減"),

            // ---------------------------------------------------------- окно-верстак и команды
            Row("wb.title",
                "Верстак: постобработка траекторий", "Workbench: trajectory post-processing",
                "工作台：轨迹后处理", "Banco: posproceso de trayectorias",
                "Werkbank: Bahn-Nachbearbeitung", "Établi : post-traitement", "ワークベンチ：軌道後処理"),
            Row("wb.variant",
                "Вариант", "Variant", "方案", "Variante", "Variante", "Variante", "候補"),
            Row("wb.metric",
                "Метрика", "Metric", "指标", "Métrica", "Metrik", "Métrique", "指標"),
            Row("wb.value",
                "Значение", "Value", "数值", "Valor", "Wert", "Valeur", "値"),
            Row("wb.pickvariant",
                "Выберите вариант в дереве моделей или ткните зелёным лазером",
                "Pick a variant in the model tree or with the green laser",
                "请在模型树中选择方案，或用绿色激光点选",
                "Elija una variante en el árbol o con el láser verde",
                "Variante im Modellbaum oder mit dem grünen Laser wählen",
                "Choisissez une variante dans l'arbre ou au laser vert",
                "モデルツリーまたは緑色レーザーで候補を選択してください"),
            Row("cmd.startmenu.show",
                "Главное меню", "Main menu", "主菜单", "Menú principal", "Hauptmenü", "Menu principal", "メインメニュー"),
            Row("cmd.startmenu.show.desc",
                "Экран запуска: новый проект, открытие сессии, демонстрация, настройки, выход",
                "Launch screen: new project, open session, demo, settings, exit",
                "启动界面：新建项目、打开会话、演示、设置、退出",
                "Pantalla inicial: nuevo proyecto, abrir sesión, demo, ajustes, salir",
                "Startbildschirm: neues Projekt, Sitzung, Demo, Einstellungen, Beenden",
                "Écran de lancement : nouveau projet, session, démo, paramètres, quitter",
                "起動画面：新規プロジェクト・セッション・デモ・設定・終了"),
            Row("cmd.demo.quick",
                "Показать демо", "Show demo", "播放演示", "Mostrar demo", "Demo zeigen",
                "Afficher la démo", "デモを表示"),
            Row("cmd.demo.quick.desc",
                "Автоматическая демонстрация: точка → 8 траекторий → фантомы → движение робота",
                "Automatic demo: point → 8 trajectories → phantoms → robot motion",
                "自动演示：点 → 8 条轨迹 → 幻影 → 机器人运动",
                "Demostración automática: punto → 8 trayectorias → fantasmas → movimiento",
                "Automatische Demo: Punkt → 8 Bahnen → Phantome → Bewegung",
                "Démo automatique : point → 8 trajectoires → fantômes → mouvement",
                "自動デモ：点 → 8 軌道 → ファントム → 動作"),
            Row("cmd.tut.toggle",
                "Обучение", "Tutorial", "教程", "Tutorial", "Tutorial", "Tutoriel", "チュートリアル"),
            Row("cmd.tut.toggle.desc",
                "Пошаговое обучение с подсветкой элементов; прогресс сохраняется",
                "Step-by-step guide with UI highlighting; progress is saved",
                "带界面高亮的分步教学；进度会自动保存",
                "Guía paso a paso con resaltado; el progreso se guarda",
                "Schritt-für-Schritt mit Hervorhebung; Fortschritt wird gespeichert",
                "Guide pas à pas avec surbrillance ; progression enregistrée",
                "UI ハイライト付きのステップ別ガイド。進捗は保存されます"),
            Row("cmd.workbench.toggle",
                "Верстак постобработки", "Post-processing workbench", "后处理工作台",
                "Banco de posproceso", "Werkbank Nachbearbeitung", "Établi de post-traitement",
                "後処理ワークベンチ"),
            Row("cmd.workbench.toggle.desc",
                "Вкладки: сглаживание, время-оптимальная траектория, энергия",
                "Tabs: smoothing, time-optimal trajectory, energy",
                "标签页：平滑、时间最优、能耗",
                "Pestañas: suavizado, óptima en tiempo, energía",
                "Tabs: Glättung, zeitoptimale Bahn, Energie",
                "Onglets : lissage, temps-optimal, énergie",
                "タブ：平滑化・時間最適・エネルギー"),
            Row("cmd.post.smooth",
                "Сглаживание траектории", "Trajectory smoothing", "轨迹平滑",
                "Suavizado de trayectoria", "Bahn-Glättung", "Lissage de trajectoire", "軌道のスムージング"),
            Row("cmd.post.smooth.desc",
                "Открыть вкладку сглаживания: уровень 0–100 %, метод, метрики «до / после»",
                "Open the smoothing tab: level 0–100 %, method, before/after metrics",
                "打开平滑标签页：程度 0–100 %、方法、前后指标",
                "Abrir la pestaña de suavizado: nivel 0–100 %, método, métricas antes/después",
                "Glättungs-Tab: Grad 0–100 %, Verfahren, Metriken vorher/nachher",
                "Onglet lissage : niveau 0–100 %, méthode, métriques avant/après",
                "平滑タブ：レベル 0–100 %・手法・前後の指標"),
            Row("cmd.post.timeoptimal",
                "Время-оптимальная", "Time-optimal", "时间最优", "Óptima en tiempo",
                "Zeitoptimal", "Temps-optimal", "時間最適"),
            Row("cmd.post.timeoptimal.desc",
                "Самая быстрая траектория при заданных лимитах скорости, ускорения и jerk",
                "Fastest trajectory under the given velocity, acceleration and jerk limits",
                "在给定速度、加速度与加加速度限制下最快的轨迹",
                "Trayectoria más rápida con los límites dados",
                "Schnellste Bahn bei gegebenen Grenzen",
                "Trajectoire la plus rapide selon les limites donnés",
                "指定した速度・加速度・ジャーク制限での最速軌道"),
            Row("cmd.post.energy",
                "Эко-профиль", "Eco profile", "节能曲线", "Perfil eco", "Öko-Profil", "Profil éco", "エコプロファイル"),
            Row("cmd.post.energy.desc",
                "Профиль движения с минимальным энергопотреблением (метрика «энергоэффективность»)",
                "Motion profile with minimum energy consumption (energy-efficiency metric)",
                "能耗最低的运动曲线（能效指标）",
                "Perfil de movimiento con mínimo consumo (métrica de eficiencia)",
                "Bewegungsprofil mit minimalem Energiebedarf (Effizienzmetrik)",
                "Profil de mouvement à consommation minimale (métrique d'efficacité)",
                "消費エネルギー最小の動作プロファイル（エネルギー効率指標）"),
            Row("cmd.post.auto",
                "Автосглаживание после планирования", "Auto-smooth after planning", "规划后自动平滑",
                "Suavizado automático", "Auto-Glättung nach Planung", "Lissage auto après planification",
                "計画後の自動平滑"),
            Row("cmd.post.auto.desc",
                "Вкл: каждая новая точка сразу даёт сглаженные варианты траектории",
                "On: every new point immediately yields smoothed trajectory variants",
                "开：每个新点直接生成平滑后的轨迹方案",
                "Activado: cada punto nuevo da variantes suavizadas",
                "Ein: jeder neue Punkt ergibt sofort geglättete Varianten",
                "Activé : chaque nouveau point donne des variantes lissées",
                "オン：新しい点は平滑化された候補になります"),

            // ---------------------------------------------------------- общие подписи модуля
            Row("common.close",
                "Закрыть", "Close", "关闭", "Cerrar", "Schließen", "Fermer", "閉じる"),
            Row("common.cancel",
                "Отмена", "Cancel", "取消", "Cancelar", "Abbrechen", "Annuler", "キャンセル"),
            Row("common.ok",
                "Готово", "Done", "完成", "Listo", "Fertig", "Terminé", "完了"),
        };

        private static string[] Row(string key, string ru, string en, string zh, string es,
            string de, string fr, string ja)
        {
            return new[] { key, ru, en, zh, es, de, fr, ja };
        }
    }
}
