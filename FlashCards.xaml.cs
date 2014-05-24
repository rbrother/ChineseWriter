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

    public partial class FlashCards : Window {

        private Word _word;

        public FlashCards( ) {
            InitializeComponent( );
            NextCard( );
        }

        public void NextCard( ) {
            var props = WordDatabase.GetRandomWord( );
            HanyuBox.Child = new WordPanel( props, showEnglish: false );
            _word = new Word( props );
            EnglishBox.Text = "";
            KnownLevel.Content = WordDatabase.KNOWLEDGE_LEVEL_DESCR[_word.KnownLevel];
            Source.Content = _word.Source;
        }

        private void Check_Click( object sender, RoutedEventArgs e ) {
            EnglishBox.Text = _word.English;
        }

        private void Correct_Click( object sender, RoutedEventArgs e ) {
            NextCard( );
        }

        private void Uncorrect_Click( object sender, RoutedEventArgs e ) {
            NextCard( );
        }
    }
}
