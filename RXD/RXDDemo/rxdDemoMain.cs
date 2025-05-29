using RXD.Base;
using RXD.Blocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace RXDDemo
{
    public partial class rxdDemoMain : Form
    {
        BinRXD rxd = null;

        public rxdDemoMain()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (dlgOpen.ShowDialog() != DialogResult.OK)
                return;

            listBox1.Items.Clear();
            rxd = BinRXD.Load(dlgOpen.FileName);

            listBox1.Items.Add(rxd.Config);
            if (rxd.ConfigFTP is not null)
                listBox1.Items.Add(rxd.ConfigFTP);
            if (rxd.ConfigMobile is not null)
                listBox1.Items.Add(rxd.ConfigMobile);
            if (rxd.ConfigS3 is not null)
                listBox1.Items.Add(rxd.ConfigS3);
            if (rxd.ConfigWiFi is not null)
                listBox1.Items.Add(rxd.ConfigWiFi);
            foreach (KeyValuePair<Int64, BinBase> bin in rxd)
                listBox1.Items.Add(bin.Value);
        }

        private void listBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            richTextBox1.Clear();
            BinBase bin = (BinBase)listBox1.SelectedItem;

            richTextBox1.AppendText("--- Block Headers ---" + Environment.NewLine + Environment.NewLine);
            richTextBox1.AppendText("Type: " + bin.header.type.ToString() + Environment.NewLine);
            richTextBox1.AppendText("Version: " + bin.header.version.ToString() + Environment.NewLine);
            richTextBox1.AppendText("Length: " + bin.header.length.ToString() + Environment.NewLine);
            richTextBox1.AppendText("UID: " + bin.header.uniqueid.ToString() + Environment.NewLine);

            richTextBox1.AppendText(Environment.NewLine + "--- Block Properties ---" + Environment.NewLine + Environment.NewLine);
            foreach (KeyValuePair<string, PropertyData> prop in bin.data)
                richTextBox1.AppendText(
                    prop.Value.Name + " (" + prop.Value.PropType + "): " +
                    (prop.Value.PropType.IsArray ? string.Join(",", prop.Value.Value) : prop.Value.Value.ToString()) +
                    Environment.NewLine);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (rxd is null)
                return;
            dlgSaveXML.FileName = Path.GetFileNameWithoutExtension(rxd.rxdUri);
            if (dlgSaveXML.ShowDialog() != DialogResult.OK)
                return;

            if (rxd.ToXML(dlgSaveXML.FileName))
                MessageBox.Show("success");
            else
                MessageBox.Show("failed");
        }

        private void btnOpenXML_Click(object sender, EventArgs e)
        {
            if (dlgOpenXml.ShowDialog() != DialogResult.OK)
                return;

            listBox1.Items.Clear();
            rxd = BinRXD.Load(dlgOpenXml.FileName);
            if (rxd.Error == "")
            {
                listBox1.Items.Add(rxd.Config);
                if (rxd.ConfigFTP is not null)
                    listBox1.Items.Add(rxd.ConfigFTP);
                if (rxd.ConfigMobile is not null)
                    listBox1.Items.Add(rxd.ConfigMobile);
                if (rxd.ConfigS3 is not null)
                    listBox1.Items.Add(rxd.ConfigS3);
                if (rxd.ConfigWiFi is not null)
                    listBox1.Items.Add(rxd.ConfigWiFi);
                foreach (KeyValuePair<Int64, BinBase> bin in rxd)
                    listBox1.Items.Add(bin.Value);
            }
            else
                MessageBox.Show(rxd.Error);

            if (dlgSaveRXC.ShowDialog() == DialogResult.OK)
                rxd.ToRXD(dlgSaveRXC.FileName);
        }

        private void btnConvert_Click(object sender, EventArgs e)
        {
            /* using (OpenFileDialog dlg = new OpenFileDialog()
             {
                 Filter = string.Join("|", new string[] { DoubleDataCollection.Filter, Matlab.Filter, ASC.Filter, BLF.Filter, TRC.Filter, MDF.Filter, BinRXD.Filter, XmlHandler.Filter }),
                 FilterIndex = 1,
                 Title = "Export datalog",
                 FileName = Path.GetFileNameWithoutExtension(r.rxdUri)
             })
             {

             }*/
        }
    }
}
