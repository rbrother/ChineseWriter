using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ChineseWriter {

    public partial class InputDialog : Window {

        public InputDialog() {
            InitializeComponent();
        }

        public static string AskInput(Window parent = null, string title = "Input", string prompt = "Give a value", string initialValue = "") {
            var dialog = new InputDialog();
            dialog.Owner = parent;
            dialog.Title = title;
            dialog.PromptLabel.Content = prompt;
            dialog.InputBox.Text = initialValue;
            var result = dialog.ShowDialog();
            return (result.HasValue && result.Value) ? dialog.InputBox.Text : null;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }
    }
}
