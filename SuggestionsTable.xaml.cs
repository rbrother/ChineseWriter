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

        public Subject<Word> SuggestionSelected = new Subject<Word>( );
        public Subject<Tuple<string, Color>> MessageStream = new Subject<Tuple<string, Color>>( );
        private Task _updaterTask = null;
        private CancellationTokenSource _cancellationSource = null;
        private CancellationToken _cancellationToken;

        public SuggestionsTable( ) {
            InitializeComponent( );
            Suggestions.ItemsSource = _suggestions;
        }

        public bool Empty { get { return _suggestions.Count == 0; } }

        public Word GetSuggestion( int index ) {
            return ( index < Suggestions.Items.Count ) ? _suggestions[index] : null;
        }

        internal void UpdateSuggestions( IEnumerable<Word> suggestions ) {
            if ( _cancellationSource != null ) {
                _cancellationSource.Cancel( );
                try { 
                    _updaterTask.Wait( );
                } catch ( Exception ex ) {
                    System.Diagnostics.Debug.Print( "Exception at waiting task cancel" );
                }
            }
            MessageStream.OnNext( Tuple.Create( "Searching dictionary...", Colors.Red ) );
            _cancellationSource = new CancellationTokenSource( );
            _cancellationToken = _cancellationSource.Token;
            _updaterTask = Task.Factory.StartNew( new Action( ( ) => UpdateSuggestionsBackground( suggestions ) ), _cancellationToken );
        }

        private void UpdateSuggestionsBackground( IEnumerable<Word> suggestions ) {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            Dispatcher.BeginInvoke( new Action( ( ) => _suggestions.Clear( ) ) );
            var index = 0;
            foreach ( var suggestion in suggestions ) {
                if ( _cancellationToken.IsCancellationRequested ) return;
                index++;
                var shortcut = index == 1 ? "Enter" :
                    index <= 10 ? string.Format( "{0}", index ) : "<click>";
                var hanyuPinyin = new HanyuPinyin { Hanyu = suggestion.Hanyu, Pinyin = suggestion.Pinyin };
                var dataWord = new Word( hanyuPinyin, shortcut, index );
                Dispatcher.BeginInvoke( new Action( ( ) => _suggestions.Add( dataWord ) ) );
            }
            MessageStream.OnNext( Tuple.Create( string.Format( "{0} suggestions", index ), Colors.Black ) );
            Dispatcher.BeginInvoke( new Action( ( ) => {
                foreach ( var col in Suggestions.Columns ) {
                    col.Width = DataGridLength.SizeToHeader;
                    col.Width = DataGridLength.SizeToCells;
                }
            } ));
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
    }
}
