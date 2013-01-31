using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

        private void HanyuChanged( object sender, TextChangedEventArgs e ) {
            var existingWord =_existingWords.
                FirstOrDefault( word => word.hanyu == HanyuBox.Text );
            if (!existingWord.IsEmpty) {
                PinyinBox.Text = existingWord.pinyin;
                EnglishBox.Text = existingWord.english;
                this.Title = "Modify existing word";
            } else {
                this.Title = "Add new word";
            }
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

    }
}
