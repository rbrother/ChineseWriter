using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Concurrency;

namespace ChineseWriter {

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ChineseWriterWindow : Window {

        private WritingState _writingState = new WritingState( );
        private TextBox _pinyinInput;
        private FrameworkElement _cursorPanel;

        private Key[] TEXT_EDIT_KEYS = new Key[] { Key.Back, Key.Delete, Key.Left, Key.Right, Key.Home, Key.End };

        public ChineseWriterWindow( ) {
            try {
                InitializeComponent( );
                _pinyinInput = new TextBox();
                _pinyinInput.TextChanged += new TextChangedEventHandler(PinyinInput_TextChanged);
                _pinyinInput.PreviewTextInput += new TextCompositionEventHandler(PinyinInput_PreviewTextInput);

                _cursorPanel = WrapToBorder(new Label { Content = _pinyinInput, VerticalContentAlignment = VerticalAlignment.Center });

                var KeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( _pinyinInput, "KeyUp" ).
                    Select( args => args.EventArgs.Key );

                KeyPresses.Where( key => TEXT_EDIT_KEYS.Contains(key) ).
                    Subscribe( key => _writingState.TextEdit( key ) );

                var EnglishChecked = Observable
                    .FromEventPattern<RoutedEventArgs>( ShowEnglish, "Checked" )
                    .Select( args => true );
                var EnglishUnchecked = Observable
                    .FromEventPattern<RoutedEventArgs>( ShowEnglish, "Unchecked" )
                    .Select( args => false );
                var EnglishChechedChanged = new bool[] { false }.ToObservable( )
                    .Merge( EnglishChecked )
                    .Merge( EnglishUnchecked );
                EnglishChechedChanged.Subscribe( value => _writingState.English = value );

                // Update UI based on writing state changes
                _writingState.WordsDatabaseChanged.ObserveOnDispatcher( ).
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

        void PinyinInput_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if (e.Text.Length == 1) {
                int n;
                if (int.TryParse(e.Text.Substring(0,1), out n)) {
                    _writingState.SelectPinyin(n);
                    e.Handled = true;
                }
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
                Characters.Children.Add( CreateHanyiPanel( word ) );
                pos++;
            }
            if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
        }

        private FrameworkElement CreateHanyiPanel( Word word ) {
            var color = word.pinyin == null ? Colors.Red : Colors.Transparent;
            var panel = new StackPanel {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush( color )
            };
            panel.Children.Add( new Label {
                Content = word.hanyu,
                FontFamily = new FontFamily( "SimSun" ), FontSize = 30,
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.Children.Add( new Label {
                Content = word.PinyinString,
                Style = (Style) this.Resources["PinyinStyle"],
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.Children.Add( new Label {
                Content = word.ShortEnglish,
                Foreground = new SolidColorBrush( Color.FromArgb( 128, 0, 0, 0 ) ),
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.ToolTip = word.english;
            return WrapToBorder( panel );
        }

        private FrameworkElement WrapToBorder( FrameworkElement child ) {
            return new Border {
                Child = child, BorderThickness = new Thickness( 1.0 ),
                BorderBrush = new SolidColorBrush( Color.FromArgb( 50, 0, 0, 0 ) )
            };
        }

        private void UpdateSuggestions( IEnumerable<Word> suggestions ) {
            Suggestions.Children.Clear( );
            Suggestions.RowDefinitions.Clear( );
            var row = 0;
            AddSuggestion( 0, new Word { pinyin = "", hanyu = "", 
                english="(literal text, hanyu parsed to words)" } );
            foreach (Word word in suggestions) {
                row++;
                AddSuggestion( row, word );
            }
        }

        private void AddSuggestion( int row, Word word ) {
            var pinyinStyle = (Style)this.Resources["PinyinStyle"];
            var color = ( row % 2 == 0 ? Colors.Transparent : Color.FromArgb( 50, 0, 0, 255 ) );
            Suggestions.RowDefinitions.Add( new RowDefinition { Tag = word } );
            Suggestions.Children.Add( CreateGridLabel( row.ToString( ), row, 0, color, pinyinStyle ) );
            Suggestions.Children.Add( CreateGridLabel( word.pinyin, row, 1, color, pinyinStyle ) );
            Suggestions.Children.Add( CreateGridLabel( word.hanyu, row, 2, color, pinyinStyle ) );
            Suggestions.Children.Add(CreateGridLabel(word.english, row, 3, color, (Style)this.Resources["WidgetStyle"]));
        }

        private static FrameworkElement CreateGridLabel( string text, int row, int col, Color color, Style style = null ) {
            var label = new Label { Content = text, Background = new SolidColorBrush(color), 
                HorizontalContentAlignment = HorizontalAlignment.Left };
            label.SetValue( Grid.ColumnProperty, col );
            label.SetValue( Grid.RowProperty, row );
            if (style != null) label.Style = style;
            return label;
        }

        private void AddWordButton_Click( object sender, RoutedEventArgs e ) {
            // TODO: Implement this in new way so that words are added automatically
            // and their definitions can then be edited
            throw new NotImplementedException( );

/*            var window = new AddWordWindow( _hanyuDb.Words.Values );
            if (SelectedChineseText != "") {
                window.HanyuBox.Text = SelectedChineseText;
                window.PinyinBox.Focus( );
                Process.Start( "http://translate.google.com/#zh-CN/en/" + SelectedChineseText );
                Process.Start( "http://www.mdbg.net/chindict/chindict.php?page=worddict&wdrst=0&wdqb=" + SelectedChineseText );
            } else {
                window.HanyuBox.Focus( );
            }
            var result = window.ShowDialog( );
            if (result.HasValue && result.Value) {
                _hanyuDb.AddOrModifyWord( window.NewWord );
            }
 */
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

        private void OpenLink_Click( object sender, RoutedEventArgs e ) {
            Process.Start( ((FrameworkElement) sender).Tag.ToString() );
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            _pinyinInput.Focus();
        }

    } // class

} // namespace
