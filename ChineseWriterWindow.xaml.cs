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
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using RT = clojure.lang.RT;
using Keyword = clojure.lang.Keyword;

namespace ChineseWriter {

    public partial class ChineseWriterWindow : Window {

        private TextBox _pinyinInput;
        private FrameworkElement _cursorPanel;

        private Key[] TEXT_EDIT_KEYS = new Key[] { Key.Back, Key.Delete, Key.Left, Key.Right, Key.Home, Key.End };
        private Key[] DECIMAL_KEYS = new Key[] { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };

        public ChineseWriterWindow( ) {
            InitializeComponent( );
            System.Environment.SetEnvironmentVariable( "CLOJURE_LOAD_PATH",
                @"C:/Google Drive/programs/clojure-clr;c:/github/ChineseWriter/Clojure" );
            try {
                RT.load( "WritingState" );
                RT.load( "ExportText" );
                RT.load( "ParseChinese" );
                RT.var( "WordDatabase", "set-add-diacritics-func!" ).invoke( StringUtils.AddDiacriticsFunc );

                _pinyinInput = new TextBox { Style = GuiUtils.PinyinStyle };
                _pinyinInput.KeyUp += new KeyEventHandler( PinyinInput_KeyUp );

                _cursorPanel = CreateCursorPanel( );

                var ControlKeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( _pinyinInput, "KeyUp" ).
                    Where( args => args.EventArgs.KeyboardDevice.Modifiers.HasFlag( ModifierKeys.Control ) ).
                    Select( args => args.EventArgs.Key );

                ControlKeyPresses.Where( key => TEXT_EDIT_KEYS.Contains( key ) ).
                    Subscribe( key => TextEdit( key ) );

                ControlKeyPresses.Where( key => DECIMAL_KEYS.Contains( key ) ).
                    Select( key => Array.IndexOf<Key>( DECIMAL_KEYS, key ) ).
                    Subscribe( pinyinIndex => SelectSuggestionIndex( pinyinIndex ) );

                var PinyinChanges = Observable.
                    FromEventPattern<TextChangedEventArgs>( _pinyinInput, "TextChanged" ).
                    ObserveOnDispatcher().
                    Select( args => ((TextBox) args.Sender).Text );
                // Update UI based on writing state changes
                GuiUtils.CheckBoxChangeObservable( ShowEnglish ).
                    CombineLatest( PinyinChanges, (english,input) => Tuple.Create(english,input) ).
                    ObserveOnDispatcher( ).
                    Subscribe( tuple => UpdateSuggestions( WordDatabase.Suggestions( tuple.Item2, tuple.Item1 ) ) );
                WritingState.WordsChanges.
                    ObserveOnDispatcher( ).Subscribe( words => PopulateCharGrid( words, WritingState.CursorPos ) );

                WritingState.LoadText( );
                _pinyinInput.Focus( );

            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
            }
        }

        public void TextEdit( Key key ) {
            switch (key) {
                case Key.Left: 
                case Key.Right:
                case Key.Home:
                case Key.End:
                    WritingState.Move( key.ToString( ) );
                    break;
                case Key.Back:
                    WritingState.BackSpace( );
                    break;
                case Key.Delete:
                    WritingState.Delete( ); 
                    break;
            }
        }

        public int CursorPos {
            get { return WritingState.CursorPos; }
        }

        private void Window_Loaded( object sender, RoutedEventArgs e ) {
            ThreadPool.QueueUserWorkItem( state => {
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
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
                /*
                TextScrollView.UpdateLayout( );
                var maxScrollPos = TextScrollView.ExtentWidth - TextScrollView.ViewportWidth;
                var scrollTo = TextScrollView.HorizontalOffset -
                    TextScrollView.TransformToVisual( _cursorPanel ).Transform( new Point( 0, 0 ) ).X -
                    TextScrollView.ViewportWidth * 0.5;
                if (scrollTo < 0) scrollTo = 0;
                if (scrollTo > maxScrollPos) scrollTo = maxScrollPos;
                TextScrollView.ScrollToHorizontalOffset( scrollTo );
                 */
            }
        }

        void PinyinInput_KeyUp( object sender, KeyEventArgs e ) {
            if (e.Key == Key.Enter) {
                if (Suggestions.Items.Count == 0) {
                    WritingState.LiteralInput( _pinyinInput.Text );
                    _pinyinInput.Text = "";
                } else {
                    SelectSuggestion( (SuggestionWord)Suggestions.Items[0] );
                }
                e.Handled = true;
            }
        }

        private void SelectSuggestionIndex( int pinyinIndex ) {
            SelectSuggestion( (SuggestionWord)Suggestions.Items[pinyinIndex] );
        }

        private void SelectSuggestion(SuggestionWord word) {
            WritingState.SelectWord( word.Word );
            ShowEnglish.IsChecked = false;
            _pinyinInput.Text = "";
        }

        private void PopulateCharGrid( IEnumerable<IDictionary<object,object>> words, int cursorPos ) {
            Characters.Children.Clear( );
            int pos = 0;
            foreach (var word in words) {
                if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
                var wordPanel = WordPanel.Create( word );
                Characters.Children.Add( wordPanel );
                wordPanel.Tag = word;
                wordPanel.MouseUp += new MouseButtonEventHandler( HanyuPanelMouseUp );
                pos++;
            }
            if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
            _pinyinInput.Focus( );
            ScrollInputVisible( );
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
                    WritingState.ExpandChars( );
                }
            }
        }

        private void CopyClick( object sender, RoutedEventArgs e ) {
            if (CopyHtml.IsChecked ?? false) {
                ClipboardTool.CopyToClipboard( 
                    (string) RT.var( "ExportText", "html" ).invoke(), 
                    new Uri( "http://www.brotherus.net" ) );
            } else {
                var data = WritingState.HanyiPinyinLines(
                    CopyPinyin.IsChecked ?? false,
                    CopyEnglish.IsChecked ?? false );
                Clipboard.SetText( data, TextDataFormat.UnicodeText );
            }
        }

        private void Clear_Text_Click( object sender, RoutedEventArgs e ) {
            WritingState.Clear( );
            _pinyinInput.Text = "";
        }

        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e ) {
            WordDatabase.SaveWordsInfo( );
            WritingState.SaveCurrentText( );
        }

        private void Suggestions_SelectedCellsChanged( object sender, SelectedCellsChangedEventArgs e ) {
            if (e.AddedCells.Count( ) > 0) {
                SelectSuggestion( (SuggestionWord)e.AddedCells.First( ).Item );
            }
        }

        private void StayOnTop_Checked( object sender, RoutedEventArgs e ) {
            this.Topmost = StayOnTop.IsChecked ?? false;
        }

    } // class

} // namespace
