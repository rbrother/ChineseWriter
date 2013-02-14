using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ChineseWriter {

    class WordPanel : UserControl {

        private Word _word;
        private WordDatabase _wordsDb;

        public Word Word { get { return _word; } }

        public WordPanel( Word word, WordDatabase wordsDb, bool big = false ) {
            _word = word;
            _wordsDb = wordsDb;
            var panel = new StackPanel {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush( word.Color ),
                MaxWidth = 150
            };
            panel.Children.Add( new Label {
                Content = word.Hanyu,
                FontFamily = new FontFamily( "SimSun" ), FontSize = big ? 50 : 30,
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.Children.Add( new Label {
                Content = word.DisplayPinyin,
                Style = GuiUtils.PinyinStyle,
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.Children.Add( new TextBlock {
                Padding = new Thickness( 4.0 ),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Text = big ? word.English : word.ShortEnglish,
                Foreground = new SolidColorBrush( Color.FromArgb( 192, 0, 0, 0 ) )
            } );
            panel.ToolTip = CreateExplanationPanel();
            this.Content = GuiUtils.WrapToBorder( panel );
        }

        private object CreateExplanationPanel( ) {
            if (_word.Hanyu.Length == 1) {
                return _word.English;
            } else {
                var panel = new StackPanel { Orientation = Orientation.Vertical };
                panel.Children.Add( new Label { Content = _word.English } );
                var detailsPanel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add( detailsPanel );
                foreach (FrameworkElement childPanel in
                    _word.Hanyu.ToCharArray( ).
                        Select( c => _wordsDb.WordForHanyu( c.ToString( ) ) ).
                        Select( word => new WordPanel( word, _wordsDb, big: true ) ))
                    detailsPanel.Children.Add( childPanel );
                return panel;
            }
        }

    }
}
