using InfluxShared.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Influx.Shared.Helpers
{
    public static class FormHelper
    {
        public static async Task<DialogResult> ShowDialogAsync(this Form @this, IWin32Window Owner = null)
        {
            await Task.Yield();
            if (@this.IsDisposed)
                return DialogResult.OK;
            return @this.ShowDialog(Owner is null ? Application.OpenForms[0] : Owner);
        }

        public static void ShowAtPoint(this Form frm, Point Location, bool AdjustBoundsToScreen = false)
        {
            const int minOffset = 10;

            if (AdjustBoundsToScreen)
            {
                Rectangle wa = Screen.FromControl(Application.OpenForms[0]).WorkingArea;
                wa.Inflate(-minOffset, -minOffset);
                wa.Width -= frm.Size.Width;
                wa.Height -= frm.Size.Height;
                frm.Location = new Point(
                    Location.X.Clamp(wa.Left, wa.Right),
                    Location.Y.Clamp(wa.Top, wa.Bottom)
                );
            }
            else
                frm.Location = Location;
            frm.Show();
        }

        public static IEnumerable<object> GetAllControls<T>(this Control control)
        {
            var controls = control.Controls.Cast<Control>();

            return controls.SelectMany(ctrl => ctrl.GetAllControls<T>()
                                      .Concat(controls)
                                      .Where(c => c.GetType() == typeof(T)));
        }

        public static IEnumerable<object> GetAllControls(this Control control, string TypeName)
        {
            var controls = control.Controls.Cast<Control>();

            return controls.SelectMany(ctrl => ctrl.GetAllControls(TypeName)
                                      .Concat(controls)
                                      .Where(c => c.GetType().ToString().Contains(TypeName)));
        }

        public static void PerformSafely(this Control target, Action action)
        {
            if (target.InvokeRequired)
                target.BeginInvoke(action);
            else
                action();
        }

        public static void PerformSafely<T1>(this Control target, Action<T1> action, T1 parameter)
        {
            if (target.InvokeRequired)
                target.BeginInvoke(action, parameter);
            else
                action(parameter);
        }

        public static void PerformSafely<T1, T2>(this Control target, Action<T1, T2> action, T1 p1, T2 p2)
        {
            if (target.InvokeRequired)
                target.BeginInvoke(action, p1, p2);
            else
                action(p1, p2);
        }

        public static string SelectedFilter(this FileDialog dlg)
        {
            string[] ftypes = dlg.Filter.Split('|');
            return String.Join("|", ftypes.Skip((dlg.FilterIndex - 1) * 2).Take(2));
        }
    }
}
