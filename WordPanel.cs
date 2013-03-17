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
            return CreateTextBlock( fontName, fontSize, new Inline[] { new Run( content ) } );
        }

        private static TextBlock CreateTextBlock( string fontName, int fontSize, IEnumerable<Inline> inlines ) {
            var textBlock = new TextBlock { 
                FontFamily = new FontFamily(fontName),
                FontSize = fontSize,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            };
            textBlock.Inlines.AddRange(inlines);
            return textBlock;
        }

        private static TextBlock CreateEnglishPanel( IDictionary<string,object> word, bool breakDown ) {
            return new TextBlock {
                Padding = new Thickness( 4.0 ),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Text = breakDown ? (string)word["english"] : (string)word["short-english"],
                Foreground = new SolidColorBrush( Color.FromArgb( 192, 0, 0, 0 ) )
            };
        }

        public static Color ToneColor( string pinyin ) {
            var lastChar = pinyin.TakeLast( );
            int tone;
            if (!int.TryParse( lastChar, out tone )) return Colors.Gray;
            return TONE_COLORS[tone - 1];
        }

        private static object CreateExplanationPanel( IDictionary<string,object> word ) {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add( 
                new TextBlock { 
                    Padding = new Thickness(4),                            
                    FontSize = 16, Text = (string)word["english"], 
                    MaxWidth = 500, TextWrapping = TextWrapping.Wrap,
            } );
            var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add( detailsPanel );
            foreach (FrameworkElement childPanel in
                ( (IList<object>)word["characters"] ).
                    Select( w => Create( (IDictionary<string,object>)w, breakDown: true ) ))
                detailsPanel.Children.Add( childPanel );
            return panel;
        }

        // Constructors

        public static FrameworkElement Create( IDictionary<string,object> word, bool breakDown = false ) {
            var chars = (IList<IList<string>>)word["characters"];
            var panel = WordStackPanel(Colors.White, new FrameworkElement[] { 
                CreateTextBlock( "SimSun", breakDown ? 80 : 30,
                    chars.Select( c => new Run {
                            Text = c[0],
                            Foreground = new SolidColorBrush( ToneColor( c[1] ) )
                        } ) ), 
                CreateTextBlock( "Times New Roman", breakDown ? 40 : 20,
                    chars.Select( c => new Run {
                            Text = " " + c[1].AddDiacritics( ) + " ",
                            Foreground = new SolidColorBrush( ToneColor( c[1] ) )
                        } ) ), 
                ((bool)word["known"]) && !breakDown ? new TextBlock() : CreateEnglishPanel( word, breakDown ) } );
            if (!breakDown) {
                panel.ToolTip = CreateExplanationPanel( word );
                panel.SetValue( ToolTipService.ShowDurationProperty, 60000 );
            }
            return GuiUtils.WrapToBorder( panel );
        }
        /*
        public static FrameworkElement Create( Word word, WordDatabase wordsDb ) {
            return GuiUtils.WrapToBorder(
                WordStackPanel(Colors.Yellow, 
                    CreateTextBlock( "SimSun", 30, word.Hanyu ),
                    CreateTextBlock( "Times New Roman", 18, word.DisplayPinyin ),
                    CreateEnglishPanel( word, false ) ) );
        }

        public static FrameworkElement Create( Word word, WordDatabase wordsDb ) {
            return  GuiUtils.WrapToBorder(
                    WordStackPanel(Color.FromRgb(220, 220, 220),
                    CreateTextBlock( "Times New Roman", 30, word.Text ) ) );
        }
        */
    }
}
