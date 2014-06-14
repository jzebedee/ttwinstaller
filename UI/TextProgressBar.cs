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
        const int WS_EX_COMPOSITED = 0x02000000;

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
        {
            // Modify the ControlStyles flags
            //http://msdn.microsoft.com/en-us/library/system.windows.forms.controlstyles.aspx
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cParams = base.CreateParams;
                cParams.ExStyle |= WS_EX_COMPOSITED;

                return cParams;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rect = ClientRectangle;
            Graphics g = e.Graphics;

            ProgressBarRenderer.DrawHorizontalBar(g, rect);
            rect.Inflate(-3, -3);
            if (Value > 0)
            {
                // As we doing this ourselves we need to draw the chunks on the progress bar
                Rectangle clip = new Rectangle(rect.X, rect.Y, (int)Math.Round(((float)Value / Maximum) * rect.Width), rect.Height);
                ProgressBarRenderer.DrawHorizontalChunks(g, clip);
            }

            // Set the Display text (Either a % amount or our custom text
            string text = DisplayStyle == ProgressBarDisplayText.Percentage ? Value.ToString() + '%' : CustomText;
            using (Font f = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold))
            {
                SizeF len = g.MeasureString(text, f);
                // Calculate the location of the text (the middle of progress bar)
                Point location = new Point(Convert.ToInt32((rect.Width - len.Width) / 2), Convert.ToInt32((rect.Height - len.Height) / 2));
                // Draw the custom text
                g.DrawString(text, f, SystemBrushes.InfoText, location);
            }
        }
    }
}