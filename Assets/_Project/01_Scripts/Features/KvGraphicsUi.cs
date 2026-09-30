using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ПОКАЗ РАЗДЕЛА «ГРАФИКА» (этапы 3 и 7 ТЗ).
    ///
    /// Три части:
    ///   1) <see cref="BuildPanel"/> — содержимое вкладки «Графика» панели настроек.
    ///      Строится ТЕМИ ЖЕ средствами, что и остальные вкладки (`KvWidgets`, `KvTheme`),
    ///      и складывает созданные объекты в общий список панели — поэтому штатная
    ///      очистка вкладки работает без правок чужого кода;
    ///   2) плашка в правом нижнем углу — короткое сообщение при смене пресета;
    ///   3) оверлей метрик — кадровая частота, время кадра, вызовы отрисовки, треугольники,
    ///      видеопамять.
    ///
    /// Плашка и оверлей живут на СОБСТВЕННОМ канвасе (`KvOverlayKit`), поэтому видны и
    /// при скрытой оболочке интерфейса и не мешают раскладке панелей.
    /// </summary>
    public static class KvGraphicsUi
    {
        private const int CanvasOrder = 62;
        private const float ToastSeconds = 4.5f;

        private static RectTransform canvasRect;
        private static RectTransform toastRoot;
        private static Text toastText;
        private static float toastLeft;

        private static RectTransform metricsRoot;
        private static Text metricsText;
        private static bool metricsVisible;

        /// <summary>Плашка метрик показана.</summary>
        public static bool MetricsVisible { get { return metricsVisible && metricsRoot != null; } }

        /// <summary>
        /// Вызывается, когда состояние службы изменилось так, что вкладку нужно пересобрать
        /// (например, закончился бенчмарк). Ставит сама вкладка при построении.
        /// </summary>
        public static Action OnStateChanged;

        // ================================================================== служебные слои

        private static void Ensure()
        {
            if (canvasRect != null) return;

            Transform parent = null;
            KazistovVvUIManager ui = KazistovVvUIManager.Instance;
            if (ui != null) parent = ui.transform;
            if (parent == null)
            {
                GameObject rootGo = new GameObject("KvGraphicsUi");
                // ФИКС 5 (§20): служебный слой HUD — только HideInHierarchy, БЕЗ DontSave.
                // DontSave оставлял контейнер живым после выгрузки сцены и выхода из PlayMode
                // (ровно то, против чего принято правило §7/§8 и написан HierarchyPhantomCleaner).
                rootGo.hideFlags = HideFlags.HideInHierarchy;
                parent = rootGo.transform;
            }

            Canvas canvas = KvOverlayKit.CreateCanvas(parent, "KvGraphicsCanvas", CanvasOrder);
            canvasRect = (RectTransform)canvas.transform;

            BuildToast();
            BuildMetrics();
            canvas.gameObject.AddComponent<KvGraphicsToastHost>();
        }

        private static void BuildToast()
        {
            GameObject go = new GameObject("GraphicsToast", typeof(Image));
            go.transform.SetParent(canvasRect, false);
            toastRoot = (RectTransform)go.transform;
            toastRoot.anchorMin = toastRoot.anchorMax = new Vector2(1f, 0f);
            toastRoot.pivot = new Vector2(1f, 0f);
            toastRoot.sizeDelta = new Vector2(440f, 38f);
            // Выше красной плашки ошибок (она занимает нижний правый угол) и выше полосы
            // загрузки: обе могут быть видны одновременно с плашкой графики.
            toastRoot.anchoredPosition = new Vector2(-10f, KvStatusBar.Height + 66f);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(KvTheme.PanelHeader.r, KvTheme.PanelHeader.g, KvTheme.PanelHeader.b, 0.96f);
            bg.raycastTarget = false;

            Outline o = go.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            Image accent = KvTheme.CreatePanel(toastRoot, "Accent", KvTheme.Accent);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(3f, 0f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.raycastTarget = false;

            toastText = KvTheme.CreateText(toastRoot, "Text", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            KvTheme.Stretch(toastText.rectTransform, 10f, 8f);

            toastRoot.gameObject.SetActive(false);
        }

        private static void BuildMetrics()
        {
            GameObject go = new GameObject("GraphicsMetrics", typeof(Image));
            go.transform.SetParent(canvasRect, false);
            metricsRoot = (RectTransform)go.transform;
            metricsRoot.anchorMin = metricsRoot.anchorMax = new Vector2(1f, 1f);
            metricsRoot.pivot = new Vector2(1f, 1f);
            metricsRoot.sizeDelta = new Vector2(320f, 104f);
            metricsRoot.anchoredPosition = new Vector2(-10f, -10f);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0.62f);
            bg.raycastTarget = true;

            Text title = KvTheme.CreateText(metricsRoot, "Title",
                KvLocExtra4.T("graphics.metrics.title", "МЕТРИКИ ГРАФИКИ"),
                KvTheme.FontSizeSmall, TextAnchor.UpperLeft, KvTheme.Accent);
            KvTheme.Stretch(title.rectTransform, 8f, 8f, 4f, 0f);
            title.rectTransform.sizeDelta = new Vector2(-16f, 16f);

            metricsText = KvTheme.CreateText(metricsRoot, "Text", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            metricsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            KvTheme.Stretch(metricsText.rectTransform, 8f, 8f, 22f, 22f);
            metricsText.raycastTarget = false;

            Button close = KvTheme.CreateSmallButton(metricsRoot, "Close",
                KvLocExtra4.T("graphics.metrics.hide", "Скрыть"), delegate { SetMetrics(false); });
            RectTransform cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(1f, 0f);
            cr.sizeDelta = new Vector2(80f, 16f);
            cr.anchoredPosition = new Vector2(-6f, 4f);

            metricsRoot.gameObject.SetActive(false);
        }

        // ================================================================== плашка и метрики

        /// <summary>Короткая плашка в правом нижнем углу (ТЗ, этап 7).</summary>
        public static void ShowToast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // В пакетном режиме без видеокарты экранных слоёв не создаём: рисовать некуда,
            // а обращение к графике даёт ошибки устройства вывода.
            if (!KvGraphics.Available) return;
            try
            {
                Ensure();
                if (toastRoot == null) return;
                toastText.text = text;
                toastRoot.gameObject.SetActive(true);
                toastRoot.SetAsLastSibling();
                toastLeft = ToastSeconds;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] плашка не показана: " + e.Message);
            }
        }

        /// <summary>Служебный вызов: обновление таймера плашки (делает носитель).</summary>
        internal static void TickToast(float dt)
        {
            if (toastLeft <= 0f) return;
            toastLeft -= dt;
            if (toastLeft > 0f) return;
            toastLeft = 0f;
            if (toastRoot != null) toastRoot.gameObject.SetActive(false);
        }

        /// <summary>Показать/скрыть оверлей метрик.</summary>
        public static void SetMetrics(bool visible)
        {
            Ensure();
            metricsVisible = visible;
            if (metricsRoot != null) metricsRoot.gameObject.SetActive(visible);
            if (visible)
            {
                KvGraphicsService service = KvGraphicsService.Current;
                if (service != null) RefreshMetrics(service.MetricsText());
            }
        }

        /// <summary>Переключить оверлей метрик.</summary>
        public static void ToggleMetrics()
        {
            SetMetrics(!metricsVisible);
        }

        /// <summary>Обновить текст оверлея.</summary>
        public static void RefreshMetrics(string text)
        {
            if (metricsText != null) metricsText.text = text ?? "";
        }

        /// <summary>Подпись режима в статус-баре (ТЗ, этап 7: индикатор пресета).</summary>
        public static void SetStatus(string text)
        {
            try
            {
                KazistovVvUIManager ui = KazistovVvUIManager.Instance;
                if (ui == null || ui.StatusBar == null) return;
                ui.StatusBar.SetGraphics(text);
            }
            catch (Exception)
            {
                // Статус-бар может быть ещё не собран — это не ошибка.
            }
        }

        /// <summary>Сообщить вкладке, что состояние изменилось (пересобрать содержимое).</summary>
        public static void NotifyChanged()
        {
            if (OnStateChanged != null)
            {
                try { OnStateChanged(); }
                catch (Exception) { }
            }
        }

        // ================================================================== вкладка «Графика»

        /// <summary>
        /// Содержимое вкладки «Графика». `sink` — список строк панели настроек: всё
        /// созданное складывается туда, чтобы штатная очистка вкладки работала как раньше.
        /// </summary>
        public static void BuildPanel(RectTransform content, List<GameObject> sink, Action refresh)
        {
            if (content == null || sink == null) return;
            OnStateChanged = refresh;

            KvGraphicsService service = KvGraphicsService.EnsureStarted();
            bool available = service.Available;

            Section(content, sink, KvLocExtra4.T("graphics.section.preset", "Режим"));

            // --- 3.1 переключатель режима (Auto / Low / Medium / High / Ultra / VR-Ready / Свой)
            KvGraphicsPreset[] order = KvGraphicsModel.Order;
            string[] labels = new string[order.Length];
            int currentIndex = 0;
            for (int i = 0; i < order.Length; i++)
            {
                labels[i] = KvGraphicsModel.Label(order[i]);
                if (order[i] == service.Preset) currentIndex = i;
            }
            KvSegmented presets = KvWidgets.Segmented(content, "GraphicsPreset", labels, currentIndex,
                delegate (int index)
                {
                    service.SetPreset(order[Mathf.Clamp(index, 0, order.Length - 1)]);
                    NotifyChanged();
                }, 20f);
            sink.Add(presets.gameObject);

            Button detect = KvTheme.CreateButton(content, "GraphicsDetect",
                KvLocExtra4.T("graphics.detect", "Определить по железу"),
                delegate
                {
                    service.Redetect(true);
                    NotifyChanged();
                }, 20, true);
            sink.Add(detect.gameObject);

            Note(content, sink, service.DetectLine(), KvTheme.TextMain);

            Note(content, sink,
                KvLocExtra4.T("graphics.preset.note",
                    "«Авто» выбирает пресет по железу и пересчитывает его, если конфигурация " +
                    "изменилась. Любая правка ползунка ниже переводит режим в «Свой»: сам пресет " +
                    "при этом не портится — вернуться к нему можно выбором режима заново."),
                KvTheme.TextDisabled);

            Divider(content, sink);

            // --- 3.2 качество
            Section(content, sink, KvLocExtra4.T("graphics.section.quality", "Качество"));
            KvGraphicsProfile p = service.Active;

            // ФИКС 1. РЕЖИМ ОТРИСОВКИ HDRP — выбор оператора. MSAA в HDRP работает только при
            // прямой отрисовке, поэтому здесь и находится «выключатель» MSAA. Смена режима —
            // тяжёлая операция (пересборка шейдерных вариантов), поэтому она НИКОГДА не идёт
            // автоматически: сначала предупреждение, потом подтверждение оператора.
            SegmentedRow(content, sink, KvLocExtra4.T("graphics.raster", "Режим отрисовки"),
                new[]
                {
                    KvGraphicsModel.LitShaderModeLabel((int)KvLitShaderMode.Deferred),
                    KvGraphicsModel.LitShaderModeLabel((int)KvLitShaderMode.Forward),
                    KvGraphicsModel.LitShaderModeLabel((int)KvLitShaderMode.Both)
                }, KvGraphicsModel.LitShaderModeIndex(service.AssetLitShaderMode()),
                delegate (int i)
                {
                    service.RequestLitShaderMode(KvGraphicsModel.LitShaderModeAt(i));
                    NotifyChanged();
                });
            if (service.LitShaderPending)
            {
                Note(content, sink, KvLocExtra4.F("graphics.raster.pending",
                    "Подтвердите смену режима на «{0}»: {1:0} с. Во время перекомпиляции " +
                    "шейдерных вариантов кадр может замереть — это ожидаемо.",
                    KvGraphicsModel.LitShaderModeLabel((int)service.PendingLitShaderMode),
                    service.LitShaderCountdown), KvTheme.Warn);

                RectTransform row = KvWidgets.CreateRow(content, "GraphRasterConfirm", 22f, 6f);
                sink.Add(row.gameObject);
                Button yes = KvTheme.CreateButton(row, "RasterYes",
                    KvLocExtra4.T("graphics.raster.confirm", "Подтвердить смену режима"),
                    delegate
                    {
                        service.ConfirmLitShaderMode();
                        NotifyChanged();
                    }, 20, true);
                sink.Add(yes.gameObject);
                Button no = KvTheme.CreateButton(row, "RasterNo",
                    KvLocExtra4.T("graphics.raster.cancelBtn", "Отмена"),
                    delegate
                    {
                        service.CancelLitShaderMode();
                        NotifyChanged();
                    }, 20, false);
                sink.Add(no.gameObject);
            }
            Note(content, sink, KvLocExtra4.T("graphics.raster.note",
                "Прямая отрисовка нужна для MSAA; отложенная быстрее на сложной геометрии. " +
                "Смена режима пересобирает шейдерные варианты HDRP (несколько секунд) и " +
                "выполняется ТОЛЬКО по подтверждению оператора. Формат стерео XR " +
                "(Single Pass Instanced) при этом не меняется."), KvTheme.TextDisabled);
            if (!string.IsNullOrEmpty(service.LitShaderReport))
                Note(content, sink, service.LitShaderReport, KvTheme.TextMain);

            SliderRow(content, sink,
                KvLocExtra4.T("graphics.renderScale", "Масштаб отрисовки"), 0.5f, 1.5f, p.renderScale,
                "0.00", delegate (float v) { service.Edit(delegate (KvGraphicsProfile x) { x.renderScale = v; }); },
                false, 0.05f);
            Note(content, sink,
                KvLocExtra4.T("graphics.renderScale.note",
                    "Масштаб отрисовки задаётся штатным динамическим разрешением HDRP и " +
                    "применяется без перезагрузки сцены. В VR разрешение шлема задаёт среда XR — " +
                    "формат стерео (Single Pass Instanced) служба не меняет."),
                KvTheme.TextDisabled);

            SegmentedRow(content, sink, KvLocExtra4.T("graphics.shadows", "Тени"),
                new[]
                {
                    KvGraphicsModel.ShadowLabel(0), KvGraphicsModel.ShadowLabel(1),
                    KvGraphicsModel.ShadowLabel(2), KvGraphicsModel.ShadowLabel(3)
                }, p.shadowQuality,
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.shadowQuality = i; }); });

            SliderRow(content, sink,
                KvLocExtra4.T("graphics.shadowDistance", "Дистанция теней, м"), 10f, 200f, p.shadowDistance,
                "0", delegate (float v) { service.Edit(delegate (KvGraphicsProfile x) { x.shadowDistance = v; }); },
                false, 5f);

            SegmentedRow(content, sink, KvLocExtra4.T("graphics.cascades", "Каскады теней"),
                new[] { "1", "2", "3", "4" }, Mathf.Clamp(p.shadowCascades - 1, 0, 3),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.shadowCascades = i + 1; }); });

            SegmentedRow(content, sink, KvLocExtra4.T("graphics.aa", "Сглаживание"),
                new[]
                {
                    KvLocExtra4.T("graphics.aa.off", "Выкл"), "FXAA", "SMAA", "TAA",
                    "MSAA 2x", "MSAA 4x", "MSAA 8x"
                }, Mathf.Clamp(p.antiAliasing, 0, 6),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.antiAliasing = i; }); });
            Note(content, sink, AntiAliasingNote(service), KvTheme.TextDisabled);

            ToggleRow(content, sink, KvLocExtra4.T("graphics.ssao", "Затенение в экранном пространстве (SSAO)"),
                p.ssao, delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.ssao = v; }); });
            ToggleRow(content, sink, KvLocExtra4.T("graphics.ssr", "Отражения в экранном пространстве (SSR)"),
                p.ssr, delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.ssr = v; }); });
            SegmentedRow(content, sink, KvLocExtra4.T("graphics.reflections", "Отражения"),
                new[]
                {
                    KvLocExtra4.T("graphics.refl.baked", "Запечённые"),
                    KvLocExtra4.T("graphics.refl.realtime", "Запечённые + реального времени")
                }, Mathf.Clamp(p.reflectionMode, 0, 1),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.reflectionMode = i; }); });
            ToggleRow(content, sink, KvLocExtra4.T("graphics.bloom", "Свечение (Bloom)"), p.bloom,
                delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.bloom = v; }); });
            ToggleRow(content, sink, KvLocExtra4.T("graphics.motionblur", "Размытие в движении"), p.motionBlur,
                delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.motionBlur = v; }); });
            ToggleRow(content, sink, KvLocExtra4.T("graphics.dof", "Глубина резкости"), p.depthOfField,
                delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.depthOfField = v; }); });
            ToggleRow(content, sink, KvLocExtra4.T("graphics.fog", "Объёмный туман"), p.volumetricFog,
                delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.volumetricFog = v; }); });
            SegmentedRow(content, sink, KvLocExtra4.T("graphics.fogQuality", "Качество тумана"),
                new[]
                {
                    KvGraphicsModel.QualityLabel(0), KvGraphicsModel.QualityLabel(1),
                    KvGraphicsModel.QualityLabel(2)
                }, Mathf.Clamp(p.volumetricFogQuality, 0, 2),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.volumetricFogQuality = i; }); });

            // Трассировка лучей — доступна только там, где её поддерживает железо (ТЗ: «иначе серый»).
            bool rtSupported = KvGraphicsHardwareProbe.SupportsRayTracing();
            KvSwitch rt = ToggleRow(content, sink, KvLocExtra4.T("graphics.rt", "Трассировка лучей"),
                p.rayTracing && rtSupported,
                delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.rayTracing = v; }); });
            if (rt != null && !rtSupported)
            {
                rt.Locked = true;
                rt.Set(false);
                Button box = rt.GetComponent<Button>();
                if (box != null) box.interactable = false;
                Note(content, sink,
                    KvLocExtra4.T("graphics.rt.unsupported",
                        "Трассировка лучей недоступна: видеокарта или драйвер её не поддерживают."),
                    KvTheme.TextDisabled);
            }

            SliderRow(content, sink, KvLocExtra4.T("graphics.textures", "Предел текстур"), 0f, 3f, p.mipmapLimit,
                "0", delegate (float v)
                {
                    int limit = Mathf.Clamp(Mathf.RoundToInt(v), 0, 3);
                    service.Edit(delegate (KvGraphicsProfile x) { x.mipmapLimit = limit; });
                }, true);
            Note(content, sink, KvLocExtra4.T("graphics.textures.note",
                "0 — полные текстуры, 1 — 1/2, 2 — 1/4, 3 — 1/8 (сейчас: " +
                KvGraphicsModel.TextureLabel(p.mipmapLimit) + ")"), KvTheme.TextDisabled);

            SliderRow(content, sink, KvLocExtra4.T("graphics.lodbias", "Смещение уровня детализации (LOD)"),
                0.3f, 2f, p.lodBias, "0.00",
                delegate (float v) { service.Edit(delegate (KvGraphicsProfile x) { x.lodBias = v; }); },
                false, 0.05f);

            SegmentedRow(content, sink, KvLocExtra4.T("graphics.decals", "Декали и детализация"),
                new[]
                {
                    KvGraphicsModel.QualityLabel(0), KvGraphicsModel.QualityLabel(1),
                    KvGraphicsModel.QualityLabel(2), KvGraphicsModel.QualityLabel(3)
                }, Mathf.Clamp(p.decalQuality, 0, 3),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.decalQuality = i; }); });

            SegmentedRow(content, sink, KvLocExtra4.T("graphics.terrain", "Качество рельефа"),
                new[]
                {
                    KvGraphicsModel.QualityLabel(0), KvGraphicsModel.QualityLabel(1),
                    KvGraphicsModel.QualityLabel(2)
                }, Mathf.Clamp(p.terrainQuality, 0, 2),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.terrainQuality = i; }); });

            Divider(content, sink);

            // --- 3.3 производительность
            Section(content, sink, KvLocExtra4.T("graphics.section.performance", "Производительность"));
            SegmentedRow(content, sink, KvLocExtra4.T("graphics.vsync", "Синхронизация кадров"),
                new[]
                {
                    KvLocExtra4.T("graphics.vsync.off", "Выключена"),
                    KvLocExtra4.T("graphics.vsync.on", "Включена"),
                    KvLocExtra4.T("graphics.vsync.half", "Каждый второй кадр")
                }, Mathf.Clamp(p.vsync, 0, 2),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.vsync = i; }); });

            SegmentedRow(content, sink, KvLocExtra4.T("graphics.framecap", "Предел кадров"),
                new[] { "30", "60", "72", "90", "120", "144", KvLocExtra4.T("graphics.fps.unlimited", "Без предела") },
                Mathf.Clamp(p.frameCap, 0, 6),
                delegate (int i) { service.Edit(delegate (KvGraphicsProfile x) { x.frameCap = i; }); });
            Note(content, sink, KvLocExtra4.T("graphics.vsync.note",
                "При включённой синхронизации предел кадров задаёт видеодрайвер, а не приложение — " +
                "это штатное поведение Unity, значение предела всё равно сохраняется."),
                KvTheme.TextDisabled);

            ToggleRow(content, sink, KvLocExtra4.T("graphics.dynres", "Динамическое разрешение (HDRP)"),
                p.dynamicResolution,
                delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.dynamicResolution = v; }); });
            ToggleRow(content, sink, KvLocExtra4.T("graphics.adaptive", "Адаптивное качество (снижение при просадке)"),
                p.adaptiveQuality,
                delegate (bool v) { service.Edit(delegate (KvGraphicsProfile x) { x.adaptiveQuality = v; }); });
            SliderRow(content, sink, KvLocExtra4.T("graphics.targetfps", "Целевой FPS"), 30f, 144f, p.targetFps,
                "0", delegate (float v)
                {
                    int fps = Mathf.Clamp(Mathf.RoundToInt(v), 30, 144);
                    service.Edit(delegate (KvGraphicsProfile x) { x.targetFps = fps; });
                }, true);
            Note(content, sink, KvLocExtra4.T("graphics.adaptive.note",
                "Адаптивное качество снижает масштаб отрисовки шагом 0,05 при устойчивой просадке " +
                "ниже цели и возвращает его при запасе. По ТЗ масштаб меняется только в пресете " +
                "VR-Ready; в остальных режимах выдаётся мягкое предупреждение в журнал."),
                KvTheme.TextDisabled);

            Divider(content, sink);

            // --- 3.4 диагностика
            Section(content, sink, KvLocExtra4.T("graphics.section.diagnostics", "Диагностика"));
            Button metrics = KvTheme.CreateButton(content, "GraphicsMetrics",
                KvLocExtra4.T("graphics.metrics.show", "Показать текущие метрики"),
                delegate { ToggleMetrics(); }, 20, true);
            sink.Add(metrics.gameObject);

            Button bench = KvTheme.CreateButton(content, "GraphicsBenchmark",
                KvLocExtra4.T("graphics.benchmark.run", "Запустить бенчмарк 5 секунд"),
                delegate
                {
                    service.StartBenchmark(5f);
                    NotifyChanged();
                }, 20, true);
            sink.Add(bench.gameObject);

            Button reset = KvTheme.CreateButton(content, "GraphicsReset",
                KvLocExtra4.T("graphics.reset", "Сбросить графику по умолчанию"),
                delegate
                {
                    service.ResetToAuto();
                    NotifyChanged();
                }, 20, true);
            sink.Add(reset.gameObject);

            if (service.BenchmarkRunning)
                Note(content, sink, KvLocExtra4.T("graphics.benchmark.running", "Замер графики") + "…",
                    KvTheme.Accent);
            else if (!string.IsNullOrEmpty(service.BenchmarkReport))
                Note(content, sink, service.BenchmarkReport, KvTheme.TextMain);
            else
                Note(content, sink, KvLocExtra4.T("graphics.benchmark.none",
                    "Замер ещё не выполнялся: он считает средний, минимальный FPS и просадки."),
                    KvTheme.TextDisabled);

            Note(content, sink, KvLocExtra4.T("graphics.diag.state", "Состояние: ") +
                (available
                    ? KvLocExtra4.T("graphics.diag.on", "графика доступна, настройки применяются")
                    : KvLocExtra4.T("graphics.diag.off",
                        "графика недоступна (пакетный режим) — настройки только рассчитываются")),
                available ? KvTheme.Ok : KvTheme.Warn);

            Note(content, sink, service.PresetFileStatus, KvTheme.TextDisabled);
            if (KvGraphicsService.PipelineAsset() != null)
                Note(content, sink, KvLocExtra4.T("graphics.diag.hdrp", "Конвейер HDRP: ") +
                    KvGraphicsService.PipelineAsset().name, KvTheme.TextDisabled);

            Note(content, sink, service.ApplyReport, KvTheme.TextDisabled);

            KvGraphicsHardware hw = service.Hardware;
            if (hw != null && hw.detected)
                Note(content, sink, hw.Details(), KvTheme.TextDim);

            // --- 3.5 служебное (видно только при включённом служебном режиме)
            if (service.DebugMode)
            {
                Divider(content, sink);
                Section(content, sink, KvLocExtra4.T("graphics.section.service", "Служебное (для разработки)"));
                ToggleRow(content, sink,
                    KvLocExtra4.T("graphics.debug", "Полный журнал применения настроек"), true,
                    delegate (bool v) { service.DebugMode = v; NotifyChanged(); });
                Note(content, sink, service.ApplyReport, KvTheme.TextDisabled);
            }
        }

        /// <summary>
        /// Пояснение к сглаживанию: честно про MSAA и режим отрисовки HDRP.
        /// ФИКС 1: служба теперь УМЕЕТ включать прямую отрисовку (по подтверждению оператора),
        /// поэтому текст больше не говорит «служба этого не делает» — он говорит, что делать.
        /// </summary>
        private static string AntiAliasingNote(KvGraphicsService service)
        {
            UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset asset =
                KvGraphicsService.PipelineAsset();
            if (asset == null)
                return KvLocExtra4.T("graphics.aa.note",
                    "FXAA и SMAA — самый дешёвый вариант; TAA сглаживает лучше, но требует " +
                    "устойчивого кадра; MSAA работает только при прямой отрисовке HDRP.");

            bool forward = service.ForwardEnabled;
            if (!forward)
                return KvLocExtra4.T("graphics.aa.note.deferred",
                    "В HDRP-ассете выбран отложенный режим отрисовки, поэтому MSAA недоступен: " +
                    "при выборе MSAA применяется TAA (честное предупреждение выше). Чтобы MSAA " +
                    "заработал по-настоящему, переключите «Режим отрисовки» на «Прямая (Forward)» " +
                    "или «Обе» и подтвердите — служба сама включит прямую отрисовку и применит " +
                    "MSAA из пресета. Переключение тяжёлое (пересборка шейдерных вариантов) и " +
                    "поэтому никогда не выполняется автоматически.");
            return KvLocExtra4.T("graphics.aa.note.forward",
                "MSAA доступен: в HDRP-ассете включена прямая отрисовка. MSAA и TAA " +
                "взаимоисключающи — при выборе MSAA сглаживание камеры отключается. " +
                "Проверьте строку «XR после смены режима» ниже: формат стерео " +
                "(Single Pass Instanced) не должен измениться.");
        }

        // ================================================================== построители строк

        private static void Section(RectTransform content, List<GameObject> sink, string title)
        {
            Text t = KvWidgets.Label(content, "H_" + title, title.ToUpperInvariant(),
                KvTheme.FontSizeSmall - 1, KvTheme.TextDim, TextAnchor.MiddleLeft);
            KvWidgets.Fit(t.gameObject, 0f, 16f);
            sink.Add(t.gameObject);
        }

        private static void Divider(RectTransform content, List<GameObject> sink)
        {
            Image img = KvTheme.CreatePanel(content, "Div", KvTheme.Separator);
            KvWidgets.Fit(img.gameObject, 0f, 1f);
            sink.Add(img.gameObject);
        }

        private static void Note(RectTransform content, List<GameObject> sink, string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            Text t = KvWidgets.Note(content, text, color);
            sink.Add(t.gameObject);
        }

        private static KvSwitch ToggleRow(RectTransform content, List<GameObject> sink, string caption,
            bool value, Action<bool> onChanged)
        {
            KvSwitch sw = KvWidgets.Switch(content, caption, value, onChanged);
            sink.Add(sw.gameObject);
            return sw;
        }

        private static void SegmentedRow(RectTransform content, List<GameObject> sink, string caption,
            string[] options, int index, Action<int> onChanged)
        {
            RectTransform row = KvWidgets.CreateRow(content, "GraphSeg", 22f, 6f);
            sink.Add(row.gameObject);

            Text label = KvWidgets.Label(row, "Caption", caption, KvTheme.FontSizeSmall,
                KvTheme.TextDim, TextAnchor.MiddleLeft, 190f);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 190f;
            le.preferredWidth = 190f;
            le.minHeight = 18f;

            KvSegmented seg = KvWidgets.Segmented(row, "Seg", options,
                Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1)),
                delegate (int i) { if (onChanged != null) onChanged(i); }, 20f);
            LayoutElement sle = seg.gameObject.GetComponent<LayoutElement>();
            if (sle == null) sle = seg.gameObject.AddComponent<LayoutElement>();
            sle.flexibleWidth = 1f;
            sle.minHeight = 20f;
        }

        /// <summary>
        /// Ползунок с подписью и текущим значением — построен теми же элементами, что
        /// `KvTabKit.Slider` верстака (единый вид интерфейса FreeCAD).
        /// `step` — шаг округления (0 — без округления, `wholeNumbers` — целые).
        /// </summary>
        private static void SliderRow(RectTransform content, List<GameObject> sink, string caption,
            float min, float max, float value, string format, Action<float> onChanged, bool wholeNumbers,
            float step = 0f)
        {
            RectTransform row = KvWidgets.CreateRow(content, "GraphSlider", 20f, 6f);
            sink.Add(row.gameObject);

            Text label = KvWidgets.Label(row, "Caption", caption, KvTheme.FontSizeSmall,
                KvTheme.TextDim, TextAnchor.MiddleLeft, 190f);
            LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.minWidth = 190f;
            labelLe.preferredWidth = 190f;
            labelLe.minHeight = 18f;

            GameObject sliderGo = new GameObject("Slider", typeof(UnityEngine.UI.Slider));
            sliderGo.transform.SetParent(row, false);
            RectTransform sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.sizeDelta = new Vector2(220f, 14f);
            LayoutElement sliderLe = sliderGo.AddComponent<LayoutElement>();
            sliderLe.minWidth = 150f;
            sliderLe.preferredWidth = 220f;
            sliderLe.minHeight = 18f;
            sliderLe.flexibleWidth = 1f;

            Image track = KvTheme.CreatePanel(sliderRect, "Track", KvTheme.InputBg);
            KvTheme.Stretch(track.rectTransform, 0f, 0f, 6f, 6f);
            Image fill = KvTheme.CreatePanel(track.rectTransform, "Fill", KvTheme.Accent);
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            GameObject handle = new GameObject("Handle", typeof(Image));
            handle.transform.SetParent(sliderRect, false);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.sprite = KvTheme.WhiteSprite;
            handleImage.color = KvTheme.ButtonBg;
            RectTransform handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(12f, 18f);

            string fmt = string.IsNullOrEmpty(format) ? "0.00" : format;
            Text valueText = KvWidgets.Label(row, "Value", "", KvTheme.FontSizeSmall,
                KvTheme.TextMain, TextAnchor.MiddleRight, 76f);
            LayoutElement valueLe = valueText.gameObject.AddComponent<LayoutElement>();
            valueLe.minWidth = 76f;
            valueLe.preferredWidth = 76f;
            valueLe.minHeight = 18f;

            // Полное имя типа: иначе `Slider` разрешился бы в сам метод построителя.
            UnityEngine.UI.Slider slider = sliderGo.GetComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = Mathf.Clamp(value, min, max);
            valueText.text = SliderText(slider.value, fmt, wholeNumbers);

            slider.onValueChanged.AddListener(delegate (float v)
            {
                // Шаг: ТЗ задаёт его явно (масштаб — 0,05), поэтому значение округляется
                // до применения — так настройка не «дрожит» между соседними сотыми.
                if (step > 0f) v = Mathf.Round(v / step) * step;
                valueText.text = SliderText(v, fmt, wholeNumbers);
                if (onChanged != null) onChanged(v);
            });
        }

        private static string SliderText(float value, string format, bool wholeNumbers)
        {
            return wholeNumbers ? Mathf.RoundToInt(value).ToString() : value.ToString(format);
        }
    }

    /// <summary>Носитель таймера плашки: гасит её через несколько секунд.</summary>
    public class KvGraphicsToastHost : MonoBehaviour
    {
        private void Update()
        {
            KvGraphicsUi.TickToast(Time.unscaledDeltaTime);
        }
    }
}
