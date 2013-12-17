﻿using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Reactive.Subjects;

namespace ChineseWriter {

    public partial class SuggestionsTable : UserControl {

        private int CurrentUpdater = 0;
        private int ActiveUpdaters = 0;
        private ObservableCollection<SuggestionWord> _suggestions = new ObservableCollection<SuggestionWord>( );

        public Subject<SuggestionWord> SuggestionSelected = new Subject<SuggestionWord>( );

        public SuggestionsTable( ) {
            InitializeComponent( );
            Suggestions.ItemsSource = _suggestions;
        }

        public bool Empty { get { return _suggestions.Count == 0; } }

        public SuggestionWord GetSuggestion( int index ) {
            return ( index < Suggestions.Items.Count ) ? _suggestions[index] : null;
        }

        internal void UpdateSuggestions( IEnumerable<IDictionary<object, object>> suggestions ) {
            ThreadPool.QueueUserWorkItem(
                state => UpdateSuggestionsBackground( (IEnumerable<IDictionary<object, object>>)state ), suggestions );
        }

        /// <summary>
        /// TODO: Use dotnet Task-framework for this?
        /// </summary>
        /// <param name="suggestions"></param>
        private void UpdateSuggestionsBackground( IEnumerable<IDictionary<object, object>> suggestions ) {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            CurrentUpdater++;
            var id = CurrentUpdater;
            ActiveUpdaters++;
            try {
                while ( ActiveUpdaters > 1 ) {
                    Thread.Sleep( 10 );
                    if ( id != CurrentUpdater ) return; // abort old updaters that have been replaced with newer ones
                }
                this.Dispatcher.BeginInvoke( new Action( ( ) => {
                    //ProcessingLabel.Content = "Searching dictionary...";
                    //ProcessingLabel.Foreground = new SolidColorBrush( Colors.Red );
                    _suggestions.Clear( );
                } ));
                var index = 1;
                foreach ( var suggestion in suggestions ) {
                    if ( id != CurrentUpdater ) return; // abort old updaters that have been replaced with newer ones
                    var shortcut = index == 1 ? "Enter" :
                        index <= 10 ? string.Format( "CTRL+{0}", index ) : "<click>";
                    var dataWord = new SuggestionWord( index, suggestion, shortcut );
                    this.Dispatcher.BeginInvoke( new Action( ( ) => _suggestions.Add( dataWord ) ) );
                    index++;
                }
                this.Dispatcher.BeginInvoke( new Action( ( ) => {
                    //ProcessingLabel.Foreground = new SolidColorBrush( Colors.Black );
                    //ProcessingLabel.Content = string.Format( "{0} suggestions", Suggestions.Items.Count );
                } ) );
            } finally {
                ActiveUpdaters--;
            }
        }

        private void Suggestions_MouseUp( object sender, MouseButtonEventArgs e ) {
            Console.WriteLine( sender );
            var source = (DependencyObject)e.OriginalSource;
            var row = (DataGridRow)GuiUtils.FindParent( source, typeof( DataGridRow ) );
            var cell = (DataGridCell)GuiUtils.FindParent( source, typeof( DataGridCell ) );
            if ( row == null || cell == null ) return;
            if ( cell.Column.Header.ToString( ) == "Shortcut" ) {
                SuggestionSelected.OnNext( (SuggestionWord) row.Item );
                e.Handled = true;
            }
        }

        void items_CollectionChanged( object sender, NotifyCollectionChangedEventArgs e ) {
            if ( e.Action == NotifyCollectionChangedAction.Remove ) {
                foreach ( SuggestionWord item in e.OldItems ) {
                    item.Delete( );
                }
            }
        }

        internal void SelectFirst( ) {
            SuggestionSelected.OnNext( _suggestions[0] );
        }
    }
}
