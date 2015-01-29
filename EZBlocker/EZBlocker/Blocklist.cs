using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EZBlocker
{
    public partial class Blocklist : Form
    {
        public Blocklist()
        {
            InitializeComponent();
        }

        private void Blocklist_Load(object sender, EventArgs e)
        {
            try
            {
                foreach (var line in File.ReadAllLines("blocklist.txt").Distinct().Reverse())
                {
                    AdsList.Items.Add(line);
                }
            }
            catch (Exception ee)
            {
                Console.WriteLine(ee);
            }
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            AdsList.Items.Remove(AdsList.SelectedItem);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                var ads = new string[AdsList.Items.Count];
                for (var i = 0; i < AdsList.Items.Count; i++)
                {
                    ads[i] = AdsList.Items[i].ToString();
                }
                File.WriteAllLines("blocklist.txt", ads);
            }
            catch (Exception ee)
            {
                Console.WriteLine(ee);
            }
            Close();
        }
    }
}