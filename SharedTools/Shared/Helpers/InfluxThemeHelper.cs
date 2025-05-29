using Influx.Shared.Objects;
using Syncfusion.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Influx.Shared.Helpers
{
    public static class InfluxThemeHelper
    {
        public static Color SelectBackColor;
        public static Color HoverBackColor;
        public static Color FormCaptionBackColor;
        public static Color FormCaptionControlColor;
        public static Color FormCaptionControlHoverColor;
        public static Color FormBackgroundColor;
        public static Color FormForeColor;
        public static Color MetroColor;

        public static InfluxToolStripButtonRenderer ToolStripRenderer;

        public static void InitTheme()
        {
            // Dialog like
            /*SelectBackColor = Color.FromArgb(0xff, 0xb4, 0xde, 0xdb);
            HoverBackColor = Color.FromArgb(0xff, 0xb4, 0xde, 0xdb);
            FormCaptionBackColor = Color.FromArgb(0xff, 41, 73, 95);
            FormCaptionControlColor = Color.WhiteSmoke;
            FormCaptionControlHoverColor = Color.White;
            MetroColor = Color.LightSeaGreen;*/

            // Rex
            SelectBackColor = Color.FromArgb(0xff, 0xb4, 0xde, 0xdb);
            HoverBackColor = Color.FromArgb(0xff, 0xb4, 0xde, 0xdb);
            FormCaptionBackColor = Color.LightSeaGreen;
            FormCaptionControlColor = Color.WhiteSmoke;
            FormCaptionControlHoverColor = Color.White;
            FormBackgroundColor = Color.White;
            FormForeColor = Color.Black;
            MetroColor = Color.LightSeaGreen;

            ToolStripRenderer = new InfluxToolStripButtonRenderer(new Pen(MetroColor), new SolidBrush(HoverBackColor), new SolidBrush(SelectBackColor));
        }

        static void OnFormResize(object Sender, EventArgs e)
        {
            var frm = Sender as MetroForm;
            var toolstrips = frm.GetAllControls<ToolStripEx>();
            foreach (ToolStripEx ts in toolstrips)
            {
                int totalWidth = 30;

                foreach (ToolStripItem item in ts.Items)
                    if (item.Visible || item.Placement == ToolStripItemPlacement.Overflow)
                        if (item is ToolStripTextBox)
                            totalWidth += item.Width;
                        else
                            totalWidth += TextRenderer.MeasureText(item.Text, item.Font).Width + (item.Image?.Width ?? 0) + item.Padding.Horizontal;

                bool FullStyle = totalWidth < ts.Parent.Width;
                foreach (ToolStripItem item in ts.Items)
                    if (item is ToolStripButton && (item.Visible || item.Placement == ToolStripItemPlacement.Overflow))
                        if (FullStyle)
                        {
                            if (item.Image is null)
                                item.DisplayStyle = ToolStripItemDisplayStyle.Text;
                            else if (item.Text == "")
                                item.DisplayStyle = ToolStripItemDisplayStyle.Image;
                            else
                                item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                        }
                        else
                        {
                            if (item.Image is null)
                                item.DisplayStyle = ToolStripItemDisplayStyle.Text;
                            else if (item.Text == "")
                                item.DisplayStyle = ToolStripItemDisplayStyle.Image;
                        }
            }
        }

        public static void ThemeUpdate(this MetroForm frm)
        {
            frm.AutoScaleMode = AutoScaleMode.None;
            frm.MetroColor = MetroColor;
            frm.BackColor = FormBackgroundColor;
            frm.BorderColor = FormCaptionBackColor;
            frm.CaptionBarColor = FormCaptionBackColor;
            frm.CaptionButtonColor = FormCaptionControlColor;
            frm.CaptionButtonHoverColor = FormCaptionControlHoverColor;
            frm.CaptionForeColor = FormCaptionControlHoverColor;
            //frm.CaptionBarHeight = 35;
            //frm.CaptionVerticalAlignment = VerticalAlignment.Bottom;

            frm.Resize += OnFormResize;
            var toolstrips = frm.GetAllControls<ToolStripEx>();
            foreach (ToolStripEx ts in toolstrips)
            {
                ts.Renderer = ToolStripRenderer;

                var splitbuttons = ts.Items.OfType<ToolStripSplitButton>();
                foreach (ToolStripSplitButton sb in splitbuttons)
                    foreach (ToolStripMenuItem mi in sb.DropDownItems)
                    {
                        //mi.ToolTipText += "Demo";
                    }
            }

            void UpdateGrid(DataGridView grid)
            {
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = FormBackgroundColor;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = FormForeColor;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = FormBackgroundColor;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = FormForeColor;
            }

            var gridlist = frm.GetAllControls("InfluxGridView").ToList();
            foreach (var grid in gridlist)
                UpdateGrid(grid as DataGridView);
            gridlist = frm.GetAllControls<DataGridView>().ToList();
            foreach (var grid in gridlist)
                UpdateGrid(grid as DataGridView);
        }
    }
}
