using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Reactive.Linq;
using RT = clojure.lang.RT;
using Keyword = clojure.lang.Keyword;

namespace ChineseWriter {

    public partial class ChineseWriterWindow : Window {

        private WritingState _writingState;
        private TextBox _pinyinInput;
        private FrameworkElement _cursorPanel;

        private Key[] TEXT_EDIT_KEYS = new Key[] { Key.Back, Key.Delete, Key.Left, Key.Right, Key.Home, Key.End };
        private Key[] DECIMAL_KEYS = new Key[] { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };

        public ChineseWriterWindow( ) {
            try {
                InitializeComponent( );

                System.Environment.SetEnvironmentVariable( "CLOJURE_LOAD_PATH",
                    @"C:/Google Drive/programs/clojure-clr;c:/github/ChineseWriter/Clojure" );
                RT.load( "WordDatabase" );

                var startTime = DateTime.Now;
                WordDatabase.LoadWords( );
                var elapsed = DateTime.Now - startTime;
                this.Title = string.Format( "Loaded {0:0.0} s", elapsed.TotalSeconds );

                _writingState = new WritingState( );

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

                // Update UI based on writing state changes
                _writingState.PinyinChanges.ObserveOnDispatcher( ).
                    Subscribe( pinyin => _pinyinInput.Text = pinyin );
                _writingState.SuggestionsChanges.ObserveOnDispatcher( ).
                    Subscribe( suggestions => UpdateSuggestions( suggestions) );
                _writingState.CursorPosChanges.
                    ObserveOnDispatcher().
                    Subscribe( cursor => ScrollInputVisible() );
                _writingState.WordsChanges.
                    CombineLatest( _writingState.CursorPosChanges, ( words, cursor ) => Tuple.Create( words, cursor ) ).
                    ObserveOnDispatcher().Subscribe( tuple => PopulateCharGrid( tuple.Item1, tuple.Item2 ));

                _writingState.Clear( );
                PopulateCharGrid( _writingState.Words, _writingState.CursorPos );
            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
                this.Close( );
            }
        }

        private void UpdateSuggestions( IList<IDictionary<string,object>> suggestions ) {
            Suggestions.ItemsSource = suggestions.Select( suggestion =>
                new SuggestionWord {
                    Pinyin = ( suggestion.Pinyin() ).AddDiacritics( ),
                    Hanyu = suggestion.Hanyu(),
                    English = suggestion.GetStr("english"),
                    UsageCountString = suggestion.ContainsKey( "usage-count" ) ?
                        Convert.ToString( suggestion["usage-count"] ) : ""
                } );
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

        void ScrollInputVisible( ) {
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

        private void PopulateCharGrid( IEnumerable<IDictionary<string,object>> words, int cursorPos ) {
            Characters.Children.Clear( );
            int pos = 0;
            foreach (IDictionary<string,object> word in _writingState.Words) {
                if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
                var wordPanel = WordPanel.Create( word );
                Characters.Children.Add( wordPanel );
                wordPanel.Tag = word;
                wordPanel.MouseUp += new MouseButtonEventHandler( HanyuPanelMouseUp );
                pos++;
            }
            if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
            _pinyinInput.Focus( );
        }

        void HanyuPanelMouseUp( object sender, MouseButtonEventArgs e ) {
            var widget = (FrameworkElement)sender;
            var word = widget.Tag as IDictionary<string,object>;
            if (word != null) {
                var editWord = new EditWord( word );
                var result = editWord.ShowDialog( );
                if (result.HasValue && result.Value) {
                    WordDatabase.SetShortEnglish( word, editWord.ShortEnglishBox.Text );
                    if (editWord.Known.IsChecked.HasValue && editWord.Known.IsChecked.Value) {
                        WordDatabase.SetWordKnown( word );
                    }
                    PopulateCharGrid( _writingState.Words, _writingState.CursorPos );
                }
            }
        }

        private void Copy_Plain_Click( object sender, RoutedEventArgs e ) {
            Clipboard.SetText( _writingState.HanyiPinyinLines, TextDataFormat.UnicodeText );
        }

        private void Copy_Chinese_Click( object sender, RoutedEventArgs e ) {
            ClipboardTool.CopyToClipboard( _writingState.Html, new Uri( "http://www.brotherus.net" ) );
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
            //throw new NotImplementedException( );
            //_wordDatabase.SaveWordsInfo( );
        }

        private void Suggestions_SelectedCellsChanged( object sender, SelectedCellsChangedEventArgs e ) {
            if (e.AddedCells.Count( ) > 0) {
                _writingState.SelectWord( (IDictionary<string,object>) e.AddedCells.First( ).Item );
            }
        }

    } // class

} // namespace
