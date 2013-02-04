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
        private int _cursorPos;
        private int _selectionStart = 0;
        private int _selectionLength = 0;
        private ChineseWordInfo[] _words;
        private ChineseWordInfo[] _suggestions;
        private bool _english;
        private string _pinyinInput = "";

        // Observables
        public Subject<string> PinyinChanges = new Subject<string>( );
        public Subject<bool> EnglishChanges = new Subject<bool>( );
        public Subject<ChineseWordInfo[]> SuggestionsChanges = new Subject<ChineseWordInfo[]>( );
        public Subject<ChineseWordInfo[]> WordsChanges = new Subject<ChineseWordInfo[]>( );
        public Subject<int> CursorPosChanges = new Subject<int>( );
        public IObservable<int> WordsDatabaseChanged;

        // Observables accessors
        public bool English {
            get { return _english;  }
            set { _english = value;  EnglishChanges.OnNext( value ); }
        }
        public string PinyinInput {
            get { return _pinyinInput;  }
            set { _pinyinInput = value; PinyinChanges.OnNext( value ); }
        }
        public ChineseWordInfo[] Words { 
            get { return _words; }
            set { _words = value; WordsChanges.OnNext( _words ); }
        }
        public int CursorPos {
            get { return _cursorPos; }
            set { _cursorPos = value; CursorPosChanges.OnNext( value ); }
        }

        // Constructor
        public WritingState( ) {
            WordsDatabaseChanged = new int[] { 0 }.ToObservable( ).
                Concat( _hanyuDb.WordsChanged );
            PinyinChanges.CombineLatest( EnglishChanges, ( pinyin, english ) => 0 ).
                Subscribe( value => UpdateSuggestions( ) );
        }

        private void UpdateSuggestions( ) {
            _suggestions = PinyinInput == "" ?
                new ChineseWordInfo[] {} :
                _hanyuDb.MatchingSuggestions( PinyinInput, _english ).Take( 9 ).ToArray();
            SuggestionsChanges.OnNext( _suggestions );
        }

        public void AddPinyinInput( string newPinyin ) {
            PinyinInput = PinyinInput + newPinyin;
        }

        public void BackSpace( ) {
            if (PinyinInput == "") {
                if (CursorPos > 0) {
                    Words = Words.Take( CursorPos - 1 ).
                        Concat( Words.Skip( CursorPos ) ).
                        ToArray( );
                    CursorPos--;
                }
            } else {
                PinyinInput = PinyinInput.DropLast( );
            }
        }

        internal void SelectPinyin( int n ) {
            if (n <= _suggestions.Length) {
                Words = Words.Take( CursorPos ).
                    Concat( new ChineseWordInfo[] { WordToInsert(n) } ).
                    Concat( Words.Skip( CursorPos ) ).
                    ToArray( );
                PinyinInput = "";
                CursorPos = CursorPos + 1;
            }
        }

        private ChineseWordInfo WordToInsert( int suggestionIndex ) {
            return suggestionIndex == 0 ?
                new ChineseWordInfo { hanyu = PinyinInput, pinyin = PinyinInput, english = PinyinInput } :
                _suggestions[suggestionIndex - 1];
        }

        public object HanyiPinyinLines { 
            get {
                var pinyinLine = string.Join( "  ", Words
                    .Select( word => word.PinyinString ).ToArray( ) );
                var hanyiLine = string.Join( " ", Words
                    .Select( word => word.hanyu ).ToArray( ) );
                return hanyiLine + "\n" + pinyinLine;
            } 
        }

        internal void MoveRight( ) {
            CursorPos = Math.Min( CursorPos + 1, Words.Length );
        }

        internal void MoveLeft( ) {
            CursorPos = Math.Max( CursorPos - 1, 0 );
        }

        internal void Clear( ) {
            Words = new ChineseWordInfo[] { };
            PinyinInput = "";
            CursorPos = 0;
        }
    } // class

} // namespace
