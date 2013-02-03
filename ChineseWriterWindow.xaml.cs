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

        WritingState _writingState = new WritingState( );

        public ChineseWriterWindow( ) {
            try {
                InitializeComponent( );
                this.Characters.Focus( );

                var KeyPresses = Observable.
                    FromEventPattern<KeyEventArgs>( this, "KeyUp" ).
                    Select( args => args.EventArgs.Key );
                var AlphaKeyPresses = KeyPresses.
                    Where( key => StringUtils.IsAlphaKey( key ) ).
                    Select( key => key.ToString().ToLower() );
                var NumberKeyPresses = KeyPresses.
                    Where( key => StringUtils.IsNumberKey( key ) ).
                    Select( key => StringUtils.NumberKeyValue( key ) );

                KeyPresses.Where( key => key == Key.Back ).
                    Subscribe( key => _writingState.BackSpace() );
                AlphaKeyPresses.
                    Subscribe( newPinyin => _writingState.AddPinyinInput(newPinyin) );
                NumberKeyPresses.Subscribe( n => _writingState.SelectPinyin( n ) ); 

                _writingState.PinyinChanges.
                    SubscribeOnDispatcher( ).Subscribe( pinyin => _cursorLabel.Content = pinyin );
                _writingState.SuggestionsChanges.
                    SubscribeOnDispatcher( ).Subscribe( suggestions => UpdateSuggestions( suggestions ) );

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

                _writingState.WordsDatabaseChanged.
                    ObserveOnDispatcher().
                    Subscribe( count => WordCountLabel.Content = string.Format( "Words: {0}", count ) );



                PopulateCharGrid( true );

            } catch (Exception ex) {
                MessageBox.Show( ex.ToString( ), "Error in startup of ChineseWriter" );
                this.Close( );
            }
        }

/*        private void ConvertPinyinToHanyu( PinyinSelectionInfo info ) {
            var hanyu = ( (ChineseWordInfo)Suggestions.RowDefinitions[info.selectedIndex - 1].Tag ).hanyu;
            Chinese.Text = info.text.Substring( 0, info.position - info.pinyinLength ) +
                            hanyu + info.text.Substring( info.position );
            Chinese.SelectionStart = info.position - info.pinyinLength + hanyu.Length;
            Suggestions.Children.Clear( );
        }
*/

/*
        private static string PinyinEntered( TextChangeInfo change ) {
            var start = change.text.Substring( 0, change.position );
            var pinyinMatch = Regex.Match( start, @"[a-zA-Z]+$" );
            return pinyinMatch.Success ? pinyinMatch.Value : null;
        }
*/

/*
        private static PinyinSelectionInfo? PinyinSelected( TextChangeInfo change ) {
            var start = change.text.Substring( 0, change.position );
            var pinyinMatch = Regex.Match( start, @"[a-zA-Z]+([1-9])$" );
            return pinyinMatch.Success ?
                new PinyinSelectionInfo { text = change.text, position = change.position,
                    pinyinLength = pinyinMatch.Length, selectedIndex = int.Parse( pinyinMatch.Groups[1].Value ) } :
                (PinyinSelectionInfo?) null;
        }
*/
        private void PopulateCharGrid( bool english ) {
            Characters.Children.Clear( );
            // Add a dummy word in the end to allow cursor rendering 
            int pos = 0;
            foreach (ChineseWordInfo word in _writingState.Words) {
                if (pos == _writingState.CursorPos) Characters.Children.Add( CursorPanel );
                Characters.Children.Add( CreateHanyiPanel( word, english ) );
                pos++;
            }
            if (pos == _writingState.CursorPos) Characters.Children.Add( CursorPanel );
        }

        private Label _cursorLabel;

        private FrameworkElement CursorPanel {
            get {
                if (_cursorLabel == null ) {
                    // TODO: Make background animated
                    _cursorLabel = new Label {
                        MinWidth = 10, MinHeight = 40,
                        Background = new SolidColorBrush( Colors.GreenYellow ),
                        Content = "",
                        FontSize = 18.0
                    };
                }
                return WrapToBorder( _cursorLabel );
            }
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
