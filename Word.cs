using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using RT = clojure.lang.RT;

namespace ChineseWriter {

    public interface IHanyuPinyin {
        string Hanyu { get; }
        string Pinyin { get; }
    }

    public class HanyuPinyin : IHanyuPinyin {
        public string Hanyu { get; set; }
        public string Pinyin { get; set; }
    }

    /// <summary>
    /// Object for data-grid inteop
    /// </summary>
    public class Word : IHanyuPinyin {

        public static Exception WordUpdateException = null;
        private IHanyuPinyin _hanyuPinyin;
        private string _pinyinDiacritics; // Immutable word ID stuff, make a copy

        public Word( IDictionary<object,object> word ) : this(word.Hanyu(), word.Pinyin())  {
        }

        public Word( string hanyu, string pinyin ) {
            _hanyuPinyin = new HanyuPinyin { Hanyu = hanyu, Pinyin = pinyin };
        }

        public Word( IHanyuPinyin hanyuPinyin, string shortCut, int index = 0 ) {
            _hanyuPinyin = hanyuPinyin;
            Index = index;
            Shortcut = shortCut;
        }

        public int Index { get; set; }
        public string Shortcut { get; set; }
        public string PinyinDiacritics { 
            get {
                if ( _pinyinDiacritics == null ) {
                    _pinyinDiacritics = DiacriticsAdderFunc.AddDiacritics( Pinyin );
                }
                return _pinyinDiacritics; } 
            set { } 
        }
        public string Pinyin { get { return _hanyuPinyin.Pinyin; } }
        public string Hanyu { get { return _hanyuPinyin.Hanyu; } set { } }
        public string Known {
            get { return WordDatabase.KNOWLEDGE_LEVEL_DESCR[ KnownLevel ]; }
            set { KnownLevel = WordDatabase.KNOWLEDGE_LEVEL_VALUE[value]; }
        }
        public int KnownLevel { 
            get { return Convert.ToInt32( Get( "known" ) ); }
            set { Set( "known", Convert.ToInt64( value ) ); }
        }
        public string ShortEnglish {
            get { return (string) Get( "short-english" ); }
            set { Set("short-english", value ); }
        }
        public string Source {
            get { return (string)Get( "source" ); }
            set { Set( "source", value ); }
        }
        public object HSKIndex {
            get { return Get( "hsk-index" ); }
            set { Set( "hsk-index", value ); }
        }
        public object HanziRarity {
            get { return Get( "hanzi-rarity" ); }
            set { Set( "hanzi-rarity", value ); }
        }
        public string English {
            get { return (string)Get("english" ); }
            set { Set("english", value ); }
        }
        public string Finnish {
            get { return (string)Get( "finnish" ); }
            set { Set( "finnish", value ); }
        }
        public string Image {
            get { return (string)Get( "image" ); }
            set { Set( "image", value ); }
        }
        internal void Delete( ) {
            WordDatabase.DeleteWordInfo( this );
        }
        private object Get( string propName ) { 
            return WordDatabase.GetWordProp( this, propName );
        }
        private void Set( string propName, object value ) {
            try {
                WordDatabase.SetWordProp( this, propName, value );
            } catch ( Exception ex ) {
                WordUpdateException = ex;
            }
        }
    }

    /// <summary>
    /// NOTE: A word can be also a literal word! Not just Hanyu-Pinyin!
    /// </summary>
    public static class WordExtensions {

        public static T Get<T>( this IDictionary<object, object> dict, string key ) {
            return (T)Convert.ChangeType( dict[RT.keyword( null, key )], typeof( T ) );
        }

        public static IList<object> GetList( this IDictionary<object, object> dict, string key ) {
            return (IList<object>)dict[RT.keyword( null, key )];
        }

        public static bool HasKeyword( this IDictionary<object, object> dict, string key ) {
            return dict.ContainsKey( RT.keyword( null, key ) );
        }

        public static bool IsLiteralText( this IDictionary<object, object> word ) { return word.HasKeyword( "text" ); }

        public static string Pinyin( this IDictionary<object, object> word ) { return word.Get<string>( "pinyin" ); }

        public static string Hanyu( this IDictionary<object, object> word ) { return word.Get<string>( "hanyu" ); }

        public static string Text( this IDictionary<object, object> word ) { 
            return IsLiteralText(word) ? word.Get<string>( "text" ) : Hanyu(word); 
        }

        public static bool Known( this IDictionary<object, object> word ) {
            return word.HasKeyword( "known" ) && word.Get<int>( "known" ) >= 2;
        }

        public static string PinyinDiacritics( this IDictionary<object, object> word ) {
            return word.IsLiteralText( ) ? word.Text( ) : new Word( word ).PinyinDiacritics.Replace( " ", "" );
        }

        

    }

}
