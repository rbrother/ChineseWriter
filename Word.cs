using System;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace ChineseWriter {

    public class SuggestionWord {
        public string Pinyin { get; set; }
        public string Hanyu { get; set; }
        public string UsageCountString { get; set; }
        public string English { get; set; }
    }

    public static class WordExtensions {

        public static string GetStr( this IDictionary<string, object> dict, string key ) {
            return Convert.ToString( dict[key] );
        }

        public static IList<object> GetList( this IDictionary<string, object> dict, string key ) {
            return (IList<object>)dict[key];
        }

        public static string Pinyin( this IDictionary<string, object> word ) {
            return word.ContainsKey( "pinyin" ) ?
                word.GetStr( "pinyin" ) : word.GetStr( "text" );
        }

        public static string PinyinDiacritics( this IDictionary<string, object> word ) {
            return word.ContainsKey( "pinyin" ) ?
                word.Pinyin( ).AddDiacritics( ) : word.GetStr( "text" );
        }

        public static string Hanyu( this IDictionary<string, object> word ) {
            return word.ContainsKey( "hanyu" ) ?
                word.GetStr( "hanyu" ) : word.GetStr( "text" );
        }

    }

}
