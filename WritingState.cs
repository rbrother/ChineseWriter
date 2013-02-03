using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ChineseWriter {

    internal class WritingState {

        private WordDatabase _hanyuDb = new WordDatabase( );
        private int _cursorPos = 0;
        private int _selectionStart = 0;
        private int _selectionLength = 0;
        private IEnumerable<ChineseWordInfo> _words = new ChineseWordInfo[] { };
        private bool _english;
        private string _pinyinInput = "";

        // Observables
        public Subject<string> PinyinChanges = new Subject<string>( );
        public Subject<bool> EnglishChanges = new Subject<bool>( );
        public Subject<IEnumerable<ChineseWordInfo>> SuggestionsChanges = new Subject<IEnumerable<ChineseWordInfo>>( );
        public Subject<IEnumerable<ChineseWordInfo>> WordsChanges = new Subject<IEnumerable<ChineseWordInfo>>( );
        public IObservable<int> WordsDatabaseChanged;

        // Simple accessors
        public int CursorPos { get { return _cursorPos; } }

        // Observables accessors
        public bool English {
            get { return _english;  }
            set { _english = value;  EnglishChanges.OnNext( value ); }
        }
        public string PinyinInput {
            get { return _pinyinInput;  }
            set { _pinyinInput = value; PinyinChanges.OnNext( value ); }
        }
        public IEnumerable<ChineseWordInfo> Words { 
            get { return _words; }
            set { _words = value; WordsChanges.OnNext( _words ); }
        }

        // Constructor
        public WritingState( ) {
            WordsDatabaseChanged = new int[] { 0 }.ToObservable( ).
                Concat( _hanyuDb.WordsChanged );
            PinyinChanges.CombineLatest( EnglishChanges, ( pinyin, english ) => 0 ).
                Subscribe( value => UpdateSuggestions( ) );
            PinyinInput = "";
            English = false;
        }

        private void UpdateSuggestions( ) {
            SuggestionsChanges.OnNext( _hanyuDb.MatchingSuggestions( PinyinInput, _english ).Take( 9 ) );
        }

        public string ChineseText {
            get {
                throw new NotImplementedException( "This should return current text as hanyi string" );
            }
        }

        public string SelectedChineseText {
            get {
                throw new NotImplementedException( "This should return current selected text as hanyi string" );
            }
        }

        public void AddPinyinInput( string newPinyin ) {
            PinyinInput = PinyinInput + newPinyin;
        }

        public void BackSpace( ) {
            if (PinyinInput == "") {
                throw new NotImplementedException( );
            } else {
                PinyinInput = PinyinInput.DropLast( );
            }
        }

        internal void SelectPinyin( int n ) {
            
        }

        public object HanyiPinyinLines { 
            get { 
                return _hanyuDb.HanyiPinyinLines( ChineseText ); 
            } 
        }

    } // class

} // namespace
