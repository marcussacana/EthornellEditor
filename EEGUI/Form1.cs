using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EthornellEditor;

namespace EEGUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            MessageBox.Show("This don't is a stable translation tool, this program is a Demo for my dll, the \"EthornellEditor.dll\" it's a opensoruce project to allow you make your program to edit any v1 BGI engine script, and don't support BSE scripts.\n\nHow to use:\n*Rigth Click in the window to open or save the file\n*Select the string in listbox and edit in the text box\n*Press enter to update the string\n\nThis program is unstable!");
        }
        public BurikoScript script = new BurikoScript();
        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All Files | *.*";
            ofd.Title = "Select a Ethornel Buriko General Interpreter Script File";
            DialogResult dr = ofd.ShowDialog();
            if (dr == DialogResult.OK)
            {
                listBox1.Items.Clear();
                script.Import(System.IO.File.ReadAllBytes(ofd.FileName));
                foreach (string str in script.strings)
                {
                    listBox1.Items.Add(str);
                }
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                textBox1.Text = script.strings[listBox1.SelectedIndex];
            }
            catch { }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {

            if (e.KeyChar == '\r' || e.KeyChar == '\n')
            {
                try
                {
                    script.strings[listBox1.SelectedIndex] = textBox1.Text;
                    listBox1.Items[listBox1.SelectedIndex] = textBox1.Text;
                }
                catch { }
            }
        }

        private void saveFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "All Files | *.*";
            sfd.Title = "Save a Ethornel Buriko General Interpreter Script File";
            DialogResult dr = sfd.ShowDialog();
            if (dr == DialogResult.OK)
            {
                System.IO.File.WriteAllBytes(sfd.FileName, script.Export());
            }
        }
    }
}