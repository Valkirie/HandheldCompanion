using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace HandheldCompanion.Common
{
    public class OverlayWindow : Window
    {
        public HorizontalAlignment _HorizontalAlignment;
        public new HorizontalAlignment HorizontalAlignment
        {
            get
            {
                return _HorizontalAlignment;
            }

            set
            {
                _HorizontalAlignment = value;
                UpdatePosition();
            }
        }

        public VerticalAlignment _VerticalAlignment;
        public new VerticalAlignment VerticalAlignment
        {
            get
            {
                return _VerticalAlignment;
            }

            set
            {
                _VerticalAlignment = value;
                UpdatePosition();
            }
        }

        public OverlayWindow() : base()
        {
            // overlay specific settings
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;

            SizeChanged += (o, e) =>
            {
                UpdatePosition();
            };
        }

        private void UpdatePosition()
        {
            var r = SystemParameters.WorkArea;

            switch (HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                    Left = 0;
                    break;

                default:
                case HorizontalAlignment.Center:
                    Left = r.Width / 2 - Width / 2;
                    break;

                case HorizontalAlignment.Right:
                    Left = r.Right - Width;
                    break;

                case HorizontalAlignment.Stretch:
                    Left = 0;
                    Width = SystemParameters.PrimaryScreenWidth;
                    break;
            }

            switch (VerticalAlignment)
            {
                case VerticalAlignment.Top:
                    Top = 0;
                    break;

                default:
                case VerticalAlignment.Center:
                    Top = r.Height / 2 - Height / 2;
                    break;

                case VerticalAlignment.Bottom:
                    Top = r.Height - Height;
                    break;

                case VerticalAlignment.Stretch:
                    Top = 0;
                    Height = SystemParameters.PrimaryScreenHeight;
                    break;
            }
        }
    }
}
