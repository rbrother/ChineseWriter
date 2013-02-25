using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

        private static TextBlock CreateEnglishPanel( Word word ) {
            return new TextBlock {
                Padding = new Thickness( 4.0 ),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Text = word.ShortEnglish,
                Foreground = new SolidColorBrush( Color.FromArgb( 192, 0, 0, 0 ) )
            };
        }

        private static Color ToneColor( string pinyin ) {
            var lastChar = pinyin.TakeLast( );
            int tone;
            if (!int.TryParse( lastChar, out tone )) return Colors.Gray;
            return TONE_COLORS[tone - 1];
        }

        private static object CreateExplanationPanel( HanyuWord word, WordDatabase wordsDb ) {
            if (word.Hanyu.Length == 1) {
                return word.English;
            } else {
                var panel = new StackPanel { Orientation = Orientation.Vertical };
                panel.Children.Add( new Label { Content = word.English } );
                var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add( detailsPanel );
                foreach (FrameworkElement childPanel in
                    word.Characters.
                        Select( c => wordsDb.WordForHanyuPinyin( c.Item1, c.Item2 ) ).
                        Select( w => Create( w, wordsDb, big: true ) ))
                    detailsPanel.Children.Add( childPanel );
                return panel;
            }
        }

        // Constructors

        public static FrameworkElement Create( HanyuWord word, WordDatabase wordsDb, bool big = false ) {
            var panel = WordStackPanel( word.PanelColor, new FrameworkElement[] { 
                CreateTextBlock( "SimSun", big ? 80 : 30,
                    word.Characters.
                        Select( c => new Run {
                            Text = c.Item1,
                            Foreground = new SolidColorBrush( ToneColor( c.Item2 ) )
                        } ) ), 
                CreateTextBlock( "Times New Roman", 18,
                    word.Characters.
                        Select( c => new Run {
                            Text = " " + c.Item2.AddDiacritics( ) + " ",
                            Foreground = new SolidColorBrush( ToneColor( c.Item2 ) )
                        } ) ), 
                CreateEnglishPanel(word) } );

            panel.ToolTip = CreateExplanationPanel( word, wordsDb );
            return GuiUtils.WrapToBorder( panel );
        }

        public static FrameworkElement Create( MultiMeaningWord word, WordDatabase wordsDb ) {
            return GuiUtils.WrapToBorder( 
                WordStackPanel( word.PanelColor, 
                    CreateTextBlock( "SimSun", 30, word.Hanyu ),
                    CreateTextBlock( "Times New Roman", 18, word.DisplayPinyin ),
                    CreateEnglishPanel( word ) ) );
        }

        public static FrameworkElement Create( LiteralWord word, WordDatabase wordsDb ) {
            return  GuiUtils.WrapToBorder(
                    WordStackPanel( word.PanelColor,
                    CreateTextBlock( "Times New Roman", 30, word.Hanyu ) ) );
        }

    }
}
