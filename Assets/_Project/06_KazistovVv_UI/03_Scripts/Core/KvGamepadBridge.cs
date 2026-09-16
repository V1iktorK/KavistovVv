namespace KazistovVvUI
{
    /// <summary>
    /// МОСТ между геймпадом и существующим вводом (ЭТАП 9).
    ///
    /// Почему мост, а не правка State Machine: проект уже читает подтверждение/отмену/
    /// вход в режим точки в `FreeFlyCameraController` (ЛКМ, Esc, Enter). Чтобы кнопки
    /// геймпада означали ТЕ ЖЕ действия, роутер геймпада (<see cref="KvGamepadRouter"/>)
    /// выставляет эти флаги на кадр, а камера/поток добавляют их к своим условиям.
    /// Логика роботов, планировщик и кинематика не затрагиваются вообще.
    /// </summary>
    public static class KvGamepadBridge
    {
        /// <summary>Геймпад подключён и роутер работает.</summary>
        public static bool Active;

        /// <summary>Подтверждение (A / RT) — аналог ЛКМ.</summary>
        public static bool Confirm;

        /// <summary>Отмена (B) — аналог Esc.</summary>
        public static bool Cancel;

        /// <summary>Вход/подтверждение режима перемещения точки (LT) — аналог Enter.</summary>
        public static bool Enter;

        /// <summary>
        /// Стики работают БЕЗ ручного тумблера R3 (ТЗ ЭТАПА 9: левая ось — ходьба,
        /// правая — обзор, «как в FPS»). Тумблер R3 оставлен как совместимость.
        /// </summary>
        public static bool SticksAlwaysActive { get { return Active; } }

        /// <summary>
        /// Старые «геймпадные» пути в контроллере камеры (LB — выбор робота по прицелу,
        /// левый триггер — подтверждение) отключаются, пока работает роутер: иначе одно
        /// нажатие срабатывало бы дважды (LB = аварийный стоп по ТЗ и выбор робота по старому коду).
        /// </summary>
        public static bool SuppressLegacyGamepad { get { return Active; } }

        /// <summary>Сбросить флаги (вызывает роутер в LateUpdate — флаги живут ровно один кадр).</summary>
        public static void ClearFrame()
        {
            Confirm = false;
            Cancel = false;
            Enter = false;
        }

        /// <summary>Полный сброс (геймпад отключён).</summary>
        public static void Reset()
        {
            Active = false;
            ClearFrame();
        }
    }
}
