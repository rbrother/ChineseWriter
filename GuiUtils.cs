﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Reactive.Linq;

namespace ChineseWriter {

    static class GuiUtils {

        public static void HandleExceptions( Action action, Window parent = null ) {
            try {
                action.Invoke( );
            } catch ( Exception ex ) {
                MessageBox.Show( parent, ex.Message, "Exception in ChineseWriter", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.None );
            }
        }

        public static FrameworkElement WrapToBorder( FrameworkElement child ) {
            return new Border {
                Child = child, BorderThickness = new Thickness( 1.0 ),
                BorderBrush = new SolidColorBrush( Color.FromArgb( 50, 0, 0, 0 ) ),
            };
        }

        public static Style PinyinStyle {
            get {
                var style = new Style(targetType: typeof(Control));
                style.Setters.Add(new Setter(Control.FontSizeProperty, 18.0));
                style.Setters.Add(new Setter(Control.FontFamilyProperty, new FontFamily("Times New Roman")));
                return style;
            }
        }

        public static IObservable<bool> CheckBoxChangeObservable(CheckBox box) {
            var boxChecked = Observable
                .FromEventPattern<RoutedEventArgs>( box, "Checked" )
                .Select( args => true );
            var boxUnchecked = Observable
                .FromEventPattern<RoutedEventArgs>( box, "Unchecked" )
                .Select( args => false );
            return new bool[] { false }.ToObservable( )
                .Merge( boxChecked )
                .Merge( boxUnchecked );
        }

        public static DependencyObject FindParent( DependencyObject child, Type parentClass ) {
            var current = child;
            while ( current.GetType( ) != parentClass ) {
                current = VisualTreeHelper.GetParent( current );
                if ( current == null ) return null;
            }
            return current;
        }

    } // class

} // namespace
