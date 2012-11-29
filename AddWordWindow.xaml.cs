using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ChineseWriter {
    /// <summary>
    /// Interaction logic for AddWordWindow.xaml
    /// </summary>
    public partial class AddWordWindow : Window {

        private IEnumerable<ChineseWordInfo> _existingWords;

        /// <param name="existingWords">Existing words to use for validating the new one is not
        /// in the database already</param>
        public AddWordWindow( IEnumerable<ChineseWordInfo> existingWords ) {
            InitializeComponent( );
            _existingWords = existingWords;
        }

        private void Ok_Click( object sender, RoutedEventArgs e ) {
            this.DialogResult = true;
            this.Close( );
        }

        public ChineseWordInfo NewWord {
            get {
                return new ChineseWordInfo {
                    pinyin = PinyinBox.Text,
                    hanyu = HanyuBox.Text,
                    english = EnglishBox.Text
                };
            }
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
                if (_existingWords.Select( word => word.hanyu ).Contains( HanyuBox.Text )) {
                    return "Database already contains " + HanyuBox.Text;
                }
                return null;
            }
        }

    }
}
