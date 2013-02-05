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

        public Word Word { get { return _word; } }

        public WordPanel( Word word ) {
            _word = word;
            var panel = new StackPanel {
                Orientation = Orientation.Vertical,
                Background = new SolidColorBrush( word.Color ),
            };
            panel.Children.Add( new Label {
                Content = word.Hanyu,
                FontFamily = new FontFamily( "SimSun" ), FontSize = 30,
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.Children.Add( new Label {
                Content = word.Pinyin,
                Style = GuiUtils.PinyinStyle,
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.Children.Add( new Label {
                Content = word.ShortEnglish,
                Foreground = new SolidColorBrush( Color.FromArgb( 128, 0, 0, 0 ) ),
                HorizontalContentAlignment = HorizontalAlignment.Center
            } );
            panel.ToolTip = word.English;
            this.Content = GuiUtils.WrapToBorder( panel );
        }

    }
}
