using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Reactive.Linq;
using System.Threading;
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
            InitializeComponent( );
            System.Environment.SetEnvironmentVariable( "CLOJURE_LOAD_PATH",
                @"C:/Google Drive/programs/clojure-clr;c:/github/ChineseWriter/Clojure" );
            try {
                _writingState = new WritingState( );

                _pinyinInput = new TextBox { Style = GuiUtils.PinyinStyle };
                _pinyinInput.TextChanged += new TextChangedEventHandler( PinyinInput_TextChanged );
                _pinyinInput.KeyUp += new KeyEventHandler( PinyinInput_KeyUp );

                _cursorPanel = CreateCursorPanel( );

                var ControlKeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( _pinyinInput, "KeyUp" ).
                    Where( args => args.EventArgs.KeyboardDevice.Modifiers.HasFlag( ModifierKeys.Control ) ).
                    Select( args => args.EventArgs.Key );

                ControlKeyPresses.Where( key => TEXT_EDIT_KEYS.Contains( key ) ).
                    Subscribe( key => _writingState.TextEdit( key ) );

                ControlKeyPresses.Where( key => DECIMAL_KEYS.Contains( key ) ).
                    Select( key => Array.IndexOf<Key>( DECIMAL_KEYS, key ) ).
                    Subscribe( pinyinIndex => SelectPinyin( pinyinIndex + 1 ) );

                GuiUtils.CheckBoxChangeObservable( ShowEnglish ).
                    Subscribe( value => _writingState.English = value );

                // Update UI based on writing state changes
                _writingState.PinyinChanges.ObserveOnDispatcher( ).
                    Subscribe( pinyin => _pinyinInput.Text = pinyin );
                _writingState.SuggestionsChanges.ObserveOnDispatcher( ).
                    Subscribe( suggestions => UpdateSuggestions( suggestions ) );
                _writingState.CursorPosChanges.
                    ObserveOnDispatcher( ).
                    Subscribe( cursor => ScrollInputVisible( ) );
                _writingState.WordsChanges.
                    CombineLatest( _writingState.CursorPosChanges, ( words, cursor ) => Tuple.Create( words, cursor ) ).
                    ObserveOnDispatcher( ).Subscribe( tuple => PopulateCharGrid( tuple.Item1, tuple.Item2 ) );

                _writingState.Clear( );
                PopulateCharGrid( _writingState.Words, _writingState.CursorPos );
                _pinyinInput.Focus( );

            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
            }
        }

        private void Window_Loaded( object sender, RoutedEventArgs e ) {
            ThreadPool.QueueUserWorkItem( state => {
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                RT.load( "WordDatabase" );
                WordDatabase.LoadWords( );            
            } );
            ThreadPool.QueueUserWorkItem( state => ReportLoadStateTask( ) );
        }

        private void ReportLoadStateTask( ) {
            var start = DateTime.Now;
            SetTitleThreadsafe("Loading Clojure runtime...");
            while (true) {
                string status;
                if (RT.var( "WordDatabase", "all-words").isBound) {
                    var wordsList = (IList<object>) ((clojure.lang.Atom)RT.var( "WordDatabase", "all-words" ).deref( )).deref();
                    var dict = (IDictionary<object,object>) ((clojure.lang.Atom) RT.var( "WordDatabase", "word-dict" ).deref( )).deref();
                    status = wordsList.Count == 0 ? "Loading word list..." :
                        wordsList.Count < 10000 ? "Short word list loaded (start writing!), loading full dictionary..." :
                        string.Format("ChineseWriter, {0} words. All ready", wordsList.Count);
                } else {
                    status = "Loading Clojure runtime...";
                }
                var dur = DateTime.Now - start;
                SetTitleThreadsafe( string.Format("{0} {1:0.0} s", status, dur.TotalSeconds ) );
                if (status.StartsWith( "ChineseWriter" )) return;
                Thread.Sleep(100);
            }
        }

        private void SetTitleThreadsafe( string title ) {
            this.Dispatcher.Invoke( new Action( ( ) => { this.Title = title; } ) );
        }

        private int CurrentUpdater = 0;
        private int ActiveUpdaters = 0;

        private void UpdateSuggestions( IEnumerable<IDictionary<object,object>> suggestions ) {
            ThreadPool.QueueUserWorkItem(
                state => UpdateSuggestionsBackground( (IEnumerable<IDictionary<object,object>>) state ), suggestions );       
        }

        private void UpdateSuggestionsBackground( IEnumerable<IDictionary<object,object>> suggestions ) {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            CurrentUpdater++;
            var id = CurrentUpdater;
            ActiveUpdaters++;
            try {
                while (ActiveUpdaters > 1) {
                    Thread.Sleep( 10 );
                    if (id != CurrentUpdater) return;
                }
                this.Dispatcher.Invoke( new Action( ( ) => {
                    ProcessingLabel.Content = "Searching dictionary...";
                    ProcessingLabel.Foreground = new SolidColorBrush( Colors.Red );
                    Suggestions.Items.Clear( );
                } ), TimeSpan.FromSeconds( 0.5 ), DispatcherPriority.Background );
                var index = 1;
                foreach (var suggestion in suggestions) {
                    if (id != CurrentUpdater) return;
                    var shortcut = index == 1 ? "Enter" :
                        index <= 10 ? string.Format( "CTRL+{0}", index - 1 ) : "<click>";
                    var dataWord = suggestion.ToDataTableWord( shortcut );
                    this.Dispatcher.Invoke( new Action( ( ) => Suggestions.Items.Add( dataWord ) ), TimeSpan.FromSeconds( 0.5 ), DispatcherPriority.Background );
                    index++;
                }
                this.Dispatcher.Invoke( new Action( ( ) => {
                    ProcessingLabel.Foreground = new SolidColorBrush( Colors.Black );
                    ProcessingLabel.Content = string.Format( "{0} suggestions", Suggestions.Items.Count );
                } ) );
            } finally {
                ActiveUpdaters--;
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

        void ScrollInputVisible( ) {
            if (_cursorPanel.Parent != null) {
                TextScrollView.UpdateLayout( );
                var maxScrollPos = TextScrollView.ExtentWidth - TextScrollView.ViewportWidth;
                var scrollTo = TextScrollView.HorizontalOffset -
                    TextScrollView.TransformToVisual( _cursorPanel ).Transform( new Point( 0, 0 ) ).X -
                    TextScrollView.ViewportWidth * 0.5;
                if (scrollTo < 0) scrollTo = 0;
                if (scrollTo > maxScrollPos) scrollTo = maxScrollPos;
                TextScrollView.ScrollToHorizontalOffset( scrollTo );
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

        private void PopulateCharGrid( IEnumerable<IDictionary<object,object>> words, int cursorPos ) {
            Characters.Children.Clear( );
            int pos = 0;
            foreach (IDictionary<object,object> word in _writingState.Words) {
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
            var word = widget.Tag as IDictionary<object,object>;
            if (word != null) {
                var editWord = new EditWord( word );
                var result = editWord.ShowDialog( );
                if (result.HasValue && result.Value) {
                    WordDatabase.SetWordInfo( word, editWord.ShortEnglishBox.Text, 
                        editWord.Known.IsChecked.HasValue && editWord.Known.IsChecked.Value);
                    _writingState.RefreshInfo( );
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

        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e ) {
            WordDatabase.SaveWordsInfo( );
        }

        private void Suggestions_SelectedCellsChanged( object sender, SelectedCellsChangedEventArgs e ) {
            if (e.AddedCells.Count( ) > 0) {
                var suggestionWord = (SuggestionWord)e.AddedCells.First( ).Item;
                _writingState.SelectWord( suggestionWord.Word );
            }
        }

        private void StayOnTop_Checked( object sender, RoutedEventArgs e ) {
            this.Topmost = StayOnTop.IsChecked ?? false;
        }

    } // class

} // namespace
