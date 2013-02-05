using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ChineseWriter {

    public partial class EditWordWindow : Window {

        public EditWordWindow( Word wordToEdit ) {
            InitializeComponent( );
            HanyuBox.Text = wordToEdit.Hanyu;
            PinyinBox.Text = wordToEdit.Pinyin;
            EnglishBox.Text = wordToEdit.English;
        }

        private void Ok_Click( object sender, RoutedEventArgs e ) {
            this.DialogResult = true;
            this.Close( );
        }

        public KnownHanyu NewWord {
            get {
                return new KnownHanyu( HanyuBox.Text, PinyinBox.Text, EnglishBox.Text);
            }
        }

        private void HanyuChanged( object sender, TextChangedEventArgs e ) {
            TextChanged( sender, e );
        }

        private void TextChanged( object sender, TextChangedEventArgs e ) {
            OkButton.IsEnabled = ValidationErrors == null;
            ErrorMessage.Content = ValidationErrors;
        }

        private string ValidationErrors {
            get {
                if (PinyinBox.Text == "") return "Pinyin must not be empty";
                if (HanyuBox.Text == "") return "Hanyu must not be empty";
                if (EnglishBox.Text == "") return "English must not be empty";
                return null;
            }
        }

        private void Lookup_Click( object sender, RoutedEventArgs e ) {
            Process.Start( "http://translate.google.com/#zh-CN/en/" + HanyuBox.Text );
            Process.Start( "http://www.mdbg.net/chindict/chindict.php?page=worddict&wdrst=0&wdqb=" + HanyuBox.Text );
        }

    }
}
