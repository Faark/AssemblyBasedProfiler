using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleTree
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private static int trimFront(ref string line)
        {
            var cnt = 0;
            while (line.Length -1 > cnt && line.ElementAt(cnt) == ' ')
            {
                cnt++;
            }
            line = line.Trim();
            return cnt;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                treeView1.SuspendLayout();
                var data = System.IO.File.ReadAllLines(openFileDialog1.FileName);
                var stack = new Stack<Tuple<int, TreeNodeCollection>>();
                stack.Push(Tuple.Create(-1, treeView1.Nodes));
                foreach (var d in data)
                {
                    var line = d;
                    var pos = trimFront(ref line);
                    var peek = stack.Peek();
                    while (peek.Item1 >= 0 && pos <= peek.Item1)
                    {
                        stack.Pop();
                        peek = stack.Peek();
                    }
                    var node = new TreeNode(line);
                    peek.Item2.Add(node);
                    stack.Push(Tuple.Create(pos, node.Nodes));
                }
                treeView1.ResumeLayout();
                Text = new System.IO.FileInfo(openFileDialog1.FileName).Name + " - SimpleTreeViewer";
                WindowState = FormWindowState.Minimized;
                WindowState = FormWindowState.Normal;
                treeView1.Focus();
            }
            else
            {
                Close();
            }
        }
    }
}
