using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Reactive.Linq;

namespace ChineseWriter {

    public partial class ChineseWriterWindow : Window {

        private WordDatabase _wordDatabase;
        private WritingState _writingState;
        private TextBox _pinyinInput;
        private FrameworkElement _cursorPanel;

        private Key[] TEXT_EDIT_KEYS = new Key[] { Key.Back, Key.Delete, Key.Left, Key.Right, Key.Home, Key.End };
        private Key[] DECIMAL_KEYS = new Key[] { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };

        public ChineseWriterWindow( ) {
            try {
                InitializeComponent( );

                _wordDatabase = new WordDatabase( );
                _writingState = new WritingState( _wordDatabase );

                _pinyinInput = new TextBox { Style = GuiUtils.PinyinStyle };
                _pinyinInput.TextChanged += new TextChangedEventHandler(PinyinInput_TextChanged);
                _pinyinInput.KeyUp += new KeyEventHandler( PinyinInput_KeyUp );

                _cursorPanel = CreateCursorPanel( );

                var ControlKeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( _pinyinInput, "KeyUp" ).
                    Where( args => args.EventArgs.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control)).
                    Select( args => args.EventArgs.Key );

                ControlKeyPresses.Where( key => TEXT_EDIT_KEYS.Contains(key) ).
                    Subscribe( key => _writingState.TextEdit( key ) );

                ControlKeyPresses.Where( key => DECIMAL_KEYS.Contains(key) ).
                    Select( key => Array.IndexOf<Key>( DECIMAL_KEYS, key ) ).
                    Subscribe( pinyinIndex => SelectPinyin( pinyinIndex + 1 ) );

                GuiUtils.CheckBoxChangeObservable( ShowEnglish ).
                    Subscribe( value => _writingState.English = value );

                var WordsDatabaseChanged = new int[] { 0 }.ToObservable( ).
                    Concat( _wordDatabase.WordsChanged );

                // Update UI based on writing state changes
                _writingState.PinyinChanges.ObserveOnDispatcher( ).
                    Subscribe( pinyin => _pinyinInput.Text = pinyin );
                _writingState.SuggestionsChanges.ObserveOnDispatcher( ).
                    Subscribe( suggestions => Suggestions.ItemsSource = suggestions );
                _writingState.WordsChanges.
                    CombineLatest(_writingState.CursorPosChanges, (words,cursor) => Tuple.Create(words,cursor)).                    
                    ObserveOnDispatcher( ).
                    Subscribe( value => PopulateCharGrid( value.Item1, value.Item2 ) );
                _writingState.CursorPosChanges.
                    ObserveOnDispatcher().
                    Subscribe( cursor => MakeInputVisible() );
                _writingState.Clear( );
            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
                this.Close( );
            }
        }

        private FrameworkElement CreateCursorPanel( ) {
            var cursorBrush = new SolidColorBrush( );
            var panel = GuiUtils.WrapToBorder( new Label {
                Content = _pinyinInput,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = cursorBrush
            } );
            var cursorAnimation = new ColorAnimation( Color.FromRgb( 192, 192, 255 ), Colors.White,
                new Duration( TimeSpan.FromSeconds( 0.5 ) ),
                FillBehavior.HoldEnd ) {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
            cursorBrush.BeginAnimation( SolidColorBrush.ColorProperty, cursorAnimation );
            return panel;
        }

        void MakeInputVisible( ) {
            try {
                TextScrollView.UpdateLayout( );
                var maxScrollPos = TextScrollView.ExtentWidth - TextScrollView.ViewportWidth;
                var scrollTo = TextScrollView.HorizontalOffset - 
                    TextScrollView.TransformToVisual( _cursorPanel ).Transform( new Point( 0, 0 ) ).X -
                    TextScrollView.ViewportWidth * 0.5;
                if (scrollTo < 0) scrollTo = 0;
                if (scrollTo > maxScrollPos) scrollTo = maxScrollPos;
                TextScrollView.ScrollToHorizontalOffset( scrollTo );
            } catch (InvalidOperationException) {
                // TextScrollView.TransformToVisual( _cursorPanel ) fails in startup, works then
            }
        }

        void PinyinInput_KeyUp( object sender, KeyEventArgs e ) {
            if (e.Key == Key.Enter) {
                SelectPinyin(1);
                e.Handled = true;
            }
        }

        private void SelectPinyin(int index) {
            _writingState.SelectPinyin(index);
            ShowEnglish.IsChecked = false;
        }

        void PinyinInput_TextChanged(object sender, TextChangedEventArgs e) {
            if (_pinyinInput.Text != _writingState.PinyinInput) {
                _writingState.PinyinInput = _pinyinInput.Text;
            }
        }

        private void PopulateCharGrid( IEnumerable<Word> words, int cursorPos ) {
            Characters.Children.Clear( );
            int pos = 0;
            foreach (Word word in _writingState.Words) {
                if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
                var wordPanel = 
                    word is HanyuWord ? WordPanel.Create( word as HanyuWord, _wordDatabase ) :
                    word is LiteralWord ? WordPanel.Create( word as LiteralWord, _wordDatabase ) :
                    word is MultiMeaningWord ? WordPanel.Create( word as MultiMeaningWord, _wordDatabase ) :
                    null;
                Characters.Children.Add( wordPanel );
                pos++;
            }
            if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
            _pinyinInput.Focus( );
        }

        private void Copy_Chinese_Click( object sender, RoutedEventArgs e ) {
            try {
                CopyToClipboard( _writingState.HanyiPinyinHtml, "Chinese", new Uri("http://www.brotherus.net"));
            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ) );
            }
        }

        public static void CopyToClipboard( string htmlFragment, string title, Uri sourceUrl ) {
            if (title == null) title = "From Clipboard";

            System.Text.StringBuilder sb = new System.Text.StringBuilder( );

            // Builds the CF_HTML header. See format specification here:
            // http://msdn.microsoft.com/library/default.asp?url=/workshop/networking/clipboard/htmlclipboard.asp

            // The string contains index references to other spots in the string, so we need placeholders so we can compute the offsets. 
            // The <<<<<<<_ strings are just placeholders. We'll backpatch them actual values afterwards.
            // The string layout (<<<) also ensures that it can't appear in the body of the html because the <
            // character must be escaped.
            string header =
    @"Format:HTML Format
Version:1.0
StartHTML:<<<<<<<1
EndHTML:<<<<<<<2
StartFragment:<<<<<<<3
EndFragment:<<<<<<<4
StartSelection:<<<<<<<3
EndSelection:<<<<<<<3
";

            string pre =
    @"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"">
<HTML><HEAD><TITLE>" + title + @"</TITLE></HEAD><BODY><!--StartFragment-->";

            string post = @"<!--EndFragment--></BODY></HTML>";

            sb.Append( header );
            if (sourceUrl != null) {
                sb.AppendFormat( "SourceURL:{0}", sourceUrl );
            }
            int startHTML = sb.Length;

            sb.Append( pre );
            int fragmentStart = sb.Length;

            sb.Append( htmlFragment );
            int fragmentEnd = sb.Length;

            sb.Append( post );
            int endHTML = sb.Length;

            // Backpatch offsets
            sb.Replace( "<<<<<<<1", To8DigitString( startHTML ) );
            sb.Replace( "<<<<<<<2", To8DigitString( endHTML ) );
            sb.Replace( "<<<<<<<3", To8DigitString( fragmentStart ) );
            sb.Replace( "<<<<<<<4", To8DigitString( fragmentEnd ) );


            // Finally copy to clipboard.
            string data = sb.ToString( );
            Clipboard.Clear( );
            Clipboard.SetText( data, TextDataFormat.Html );
        }
        static string To8DigitString( int x ) {
            return String.Format( "{0,8}", x );
        }

        private void Clear_Text_Click( object sender, RoutedEventArgs e ) {
            _writingState.Clear( );
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            _pinyinInput.Focus();
        }

        private void Suggestions_LoadingRow( object sender, DataGridRowEventArgs e ) {
            int n = e.Row.GetIndex( ) + 1;
            e.Row.Header =  n == 1 ? "Enter" :
                n <= 10 ? string.Format( "CTRL+{0}", n - 1 ) : "<click>";
        }

        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e ) {
            _wordDatabase.SaveWordsInfo( );
        }

        private void Suggestions_SelectedCellsChanged( object sender, SelectedCellsChangedEventArgs e ) {
            if (e.AddedCells.Count( ) > 0) {
                _writingState.SelectWord( (HanyuWord)e.AddedCells.First( ).Item );
            }
        }

    } // class

} // namespace
