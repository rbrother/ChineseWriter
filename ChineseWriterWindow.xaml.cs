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
        private Label _cursorLabel;
        private FrameworkElement _cursorPanel;

        public ChineseWriterWindow( ) {
            try {
                InitializeComponent( );
                _cursorLabel = new Label {
                    MinWidth = 10, MinHeight = 40,
                    Background = new SolidColorBrush( Colors.GreenYellow ),
                    Content = "",
                    FontSize = 18.0,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                _cursorPanel = WrapToBorder( _cursorLabel );

                var KeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( this, "KeyUp" ).
                    Select( args => args.EventArgs );
                var AlphaKeyPresses = KeyPresses.
                    Where( args => StringUtils.IsAlphaKey( args.Key ) ).
                    Select( args => args.Key.ToString( ).ToLower( ) );
                var NumberKeyPresses = KeyPresses.
                    Where( args => StringUtils.IsNumberKey( args.Key ) ).
                    Select( args => StringUtils.NumberKeyValue( args.Key ) );

                AlphaKeyPresses.
                    Subscribe( newPinyin => _writingState.AddPinyinInput( newPinyin ) );
                NumberKeyPresses.Subscribe( n => _writingState.SelectPinyin( n ) );
                KeyPresses.Where( args => args.Key == Key.Back ).
                    Subscribe( args => _writingState.BackSpace() );
                KeyPresses.Where( args => args.Key == Key.Left ).
                    Subscribe( args => _writingState.MoveLeft( ) );
                KeyPresses.Where( args => args.Key == Key.Right ).
                    Subscribe( args => _writingState.MoveRight( ) );

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
                    Subscribe( count => WordCountLabel.Content = string.Format( "Words: {0}", count ) );
                _writingState.PinyinChanges.ObserveOnDispatcher( ).
                    Subscribe( pinyin => _cursorLabel.Content = pinyin );
                _writingState.SuggestionsChanges.ObserveOnDispatcher( ).
                    Subscribe( suggestions => UpdateSuggestions( suggestions ) );
                _writingState.WordsChanges.
                    CombineLatest(_writingState.CursorPosChanges, (words,cursor) => Tuple.Create(words,cursor)).                    
                    ObserveOnDispatcher( ).
                    Subscribe( value => PopulateCharGrid( value.Item1, value.Item2 ) );

                _writingState.PinyinInput = "";
                _writingState.English = false;
                _writingState.Words = new ChineseWordInfo[] { };
                _writingState.CursorPos = 0;

            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
                this.Close( );
            }
        }

        private void PopulateCharGrid( IEnumerable<ChineseWordInfo> words, int cursorPos ) {
            Characters.Children.Clear( );
            int pos = 0;
            foreach (ChineseWordInfo word in _writingState.Words) {
                if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
                Characters.Children.Add( CreateHanyiPanel( word ) );
                pos++;
            }
            if (pos == cursorPos) Characters.Children.Add( _cursorPanel );
        }

        private FrameworkElement CreateHanyiPanel( ChineseWordInfo word ) {
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
                FontSize = 12,
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

        private void UpdateSuggestions( IEnumerable<ChineseWordInfo> suggestions ) {
            Suggestions.Children.Clear( );
            Suggestions.RowDefinitions.Clear( );
            var row = 0;
            AddSuggestion( 0, new ChineseWordInfo { pinyin = "", hanyu = "", english="(literal latin text, no conversion to hanyu)" } );
            foreach (ChineseWordInfo word in suggestions) {
                row++;
                AddSuggestion( row, word );
            }
        }

        private void AddSuggestion( int row, ChineseWordInfo word ) {
            var pinyinStyle = (Style)this.Resources["PinyinStyle"];
            var color = ( row % 2 == 0 ? Colors.Transparent : Color.FromArgb( 50, 0, 0, 255 ) );
            Suggestions.RowDefinitions.Add( new RowDefinition { Tag = word } );
            Suggestions.Children.Add( CreateGridLabel( row.ToString( ), row, 0, color, pinyinStyle ) );
            Suggestions.Children.Add( CreateGridLabel( word.pinyin, row, 1, color, pinyinStyle ) );
            Suggestions.Children.Add( CreateGridLabel( word.hanyu, row, 2, color, pinyinStyle ) );
            Suggestions.Children.Add( CreateGridLabel( word.english, row, 3, color, pinyinStyle ) );
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
            throw new NotImplementedException( );
        }

        private void OpenLink_Click( object sender, RoutedEventArgs e ) {
            Process.Start( ((FrameworkElement) sender).Tag.ToString() );
        }

    } // class

} // namespace
