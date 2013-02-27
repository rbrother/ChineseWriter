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

namespace ChineseWriter {

    public partial class EditWord : Window {
        public EditWord( HanyuWord word ) {
            InitializeComponent( );
            this.ShortEnglishBox.Text = word.ShortEnglish;
            this.ShortEnglishBox.Focus( );
        }

        private void OkClick( object sender, RoutedEventArgs e ) {
            this.DialogResult = true;
            this.Close( );
        }
    } // class

} // namespace
