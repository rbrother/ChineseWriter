using System;
using System.Threading;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using System.Reactive.Subjects;

namespace ChineseWriter {

    public partial class SuggestionsTable : UserControl {

        private ObservableCollection<Word> _suggestions = new ObservableCollection<Word>( );
        private IEnumerable<Word> _newSuggestions = null;

        public Subject<Word> SuggestionSelected = new Subject<Word>( );
        public Subject<Tuple<string, Color>> MessageStream = new Subject<Tuple<string, Color>>( );
        private Task _updaterTask = null;
        private CancellationTokenSource _cancellationSource = null;
        private CancellationToken _cancellationToken;

        public SuggestionsTable( ) {
            InitializeComponent( );
            Suggestions.ItemsSource = _suggestions;
            _cancellationSource = new CancellationTokenSource( );
            _cancellationToken = _cancellationSource.Token;
            _updaterTask = Task.Factory.StartNew( new Action( ( ) => UpdateSuggestionsBackground( ) ), _cancellationToken );
        }

        public bool Empty { get { return _suggestions.Count == 0; } }

        public Word GetSuggestion( int index ) {
            return ( index < Suggestions.Items.Count ) ? _suggestions[index] : null;
        }

        internal void NewSuggestions( IEnumerable<Word> suggestions ) {
            Interlocked.Exchange( ref _newSuggestions, suggestions );
            MessageStream.OnNext( Tuple.Create( "Searching dictionary...", Colors.Red ) );
        }

        /// <summary>
        /// This will run for the whole duration of the software
        /// </summary>
        private void UpdateSuggestionsBackground( ) {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            IEnumerable<Word> currentlyProcessingSuggestions = null;
            while ( true ) {
                if ( _cancellationToken.IsCancellationRequested ) return;
                if ( object.ReferenceEquals( _newSuggestions, currentlyProcessingSuggestions ) ) {
                    Thread.Sleep( 50 );
                } else {
                    currentlyProcessingSuggestions = _newSuggestions;
                    UpdateSuggestionsInner( currentlyProcessingSuggestions );
                }
            }
        }

        private void UpdateSuggestionsInner( IEnumerable<Word> suggestions ) {
            Dispatcher.BeginInvoke( new Action( ( ) => _suggestions.Clear( ) ) );
            var index = 0;
            foreach ( var suggestion in suggestions ) {
                if ( !object.ReferenceEquals( _newSuggestions, suggestions ) ) return;
                index++;
                suggestion.Index = index;
                suggestion.Shortcut = index == 1 ? "Enter" : index <= 10 ? index.ToString() : "<click>";
                Dispatcher.BeginInvoke( new Action( ( ) => _suggestions.Add( suggestion) ) );
            }
            MessageStream.OnNext( Tuple.Create( string.Format( "{0} suggestions", index ), Colors.Black ) );
            Dispatcher.BeginInvoke( new Action( ( ) => {
                foreach ( var col in Suggestions.Columns ) {
                    col.Width = DataGridLength.SizeToHeader;
                    col.Width = DataGridLength.SizeToCells;
                }
            } ) );
        }

        private void Suggestions_MouseUp( object sender, MouseButtonEventArgs e ) {
            Console.WriteLine( sender );
            var source = (DependencyObject)e.OriginalSource;
            var row = (DataGridRow)GuiUtils.FindParent( source, typeof( DataGridRow ) );
            var cell = (DataGridCell)GuiUtils.FindParent( source, typeof( DataGridCell ) );
            if ( row == null || cell == null ) return;
            if ( cell.Column.Header.ToString( ) == "Shortcut" ) {
                SuggestionSelected.OnNext( (Word) row.Item );
                e.Handled = true;
            }
        }

        void items_CollectionChanged( object sender, NotifyCollectionChangedEventArgs e ) {
            if ( e.Action == NotifyCollectionChangedAction.Remove ) {
                foreach ( Word item in e.OldItems ) {
                    item.Delete( );
                }
            }
        }

        internal void SelectFirst( ) {
            SuggestionSelected.OnNext( _suggestions[0] );
        }

        private void Suggestions_RowEditEnding( object sender, DataGridRowEditEndingEventArgs e ) {
            if ( Word.WordUpdateException != null ) {
                var ex = Word.WordUpdateException;
                Word.WordUpdateException = null;
                MessageBox.Show( Window.GetWindow( this ), ex.ToString(), "Error during word editing" );
            }
        }

        private void UserControl_Unloaded( object sender, RoutedEventArgs e ) {
            _cancellationSource.Cancel( );
            _updaterTask.Wait( );
        }
    }
}
