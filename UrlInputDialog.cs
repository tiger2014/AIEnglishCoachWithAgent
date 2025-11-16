using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AIEnglishCoachWithAgent
{
    public partial class UrlInputDialog : Form
    {

        public string urlTxt = "";
        public UrlInputDialog()
        {
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            urlTxt = "";
        }

        private async void btnOk_Click(object sender, EventArgs e)
        {
            urlTxt = txtUrl.Text.Trim();

            //var client = new HttpClient();
            //var responseStr = await client.GetStringAsync(@"https://www.bbc.com/news/articles/c231e284345o");
            //Debug.WriteLine(responseStr);

        }
    }
}
