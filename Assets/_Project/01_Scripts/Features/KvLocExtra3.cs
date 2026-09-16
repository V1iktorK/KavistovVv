using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЛОКАЛИЗАЦИЯ ЭТАПОВ 13–36 (коллизионные меши, бенчмарк, дерево RRT, камеры, силы,
    /// тепловая карта времени, PDF-отчёт, сеть, веб-дашборд, мобильный пульт, голос,
    /// hand/eye tracking, скрипты, деревья поведения, окружения, свет, материалы,
    /// плёночный режим, титры, голосовой комментарий, отказы, валидация, уровни логов).
    ///
    /// Правила те же, что у <see cref="KvLocExtra"/> и <see cref="KvLocExtra2"/>: таблица
    /// «ключ + 7 языков», регистрация во время работы, внешние словари главнее,
    /// fallback «текущий язык → английский → русский текст из кода».
    /// </summary>
    public static class KvLocExtra3
    {
        private static bool installed;
        private static int registered;

        public static int RegisteredCount { get { return registered; } }

        public static string T(string key, string fallback)
        {
            return KvLoc.T(key, fallback);
        }

        public static string F(string key, string fallback, params object[] args)
        {
            string pattern = KvLoc.T(key, fallback);
            try { return string.Format(pattern, args); }
            catch { return pattern; }
        }

        public static void Install()
        {
            if (installed) return;
            installed = true;

            int count = 0;
            for (int r = 0; r < Rows.Length; r++)
            {
                string[] row = Rows[r];
                if (row == null || row.Length < 1 + KvLocExtra.Codes.Length) continue;
                string key = row[0];
                if (string.IsNullOrEmpty(key) || KvLoc.Has(key)) continue;

                bool wrote = false;
                for (int c = 0; c < KvLocExtra.Codes.Length; c++)
                {
                    string text = row[c + 1];
                    if (string.IsNullOrEmpty(text)) continue;
                    KvLoc.AddRuntimeStrings(KvLocExtra.Codes[c], key, text);
                    wrote = true;
                }
                if (wrote) count++;
            }
            registered = count;
            UnityEngine.Debug.Log("[Loc] строки этапов 13–36 зарегистрированы: " + count +
                                  " · всего в словарях: " + KvLoc.TotalStrings);
        }

        // ================================================================== таблица строк

        private static readonly string[][] Rows =
        {
            // ---------------------------------------------------------- ЭТАП 13: коллизионные меши
            Row("coll.title", "Коллизионные меши", "Collision meshes", "碰撞网格", "Mallas de colisión",
                "Kollisionsmeshes", "Maillages de collision", "コリジョンメッシュ"),
            Row("coll.enable", "Упрощать меши для проверки коллизий",
                "Simplify meshes for collision checks", "简化碰撞检测网格",
                "Simplificar mallas para colisiones", "Meshes für Kollisionen vereinfachen",
                "Simplifier les maillages de collision", "衝突判定用にメッシュを簡略化"),
            Row("coll.rebuild", "Пересобрать прокси", "Rebuild proxies", "重建代理", "Reconstruir proxies",
                "Proxys neu berechnen", "Reconstruire les proxys", "プロキシを再構築"),
            Row("coll.preview", "Показать прокси в сцене", "Show proxies in scene",
                "在场景中显示代理", "Mostrar proxies en la escena", "Proxys in der Szene zeigen",
                "Afficher les proxys", "シーンにプロキシを表示"),
            Row("coll.measure", "Замерить ускорение", "Measure speed-up", "测量加速",
                "Medir la mejora", "Beschleunigung messen", "Mesurer le gain", "高速化を計測"),
            Row("coll.mesh", "Меш", "Mesh", "网格", "Malla", "Mesh", "Maillage", "メッシュ"),
            Row("coll.kind", "Прокси", "Proxy", "代理", "Proxy", "Proxy", "Proxy", "プロキシ"),
            Row("coll.more", "Показать ещё", "Show more", "显示更多", "Mostrar más", "Mehr anzeigen",
                "Afficher plus", "さらに表示"),
            Row("coll.nomeasure", "Замер ещё не выполнялся", "No measurement yet",
                "尚未测量", "Sin medición", "Noch keine Messung", "Pas encore de mesure", "未計測"),
            Row("coll.info",
                "Прокси строятся по ВСЕМ вершинам меша (ориентированный бокс по главным осям), " +
                "поэтому всегда накрывают геометрию: повёрнутые детали перестают «раздуваться» " +
                "до осевого габарита, и планировщик находит путь там, где раньше отказывался.",
                "Proxies are built from ALL mesh vertices (oriented box along principal axes), so they " +
                "always cover the geometry: rotated parts no longer inflate to their AABB, and the " +
                "planner finds paths where it used to fail.",
                "代理基于全部网格顶点（主轴方向的有向包围盒），始终覆盖几何体：旋转部件不再膨胀到轴对齐包围盒，",
                "Los proxys se construyen con TODOS los vértices (caja orientada), por lo que siempre " +
                "cubren la geometría y el planificador encuentra rutas donde antes fallaba.",
                "Proxys werden aus ALLEN Vertices gebaut (orientierte Box) und decken die Geometrie " +
                "immer ab — der Planer findet Wege, wo er vorher scheiterte.",
                "Les proxys utilisent TOUS les sommets (boîte orientée) et couvrent toujours la " +
                "géométrie : le planificateur trouve des chemins là où il échouait.",
                "プロキシは全頂点から作り（主軸に沿った有向ボックス）、形状を必ず覆います。"),
            Row("cmd.coll.toggle", "Коллизионные меши", "Collision meshes", "碰撞网格",
                "Mallas de colisión", "Kollisionsmeshes", "Maillages de collision", "コリジョンメッシュ"),
            Row("cmd.coll.toggle.desc",
                "Упрощение мешей для быстрой проверки коллизий (низкополигональные прокси)",
                "Mesh simplification for fast collision checks (low-poly proxies)",
                "为快速碰撞检测简化网格（低模代理）",
                "Simplificación de mallas para colisiones rápidas",
                "Mesh-Vereinfachung für schnelle Kollisionsprüfung",
                "Simplification des maillages pour des collisions rapides",
                "高速な衝突判定のためのメッシュ簡略化"),

            // ---------------------------------------------------------- ЭТАП 14: бенчмарк
            Row("bench.title", "Бенчмарк планировщика", "Planner benchmark", "规划器基准测试",
                "Benchmark del planificador", "Planer-Benchmark", "Benchmark du planificateur",
                "プランナのベンチマーク"),
            Row("bench.tasks", "Задач в прогоне", "Tasks per run", "每次任务数", "Tareas por ejecución",
                "Aufgaben pro Lauf", "Tâches par exécution", "1 回のタスク数"),
            Row("bench.start", "Запустить прогон", "Run benchmark", "开始测试", "Ejecutar",
                "Lauf starten", "Lancer", "実行"),
            Row("bench.stop", "Остановить", "Stop", "停止", "Detener", "Stoppen", "Arrêter", "停止"),
            Row("bench.export", "Выгрузить таблицу (CSV)", "Export table (CSV)", "导出表格（CSV）",
                "Exportar tabla (CSV)", "Tabelle exportieren (CSV)", "Exporter le tableau (CSV)",
                "表を書き出す（CSV）"),
            Row("bench.table", "Результаты", "Results", "结果", "Resultados", "Ergebnisse",
                "Résultats", "結果"),
            Row("bench.strategy", "Стратегия", "Strategy", "策略", "Estrategia", "Strategie",
                "Stratégie", "戦略"),
            Row("bench.values", "Успех · время · длина", "Success · time · length", "成功 · 时间 · 长度",
                "Éxito · tiempo · longitud", "Erfolg · Zeit · Länge", "Succès · temps · longueur",
                "成功率 · 時間 · 長さ"),
            Row("bench.info",
                "BiRRT — штатный планировщик; RRT* — несколько перезапусков и выбор самого короткого " +
                "пути; TrajOpt — сокращение, сглаживание и перепараметризация по лимитам.",
                "BiRRT is the built-in planner; RRT* reruns it and keeps the shortest path; TrajOpt adds " +
                "shortcutting, smoothing and limit-based retiming.",
                "BiRRT 为内置规划器；RRT* 多次重跑取最短路径；TrajOpt 进行捷径、平滑与限位重定时。",
                "BiRRT es el planificador integrado; RRT* repite y conserva la ruta más corta; TrajOpt " +
                "añade atajos, suavizado y recálculo de tiempos.",
                "BiRRT ist der eingebaute Planer; RRT* wiederholt und nimmt den kürzesten Weg; TrajOpt " +
                "ergänzt Shortcuts, Glättung und Zeit-Neuberechnung.",
                "BiRRT est le planificateur intégré ; RRT* relance et garde le chemin le plus court ; " +
                "TrajOpt ajoute raccourcissement, lissage et recalcul des temps.",
                "BiRRT は内蔵プランナ、RRT* は再実行して最短経路を採用、TrajOpt は短縮・平滑化・再時間化。"),
            Row("cmd.bench.tab", "Бенчмарк планировщика", "Planner benchmark", "规划器基准测试",
                "Benchmark del planificador", "Planer-Benchmark", "Benchmark du planificateur",
                "プランナのベンチマーク"),
            Row("cmd.bench.tab.desc",
                "Прогон 100 задач: среднее время, success rate, длина; сравнение BiRRT / RRT* / TrajOpt",
                "100-task run: average time, success rate, length; BiRRT / RRT* / TrajOpt comparison",
                "100 个任务：平均时间、成功率、长度；BiRRT / RRT* / TrajOpt 对比",
                "100 tareas: tiempo medio, éxito, longitud; comparación BiRRT / RRT* / TrajOpt",
                "100 Aufgaben: Zeit, Erfolgsrate, Länge; Vergleich BiRRT / RRT* / TrajOpt",
                "100 tâches : temps, réussite, longueur ; comparaison BiRRT / RRT* / TrajOpt",
                "100 タスク：平均時間・成功率・長さ。BiRRT / RRT* / TrajOpt 比較"),

            // ---------------------------------------------------------- ЭТАП 15: дерево RRT
            Row("rrt.title", "Дерево RRT", "RRT tree", "RRT 树", "Árbol RRT", "RRT-Baum",
                "Arbre RRT", "RRT ツリー"),
            Row("rrt.show", "Показывать дерево планировщика в сцене",
                "Show the planner tree in the scene", "在场景中显示规划树",
                "Mostrar el árbol del planificador", "Planerbaum in der Szene zeigen",
                "Afficher l'arbre du planificateur", "プランナの木を表示"),
            Row("cmd.rrt.toggle", "Дерево RRT", "RRT tree", "RRT 树", "Árbol RRT", "RRT-Baum",
                "Arbre RRT", "RRT ツリー"),
            Row("cmd.rrt.toggle.desc",
                "Показать в реальном времени, как планировщик исследует пространство (для демонстраций)",
                "Show in real time how the planner explores the space (great for demos)",
                "实时显示规划器如何探索空间（演示效果好）",
                "Ver en tiempo real cómo explora el planificador",
                "In Echtzeit zeigen, wie der Planer den Raum erkundet",
                "Voir en temps réel l'exploration du planificateur",
                "プランナの探索をリアルタイム表示"),

            // ---------------------------------------------------------- ЭТАП 16: камеры / PiP
            Row("cam.title", "Камеры и PiP", "Cameras and PiP", "相机与画中画", "Cámaras y PiP",
                "Kameras und PiP", "Caméras et PiP", "カメラと PiP"),
            Row("cam.pip", "Картинка в картинке", "Picture in picture", "画中画", "Imagen en imagen",
                "Bild im Bild", "Image dans l'image", "ピクチャインピクチャ"),
            Row("cam.top", "Вид сверху", "Top view", "俯视图", "Vista superior", "Draufsicht",
                "Vue de dessus", "上面図"),
            Row("cam.side", "Вид сбоку", "Side view", "侧视图", "Vista lateral", "Seitenansicht",
                "Vue de côté", "側面図"),
            Row("cam.fpv", "Вид от первого лица (глаза робота)", "First-person view (robot eye)",
                "第一人称视角（机器人视点）", "Vista en primera persona", "Ego-Perspektive",
                "Vue à la première personne", "一人称視点"),
            Row("cam.info",
                "Дополнительные камеры рисуются в отдельные текстуры и показываются маленькими окнами " +
                "поверх сцены; основная камера оператора продолжает работать как обычно.",
                "Extra cameras render into textures shown as small windows over the scene; the operator " +
                "camera keeps working as usual.",
                "附加相机渲染到纹理并以小窗口显示；操作员主相机照常工作。",
                "Las cámaras extra se dibujan en texturas y se muestran en ventanas pequeñas.",
                "Zusatzkameras rendern in Texturen und erscheinen als kleine Fenster.",
                "Les caméras supplémentaires s'affichent dans de petites fenêtres.",
                "追加カメラはテクスチャに描画し小さなウィンドウで表示します。"),
            Row("cmd.cam.toggle", "Камеры и PiP", "Cameras and PiP", "相机与画中画", "Cámaras y PiP",
                "Kameras und PiP", "Caméras et PiP", "カメラと PiP"),
            Row("cmd.cam.toggle.desc",
                "Окна дополнительных камер: сверху, сбоку и от первого лица (для отладки и презентаций)",
                "Extra camera windows: top, side and first-person (debug and presentations)",
                "附加相机窗口：俯视、侧视、第一人称",
                "Ventanas extra: superior, lateral y primera persona",
                "Zusatzfenster: oben, seitlich, Ego-Perspektive",
                "Fenêtres : dessus, côté, première personne",
                "追加ウィンドウ：上面・側面・一人称"),

            // ---------------------------------------------------------- ЭТАП 17: силы и моменты
            Row("force.title", "Силы и моменты", "Forces and torques", "力与力矩", "Fuerzas y pares",
                "Kräfte und Momente", "Forces et couples", "力とトルク"),
            Row("force.show", "Показывать моменты на осях", "Show joint torques", "显示关节力矩",
                "Mostrar pares en los ejes", "Gelenkmomente anzeigen", "Afficher les couples",
                "関節トルクを表示"),
            Row("force.tcp", "Сила на инструменте", "Force at the tool", "工具受力",
                "Fuerza en la herramienta", "Kraft am Werkzeug", "Force à l'outil", "工具の力"),
            Row("force.info",
                "Момент каждого сустава считается по той же модели, что в калькуляторе нагрузки " +
                "(вес звеньев и груза на фактических плечах), и рисуется вектором у оси сустава; " +
                "длина вектора пропорциональна моменту, цвет — доля от номинала.",
                "Each joint torque uses the same model as the payload calculator (link and payload weight " +
                "on real lever arms) and is drawn as a vector at the joint axis; length is proportional " +
                "to torque, colour is the share of the rated value.",
                "各关节力矩与负载计算器使用同一模型，并在关节轴处绘制矢量；长度与力矩成正比，颜色表示额定占比。",
                "El par de cada eje usa el mismo modelo que la calculadora de carga y se dibuja como vector.",
                "Das Gelenkmoment nutzt dasselbe Modell wie der Nutzlast-Rechner und wird als Vektor gezeichnet.",
                "Le couple de chaque axe suit le même modèle que le calculateur et est dessiné en vecteur.",
                "各関節トルクはペイロード計算と同じモデルで、ベクトルとして描画します。"),
            Row("cmd.force.toggle", "Силы и моменты", "Forces and torques", "力与力矩",
                "Fuerzas y pares", "Kräfte und Momente", "Forces et couples", "力とトルク"),
            Row("cmd.force.toggle.desc",
                "Векторы моментов на осях и силы на инструменте (по модели калькулятора нагрузки)",
                "Torque vectors at joints and tool force (payload calculator model)",
                "关节力矩矢量与工具受力（负载计算器模型）",
                "Vectores de par y fuerza (modelo del calculador)",
                "Momentvektoren und Werkzeugkraft (Nutzlastmodell)",
                "Vecteurs de couple et force (modèle du calculateur)",
                "関節トルクと工具力のベクトル"),

            // ---------------------------------------------------------- ЭТАП 18: тепловая карта времени
            Row("time.title", "Тепловая карта времени", "Time-to-reach heatmap", "到达时间热力图",
                "Mapa de tiempo de alcance", "Zeit-Heatmap", "Carte de temps d'accès", "到達時間ヒートマップ"),
            Row("time.show", "Показывать время достижения (секунды)",
                "Show time to reach (seconds)", "显示到达时间（秒）",
                "Mostrar tiempo de alcance (segundos)", "Erreichzeit anzeigen (Sekunden)",
                "Afficher le temps d'accès (secondes)", "到達時間を表示（秒）"),
            Row("time.scale", "Верхняя граница шкалы, с", "Scale upper bound, s", "色标上限 s",
                "Límite superior, s", "Skalenobergrenze, s", "Borne supérieure, s", "スケール上限 s"),
            Row("time.info",
                "Цвет точки показывает, СКОЛЬКО СЕКУНД нужно роботу, чтобы добраться до неё (по лимитам " +
                "скорости и ускорения), а не абстрактную «стоимость»: зелёный — быстро, красный — долго.",
                "Colour shows HOW MANY SECONDS the robot needs to reach a point (within velocity and " +
                "acceleration limits) instead of an abstract cost: green is fast, red is slow.",
                "颜色表示机器人到达该点所需的秒数（受速度与加速度限制），绿色快、红色慢。",
                "El color muestra CUÁNTOS SEGUNDOS necesita el robot para llegar (según límites).",
                "Die Farbe zeigt, wie viele SEKUNDEN der Roboter bis zu einem Punkt braucht.",
                "La couleur indique le nombre de SECONDES nécessaires pour atteindre un point.",
                "色は到達に必要な秒数を示します（速度・加速度制限内）。"),
            Row("cmd.time.toggle", "Тепловая карта времени", "Time heatmap", "到达时间热力图",
                "Mapa de tiempo", "Zeit-Heatmap", "Carte de temps", "到達時間ヒートマップ"),
            Row("cmd.time.toggle.desc",
                "Раскраска рабочей зоны по времени достижения в секундах (включается/выключается)",
                "Colour the workspace by time-to-reach in seconds (toggle)",
                "按到达时间（秒）着色工作空间（可开关）",
                "Colorear el espacio por tiempo de alcance (se puede activar)",
                "Arbeitsraum nach Erreichzeit einfärben (umschaltbar)",
                "Colorer l'espace selon le temps d'accès (activable)",
                "ワークスペースを到達時間で色分け（切替可）"),

            // ---------------------------------------------------------- ЭТАП 19: PDF-отчёт
            Row("report.title", "Отчёт (PDF)", "Report (PDF)", "报告（PDF）", "Informe (PDF)",
                "Bericht (PDF)", "Rapport (PDF)", "レポート（PDF）"),
            Row("report.make", "Экспорт отчёта", "Export report", "导出报告", "Exportar informe",
                "Bericht exportieren", "Exporter le rapport", "レポートを書き出す"),
            Row("report.done", "отчёт создан", "report created", "报告已生成", "informe creado",
                "Bericht erstellt", "rapport créé", "レポートを作成しました"),
            Row("report.folder", "Папка отчётов", "Report folder", "报告文件夹", "Carpeta de informes",
                "Berichtordner", "Dossier des rapports", "レポートフォルダ"),
            Row("report.info",
                "PDF собирается кодом (без внешних библиотек): заголовок, метрики траектории, " +
                "таблица суставов, кинематика и скриншоты сцены.",
                "The PDF is generated in code (no external libraries): title, trajectory metrics, joint " +
                "table, kinematics and scene screenshots.",
                "PDF 由代码生成（无外部库）：标题、轨迹指标、关节表、运动学与场景截图。",
                "El PDF se genera en código: título, métricas, tabla de ejes, cinemática y capturas.",
                "Das PDF wird im Code erzeugt: Titel, Metriken, Gelenktabelle, Kinematik, Screenshots.",
                "Le PDF est généré en code : titre, métriques, table des axes, cinématique, captures.",
                "PDF はコードで生成（外部ライブラリ不要）：見出し・指標・関節表・運動学・スクリーンショット。"),
            Row("cmd.report.make", "Экспорт отчёта (PDF)", "Export report (PDF)", "导出报告（PDF）",
                "Exportar informe (PDF)", "Bericht exportieren (PDF)", "Exporter le rapport (PDF)",
                "レポートを書き出す（PDF）"),
            Row("cmd.report.make.desc",
                "PDF с выбранной траекторией, метриками, кинематикой и скриншотами — «Документы\\KazistovVv\\reports»",
                "PDF with the selected trajectory, metrics, kinematics and screenshots",
                "包含所选轨迹、指标、运动学与截图的 PDF",
                "PDF con la trayectoria, métricas, cinemática y capturas",
                "PDF mit Bahn, Metriken, Kinematik und Screenshots",
                "PDF avec trajectoire, métriques, cinématique et captures",
                "軌道・指標・運動学・スクリーンショットを含む PDF"),

            // ---------------------------------------------------------- ЭТАП 20-22: сеть
            Row("net.title", "Сеть: совместная работа", "Network: collaboration", "网络：协作",
                "Red: colaboración", "Netzwerk: Zusammenarbeit", "Réseau : collaboration",
                "ネットワーク：共同作業"),
            Row("net.role", "Роль", "Role", "角色", "Rol", "Rolle", "Rôle", "役割"),
            Row("net.role.off", "выключено", "off", "关闭", "apagado", "aus", "désactivé", "オフ"),
            Row("net.role.operator", "оператор (управляет)", "operator (drives)", "操作者（控制）",
                "operador (controla)", "Bediener (steuert)", "opérateur (pilote)", "オペレータ（操作）"),
            Row("net.role.observer", "наблюдатель (смотрит)", "observer (watches)", "观察者（观看）",
                "observador (mira)", "Beobachter (schaut)", "observateur (regarde)", "オブザーバ（閲覧）"),
            Row("net.connect", "Подключиться", "Connect", "连接", "Conectar", "Verbinden", "Connecter",
                "接続"),
            Row("net.port", "Порт", "Port", "端口", "Puerto", "Port", "Port", "ポート"),
            Row("net.host", "Адрес партнёра", "Peer address", "对端地址", "Dirección del par",
                "Partneradresse", "Adresse du pair", "相手のアドレス"),
            Row("net.info",
                "Обмен состоянием идёт по UDP: оператор рассылает позу робота, состояние State Machine " +
                "и выбранную траекторию, наблюдатель их применяет и видит ту же сцену.",
                "State is exchanged over UDP: the operator broadcasts the robot pose, state machine " +
                "phase and selected trajectory; the observer applies them.",
                "通过 UDP 交换状态：操作者广播机器人位姿、状态机与所选轨迹，观察者应用。",
                "El estado se intercambia por UDP: el operador difunde la pose y el observador la aplica.",
                "Der Zustand wird per UDP getauscht: der Bediener sendet Pose und Phase, der Beobachter " +
                "wendet sie an.",
                "L'état est échangé en UDP : l'opérateur diffuse la pose, l'observateur l'applique.",
                "状態は UDP で交換：オペレータが姿勢・フェーズを送信し、オブザーバが適用します。"),
            Row("web.title", "Веб-дашборд", "Web dashboard", "网页仪表板", "Panel web",
                "Web-Dashboard", "Tableau de bord web", "Web ダッシュボード"),
            Row("web.start", "Запустить сервер", "Start server", "启动服务器", "Iniciar servidor",
                "Server starten", "Démarrer le serveur", "サーバを起動"),
            Row("web.stop", "Остановить сервер", "Stop server", "停止服务器", "Detener servidor",
                "Server stoppen", "Arrêter le serveur", "サーバを停止"),
            Row("web.address", "Адрес страницы", "Page address", "页面地址", "Dirección",
                "Seitenadresse", "Adresse", "ページアドレス"),
            Row("web.info",
                "REST-подобный сервер на встроенном сокете: страница мониторинга и JSON с состоянием, " +
                "метриками, здоровьем суставов и текущей траекторией.",
                "A small HTTP server on a raw socket: monitoring page plus JSON with state, metrics, " +
                "joint health and the current trajectory.",
                "基于原始套接字的小型 HTTP 服务：监控页面与包含状态、指标、关节健康与轨迹的 JSON。",
                "Servidor HTTP simple: página de monitorización y JSON con estado, métricas y salud.",
                "Kleiner HTTP-Server: Überwachungsseite und JSON mit Zustand, Metriken und Gelenkgesundheit.",
                "Petit serveur HTTP : page de supervision et JSON (état, métriques, santé).",
                "小型 HTTP サーバ：監視ページと状態・指標・関節健全性の JSON。"),
            Row("mobile.title", "Мобильный пульт", "Mobile companion", "移动端控制", "Mando móvil",
                "Mobile Steuerung", "Télécommande mobile", "モバイル操作"),
            Row("mobile.enable", "Принимать команды с планшета (UDP)",
                "Accept commands from a tablet (UDP)", "接受平板命令（UDP）",
                "Aceptar comandos del tablet (UDP)", "Befehle vom Tablet annehmen (UDP)",
                "Accepter les commandes (UDP)", "タブレットからの命令を受け付け（UDP）"),
            Row("mobile.info",
                "Канал команд: `PING`, `STOP`, `HOME`, `SELECT 3`, `STATUS`. Ответ — состояние робота. " +
                "Команды выполняются теми же обработчиками, что кнопки интерфейса.",
                "Command channel: `PING`, `STOP`, `HOME`, `SELECT 3`, `STATUS`; replies carry robot state.",
                "命令通道：PING、STOP、HOME、SELECT 3、STATUS。",
                "Canal de comandos: PING, STOP, HOME, SELECT 3, STATUS.",
                "Befehlskanal: PING, STOP, HOME, SELECT 3, STATUS.",
                "Canal de commandes : PING, STOP, HOME, SELECT 3, STATUS.",
                "コマンド：PING / STOP / HOME / SELECT 3 / STATUS。"),
            Row("cmd.net.tab", "Сеть (совместная работа)", "Network (collaboration)",
                "网络（协作）", "Red (colaboración)", "Netzwerk (Zusammenarbeit)",
                "Réseau (collaboration)", "ネットワーク（共同作業）"),
            Row("cmd.net.tab.desc",
                "Два пользователя в одной сцене: один управляет роботом, второй наблюдает",
                "Two users in one scene: one drives the robot, the other watches",
                "两名用户同一场景：一人操作，一人观看",
                "Dos usuarios en una escena: uno maneja y otro observa",
                "Zwei Nutzer in einer Szene: einer steuert, der andere schaut zu",
                "Deux utilisateurs : l'un pilote, l'autre observe",
                "2 ユーザが同一シーン：操作と閲覧"),
            Row("cmd.web.tab", "Веб-дашборд", "Web dashboard", "网页仪表板", "Panel web",
                "Web-Dashboard", "Tableau de bord web", "Web ダッシュボード"),
            Row("cmd.web.tab.desc",
                "Мониторинг робота через браузер: REST-подобный сервер и страница состояния",
                "Monitor the robot in a browser: small HTTP server and status page",
                "通过浏览器监控机器人：小型 HTTP 服务与状态页",
                "Monitorizar el robot en el navegador",
                "Roboter im Browser überwachen",
                "Superviser le robot dans un navigateur",
                "ブラウザでロボットを監視"),

            // ---------------------------------------------------------- ЭТАП 23-25: XR-ввод
            Row("voice.title", "Голосовые команды", "Voice commands", "语音命令", "Comandos de voz",
                "Sprachbefehle", "Commandes vocales", "音声コマンド"),
            Row("voice.enable", "Слушать фразы (ввод текста вручную или SDK)",
                "Listen for phrases (typed input or SDK)", "监听短语（手动输入或 SDK）",
                "Escuchar frases (entrada manual o SDK)", "Phrasen annehmen (Eingabe oder SDK)",
                "Écouter les phrases (saisie ou SDK)", "フレーズを待ち受け（入力または SDK）"),
            Row("voice.say", "Произнести фразу (проверка)", "Say a phrase (test)", "输入短语（测试）",
                "Decir una frase (prueba)", "Phrase eingeben (Test)", "Dire une phrase (test)",
                "フレーズを入力（テスト）"),
            Row("voice.info",
                "Распознавание подключим к PICO / Meta Voice SDK; сейчас фразы принимаются текстом " +
                "и выполняются теми же действиями, что кнопки (заготовка по ТЗ).",
                "Recognition will hook into the PICO / Meta Voice SDK; for now phrases are typed and " +
                "executed exactly like UI buttons (stub per spec).",
                "识别将接入 PICO / Meta Voice SDK；当前以文本输入并执行与按钮相同的动作。",
                "El reconocimiento se conectará al SDK de PICO / Meta; por ahora las frases se escriben.",
                "Die Erkennung wird an das PICO-/Meta-SDK angebunden; derzeit Texteingabe.",
                "La reconnaissance se branchera au SDK PICO / Meta ; pour l'instant saisie texte.",
                "認識は PICO / Meta Voice SDK に接続予定。現在はテキスト入力。"),
            Row("hand.title", "Hand tracking", "Hand tracking", "手势追踪", "Seguimiento de manos",
                "Handtracking", "Suivi des mains", "ハンドトラッキング"),
            Row("hand.enable", "Управление жестами без контроллеров",
                "Gesture control without controllers", "无控制器手势控制",
                "Control por gestos", "Gestensteuerung", "Commande par gestes", "ジェスチャ操作"),
            Row("hand.pinch", "Pinch — выбор", "Pinch — select", "捏合 — 选择", "Pinch — seleccionar",
                "Pinch — Auswahl", "Pinch — sélection", "ピンチ — 選択"),
            Row("hand.swipe", "Swipe — переключение", "Swipe — switch", "滑动 — 切换",
                "Swipe — cambiar", "Swipe — Wechsel", "Swipe — changer", "スワイプ — 切替"),
            Row("hand.info",
                "Заготовка под PICO / Meta: жесты уже разбираются и вызывают те же действия, что " +
                "кнопки; подключается к потоку позы кисти из XR-плагина.",
                "Stub for PICO / Meta: gestures are parsed and trigger the same actions as buttons; " +
                "hooks into the XR hand pose stream.",
                "PICO / Meta 预留：手势已解析并触发与按钮相同的动作。",
                "Preparado para PICO / Meta: los gestos ya se interpretan.",
                "Vorbereitung für PICO / Meta: Gesten werden bereits ausgewertet.",
                "Préparé pour PICO / Meta : les gestes sont déjà interprétés.",
                "PICO / Meta 向けの下準備：ジェスチャは既に解釈されます。"),
            Row("eye.title", "Eye tracking", "Eye tracking", "眼动追踪", "Seguimiento ocular",
                "Eyetracking", "Suivi oculaire", "アイトラッキング"),
            Row("eye.enable", "Выбор взглядом + подтверждение", "Gaze selection + confirmation",
                "注视选择 + 确认", "Selección por mirada", "Auswahl per Blick", "Sélection du regard",
                "注視で選択"),
            Row("eye.foveated", "Foveated rendering", "Foveated rendering", "注视点渲染",
                "Renderizado foveal", "Foveated Rendering", "Rendu fovéal", "フォビエイテッドレンダリング"),
            Row("eye.info",
                "Заготовка под PICO 4 Ultra: взгляд выбирает объект, подтверждение — голос или жест; " +
                "foveated rendering запрашивается у плагина, если он доступен.",
                "Stub for PICO 4 Ultra: gaze selects, voice or gesture confirms; foveated rendering is " +
                "requested from the plugin when available.",
                "PICO 4 Ultra 预留：注视选择，语音或手势确认；可用时请求注视点渲染。",
                "Preparado para PICO 4 Ultra: la mirada selecciona y la voz confirma.",
                "Vorbereitung für PICO 4 Ultra: Blick wählt, Stimme bestätigt.",
                "Préparé pour PICO 4 Ultra : le regard sélectionne, la voix confirme.",
                "PICO 4 Ultra 向け：注視で選択、音声で確定。"),
            Row("cmd.voice.tab", "Голосовые команды", "Voice commands", "语音命令", "Comandos de voz",
                "Sprachbefehle", "Commandes vocales", "音声コマンド"),
            Row("cmd.voice.tab.desc",
                "Заготовка: «Робот, домой», «Стоп», «Выбери траекторию 3» — с проверкой текстом",
                "Stub: “robot home”, “stop”, “select trajectory 3” — testable by typing",
                "预留：回家/停止/选择轨迹 3 — 可用文本测试",
                "Preparado: «robot a casa», «stop», «trajectoria 3»",
                "Vorbereitung: „Roboter nach Hause“, „Stopp“, „Bahn 3“",
                "Préparé : « robot maison », « stop », « trajectoire 3 »",
                "準備：ロボット帰還・停止・軌道 3 選択"),
            Row("cmd.hand.tab", "Hand tracking", "Hand tracking", "手势追踪", "Seguimiento de manos",
                "Handtracking", "Suivi des mains", "ハンドトラッキング"),
            Row("cmd.hand.tab.desc",
                "Заготовка: pinch — выбор, swipe — переключение (без контроллеров)",
                "Stub: pinch selects, swipe switches (no controllers)",
                "预留：捏合选择，滑动切换（无需控制器）",
                "Preparado: pinch selecciona, swipe cambia",
                "Vorbereitung: Pinch wählt, Swipe wechselt",
                "Préparé : pinch sélectionne, swipe change",
                "準備：ピンチで選択、スワイプで切替"),
            Row("cmd.eye.tab", "Eye tracking", "Eye tracking", "眼动追踪", "Seguimiento ocular",
                "Eyetracking", "Suivi oculaire", "アイトラッキング"),
            Row("cmd.eye.tab.desc",
                "Заготовка под PICO 4 Ultra: выбор взглядом, подтверждение голосом или жестом, foveated rendering",
                "PICO 4 Ultra stub: gaze selection, voice/gesture confirmation, foveated rendering",
                "PICO 4 Ultra 预留：注视选择、语音/手势确认、注视点渲染",
                "Preparado para PICO 4 Ultra: mirada, confirmación y renderizado foveal",
                "Vorbereitung für PICO 4 Ultra: Blick, Bestätigung, Foveated Rendering",
                "Préparé pour PICO 4 Ultra : regard, confirmation, rendu fovéal",
                "PICO 4 Ultra 向け：注視・確定・フォビエイテッド"),

            // ---------------------------------------------------------- ЭТАП 26-27: скрипты и поведения
            Row("script.title", "Скрипты робота", "Robot scripts", "机器人脚本", "Scripts del robot",
                "Roboter-Skripte", "Scripts du robot", "ロボットスクリプト"),
            Row("script.run", "Выполнить скрипт", "Run script", "运行脚本", "Ejecutar script",
                "Skript ausführen", "Exécuter le script", "スクリプトを実行"),
            Row("script.stop", "Остановить скрипт", "Stop script", "停止脚本", "Detener script",
                "Skript stoppen", "Arrêter le script", "スクリプトを停止"),
            Row("script.sample", "Пример", "Sample", "示例", "Ejemplo", "Beispiel", "Exemple", "サンプル"),
            Row("script.info",
                "Песочница: доступны только команды робота (движение, домой, пауза, скорость, гриппер), " +
                "циклы `repeat N { … }` и условия `if … { … }`. Файлы, сеть и системные вызовы запрещены, " +
                "число шагов ограничено — скрипт не может «подвесить» платформу.",
                "Sandbox: only robot commands (move, home, wait, speed, gripper), `repeat N { … }` loops and " +
                "`if … { … }` conditions. No file, network or system calls; steps are limited.",
                "沙箱：仅机器人命令、repeat 循环与 if 条件；禁止文件/网络/系统调用，步数受限。",
                "Sandbox: solo comandos del robot, bucles repeat y condiciones if; sin archivos ni red.",
                "Sandbox: nur Roboterbefehle, repeat-Schleifen, if-Bedingungen; kein Datei-/Netzzugriff.",
                "Bac à sable : commandes robot, boucles repeat, conditions if ; pas d'accès fichier/réseau.",
                "サンドボックス：ロボット命令・repeat・if のみ。ファイル/ネットワーク不可、歩数制限あり。"),
            Row("bt.title", "Деревья поведения", "Behavior trees", "行为树", "Árboles de comportamiento",
                "Verhaltensbäume", "Arbres de comportement", "ビヘイビアツリー"),
            Row("bt.add", "Добавить ноду", "Add node", "添加节点", "Añadir nodo", "Knoten hinzufügen",
                "Ajouter un nœud", "ノード追加"),
            Row("bt.run", "Запустить", "Run", "运行", "Ejecutar", "Starten", "Lancer", "実行"),
            Row("bt.stop", "Остановить", "Stop", "停止", "Detener", "Stoppen", "Arrêter", "停止"),
            Row("bt.export", "Экспорт в скрипт", "Export to script", "导出为脚本",
                "Exportar a script", "In Skript exportieren", "Exporter en script", "スクリプトへ書き出し"),
            Row("bt.info",
                "Ноды: «двигайся», «жди», «если — то», «домой», «гриппер», последовательность и выбор. " +
                "Собранное дерево можно выгрузить в скрипт (этап 26) и сохранить на диск.",
                "Nodes: move, wait, if-then, home, gripper, sequence and selector. The tree can be " +
                "exported to a script (stage 26) and saved to disk.",
                "节点：移动、等待、条件、回家、夹爪、顺序与选择。可导出为脚本（阶段 26）。",
                "Nodos: mover, esperar, si-entonces, casa, pinza, secuencia y selector; exportable a script.",
                "Knoten: bewegen, warten, wenn-dann, home, Greifer, Sequenz, Auswahl; Export als Skript.",
                "Nœuds : déplacer, attendre, si-alors, home, pince, séquence, sélecteur ; export en script.",
                "ノード：移動・待機・条件・帰還・グリッパ・シーケンス・セレクタ。スクリプト出力可。"),
            Row("cmd.script.tab", "Скрипты робота", "Robot scripts", "机器人脚本", "Scripts del robot",
                "Roboter-Skripte", "Scripts du robot", "ロボットスクリプト"),
            Row("cmd.script.tab.desc",
                "Песочница скриптов (Python-подобный синтаксис): макросы для повторяющихся задач",
                "Script sandbox (Python-like syntax): macros for repetitive tasks",
                "脚本沙箱（类 Python 语法）：重复任务宏",
                "Sandbox de scripts (sintaxis tipo Python): macros",
                "Skript-Sandbox (Python-ähnlich): Makros",
                "Bac à sable (syntaxe type Python) : macros",
                "スクリプト砂場（Python 風）：マクロ"),
            Row("cmd.bt.tab", "Деревья поведения", "Behavior trees", "行为树",
                "Árboles de comportamiento", "Verhaltensbäume", "Arbres de comportement", "ビヘイビアツリー"),
            Row("cmd.bt.tab.desc",
                "Визуальный редактор поведений: drag-and-drop ноды и экспорт в скрипт",
                "Visual behaviour editor: drag-and-drop nodes and script export",
                "可视化行为编辑器：拖放节点并导出脚本",
                "Editor visual: nodos arrastrables y exportación",
                "Visueller Editor: Drag-and-drop-Knoten und Skript-Export",
                "Éditeur visuel : nœuds glisser-déposer et export",
                "ビジュアルエディタ：ドラッグ&ドロップとエクスポート"),

            // ---------------------------------------------------------- ЭТАП 28-30: студия сцены
            Row("env.title", "Окружение", "Environment", "环境", "Entorno", "Umgebung",
                "Environnement", "環境"),
            Row("env.hangar", "Ангар", "Hangar", "机库", "Hangar", "Hangar", "Hangar", "格納庫"),
            Row("env.lab", "Лаборатория", "Laboratory", "实验室", "Laboratorio", "Labor",
                "Laboratoire", "実験室"),
            Row("env.factory", "Фабрика", "Factory", "工厂", "Fábrica", "Fabrik", "Usine", "工場"),
            Row("env.clean", "Чистый фон", "Clean background", "纯色背景", "Fondo limpio",
                "Sauberer Hintergrund", "Fond propre", "クリーン背景"),
            Row("env.info",
                "Окружение строится кодом: пол, стены, стеллажи и детали — служебные объекты, " +
                "которые не мешают существующим стендам и не попадают в мир столкновений там, где " +
                "это не нужно.",
                "Environments are built in code: floor, walls, shelves and props are service objects " +
                "that do not disturb the existing stands.",
                "环境由代码生成：地板、墙体、货架等为服务对象，不影响现有工作台。",
                "El entorno se construye en código: suelo, paredes y estanterías son objetos de servicio.",
                "Umgebungen werden im Code gebaut: Boden, Wände, Regale sind Dienstobjekte.",
                "L'environnement est créé par code : sol, murs, étagères sont des objets de service.",
                "環境はコードで生成：床・壁・棚はサービスオブジェクトです。"),
            Row("light.title", "Освещение", "Lighting", "照明", "Iluminación", "Beleuchtung",
                "Éclairage", "照明"),
            Row("light.day", "Дневной свет", "Daylight", "日间", "Luz diurna", "Tageslicht",
                "Lumière du jour", "昼光"),
            Row("light.night", "Ночь", "Night", "夜晚", "Noche", "Nacht", "Nuit", "夜"),
            Row("light.studio", "Студийный", "Studio", "影棚", "Estudio", "Studio", "Studio", "スタジオ"),
            Row("light.drama", "Драматичный", "Dramatic", "戏剧化", "Dramático", "Dramatisch",
                "Dramatique", "ドラマチック"),
            Row("light.info",
                "Пресеты меняют существующие источники света сцены (яркость, цвет, температура) и " +
                "добавляют служебные лампы; исходные значения восстанавливаются при выходе.",
                "Presets retune the existing scene lights (intensity, colour, temperature) and add " +
                "service lamps; originals are restored on exit.",
                "预设调整现有灯光（强度、颜色、色温）并添加服务灯；退出时恢复。",
                "Los presets ajustan las luces existentes y añaden lámparas; se restauran al salir.",
                "Presets stimmen vorhandene Lichter ab und fügen Dienstlampen hinzu.",
                "Les presets règlent les lumières existantes et ajoutent des lampes.",
                "プリセットは既存のライトを調整し、補助ライトを追加します。"),
            Row("mat.title", "Материалы в runtime", "Runtime materials", "运行时材质",
                "Materiales en runtime", "Materialien zur Laufzeit", "Matériaux à l'exécution",
                "ランタイムマテリアル"),
            Row("mat.apply", "Применить материал", "Apply material", "应用材质", "Aplicar material",
                "Material anwenden", "Appliquer le matériau", "マテリアルを適用"),
            Row("mat.reset", "Вернуть исходные", "Restore originals", "恢复原始", "Restaurar originales",
                "Originale wiederherstellen", "Restaurer les originaux", "元に戻す"),
            Row("mat.info",
                "Палитра собрана из материалов сцены плюс можно создать новый материал с нужным " +
                "цветом, металличностью и шероховатостью — HDRP-шейдер берётся у существующих " +
                "материалов, поэтому вид остаётся «родным».",
                "The palette is built from scene materials, plus you can create a new one with the " +
                "desired colour, metallic and smoothness using the HDRP shader.",
                "调色板来自场景材质，也可创建新的颜色/金属度/粗糙度材质（使用 HDRP 着色器）。",
                "La paleta usa materiales de la escena y permite crear uno nuevo (color, metal, rugosidad).",
                "Die Palette nutzt Szenematerialien; neue lassen sich mit Farbe/Metallic/Rauheit anlegen.",
                "La palette vient de la scène ; un nouveau matériau (couleur, métal, rugosité) est possible.",
                "パレットはシーン材质から。色・金属度・粗さで新規作成も可能です。"),
            Row("cmd.env.tab", "Окружения", "Environments", "环境", "Entornos", "Umgebungen",
                "Environnements", "環境"),
            Row("cmd.env.tab.desc",
                "Пресеты окружений: ангар, лаборатория, фабрика, чистый фон (в один клик)",
                "Environment presets: hangar, lab, factory, clean background (one click)",
                "环境预设：机库、实验室、工厂、纯色背景（一键）",
                "Presets de entorno: hangar, laboratorio, fábrica, fondo limpio",
                "Umgebungs-Presets: Hangar, Labor, Fabrik, sauberer Hintergrund",
                "Presets : hangar, laboratoire, usine, fond propre",
                "環境プリセット：格納庫・実験室・工場・クリーン背景"),
            Row("cmd.light.tab", "Освещение", "Lighting", "照明", "Iluminación", "Beleuchtung",
                "Éclairage", "照明"),
            Row("cmd.light.tab.desc",
                "Пресеты освещения: день, ночь, студия, драматичный свет",
                "Lighting presets: day, night, studio, dramatic",
                "照明预设：日间、夜晚、影棚、戏剧化",
                "Presets: día, noche, estudio, dramático",
                "Presets: Tag, Nacht, Studio, dramatisch",
                "Presets : jour, nuit, studio, dramatique",
                "照明プリセット：昼・夜・スタジオ・ドラマチック"),
            Row("cmd.mat.tab", "Редактор материалов", "Material editor", "材质编辑器",
                "Editor de materiales", "Materialeditor", "Éditeur de matériaux", "マテリアル編集"),
            Row("cmd.mat.tab.desc",
                "Смена материалов роботов и столов прямо в приложении (для презентаций)",
                "Change robot and table materials right in the app (for presentations)",
                "在应用中更换机器人与桌面材质（用于演示）",
                "Cambiar materiales de robots y mesas en la aplicación",
                "Materialien von Robotern und Tischen direkt ändern",
                "Changer les matériaux des robots et tables dans l'application",
                "アプリ内でロボット・テーブルの材質を変更"),

            // ---------------------------------------------------------- ЭТАП 31-33: кино
            Row("cine.title", "Плёночный режим", "Cinematic mode", "电影模式", "Modo cine",
                "Kinomodus", "Mode cinéma", "シネマティックモード"),
            Row("cine.enter", "Войти в плёночный режим", "Enter cinematic mode", "进入电影模式",
                "Entrar en modo cine", "Kinomodus starten", "Entrer en mode cinéma", "シネマモード開始"),
            Row("cine.exit", "Выйти", "Exit", "退出", "Salir", "Beenden", "Quitter", "終了"),
            Row("cine.shot", "Следующий ракурс", "Next shot", "下一个镜头", "Siguiente plano",
                "Nächste Einstellung", "Plan suivant", "次のショット"),
            Row("cine.slow", "Замедление ×0.35", "Slow motion ×0.35", "慢动作 ×0.35",
                "Cámara lenta ×0.35", "Zeitlupe ×0.35", "Ralenti ×0.35", "スローモーション ×0.35"),
            Row("cine.info",
                "Камера сама ведёт серию ракурсов (облёт, наезд, крупный план инструмента, общий план), " +
                "интерфейс скрывается, включаются киношные полосы; замедление — для трейлера.",
                "The camera plays a shot list (orbit, dolly, tool close-up, wide shot), the UI hides and " +
                "letterbox bars appear; slow motion for the trailer.",
                "相机自动拍摄一组镜头（环绕、推近、工具特写、全景），隐藏界面并显示黑边；可慢动作。",
                "La cámara recorre planos (órbita, acercamiento, primer plano), oculta la interfaz y añade barras.",
                "Die Kamera fährt eine Shotlist ab, die Oberfläche wird ausgeblendet, Balken erscheinen.",
                "La caméra enchaîne les plans, l'interface se cache, des bandes apparaissent.",
                "カメラがショットを巡回し、UI を隠してレターボックスを表示します。"),
            Row("sub.title", "Титры и аннотации", "Titles and annotations", "字幕与注释",
                "Títulos y anotaciones", "Titel und Anmerkungen", "Titres et annotations", "字幕と注釈"),
            Row("sub.add", "Добавить титр", "Add title", "添加字幕", "Añadir título", "Titel hinzufügen",
                "Ajouter un titre", "字幕を追加"),
            Row("sub.export", "Экспорт .srt", "Export .srt", "导出 .srt", "Exportar .srt",
                ".srt exportieren", "Exporter .srt", ".srt を書き出す"),
            Row("sub.info",
                "Титры привязаны ко времени записи, показываются поверх видео и выгружаются в .srt — " +
                "файл субтитров ложится рядом с видео и подхватывается плеерами.",
                "Titles are tied to the recording timeline, drawn over the video and exported to .srt " +
                "next to the video file.",
                "字幕绑定录制时间轴，叠加在视频上并导出为 .srt。",
                "Los títulos se ligan a la línea de tiempo y se exportan a .srt.",
                "Titel sind an die Aufnahmezeit gebunden und werden als .srt exportiert.",
                "Les titres suivent la timeline et s'exportent en .srt.",
                "字幕は録画タイムラインに紐づき、.srt として書き出されます。"),
            Row("vo.title", "Голосовой комментарий", "Voice-over", "语音解说", "Locución",
                "Voice-over", "Commentaire voix", "ボイスオーバー"),
            Row("vo.record", "Начать запись комментария", "Start voice-over", "开始录音",
                "Iniciar locución", "Voice-over starten", "Démarrer le commentaire", "録音開始"),
            Row("vo.stop", "Остановить и сохранить", "Stop and save", "停止并保存", "Detener y guardar",
                "Stoppen und speichern", "Arrêter et enregistrer", "停止して保存"),
            Row("vo.info",
                "Запись идёт с микрофона и сохраняется в WAV рядом с видео; старт синхронизируется " +
                "со стартом записи видео, поэтому дорожки совпадают по времени.",
                "Recording uses the microphone and saves a WAV next to the video; the start is " +
                "synchronised with video recording so the tracks line up.",
                "使用麦克风录音并保存为 WAV，与视频录制同步启动。",
                "La grabación usa el micrófono y guarda un WAV junto al vídeo, sincronizado.",
                "Die Aufnahme nutzt das Mikrofon und speichert eine WAV neben dem Video.",
                "L'enregistrement utilise le micro et enregistre un WAV à côté de la vidéo.",
                "マイクで録音し、動画と同期した WAV を保存します。"),
            Row("cmd.cine.toggle", "Плёночный режим", "Cinematic mode", "电影模式", "Modo cine",
                "Kinomodus", "Mode cinéma", "シネマティックモード"),
            Row("cmd.cine.toggle.desc",
                "Красивые ракурсы, замедление и фокус на роботе — для трейлера проекта",
                "Cinematic shots, slow motion and focus on the robot — for the trailer",
                "电影镜头、慢动作与机器人特写 — 用于宣传片",
                "Planos, cámara lenta y foco en el robot",
                "Schöne Einstellungen, Zeitlupe, Fokus auf den Roboter",
                "Plans, ralenti et focus sur le robot",
                "美しいショット・スロー・ロボット焦点"),
            Row("cmd.sub.tab", "Титры и аннотации", "Titles and annotations", "字幕与注释",
                "Títulos y anotaciones", "Titel und Anmerkungen", "Titres et annotations", "字幕と注釈"),
            Row("cmd.sub.tab.desc",
                "Наложение текста на видео и экспорт субтитров .srt",
                "Text overlay on video and .srt subtitle export",
                "视频叠加文字并导出 .srt",
                "Texto sobre el vídeo y exportación .srt",
                "Textüberlagerung und .srt-Export",
                "Texte sur la vidéo et export .srt",
                "動画へのテキスト重ねと .srt 出力"),
            Row("cmd.vo.tab", "Голосовой комментарий", "Voice-over", "语音解说", "Locución",
                "Voice-over", "Commentaire voix", "ボイスオーバー"),
            Row("cmd.vo.tab.desc",
                "Запись комментария с микрофона синхронно с видео (WAV рядом с роликом)",
                "Microphone commentary recorded in sync with the video (WAV next to it)",
                "麦克风解说与视频同步录制（同目录 WAV）",
                "Locución sincronizada con el vídeo (WAV)",
                "Kommentar synchron zum Video (WAV)",
                "Commentaire synchronisé à la vidéo (WAV)",
                "動画と同期した音声解説（WAV）"),

            // ---------------------------------------------------------- ЭТАП 34-36: безопасность и логи
            Row("fail.title", "Симуляция отказов", "Failure simulation", "故障仿真", "Simulación de fallos",
                "Fehlersimulation", "Simulation de pannes", "故障シミュレーション"),
            Row("fail.joint", "Отказ сустава", "Joint failure", "关节故障", "Fallo de eje",
                "Gelenkausfall", "Panne d'axe", "関節故障"),
            Row("fail.comms", "Потеря связи", "Communication loss", "通信丢失", "Pérdida de comunicación",
                "Verbindungsverlust", "Perte de liaison", "通信断"),
            Row("fail.overload", "Перегрузка", "Overload", "过载", "Sobrecarga", "Überlast",
                "Surcharge", "過負荷"),
            Row("fail.info",
                "Отказ сустава: звено провисает по модели маятника с вязким трением (момент веса " +
                "считает штатная модель нагрузки), потеря управления обесточивает ось и игнорирует её " +
                "цель, отключение с фиксацией замораживает угол. Это не физический движок: динамика " +
                "Ньютона–Эйлера, трение в редукторах и упругость не считаются.",
                "Joint failure: the link sags as a pendulum with viscous friction (the gravity torque " +
                "comes from the platform load model), loss of control de-energizes the axis and ignores " +
                "its target, a brake-locked shutdown freezes the angle. This is not a physics engine: " +
                "rigid-body dynamics, gearbox friction and elasticity are not simulated.",
                "关节故障：连杆按带黏性摩擦的摆模型下垂（重力力矩由平台负载模型给出），" +
                "失去控制会断电该轴并忽略其目标，抱闸停机则冻结角度。这不是物理引擎：" +
                "不计刚体动力学、减速器摩擦与弹性。",
                "Fallo de eje: el eslabón cae como un péndulo con fricción viscosa (el par de gravedad " +
                "lo da el modelo de carga), la pérdida de control desenergiza el eje y su consigna se " +
                "ignora, el bloqueo con freno congela el ángulo. No es un motor físico: no se calculan " +
                "dinámica, fricción de reductores ni elasticidad.",
                "Gelenkausfall: das Glied sinkt als Pendel mit viskoser Reibung (das Gewichtsmoment " +
                "liefert das Lastmodell), Steuerungsverlust schaltet die Achse ab und ignoriert ihr " +
                "Ziel, Bremsenstillstand friert den Winkel ein. Keine Physik-Engine: Dynamik, " +
                "Getriebereibung und Elastizität werden nicht berechnet.",
                "Panne d'axe : le segment descend comme un pendule à frottement visqueux (le couple de " +
                "gravité vient du modèle de charge), la perte de commande met l'axe hors tension et " +
                "ignore sa consigne, le blocage au frein gèle l'angle. Ce n'est pas un moteur physique : " +
                "dynamique, frottements de réducteur et élasticité ne sont pas calculés.",
                "関節故障：リンクは粘性摩擦付き振り子モデルで垂下（重力トルクは負荷モデルから取得）。" +
                "制御喪失は軸を無通電にして目標を無視、ブレーキ停止は角度を凍結します。" +
                "物理エンジンではなく、剛体動力学・減速機摩擦・弾性は計算しません。"),
            Row("valid.title", "Валидация перед запуском", "Pre-run validation", "启动前校验",
                "Validación previa", "Prüfung vor dem Start", "Validation avant départ", "起動前検証"),
            Row("valid.run", "Проверить траекторию", "Validate trajectory", "校验轨迹",
                "Validar trayectoria", "Bahn prüfen", "Valider la trajectoire", "軌道を検証"),
            Row("valid.confirm", "Подтвердить запуск", "Confirm start", "确认启动", "Confirmar inicio",
                "Start bestätigen", "Confirmer le départ", "開始を確認"),
            Row("valid.ok", "траектория безопасна", "trajectory is safe", "轨迹安全", "trayectoria segura",
                "Bahn ist sicher", "trajectoire sûre", "軌道は安全"),
            Row("valid.info",
                "Перед стартом проверяются зазоры, зоны запрета, близость к человеку (оператор и метки), " +
                "запас до лимитов, нагрузка и состояние калибровки; опасная траектория требует явного " +
                "подтверждения оператора.",
                "Before starting, the platform checks clearances, keep-out zones, proximity to people " +
                "(operator and markers), limit margins, payload and calibration; a risky trajectory " +
                "requires explicit operator confirmation.",
                "启动前检查间隙、禁入区、人员距离、限位余量、负载与标定；风险轨迹需明确确认。",
                "Antes de arrancar se comprueban holguras, zonas, proximidad a personas, márgenes, carga y " +
                "calibración; una trayectoria riesgosa exige confirmación.",
                "Vor dem Start werden Abstände, Sperrzonen, Personennähe, Grenzreserven, Last und " +
                "Kalibrierung geprüft; eine riskante Bahn verlangt Bestätigung.",
                "Avant le départ : dégagements, zones, proximité des personnes, marges, charge, calibration ; " +
                "une trajectoire risquée exige une confirmation.",
                "起動前にクリアランス・禁止ゾーン・人との距離・限界余裕・負荷・校正を検査し、" +
                "危険な軌道は確認を要求します。"),
            Row("log.title", "Журнал: уровни и поиск", "Log: levels and search",
                "日志：级别与搜索", "Registro: niveles y búsqueda", "Protokoll: Stufen und Suche",
                "Journal : niveaux et recherche", "ログ：レベルと検索"),
            Row("log.level", "Уровень", "Level", "级别", "Nivel", "Stufe", "Niveau", "レベル"),
            Row("log.search", "Поиск", "Search", "搜索", "Buscar", "Suche", "Recherche", "検索"),
            Row("log.info", "Info", "Info", "信息", "Info", "Info", "Info", "情報"),
            Row("log.warning", "Warning", "Warning", "警告", "Aviso", "Warnung", "Avertissement", "警告"),
            Row("log.error", "Error", "Error", "错误", "Error", "Fehler", "Erreur", "エラー"),
            Row("log.info.text",
                "Записи окрашены по уровню, есть фильтры по уровням/категориям и поиск по тексту; " +
                "журнал по-прежнему пишется в файл.",
                "Entries are colour-coded by level, with level/category filters and free-text search; " +
                "the journal is still written to a file.",
                "条目按级别着色，支持级别/类别过滤与文本搜索；日志仍写入文件。",
                "Las entradas se colorean por nivel, con filtros y búsqueda; el registro sigue en archivo.",
                "Einträge sind nach Stufe gefärbt, mit Filtern und Suche; das Protokoll wird weiter " +
                "in eine Datei geschrieben.",
                "Les entrées sont colorées par niveau, avec filtres et recherche ; le journal est " +
                "toujours écrit dans un fichier.",
                "レベルで色分けし、フィルタと検索が可能。ログはファイルにも保存されます。"),
            Row("cmd.fail.tab", "Симуляция отказов", "Failure simulation", "故障仿真",
                "Simulación de fallos", "Fehlersimulation", "Simulation de pannes",
                "故障シミュレーション"),
            Row("cmd.fail.tab.desc",
                "Отказ сустава, потеря связи, перегрузка и поведение системы при аварии",
                "Joint failure, comms loss, overload and the system reaction",
                "关节故障、通信丢失、过载与系统反应",
                "Fallo de eje, pérdida de comunicación, sobrecarga y reacción",
                "Gelenkausfall, Verbindungsverlust, Überlast und Reaktion",
                "Panne d'axe, perte de liaison, surcharge et réaction",
                "関節故障・通信断・過負荷とその挙動"),
            Row("cmd.valid.tab", "Валидация перед запуском", "Pre-run validation", "启动前校验",
                "Validación previa", "Prüfung vor dem Start", "Validation avant départ", "起動前検証"),
            Row("cmd.valid.tab.desc",
                "Проверка траектории на безопасность перед стартом и подтверждение оператора",
                "Safety check of the trajectory before start plus operator confirmation",
                "启动前安全校验与操作者确认",
                "Comprobación de seguridad y confirmación del operador",
                "Sicherheitsprüfung vor dem Start und Bestätigung",
                "Contrôle de sécurité avant départ et confirmation",
                "起動前の安全検査と確認"),
            Row("cmd.log.tab", "Журнал: уровни и поиск", "Log: levels and search",
                "日志：级别与搜索", "Registro: niveles y búsqueda", "Protokoll: Stufen und Suche",
                "Journal : niveaux et recherche", "ログ：レベルと検索"),
            Row("cmd.log.tab.desc",
                "Фильтрация журнала по уровням (Info / Warning / Error), поиск и цветовая маркировка",
                "Filter the journal by level (Info / Warning / Error), search and colour coding",
                "按级别过滤日志（信息/警告/错误），搜索与颜色标记",
                "Filtrar por nivel (Info / Aviso / Error), buscar y colorear",
                "Nach Stufe filtern (Info / Warnung / Fehler), suchen und färben",
                "Filtrer par niveau, rechercher et colorer",
                "レベルで絞り込み（情報/警告/エラー）、検索と色分け"),
        };

        private static string[] Row(string key, string ru, string en, string zh, string es,
            string de, string fr, string ja)
        {
            return new[] { key, ru, en, zh, es, de, fr, ja };
        }
    }
}
