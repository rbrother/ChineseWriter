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

    static class WordPanel {

        private static readonly Color[] TONE_COLORS = { 
            Color.FromRgb(255,0,0), Color.FromRgb(160,160,0), Color.FromRgb(0,180,0), 
            Color.FromRgb(0,0,255), Colors.Black };

        private static StackPanel WordStackPanel( Color color, params FrameworkElement[] content ) {
            var panel = new StackPanel {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush( color ),
                MaxWidth = 150,
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

        private static object CreateExplanationPanel( IDictionary<object,object> word ) {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add( 
                new TextBlock { 
                    Padding = new Thickness(4),                            
                    FontSize = 16, Text = word.Get<string>("english"), 
                    MaxWidth = 500, TextWrapping = TextWrapping.Wrap,
            } );
            var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add( detailsPanel );
            foreach (FrameworkElement childPanel in
                word.Characters().Select( w => CharacterPanel( w ) ))
                detailsPanel.Children.Add( childPanel );
            return panel;
        }

        public static FrameworkElement CharacterPanel( IDictionary<object,object> character ) {
            var foreground = new SolidColorBrush( ToneColor( character.Pinyin( ) ) );
            return GuiUtils.WrapToBorder(
                WordStackPanel( Colors.White,
                    CreateTextBlock( "SimSun", 80, new Run {
                        Text = character.Hanyu( ),
                        Foreground = foreground } ), 
                    CreateTextBlock( "Times New Roman", 40, new Run {
                        Text = " " + character.Pinyin( ).AddDiacritics( ) + " ",
                        Foreground = foreground } ), 
                    CreateEnglishPanel( character, true ) ) );
        }

        private static FrameworkElement CreateForHanyu( IDictionary<object,object> word ) {
            var chars = word.GetList( "characters" ).Cast<IDictionary<object,object>>( );
            var panel = WordStackPanel( Colors.White,
                CreateTextBlock( "SimSun", 30,
                    chars.Select( c => new Run {
                        Text = c.Hanyu(),
                        Foreground = new SolidColorBrush( ToneColor( c.Pinyin() ) )
                    } ).ToArray( ) ),
                CreateTextBlock( "Times New Roman", 20,
                    chars.Select( c => new Run {
                        Text = " " + c.Pinyin().AddDiacritics( ) + " ",
                        Foreground = new SolidColorBrush( ToneColor( c.Pinyin( ) ) )
                    } ).ToArray( ) ),
                word.Get<bool>("known") ? new TextBlock( ) : CreateEnglishPanel( word, false ) );
            panel.ToolTip = CreateExplanationPanel( word );
            panel.SetValue( ToolTipService.ShowDurationProperty, 60000 );
            return panel;
        }

        private static FrameworkElement CreateForLiteral( string text ) {
            return WordStackPanel( Color.FromRgb( 220, 220, 220 ),
                        CreateTextBlock( "Times New Roman", 30, text ) );
        }

        public static FrameworkElement Create( IDictionary<object,object> word) {
            return GuiUtils.WrapToBorder(
                word.HasKeyword( "text" ) ?
                CreateForLiteral( word.Get<string>("text") ) :
                CreateForHanyu(word));
        }

    }
}
