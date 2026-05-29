using System;
using System.IO;
using System.Media;

namespace MatrixHole.Core
{
    /// <summary>
    /// SoundNotifications — кастомные звуки твикера (п.89 OptimizatorPlan).
    /// Щелчок при сохранении, писк при ошибке, звук при успешном инжекте.
    /// </summary>
    public static class SoundNotifications
    {
        private static bool _enabled = true;
        public static bool Enabled { get => _enabled; set => _enabled = value; }

        public static void PlaySave()
        {
            if (!_enabled) return;
            try
            {
                // Soft click using built-in beep frequency
                Console.Beep(800, 50);
            }
            catch { }
        }

        public static void PlayError()
        {
            if (!_enabled) return;
            try
            {
                Console.Beep(300, 150);
                System.Threading.Thread.Sleep(50);
                Console.Beep(250, 200);
            }
            catch { }
        }

        public static void PlaySuccess()
        {
            if (!_enabled) return;
            try
            {
                Console.Beep(600, 100);
                System.Threading.Thread.Sleep(50);
                Console.Beep(800, 150);
            }
            catch { }
        }

        public static void PlayInjectSuccess()
        {
            if (!_enabled) return;
            try
            {
                Console.Beep(500, 100);
                System.Threading.Thread.Sleep(80);
                Console.Beep(700, 100);
                System.Threading.Thread.Sleep(80);
                Console.Beep(900, 200);
            }
            catch { }
        }

        public static void PlayAlert()
        {
            if (!_enabled) return;
            try
            {
                Console.Beep(440, 100);
                System.Threading.Thread.Sleep(50);
                Console.Beep(440, 100);
            }
            catch { }
        }
    }
}
