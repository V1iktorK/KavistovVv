using System;
using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Виды звуковых событий (ЭТАП 16 ТЗ).</summary>
    public enum KvSound
    {
        PointSelected,     // выбранa точка — щелчок
        TrajectoryConfirmed, // траектория подтверждена
        MotionStarted,     // робот начал движение
        Error,             // ошибка (недостижимая точка и т.п.)
        EmergencyStop,     // аварийная остановка
        UiClick            // служебный щелчок интерфейса
    }

    /// <summary>
    /// ПРОСТРАНСТВЕННЫЙ ЗВУК (ЭТАП 16 ТЗ).
    ///
    /// Аудиофайлов в проекте нет, поэтому сигналы СИНТЕЗИРУЮТСЯ кодом
    /// (`AudioClip.Create` + `SetData`) — пять коротких сигналов: щелчок, подтверждение,
    /// старт движения, ошибка, аварийная остановка. Никаких ассетов и .meta.
    ///
    /// Пространственное позиционирование: один общий `AudioSource` с `spatialBlend = 1`,
    /// который перед проигрыванием ставится в МИРОВУЮ точку события (по умолчанию — позиция
    /// TCP робота потока), поэтому звук идёт «от робота». Rolloff — логарифмический.
    /// </summary>
    public class KvSpatialAudio
    {
        public const int SampleRate = 44100;

        /// <summary>Общая громкость.</summary>
        public float volume = 0.55f;
        /// <summary>Играть ли звук из позиции робота (false — 2D, «в голове оператора»).</summary>
        public bool spatial = true;
        /// <summary>Максимальная слышимость, м.</summary>
        public float maxDistance = 25f;

        private readonly Dictionary<KvSound, AudioClip> clips = new Dictionary<KvSound, AudioClip>();
        private AudioSource source;
        private Transform listener;

        /// <summary>Создать источник звука (вешается на объект потока/камеры).</summary>
        public void Bind(Transform host, Transform listenerTransform = null)
        {
            if (host == null) return;
            listener = listenerTransform;
            if (source == null)
            {
                source = host.GetComponent<AudioSource>();
                if (source == null) source = host.gameObject.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.6f;
            source.maxDistance = Mathf.Max(2f, maxDistance);
            source.dopplerLevel = 0f;
            source.volume = 1f;                     // громкость задаётся в PlayOneShot
            source.hideFlags = HideFlags.HideInHierarchy;

            EnsureClips();
        }

        /// <summary>Проиграть сигнал в мировой точке (по умолчанию — позиция источника).</summary>
        public void Play(KvSound sound, Vector3 worldPosition, float gain = 1f)
        {
            if (source == null) return;
            AudioClip clip;
            if (!clips.TryGetValue(sound, out clip) || clip == null) return;

            if (spatial)
            {
                source.transform.position = worldPosition;
                source.spatialBlend = 1f;
            }
            else
            {
                source.spatialBlend = 0f;
            }
            source.PlayOneShot(clip, Mathf.Clamp01(volume * Mathf.Clamp01(gain)));
        }

        /// <summary>Сигнал «от робота»: точка события — TCP робота потока (или его корень).</summary>
        public void PlayAtRobot(KvSound sound, TrajectoryFlowController flow, float gain = 1f)
        {
            Vector3 point = Vector3.zero;
            if (flow != null && flow.Robot != null)
            {
                Transform tcp = flow.Robot.tcp;
                point = tcp != null ? tcp.position : flow.Robot.transform.position;
            }
            Play(sound, point, gain);
        }

        private void EnsureClips()
        {
            if (clips.Count > 0) return;
            clips[KvSound.PointSelected] = Click("Kv_Click", 0.055f, 2600f, 0.35f);
            clips[KvSound.UiClick] = Click("Kv_UiClick", 0.035f, 1800f, 0.22f);
            clips[KvSound.TrajectoryConfirmed] = TwoTone("Kv_Confirm", 880f, 1320f, 0.16f, 0.30f);
            clips[KvSound.MotionStarted] = Sweep("Kv_Start", 380f, 920f, 0.42f, 0.28f);
            clips[KvSound.Error] = Buzz("Kv_Error", 170f, 0.30f, 0.32f);
            clips[KvSound.EmergencyStop] = Sweep("Kv_EStop", 1100f, 180f, 0.65f, 0.38f);
        }

        /// <summary>Короткий щелчок: шумовая посылка с быстрым спадом + тон.</summary>
        public static AudioClip Click(string name, float duration, float toneHz, float amplitude)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[n];
            System.Random rnd = new System.Random(12345);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (28f / Mathf.Max(0.01f, duration)));
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float tone = Mathf.Sin(2f * Mathf.PI * toneHz * t);
                data[i] = amplitude * env * (0.45f * noise + 0.55f * tone);
            }
            return FromData(name, data);
        }

        /// <summary>Два тона подряд (подтверждение).</summary>
        public static AudioClip TwoTone(string name, float f0, float f1, float duration, float amplitude)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[n];
            int half = n / 2;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float f = i < half ? f0 : f1;
                float env = Mathf.Min(1f, i / (0.004f * SampleRate)) * Mathf.Exp(-t * 6f);
                data[i] = amplitude * env * Mathf.Sin(2f * Mathf.PI * f * t);
            }
            return FromData(name, data);
        }

        /// <summary>Плавный свип по частоте (старт/аварийный стоп).</summary>
        public static AudioClip Sweep(string name, float fromHz, float toHz, float duration, float amplitude)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float f = Mathf.Lerp(fromHz, toHz, u);
                phase += 2f * Mathf.PI * f / SampleRate;
                float env = Mathf.Sin(Mathf.PI * u);              // мягкое появление/затухание
                data[i] = amplitude * env * Mathf.Sin(phase);
            }
            return FromData(name, data);
        }

        /// <summary>Низкий «неприятный» сигнал ошибки.</summary>
        public static AudioClip Buzz(string name, float frequency, float duration, float amplitude)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float square = Mathf.Sin(2f * Mathf.PI * frequency * t) >= 0f ? 1f : -1f;
                float tremolo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 26f * t);
                float env = Mathf.Min(1f, i / (0.005f * SampleRate)) *
                            Mathf.Clamp01((n - i) / (0.02f * SampleRate));
                data[i] = amplitude * env * tremolo * square * 0.6f;
            }
            return FromData(name, data);
        }

        private static AudioClip FromData(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Освободить созданные клипы (вызывается при уничтожении хаба).</summary>
        public void Dispose()
        {
            foreach (KeyValuePair<KvSound, AudioClip> pair in clips)
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            clips.Clear();
        }
    }
}
