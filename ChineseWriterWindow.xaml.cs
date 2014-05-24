using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using RT = clojure.lang.RT;
using Brotherus;

namespace ChineseWriter {

    public partial class ChineseWriterWindow : Window {

        private TextBox _pinyinInput;
        private FrameworkElement _cursorPanel;
        private bool _draggingSelection = false;
        private Tuple<WordPanel,WordPanel> _selection = null;
        private WordPanel[] _wordPanels = new WordPanel[] {};
        private ContextMenu _wordPanelContextMenu;

        private Key[] TEXT_EDIT_KEYS = new Key[] { Key.Back, Key.Delete, Key.Left, Key.Right, Key.Home, Key.End };

        public Subject<Tuple<string, Color>> MessageStream = new Subject<Tuple<string, Color>>( );

        public void InitClojureLoadPath( ) {
            var loadPath = Environment.GetEnvironmentVariable( "CLOJURE_LOAD_PATH" ) ?? "";
            loadPath = AppendToPath( loadPath, Utils.FindRelativeFile("Clojure") ); // TODO: USE relative path, from SearchUpwardFile
            loadPath = loadPath.Replace( '\\', '/' );
            Environment.SetEnvironmentVariable( "CLOJURE_LOAD_PATH", loadPath );
        }

        private string AppendToPath( string path, string newDir ) {
            return path + ( Directory.Exists( newDir ) ? ";" + newDir : "" );
        }

        public ChineseWriterWindow( ) {
            InitializeComponent( );
            InitClojureLoadPath( );
            try {

                _pinyinInput = new TextBox { Style = GuiUtils.PinyinStyle, MinWidth = 20 };
                _pinyinInput.KeyUp += new KeyEventHandler( PinyinInput_KeyUp );
                _pinyinInput.GotFocus += _pinyinInput_GotFocus;

                _cursorPanel = CreateCursorPanel( );

                var ControlKeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( _pinyinInput, "KeyUp" ).
                    Where( args => args.EventArgs.KeyboardDevice.Modifiers.HasFlag( ModifierKeys.Control ) ).
                    Select( args => args.EventArgs.Key );

                ControlKeyPresses.Where( key => TEXT_EDIT_KEYS.Contains( key ) ).
                    Subscribe( key => TextEdit( key ) );

                var PinyinChanges = Observable.
                    FromEventPattern<TextChangedEventArgs>( _pinyinInput, "TextChanged" ).
                    ObserveOnDispatcher( ).
                    Select( args => ( (TextBox)args.Sender ).Text );

                var suggestionSelections = PinyinChanges.
                    Where( pinyin => EndsWithNumber(pinyin) ).
                    Select( pinyin => Convert.ToInt32( pinyin.TakeLast( ) ) ).
                    Select( index => Suggestions.GetSuggestion( index - 1 ) ).
                    Where( word => word != null ).
                    Merge( Suggestions.SuggestionSelected );

                suggestionSelections.Subscribe( word => WritingState.InsertWord( word ) );
                suggestionSelections.ObserveOnDispatcher( ).Subscribe( word => {
                    ShowEnglish.IsChecked = false;
                    _pinyinInput.Text = "";
                } );

                var PinyinInputChanges = PinyinChanges.Where( pinyin => !EndsWithNumber( pinyin ) );

                // Update UI based on writing state changes
                GuiUtils.CheckBoxChangeObservable( ShowEnglish ).
                    CombineLatest( PinyinInputChanges, ( english, input ) => Tuple.Create( english, input ) ).
                    ObserveOnDispatcher( ).
                    Subscribe( tuple => Suggestions.UpdateSuggestions( WordDatabase.Suggestions( tuple.Item2, tuple.Item1 ) ) );
                WritingState.WordsChanges.
                    ObserveOnDispatcher( ).Subscribe( words => PopulateCharGrid( words, WritingState.CursorPos ) );
                WritingState.WordsChanges.Subscribe( words => WritingState.SaveCurrentText( ) );

                MessageStream.Merge(Suggestions.MessageStream).
                    ObserveOnDispatcher( ).Subscribe( messageAndColor => {
                        ProcessingLabel.Content = messageAndColor.Item1;
                        ProcessingLabel.Foreground = new SolidColorBrush( messageAndColor.Item2 );
                    } );
                _pinyinInput.Focus( );
            } catch ( Exception ex ) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
            }
        }

        private static bool EndsWithNumber( string s ) {
            return s.Length > 0 && char.IsNumber( s.ToCharArray( ).Last( ) );
        }

        void _pinyinInput_GotFocus( object sender, RoutedEventArgs e ) {
            RemoveTextSelection( );
        }

        void RemoveTextSelection( ) {
            _selection = Tuple.Create<WordPanel, WordPanel>( null, null );
            MarkDraggedSelection( );            
        }

        public void TextEdit( Key key ) {
            switch ( key ) {
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
                try {
                    RT.load( "WritingState" );
                    RT.load( "ExportText" );
                    RT.load( "ParseChinese" );
                    RT.var( "ExportText", "set-add-diacritics-func!" ).invoke( StringUtils.AddDiacriticsFunc );
                    WordDatabase.LoadWords( );
                    WritingState.LoadText( );
                } catch ( Exception ex ) {
                    ReportErrorThreadSafe( ex );
                }
            } );
            ThreadPool.QueueUserWorkItem( state => ReportLoadStateTask( ) );
        }

        private void ReportLoadStateTask( ) {
            var start = DateTime.Now;
            SetTitleThreadsafe( "Loading Clojure runtime..." );
            try {
                while ( true ) {
                    var clojureLoaded = RT.var( "WordDatabase", "all-words" ).isBound;
                    var databaseLoaded = clojureLoaded && WordDatabase.IsDatabaseLoaded;
                    var status = databaseLoaded ? "ChineseWriter. " + WordDatabase.DatabaseInfo : 
                            clojureLoaded ? "Loading word list..." : "Loading Clojure runtime...";
                    var dur = DateTime.Now - start;
                    SetTitleThreadsafe( string.Format( "{0} {1:0} s", status, dur.TotalSeconds ) );
                    if ( databaseLoaded ) return;
                    Thread.Sleep( 100 );
                }
            } catch ( Exception ex ) {
                ReportErrorThreadSafe( ex );
            }
        }

        private void ReportErrorThreadSafe( Exception ex ) {
            var message = ex.ToString( );
            if ( message.Contains( "Could not locate clojure.core.clj.dll" ) ) {
                this.Dispatcher.Invoke( new Action( ( ) => {
                    MessageBox.Show( "Please ensure that environment variable CLOJURE_LOAD_PATH\n" +
                        "Contains clojure-clr directory and any other library directories needed",
                        "Cannot find Clojure files" );
                } ) );
            } else {
                this.Dispatcher.Invoke( new Action( ( ) => {
                    MessageBox.Show( ex.ToString( ), "Error in ChineseWriter Clojure initialization" );
                } ) );
            }
        }

        private void SetTitleThreadsafe( string title ) {
            this.Dispatcher.Invoke( new Action( ( ) => { this.Title = title; } ) );
        }

        void ShowTempMessage( string message ) {
            MessageStream.OnNext( Tuple.Create( message, Colors.DarkGreen ) );
            ThreadPool.QueueUserWorkItem( state => {
                Thread.Sleep( 1500 );
                MessageStream.OnNext( Tuple.Create( "", Colors.Black ) );
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

        void PinyinInput_KeyUp( object sender, KeyEventArgs e ) {
            if ( e.Key == Key.Enter ) {
                if ( Suggestions.Empty ) {
                    WritingState.LiteralInput( _pinyinInput.Text );
                    _pinyinInput.Text = "";
                } else {
                    Suggestions.SelectFirst( );
                }
                e.Handled = true;
            }
        }

        private void PopulateCharGrid( IEnumerable<IDictionary<object, object>> words, int cursorPos ) {
            if ( !WordDatabase.IsDatabaseLoaded ) throw new ApplicationException( "Word database must be loaded to show characters" );
            _selection = Tuple.Create<WordPanel, WordPanel>( null, null );
            _wordPanels = words.Select( word => new WordPanel( word ) ).ToArray();
            Characters.Children.Clear( );
            int pos = 0;            
            foreach ( var wordPanel in _wordPanels ) {
                if ( pos == cursorPos ) Characters.Children.Add( _cursorPanel );
                Characters.Children.Add( wordPanel );
                wordPanel.MouseEnter += wordPanel_MouseEnter;
                wordPanel.MouseLeftButtonDown += wordPanel_MouseLeftButtonDown;
                wordPanel.MouseLeftButtonUp += wordPanel_MouseLeftButtonUp;
                wordPanel.ContextMenu = WordPanelContextMenu;
                pos++;
            }
            if ( pos == cursorPos ) Characters.Children.Add( _cursorPanel );
            _pinyinInput.Focus( );
        }

        private ContextMenu WordPanelContextMenu {
            get {
                if ( _wordPanelContextMenu == null ) {
                    _wordPanelContextMenu = new ContextMenu( );
                    var copySelectionItem = new MenuItem { Header = "Copy selected words" };
                    copySelectionItem.Click += copySelectionItem_Click;
                    _wordPanelContextMenu.Items.Add( copySelectionItem );                    
                    var addWordItem = new MenuItem { Header = "New word from the selection" };
                    addWordItem.Click += addWordItem_Click;
                    _wordPanelContextMenu.Items.Add( addWordItem );
                }
                return _wordPanelContextMenu;
            }
        }

        void copySelectionItem_Click( object sender, RoutedEventArgs e ) {
            CopyNow( );
        }

        void addWordItem_Click( object sender, RoutedEventArgs e ) {
            WordDatabase.AddNewWord( SelectedWords );
            ShowTempMessage( "Word added" );
        }

        void wordPanel_MouseLeftButtonDown( object sender, MouseButtonEventArgs e ) {
            var wordPanel = (WordPanel)sender;
            _draggingSelection = true;
            _selection = Tuple.Create( wordPanel, wordPanel );
            MarkDraggedSelection( );
        }

        void wordPanel_MouseEnter( object sender, MouseEventArgs e ) {
            var thisPanel = (WordPanel)sender;
            if (Mouse.LeftButton != MouseButtonState.Pressed) _draggingSelection = false;
            if ( _draggingSelection ) _selection = Tuple.Create( _selection.Item1, thisPanel );
            MarkDraggedSelection( );
        }

        void wordPanel_MouseLeftButtonUp( object sender, MouseButtonEventArgs e ) {
            if ( _draggingSelection ) {
                _draggingSelection = false;
                if ( object.ReferenceEquals( _selection.Item1, _selection.Item2 ) ) {
                    var panel = (WordPanel)sender;
                    if ( panel.IsHanyuWord ) {
                        var word = ( (WordPanel)sender ).HanyuWord;
                        // Show word breakdown and Move cursor only on clicking on single field, not dragging selection
                        Suggestions.UpdateSuggestions( WordDatabase.BreakDown( word ) );
                        WritingState.SetCursorPos( WordPanelIndex( panel ) + 1 );
                    }
                }
            }
        }

        void MarkDraggedSelection( ) {
            // First deselect all
            foreach ( WordPanel wordPanel in _wordPanels ) {
                wordPanel.SetSelected(false);
            }
            // Then mark selected
            foreach ( WordPanel wordPanel in SelectedWordPanels ) {
                wordPanel.SetSelected(true );
            }
        }

        int WordPanelIndex( WordPanel panelToFind ) {
            int index = 0;
            foreach ( WordPanel wordPanel in _wordPanels ) {
                if ( object.ReferenceEquals( wordPanel, panelToFind ) ) return index;
                index++;
            }
            throw new ApplicationException( "Panel not found");
        }

        WordPanel[] SelectedWordPanels {
            get {
                if ( _selection.Item1 == null ) {
                    return new WordPanel[] {};
                } else {
                    int startIndex = WordPanelIndex( _selection.Item1 );
                    int endIndex = WordPanelIndex( _selection.Item2 );
                    var first = Math.Min( startIndex, endIndex );
                    var last = Math.Max( startIndex, endIndex );
                    return _wordPanels.Skip( first ).Take( last - first + 1 ).ToArray( );           
                }
            }
        }

        private void CopyClick( object sender, RoutedEventArgs e ) {
            CopyNow( );
        }

        private void CopyNow( ) {
            CopyWrapper( () => 
                Clipboard.SetText( WritingState.HanyiText( SelectedWords ), TextDataFormat.UnicodeText ));
        }

        private void CopyHtmlClick( object sender, RoutedEventArgs e ) {
            CopyWrapper( () => 
                ClipboardTool.CopyToClipboard(
                    (string)RT.var( "ExportText", "html" ).invoke( ),
                    new Uri( "http://www.brotherus.net" )));
        }

        private void CopyWrapper( Action copyAction ) {
            try {
                copyAction.Invoke( );
                ShowTempMessage( "Copied" );
            } catch ( Exception ex ) {
                ShowTempMessage( ex.Message ); // Usually the exception does not prevent the copying
            }

        }

        private IEnumerable<IDictionary<object, object>> SelectedWords {
            get {
                if ( _selection.Item1 == null ) return null;
                return SelectedWordPanels.Select( panel => panel.WordProperties );
            }
        }

        private void Clear_Text_Click( object sender, RoutedEventArgs e ) {
            WritingState.Clear( );
            _pinyinInput.Text = "";
        }

        private void StayOnTop_Checked( object sender, RoutedEventArgs e ) {
            this.Topmost = StayOnTop.IsChecked ?? false;
        }

        private void HandleExceptions( Action action ) {
            try {
                action.Invoke( );
            } catch ( Exception ex ) {
                MessageBox.Show( this, ex.Message, "Exception in ChineseWriter", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.None );
            }
        }

        private void Window_Activated( object sender, EventArgs e ) {
            this._pinyinInput.Focus( );
        }

        private void PasteChineseClick( object sender, RoutedEventArgs e ) {
            WritingState.InsertChinese(
                Regex.Replace( Clipboard.GetText( ), @"\s", "", RegexOptions.None ) );
        }

        private void SayClick( object sender, RoutedEventArgs e ) {
            var soundsFolder = Utils.FindRelativeFile( "sounds" );
            var pinyins = WritingState.WordPinyins.Where( pinyin => pinyin != null).
                SelectMany( pinyin => pinyin.ToLower().Split( ' ' ) );
            var players = pinyins.Select( pinyin => pinyin.EndsWith("5") ? pinyin.DropLast() + "1" : pinyin ).
                Select( pinyin => Path.Combine( soundsFolder, pinyin + ".wav" )).
                Where( path => File.Exists(path) ).
                Select( path => new SoundPlayer( path ) ).ToArray();
            foreach ( var player in players ) { 
                player.PlaySync( );
            }
        }

        private void FlashCardsClick( object sender, RoutedEventArgs e ) {
            var flashCardsWindow = new FlashCards( );
            flashCardsWindow.Owner = this;
            flashCardsWindow.Show( );
        }
    } // class

} // namespace
