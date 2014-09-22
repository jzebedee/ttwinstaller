using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TaleOfTwoWastelands.UI
{
    public enum ProgressBarDisplayText
    {
        Percentage,
        CustomText
    }

    //thanks Barry
    //http://stackoverflow.com/a/3529945
    class TextProgressBar : ProgressBar
    {
        //Property to set to decide whether to print a % or Text
        public ProgressBarDisplayText DisplayStyle { get; set; }

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

        public TextProgressBar()
            : base()
        {
            // Modify the ControlStyles flags
            //http://msdn.microsoft.com/en-us/library/system.windows.forms.controlstyles.aspx
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rect = ClientRectangle;
            rect.Inflate(-3, -3);

            Graphics g = e.Graphics;

            if (ProgressBarRenderer.IsSupported)
            {
                ProgressBarRenderer.DrawHorizontalBar(g, rect);
                if (Value > 0)
                {
                    // As we doing this ourselves we need to draw the chunks on the progress bar
                    Rectangle clip = new Rectangle(rect.X, rect.Y, (int)Math.Round(((float)Value / Maximum) * rect.Width), rect.Height);
                    ProgressBarRenderer.DrawHorizontalChunks(g, clip);
                }
            }
            else
            {
                if (Value > 0)
                {
                    Rectangle clip = new Rectangle(rect.X, rect.Y, (int)Math.Round(((float)Value / Maximum) * rect.Width), rect.Height);
                    g.FillRegion(Brushes.ForestGreen, new Region(clip));
                }
            }

            // Set the Display text (Either a % amount or our custom text
            string text = DisplayStyle == ProgressBarDisplayText.Percentage ? Value + "%" : CustomText;

            SizeF len = g.MeasureString(text, Font);

            // Calculate the location of the text (the middle of progress bar)
            Point location = new Point(Convert.ToInt32((rect.Width - len.Width) / 2), Convert.ToInt32((rect.Height - len.Height) / 2));
            // Draw the custom text
            g.DrawString(text, Font, SystemBrushes.InfoText, location);
        }
    }
}