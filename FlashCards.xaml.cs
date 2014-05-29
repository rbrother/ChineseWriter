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
using Brotherus;

namespace ChineseWriter {

    public partial class FlashCards : Window {

        private Word _word;

        public FlashCards( ) {
            InitializeComponent( );
            NextCard( );
        }

        private FrameworkElement[] HintFields {
            get {
                return ChineseEnglish.IsChecked.Value ?
                    new FrameworkElement[] { HanyuBox, Source } :
                    new FrameworkElement[] { EnglishBox, Picture };
            }
        }

        private FrameworkElement[] AllFields {
            get {
                return new FrameworkElement[] {  HanyuBox, EnglishBox, Picture, KnownLevel, Source };
            }
        }

        private void SetVisibility( FrameworkElement[] widgets, Visibility vis ) {
            foreach ( var widget in widgets ) { widget.Visibility = vis; }
        }

        public void NextCard( ) {
            SetVisibility( AllFields, Visibility.Hidden );
            SetVisibility( HintFields, Visibility.Visible );
            var props = WordDatabase.GetRandomWord( );
            _word = new Word( props );
            HanyuBox.Child = new WordPanel( props, showEnglish: false );
            EnglishBox.Text = _word.English;
            var file = System.IO.Path.Combine( Utils.FindRelativeFile( "images" ), _word.Image + ".jpg" ).Replace("\\","/");
            Picture.Source = new BitmapImage( new Uri( "file://" + file.Replace("\\","/") ) );
            KnownLevel.Content = WordDatabase.KNOWLEDGE_LEVEL_DESCR[_word.KnownLevel];
            Source.Content = _word.Source;
            CheckPanel.Visibility = Visibility.Visible;
            CorrectIncorrectPanel.Visibility = Visibility.Collapsed;
        }

        private void Check_Click( object sender, RoutedEventArgs e ) {
            CheckPanel.Visibility = Visibility.Collapsed;
            CorrectIncorrectPanel.Visibility = Visibility.Visible;
            SetVisibility( AllFields, Visibility.Visible );
        }

        private void Correct_Click( object sender, RoutedEventArgs e ) {
            NextCard( );
        }

        private void Uncorrect_Click( object sender, RoutedEventArgs e ) {
            NextCard( );
        }
    }
}
