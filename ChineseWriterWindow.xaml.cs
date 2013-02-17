using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

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

                _cursorPanel = GuiUtils.WrapToBorder(new Label { Content = _pinyinInput, VerticalContentAlignment = VerticalAlignment.Center });

                var ControlKeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( _pinyinInput, "KeyUp" ).
                    Where( args => args.EventArgs.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control)).
                    Select( args => args.EventArgs.Key );

                ControlKeyPresses.Where( key => TEXT_EDIT_KEYS.Contains(key) ).
                    Subscribe( key => _writingState.TextEdit( key ) );

                ControlKeyPresses.Where( key => DECIMAL_KEYS.Contains(key) ).
                    Select( key => Array.IndexOf<Key>( DECIMAL_KEYS, key ) ).
                    Subscribe( pinyinIndex => _writingState.SelectPinyin( pinyinIndex ) );

                GuiUtils.CheckBoxChangeObservable( ShowEnglish ).
                    Subscribe( value => _writingState.English = value );

                var WordsDatabaseChanged = new int[] { 0 }.ToObservable( ).
                    Concat( _wordDatabase.WordsChanged );

                // Update UI based on writing state changes
                WordsDatabaseChanged.ObserveOnDispatcher( ).
                    Subscribe( count => this.Title = string.Format("ChineseWriter ({0} words)", count ) );
                _writingState.PinyinChanges.ObserveOnDispatcher( ).
                    Subscribe( pinyin => _pinyinInput.Text = pinyin );
                _writingState.SuggestionsChanges.ObserveOnDispatcher( ).
                    Subscribe( suggestions => UpdateSuggestions( suggestions ) );
                _writingState.WordsChanges.
                    CombineLatest(_writingState.CursorPosChanges, (words,cursor) => Tuple.Create(words,cursor)).                    
                    ObserveOnDispatcher( ).
                    Subscribe( value => PopulateCharGrid( value.Item1, value.Item2 ) );
                _writingState.Clear( );
            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
                this.Close( );
            }
        }

        void PinyinInput_TextChanged(object sender, TextChangedEventArgs e) {
            _writingState.PinyinInput = _pinyinInput.Text;
        }

        private void PopulateCharGrid( IEnumerable<Word> words, int cursorPos ) {
            Characters.Children.Clear( );
            int pos = 0;
            foreach (Word word in _writingState.Words) {
                if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
                var wordPanel = new WordPanel( word, _wordDatabase );
                Characters.Children.Add( wordPanel );
                pos++;
            }
            if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
            _pinyinInput.Focus( );
        }

        private void UpdateSuggestions( IEnumerable<Word> suggestions ) {
            Suggestions.ItemsSource = suggestions;
        }

        private void Copy_Chinese_Click( object sender, RoutedEventArgs e ) {
            try {
                Clipboard.SetData( DataFormats.UnicodeText, _writingState.HanyiPinyinLines );
            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ) );
            }
        }

        private void Clear_Text_Click( object sender, RoutedEventArgs e ) {
            _writingState.Clear( );
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            _pinyinInput.Focus();
        }

        private void Suggestions_LoadingRow( object sender, DataGridRowEventArgs e ) {
            int n = e.Row.GetIndex( ) + 1;
            if (n <= 9) {
                e.Row.Header = string.Format( "CTRL+{0}", n );
            } else {
                e.Row.Header = "<click>";
            }
        }

        private void Window_Closing( object sender, System.ComponentModel.CancelEventArgs e ) {
            _wordDatabase.SaveWordsInfo( );
        }

    } // class

} // namespace
