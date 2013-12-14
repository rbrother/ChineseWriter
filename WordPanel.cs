using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RT = clojure.lang.RT;
using Keyword = clojure.lang.Keyword;

namespace ChineseWriter {

    /// <summary>
    /// TODO: Make this non-static class. Now that we actually manipulate the panels it
    /// is better to have proper state, getters and setters...
    /// </summary>
    class WordPanel : UserControl {

        private Panel _mainPanel;

        private static readonly Color[] TONE_COLORS = { 
            Color.FromRgb(255,0,0), Color.FromRgb(160,160,0), Color.FromRgb(0,180,0), 
            Color.FromRgb(0,0,255), Colors.Black };

        public WordPanel( IDictionary<object, object> word ) {
            this.Content = GuiUtils.WrapToBorder(
                word.HasKeyword( "text" ) ?
                CreateForLiteral( word.Get<string>( "text" ) ) :
                CreateForHanyu( word ) );
            this.Tag = word;
        }

        public void SetSelected( bool selected ) {
            var border = (Border)this.Content;
            var panel = (Panel)border.Child;
            var colorLevel = (byte)( selected ? 200 : 255 );
            panel.Background = new SolidColorBrush( Color.FromRgb( colorLevel, colorLevel, colorLevel ) );
        }

        private static StackPanel WordStackPanel( params FrameworkElement[] content ) {
            var panel = new StackPanel {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush( Colors.White ),
                MaxWidth = 200,
                Margin = new Thickness( 2 )
            };
            foreach (FrameworkElement item in content) {
                panel.Children.Add( item );
            }
            return panel;
        }

        private static TextBlock CreateTextBlock( string fontName, int fontSize, string content ) {
            return CreateTextBlock( fontName, fontSize, new Run( content ) );
        }

        private static TextBlock CreateTextBlock( string fontName, int fontSize, params Inline[] inlines ) {
            var textBlock = new TextBlock { 
                FontFamily = new FontFamily(fontName),
                FontSize = fontSize,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            };
            textBlock.Inlines.AddRange(inlines);
            return textBlock;
        }

        private static TextBlock CreateEnglishPanel( IDictionary<object,object> word, bool breakDown ) {
            return new TextBlock {
                Padding = new Thickness( 4.0 ),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Text = breakDown ? word.Get<string>("english") : word.Get<string>("short-english"),
                Foreground = new SolidColorBrush( Color.FromArgb( 192, 0, 0, 0 ) )
            };
        }

        public static Color ToneColor( string pinyin ) {
            var lastChar = pinyin.TakeLast( );
            int tone;
            if (!int.TryParse( lastChar, out tone )) return Colors.Gray;
            return TONE_COLORS[tone - 1];
        }

        private FrameworkElement CreateForHanyu( IDictionary<object,object> word ) {
            var chars = word.GetList( "characters" ).Cast<IDictionary<object,object>>( );
            _mainPanel = WordStackPanel(
                CreateTextBlock( "SimSun", 30,
                    chars.Select( c => new Run {
                        Text = c.Hanyu(),
                        Foreground = new SolidColorBrush( ToneColor( c.Pinyin() ) )
                    } ).ToArray( ) ),
                CreateTextBlock( "Times New Roman", 20,
                    chars.Select( c => new Run {
                        Text = " " + c.PinyinDiacritics() + " ",
                        Foreground = new SolidColorBrush( ToneColor( c.Pinyin( ) ) )
                    } ).ToArray( ) ),
                word.Known() ? new TextBlock( ) : CreateEnglishPanel( word, false ) );
            _mainPanel.SetValue( ToolTipService.ShowDurationProperty, 60000 );
            return _mainPanel;
        }

        private FrameworkElement CreateForLiteral( string text ) {
            _mainPanel = WordStackPanel( CreateTextBlock( "Times New Roman", 30, text ) );
            return _mainPanel;
        }
    }
}
