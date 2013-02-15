﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Key = System.Windows.Input.Key;

namespace ChineseWriter {

    internal class WritingState {

        private WordDatabase _hanyuDb;
        private int _cursorPos;
        private Word[] _words;
        private Word[] _suggestions;
        private bool _english;
        private string _pinyinInput = "";

        // Observables
        public Subject<string> PinyinChanges = new Subject<string>( );
        public Subject<bool> EnglishChanges = new Subject<bool>( );
        public Subject<Word[]> SuggestionsChanges = new Subject<Word[]>( );
        public Subject<Word[]> WordsChanges = new Subject<Word[]>( );
        public Subject<int> CursorPosChanges = new Subject<int>( );

        // Observables accessors
        public bool English {
            get { return _english;  }
            set { _english = value;  EnglishChanges.OnNext( value ); }
        }
        public string PinyinInput {
            get { return _pinyinInput;  }
            set { _pinyinInput = value; PinyinChanges.OnNext( value ); }
        }
        public Word[] Words { 
            get { return _words; }
            set { 
                _words = value;
                if (_cursorPos > _words.Length) CursorPos = _words.Length;
                WordsChanges.OnNext( _words ); 
            }
        }
        public int CursorPos {
            get { return _cursorPos; }
            set { _cursorPos = value; CursorPosChanges.OnNext( value ); }
        }

        // Constructor
        public WritingState( WordDatabase wordDatabase ) {
            _hanyuDb = wordDatabase;
            PinyinChanges.CombineLatest( EnglishChanges, ( pinyin, english ) => 0 ).
                Subscribe( value => UpdateSuggestions( ) );
        }

        private void UpdateSuggestions( ) {
            _suggestions = PinyinInput == "" ?
                new Word[] {} :
                _hanyuDb.MatchingSuggestions( PinyinInput, _english ).Take( 20 ).ToArray();
            SuggestionsChanges.OnNext( _suggestions );
        }

        public void TextEdit( Key key ) {
            switch (key ) {
                case Key.Back: BackSpace(); break;
                case Key.Delete: Delete(); break;
                case Key.Left: CursorPos--; break;
                case Key.Right: CursorPos++; break;
                case Key.Home: CursorPos = 0; break;
                case Key.End: CursorPos = Words.Length; break;
            }
            if (CursorPos < 0) CursorPos = 0;
            if (CursorPos > Words.Length) CursorPos = Words.Length;
        }

        private void Delete() {
            if (CursorPos < Words.Length) {
                Words = Words.Take(CursorPos).
                    Concat(Words.Skip(CursorPos + 1)).
                    ToArray();
            }            
        }

        private void BackSpace() {
            if (CursorPos > 0) {
                var orignalCursor = CursorPos;
                Words = Words.Take( orignalCursor - 1 ).
                    Concat( Words.Skip( orignalCursor ) ).
                    ToArray();
                CursorPos = orignalCursor - 1;
            }
        }

        internal void SelectPinyin( int n ) {
            if (n > _suggestions.Length) return;
            if (n == 0) {
                InsertWords( _hanyuDb.HanyuToWords( PinyinInput ) );
            } else {
                InsertWords( new Word[] { _suggestions[n - 1] } );
            }
            PinyinInput = "";
        }

        private void InsertWords( IEnumerable<Word> newWords ) {
            Words = Words.Take( CursorPos ).
                Concat( newWords ).
                Concat( Words.Skip( CursorPos ) ).
                ToArray( );
            CursorPos = CursorPos + newWords.Count();
        }

        public object HanyiPinyinLines { 
            get {
                var pinyinLine = string.Join( "  ", Words
                    .Select( word => word.DisplayPinyin ).ToArray( ) );
                var hanyiLine = string.Join( " ", Words
                    .Select( word => word.Hanyu ).ToArray( ) );
                return hanyiLine + "\n" + pinyinLine;
            } 
        }

        internal void Clear( ) {
            Words = new Word[] { };
            PinyinInput = "";
            CursorPos = 0;
        }

        internal string Hanyu {
            get { return string.Join( "", Words.Select( word => word.Hanyu ).ToArray( ) ); }
        }

        internal void Reparse( ) {
            Words = _hanyuDb.HanyuToWords( Hanyu );
        }
    } // class

} // namespace
