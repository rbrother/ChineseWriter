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

    struct TextChangeInfo { 
        public string text;
        public int position;
    }

    struct PinyinSelectionInfo {
        public string text;
        public int position, selectedIndex, pinyinLength;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ChineseWriterWindow : Window {

        WordDatabase _words = new WordDatabase();

        public ChineseWriterWindow( ) {
            try {
                InitializeComponent( );
                _words.LoadWords( ); // These would be loaded later lazily, but force here early loading for fail-fast in case of errors
                Chinese.Focus( );

                var ChineseTextChanges = Observable
                    .FromEventPattern<TextChangedEventArgs>( Chinese, "TextChanged" )
                    .Select( args => args.EventArgs );

                var KeyPresses = ChineseTextChanges
                    .Where( arg => arg.Changes.Count == 1 )
                    .Select( arg => arg.Changes.First( ) )
                    .Where( change => change.AddedLength == 1 || change.RemovedLength == 1 )
                    .ObserveOnDispatcher( )
                    .Select( change => new TextChangeInfo { text = Chinese.Text, position = Chinese.SelectionStart } );

                var PinyinWrites = KeyPresses
                    .Where( change => PinyinEntered( change ) != null )
                    .Select( change => PinyinEntered( change ) )
                    .DistinctUntilChanged( );

                var PinyinSelections = KeyPresses
                    .Where( change => PinyinSelected( change ).HasValue )
                    .Select( change => PinyinSelected( change ).Value )
                    .Where( pinyinInfo => Suggestions.RowDefinitions.Count >= pinyinInfo.selectedIndex );

                var EnglishChecked = Observable
                    .FromEventPattern<RoutedEventArgs>( ShowEnglish, "Checked" )
                    .Select( args => true );
                var EnglishUnchecked = Observable
                    .FromEventPattern<RoutedEventArgs>( ShowEnglish, "Unchecked" )
                    .Select( args => false );
                var EnglishChechedChanged = new bool[] { false }.ToObservable( )
                    .Merge( EnglishChecked )
                    .Merge( EnglishUnchecked );

                var WordsDatabaseChanged = new int[] { 0 }.ToObservable( )
                    .Concat( _words.WordsChanged )
                    .ObserveOnDispatcher( );

                WordsDatabaseChanged
                    .Subscribe( arg => WordCountLabel.Content = string.Format( "Words: {0}", _words.Words.Count ) );

                PinyinWrites
                    .CombineLatest( EnglishChechedChanged, ( pinyin, english ) => Tuple.Create( pinyin, english ) )
                    .Throttle( TimeSpan.FromSeconds( 0.2 ) )
                    .ObserveOnDispatcher( )
                    .Subscribe( args => UpdateSuggestions( args.Item1, args.Item2 ) );

                PinyinSelections
                    .Subscribe( pinyinInfo => ConvertPinyinToHanyu( pinyinInfo ) );

                ChineseTextChanges
                    .ObserveOnDispatcher( )
                    .Subscribe( args => RemoveSpaces( ) );

                ChineseTextChanges
                    .Select( e => Chinese.Text )
                    .CombineLatest( EnglishChechedChanged, ( chinese, english ) => Tuple.Create( chinese, english ) )
                    .DistinctUntilChanged( )
                    .CombineLatest( WordsDatabaseChanged, ( args, newword ) => args )
                    .ObserveOnDispatcher( )
                    .Subscribe( args => PopulateCharGrid( args.Item1, args.Item2 ) );
            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
                this.Close( );
            }
        }

        private void RemoveSpaces( ) {
            if (Chinese.Text.Contains( ' ' )) {
                Chinese.Text = Chinese.Text.Replace( " ", "" );
            }
        }

        private void ConvertPinyinToHanyu( PinyinSelectionInfo info ) {
            var hanyu = ( (ChineseWordInfo)Suggestions.RowDefinitions[info.selectedIndex - 1].Tag ).hanyu;
            Chinese.Text = info.text.Substring( 0, info.position - info.pinyinLength ) +
                            hanyu + info.text.Substring( info.position );
            Chinese.SelectionStart = info.position - info.pinyinLength + hanyu.Length;
            Suggestions.Children.Clear( );
        }

        private static string PinyinEntered( TextChangeInfo change ) {
            var start = change.text.Substring( 0, change.position );
            var pinyinMatch = Regex.Match( start, @"[a-zA-Z]+$" );
            return pinyinMatch.Success ? pinyinMatch.Value : null;
        }

        private static PinyinSelectionInfo? PinyinSelected( TextChangeInfo change ) {
            var start = change.text.Substring( 0, change.position );
            var pinyinMatch = Regex.Match( start, @"[a-zA-Z]+([1-9])$" );
            return pinyinMatch.Success ?
                new PinyinSelectionInfo { text = change.text, position = change.position,
                    pinyinLength = pinyinMatch.Length, selectedIndex = int.Parse( pinyinMatch.Groups[1].Value ) } :
                (PinyinSelectionInfo?) null;
        }

        private void PopulateCharGrid( string chinese, bool english ) {
            var words = _words.HanyuToWords( chinese );
            Characters.Children.Clear( );
            foreach (ChineseWordInfo word in words) {
                Characters.Children.Add( CreateHanyiPanel( word, english ) );
            }
            Pinyin.Text = _words.PinyinText( words );
        }

        private FrameworkElement CreateHanyiPanel( ChineseWordInfo word, bool english ) {
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
            if ( english ) {
                panel.Children.Add( new Label {
                    Content = word.ShortEnglish,
                    FontSize = 12,
                    Foreground = new SolidColorBrush( Color.FromArgb( 128, 0, 0, 0 ) ),
                    HorizontalContentAlignment = HorizontalAlignment.Center
                } );
            }
            panel.ToolTip = word.english;
            return new Border {
                Child = panel, BorderThickness = new Thickness( 1.0 ),
                BorderBrush = new SolidColorBrush( Color.FromArgb( 50, 0, 0, 0 ) )
            };
        }

        private void UpdateSuggestions( string pinyinInput, bool english ) {
            Suggestions.Children.Clear( );
            Suggestions.RowDefinitions.Clear( );
            if (pinyinInput == null) return;
            var row = 0;
            var pinyinStyle = (Style) this.Resources["PinyinStyle"];
            foreach (ChineseWordInfo word in _words.MatchingSuggestions( pinyinInput, english ).Take( 9 )) {
                var color = ( row % 2 == 0 ? Colors.Transparent : Color.FromArgb(50,0,0,255) );
                Suggestions.RowDefinitions.Add( new RowDefinition { Tag = word } );
                Suggestions.Children.Add( CreateGridLabel( ( row + 1 ).ToString( ), row, 0, color, pinyinStyle ) );
                Suggestions.Children.Add( CreateGridLabel( word.pinyin, row, 1, color, pinyinStyle ) );
                Suggestions.Children.Add( CreateGridLabel( word.hanyu, row, 2, color, pinyinStyle ) );
                Suggestions.Children.Add( CreateGridLabel( word.english, row, 3, color, pinyinStyle ) );
                row++;
            }
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
            var window = new AddWordWindow( _words.Words.Values );
            if (Chinese.SelectedText != "") {
                window.HanyuBox.Text = Chinese.SelectedText;
                window.PinyinBox.Focus( );
                Process.Start( "http://translate.google.com/#zh-CN/en/" + Chinese.SelectedText );
                Process.Start( "http://www.mdbg.net/chindict/chindict.php?page=worddict&wdrst=0&wdqb=" + Chinese.SelectedText );
            } else {
                window.HanyuBox.Focus( );
            }
            var result = window.ShowDialog( );
            if (result.HasValue && result.Value) {
                _words.AddOrModifyWord( window.NewWord );
            }
        }

        private void Copy_Chinese_Click( object sender, RoutedEventArgs e ) {
            try {
                Clipboard.SetData( DataFormats.UnicodeText, _words.HanyiPinyinLines( Chinese.Text ) );
            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ) );
            }
        }

        private void Clear_Text_Click( object sender, RoutedEventArgs e ) {
            Chinese.Clear( );
            Chinese.Focus( );
        }

        private void OpenLink_Click( object sender, RoutedEventArgs e ) {
            Process.Start( ((FrameworkElement) sender).Tag.ToString() );
        }

    } // class

} // namespace
