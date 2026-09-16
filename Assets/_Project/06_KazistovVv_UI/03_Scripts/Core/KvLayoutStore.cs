using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// Раскладка ОДНОЙ панели (ЭТАП 3): край, толщина/размер, свёрнутость, видимость,
    /// «полочка» (свёрнута в боковую панель) и позиция плавающего окна.
    /// </summary>
    public class KvPanelLayout
    {
        public int Side;
        public float Thickness = 272f;
        public bool Collapsed;
        public bool Visible = true;
        /// <summary>Панель свёрнута в боковую «полочку» (кнопка ✕ по ТЗ ЭТАПА 3).</summary>
        public bool Rail;
        public float X = 80f;
        public float Y = 140f;
        public float Width = 320f;
        public float Height = 300f;

        public KvPanelLayout Clone()
        {
            return new KvPanelLayout
            {
                Side = Side,
                Thickness = Thickness,
                Collapsed = Collapsed,
                Visible = Visible,
                Rail = Rail,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height
            };
        }

        public string Describe()
        {
            return "край " + Side + " · толщина " + Thickness.ToString("0") +
                   (Collapsed ? " · свёрнута" : "") + (Rail ? " · полочка" : "") +
                   (Visible ? "" : " · скрыта") +
                   " · окно " + Width.ToString("0") + "×" + Height.ToString("0") +
                   " @ " + X.ToString("0") + "," + Y.ToString("0");
        }
    }

    /// <summary>
    /// ХРАНИЛИЩЕ РАСКЛАДКИ ОКОН (ЭТАП 3): сохраняет край/толщину/размер/позицию каждой
    /// панели в PlayerPrefs, чтобы при следующем запуске окна стояли на своих местах.
    /// Кнопка «Сбросить раскладку» возвращает значения по умолчанию
    /// (<see cref="Clear"/> + пересборка оболочки).
    /// </summary>
    public static class KvLayoutStore
    {
        /// <summary>Префикс ключей PlayerPrefs.</summary>
        public const string Prefix = "KazistovVv.Layout.";

        /// <summary>Раскладка сохранена хотя бы для одной панели.</summary>
        public static bool HasAny
        {
            get { return PlayerPrefs.HasKey(Prefix + "saved"); }
        }

        /// <summary>Прочитать раскладку панели (null — сохранённой раскладки нет).</summary>
        public static KvPanelLayout Load(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (PlayerPrefs.GetInt(Prefix + key + ".set", 0) == 0) return null;
            KvPanelLayout l = new KvPanelLayout();
            l.Side = PlayerPrefs.GetInt(Prefix + key + ".side", 0);
            l.Thickness = PlayerPrefs.GetFloat(Prefix + key + ".thick", 272f);
            l.Collapsed = PlayerPrefs.GetInt(Prefix + key + ".coll", 0) != 0;
            l.Visible = PlayerPrefs.GetInt(Prefix + key + ".vis", 1) != 0;
            l.Rail = PlayerPrefs.GetInt(Prefix + key + ".rail", 0) != 0;
            l.X = PlayerPrefs.GetFloat(Prefix + key + ".x", 80f);
            l.Y = PlayerPrefs.GetFloat(Prefix + key + ".y", 140f);
            l.Width = PlayerPrefs.GetFloat(Prefix + key + ".w", 320f);
            l.Height = PlayerPrefs.GetFloat(Prefix + key + ".h", 300f);
            return l;
        }

        /// <summary>Сохранить раскладку панели.</summary>
        public static void Save(string key, KvPanelLayout layout)
        {
            if (string.IsNullOrEmpty(key) || layout == null) return;
            PlayerPrefs.SetInt(Prefix + key + ".set", 1);
            PlayerPrefs.SetInt(Prefix + "saved", 1);
            PlayerPrefs.SetInt(Prefix + key + ".side", layout.Side);
            PlayerPrefs.SetFloat(Prefix + key + ".thick", layout.Thickness);
            PlayerPrefs.SetInt(Prefix + key + ".coll", layout.Collapsed ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + key + ".vis", layout.Visible ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + key + ".rail", layout.Rail ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + key + ".x", layout.X);
            PlayerPrefs.SetFloat(Prefix + key + ".y", layout.Y);
            PlayerPrefs.SetFloat(Prefix + key + ".w", layout.Width);
            PlayerPrefs.SetFloat(Prefix + key + ".h", layout.Height);
        }

        /// <summary>Записать изменения на диск немедленно (после перетаскивания окна).</summary>
        public static void Flush()
        {
            try { PlayerPrefs.Save(); } catch { }
        }

        /// <summary>Сбросить сохранённую раскладку (кнопка «Сбросить раскладку»).</summary>
        public static void Clear(string[] keys)
        {
            if (keys != null)
            {
                foreach (string key in keys)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    PlayerPrefs.DeleteKey(Prefix + key + ".set");
                    PlayerPrefs.DeleteKey(Prefix + key + ".side");
                    PlayerPrefs.DeleteKey(Prefix + key + ".thick");
                    PlayerPrefs.DeleteKey(Prefix + key + ".coll");
                    PlayerPrefs.DeleteKey(Prefix + key + ".vis");
                    PlayerPrefs.DeleteKey(Prefix + key + ".rail");
                    PlayerPrefs.DeleteKey(Prefix + key + ".x");
                    PlayerPrefs.DeleteKey(Prefix + key + ".y");
                    PlayerPrefs.DeleteKey(Prefix + key + ".w");
                    PlayerPrefs.DeleteKey(Prefix + key + ".h");
                }
            }
            PlayerPrefs.DeleteKey(Prefix + "saved");
            Flush();
        }
    }
}
