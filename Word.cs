using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using RT = clojure.lang.RT;

namespace ChineseWriter {

    public class SuggestionWord {

        public SuggestionWord( int index, IDictionary<object, object> word, string shortCut ) {
            Index = index;
            Word = word; // for later retrieval when suggetion used
            Shortcut = shortCut;
        }

        public int Index { get; set; }
        public string Shortcut { get; set; }
        public string Pinyin { get { return Word.PinyinDiacritics( ); } set { } }
        public string Hanyu { get { return Word.Hanyu( ); } set { } }
        public string UsageCountString { get { return Word.UsageCountStr( ); } set { } }
        public bool Known { get { return Word.Known( ); } set { } }
        public string English { get { return Word.Get<string>( "english" ); } set { } }
        public IDictionary<object, object> Word { set; get; }
    }

    public static class WordExtensions {

        public static T Get<T>( this IDictionary<object,object> dict, string key ) {
            return (T) Convert.ChangeType( dict[ RT.keyword( null, key) ], typeof(T) );
        }

        public static IList<object> GetList( this IDictionary<object,object> dict, string key ) {
            return (IList<object>)dict[RT.keyword( null, key )];
        }

        public static bool HasKeyword( this IDictionary<object, object> dict, string key ) {
            return dict.ContainsKey( RT.keyword( null, key ) );
        }

        public static string Pinyin( this IDictionary<object,object> word ) {
            return word.HasKeyword("pinyin") ?
                word.Get<string>( "pinyin" ) : word.Get<string>( "text" );
        }

        public static string ShortEnglish( this IDictionary<object,object> word ) {
            return word.HasKeyword("text") ? word.Get<string>("text") :
                word.Known() ? "" : word.Get<string>( "short-english" );
        }

        public static bool Known( this IDictionary<object, object> word ) {
            return word.HasKeyword("known") && word.Get<bool>( "known" );
        }

        public static IEnumerable<IDictionary<object,object>> Characters( this IDictionary<object, object> word ) {
            return word.GetList( "characters" ).Cast<IDictionary<object, object>>( );
        }

        public static string PinyinDiacritics( this IDictionary<object,object> word ) {
            return word.HasKeyword( "pinyin-diacritics" ) ? word.Get<string>("pinyin-diacritics") :
                word.HasKeyword( "pinyin" ) ? word.Get<string>("pinyin") : 
                word.Get<string>( "text" );
        }

        public static string Hanyu( this IDictionary<object,object> word ) {
            return word.HasKeyword( "hanyu" ) ?
                word.Get<string>( "hanyu" ) : word.Get<string>( "text" );
        }

        public static string UsageCountStr( this IDictionary<object,object> word ) {
            return word.HasKeyword( "usage-count" ) ?
                Convert.ToString( word.Get<int>("usage-count") ) : "";
        }

    }

}
