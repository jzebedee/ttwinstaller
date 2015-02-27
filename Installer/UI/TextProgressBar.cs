using System;
using System.Drawing;
using System.Windows.Forms;

namespace TaleOfTwoWastelands.UI
{
	//thanks Barry
	//http://stackoverflow.com/a/3529945
	sealed class TextProgressBar : ProgressBar
    {
        //Property to hold the custom text
        private string _customText;
        public string CustomText
        {
            get
            {
                return _customText;
            }
            set
            {
                if (_customText != value)
                {
                    _customText = value;
                    Invalidate();
                }
            }
        }

        private float ValueF
        {
            get { return Value; }
        }

        public TextProgressBar()
        {
            // Modify the ControlStyles flags
            //http://msdn.microsoft.com/en-us/library/system.windows.forms.controlstyles.aspx
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            rect.Inflate(-3, -3);

            var g = e.Graphics;

            var clip = new Rectangle(rect.X, rect.Y, (int)Math.Round((ValueF / Maximum) * rect.Width), rect.Height);
            if (ProgressBarRenderer.IsSupported)
            {
                ProgressBarRenderer.DrawHorizontalBar(g, rect);
                ProgressBarRenderer.DrawHorizontalChunks(g, clip);
            }
            else
            {
                g.FillRegion(Brushes.ForestGreen, new Region(clip));
            }

            var len = g.MeasureString(CustomText, Font);

            // Calculate the location of the text (the middle of progress bar)
            var location = new Point((int)((rect.Width - len.Width) / 2), (int)((rect.Height - len.Height) / 2));
            // Draw the custom text
            g.DrawString(CustomText, Font, SystemBrushes.InfoText, location);
        }
    }
}