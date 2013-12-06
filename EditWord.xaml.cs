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

        public EditWord( IDictionary<object,object> word ) {
            InitializeComponent( );
            this.ShortEnglishBox.Text = word.Get<string>("short-english");
            this.Known.IsChecked = word.Known();
            this.ShortEnglishBox.Focus( );
            DeleteWordClicked = false;
        }

        private void OkClick( object sender, RoutedEventArgs e ) {
            this.DialogResult = true;
            this.Close( );
        }

        public bool DeleteWordClicked { get; set; }

        private void DeleteWordButton_Click( object sender, RoutedEventArgs e ) {
            DeleteWordClicked = true;
            this.DialogResult = true;
            this.Close( );
        }

    } // class

} // namespace
