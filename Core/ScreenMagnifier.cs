using System;
using System.Drawing;
using SWF = System.Windows.Forms;

namespace MatrixHole.Core
{
    /// <summary>
    /// ScreenMagnifier — виртуальная лупа (п.61 OptimizatorPlan).
    /// Увеличение центра экрана или прицела.
    /// </summary>
    public static class ScreenMagnifier
    {
        private static Form? _magnifierForm;
        private static bool _isActive;

        public static bool IsActive => _isActive;

        public static void Toggle()
        {
            if (_isActive) Disable();
            else Enable();
        }

        public static void Enable()
        {
            if (_isActive) return;
            _isActive = true;

            var t = new System.Threading.Thread(() =>
            {
                _magnifierForm = new MagnifierForm();
                System.Windows.Forms.Application.Run(_magnifierForm);
            })
            { IsBackground = true };
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
        }

        public static void Disable()
        {
            _isActive = false;
            _magnifierForm?.Close();
            _magnifierForm = null;
        }

        private class MagnifierForm : Form
        {
            private readonly SWF.Timer _timer;
            private const int ZoomFactor = 2;
            private const int LensSize = 200;

            public MagnifierForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                TopMost = true;
                Width = LensSize;
                Height = LensSize;
                StartPosition = FormStartPosition.Manual;
                BackColor = Color.Black;
                _timer = new SWF.Timer { Interval = 16 };
                _timer.Tick += (s, e) =>
                {
                    var cursor = Cursor.Position;
                    Left = cursor.X - LensSize / 2;
                    Top = cursor.Y - LensSize / 2;
                    Invalidate();
                };
                _timer.Start();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var cursor = Cursor.Position;
                var srcRect = new Rectangle(cursor.X - LensSize / (2 * ZoomFactor), cursor.Y - LensSize / (2 * ZoomFactor), LensSize / ZoomFactor, LensSize / ZoomFactor);
                try
                {
                    using var bmp = new Bitmap(srcRect.Width, srcRect.Height);
                    using var gBmp = Graphics.FromImage(bmp);
                    gBmp.CopyFromScreen(srcRect.Location, Point.Empty, srcRect.Size);
                    g.DrawImage(bmp, 0, 0, LensSize, LensSize);
                }
                catch { }
            }
        }
    }
}
