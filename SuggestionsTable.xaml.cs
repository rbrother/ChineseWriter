using System;
using System.Threading;
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

        private int CurrentUpdater = 0;
        private int ActiveUpdaters = 0;
        private ObservableCollection<Word> _suggestions = new ObservableCollection<Word>( );

        public Subject<Word> SuggestionSelected = new Subject<Word>( );
        public Subject<Tuple<string, Color>> MessageStream = new Subject<Tuple<string, Color>>( );

        public SuggestionsTable( ) {
            InitializeComponent( );
            Suggestions.ItemsSource = _suggestions;
        }

        public bool Empty { get { return _suggestions.Count == 0; } }

        public Word GetSuggestion( int index ) {
            return ( index < Suggestions.Items.Count ) ? _suggestions[index] : null;
        }

        internal void UpdateSuggestions( IEnumerable<Word> suggestions ) {
            ThreadPool.QueueUserWorkItem(
                state => UpdateSuggestionsBackground( (IEnumerable<Word>)state ), suggestions );
        }

        /// <summary>
        /// TODO: Use dotnet Task-framework for this? Or simply lock-block?
        /// </summary>
        /// <param name="suggestions"></param>
        private void UpdateSuggestionsBackground( IEnumerable<Word> suggestions ) {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            CurrentUpdater++;
            var id = CurrentUpdater;
            ActiveUpdaters++;
            try {
                while ( ActiveUpdaters > 1 ) {
                    Thread.Sleep( 10 );
                    if ( id != CurrentUpdater ) return; // abort old updaters that have been replaced with newer ones
                }
                MessageStream.OnNext( Tuple.Create( "Searching dictionary...", Colors.Red ) );
                this.Dispatcher.BeginInvoke( new Action( ( ) => { _suggestions.Clear( ); } ));
                var index = 1;
                foreach ( var suggestion in suggestions ) {
                    if ( id != CurrentUpdater ) return; // abort old updaters that have been replaced with newer ones
                    var shortcut = index == 1 ? "Enter" :
                        index <= 10 ? string.Format( "CTRL+{0}", index ) : "<click>";
                    var hanyuPinyin = new HanyuPinyin { Hanyu = suggestion.Hanyu, Pinyin = suggestion.Pinyin };
                    var dataWord = new Word( hanyuPinyin, shortcut, index );
                    this.Dispatcher.BeginInvoke( new Action( ( ) => _suggestions.Add( dataWord ) ) );
                    index++;
                }
                MessageStream.OnNext( Tuple.Create( string.Format( "{0} suggestions", Suggestions.Items.Count ), Colors.Black ) );
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
