using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Keyword = clojure.lang.Keyword;
using RT = clojure.lang.RT;

namespace ChineseWriter {

    public partial class EditWord : Window {
        public EditWord( IDictionary<string,object> word ) {
            InitializeComponent( );
            this.ShortEnglishBox.Text = word.GetStr("short-english");
            this.Known.IsChecked = (bool)word["known"];
            this.ShortEnglishBox.Focus( );
        }

        private void OkClick( object sender, RoutedEventArgs e ) {
            this.DialogResult = true;
            this.Close( );
        }
    } // class

} // namespace
