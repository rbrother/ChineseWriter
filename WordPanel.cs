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

    class WordPanel : UserControl {

        private Panel _mainPanel;

        public IDictionary<object, object> WordProperties { get; set; }

        public bool IsHanyuWord { get { return !WordProperties.IsLiteralText(); } }

        public Word HanyuWord { 
            get {
                return IsHanyuWord ? new Word( WordProperties.Hanyu(), WordProperties.Pinyin() ) : null;
            } 
        } 

        private static readonly Color[] TONE_COLORS = { 
            Color.FromRgb(255,0,0), Color.FromRgb(160,160,0), Color.FromRgb(0,180,0), 
            Color.FromRgb(0,0,255), Colors.Black };

        public WordPanel( IDictionary<object, object> word ) {
            this.WordProperties = word;
            this.Content = GuiUtils.WrapToBorder(
                word.IsLiteralText() ?
                    CreateForLiteral( word.Text() ) :
                    CreateForHanyu( HanyuWord ) );            
        }

        public void SetSelected( bool selected ) {
            var colorLevel = (byte)( selected ? 220 : 255 );
            _mainPanel.Background = new SolidColorBrush( Color.FromRgb( colorLevel, colorLevel, colorLevel ) );
        }

        private static StackPanel CreateStackPanel( params FrameworkElement[] content ) {
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

        private static TextBlock CreateEnglishPanel( string shortEnglish ) {
            return new TextBlock {
                Padding = new Thickness( 4.0 ),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Text = shortEnglish,
                Foreground = new SolidColorBrush( Color.FromArgb( 192, 0, 0, 0 ) )
            };
        }

        public static Color ToneColor( string pinyin ) {
            var lastChar = pinyin.TakeLast( );
            int tone;
            if (!int.TryParse( lastChar, out tone )) return Colors.Gray;
            return TONE_COLORS[tone - 1];
        }

        private FrameworkElement CreateForHanyu( Word word ) {
            var chars = WordDatabase.Characters( word );
            _mainPanel = CreateStackPanel(
                CreateTextBlock( "SimSun", 30,
                    chars.Select( c => new Run {
                        Text = c.Hanyu,
                        Foreground = new SolidColorBrush( ToneColor( c.Pinyin ) )
                    } ).ToArray( ) ),
                CreateTextBlock( "Times New Roman", 20,
                    chars.Select( c => new Run {
                        Text = " " + c.PinyinDiacritics + " ",
                        Foreground = new SolidColorBrush( ToneColor( c.Pinyin ) )
                    } ).ToArray( ) ),
                    CreateEnglishPanel( word.KnownLevel < 2 ? word.ShortEnglish : "" ) );
            _mainPanel.SetValue( ToolTipService.ShowDurationProperty, 60000 );
            return _mainPanel;
        }

        private FrameworkElement CreateForLiteral( string text ) {
            _mainPanel = CreateStackPanel( CreateTextBlock( "Times New Roman", 30, text ) );
            return _mainPanel;
        }
    }
}
