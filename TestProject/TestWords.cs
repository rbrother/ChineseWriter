using System;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChineseWriter {
    [TestClass]
    public class TestWords {

        private static string INFO_STRING = @"
            <Chinese>
              <Words>
                <Word pinyin='a1' hanyu='啊' />
                <Word pinyin='ai4' hanyu='爱' />
                <Word pinyin='ai4 ren5' hanyu='爱人' />
                <Word pinyin='an1' hanyu='安' />
                <Word pinyin='wo3' hanyu='我' usage_count='5' />
                <Word pinyin='wo3 men5' hanyu='我们' known='true' short_english='we' />
              </Words>
            </Chinese>
            ";

        private static string[] CC_LINES = new string[] {
            "啊 啊 [a1] /interjection of surprise/Ah!/Oh!/",
            "啊 啊 [a2] /interjection expressing doubt or requiring answer/Eh?/what?/to show realization/to stress/",
            "婐 婐 [wo3] /maid/",
            "我 我 [wo3] /I/me/my/",
            "我們 我们 [wo3 men5] /we/us/ourselves/our/",
            "戒刀 戒刀 [jie4 dao1] /Buddhist monk's knife (not used for killing)/",
            "女友 女友 [nu:3 you3] /girlfriend/"
        };

        private static Dictionary<Tuple<string,string>, XElement> INFO_DICT =
            WordDatabase.ParseInfoDict( XElement.Parse( INFO_STRING ) );

        [TestMethod]
        public void TestParseInfo( ) {
            Assert.AreEqual<int>( 6, INFO_DICT.Count );
            Assert.AreEqual( typeof( XElement ), INFO_DICT[Tuple.Create("我","wo3")].GetType( ) );
            Assert.AreEqual( typeof( XElement ), INFO_DICT[Tuple.Create("我们","wo3 men5")].GetType( ) ); 
        }

        [TestMethod]
        public void TestParseCC( ) {
            var words = WordDatabase.ParseCCLines( CC_LINES, INFO_DICT );
            Assert.AreEqual<int>( 7, words.Length );
            Assert.AreEqual<int>( 3, words.Where( word => word.Suggest ).Count( ) );
            Assert.AreEqual<int>( 2, words.Where( word => word.Hanyu == "啊" ).Count( ) );
            Assert.AreEqual<int>( 1, words.Where( word => word.Hanyu == "啊" && word.Pinyin == "a1" ).Count( ) );
            var girlfriend = words.Where( word => word.Hanyu == "女友" ).First();
            Assert.AreEqual( "nu:3 you3", girlfriend.Pinyin );
            Assert.AreEqual( "nǚ yǒu", girlfriend.DisplayPinyin );
            Assert.AreEqual("girlfriend", girlfriend.ShortEnglish);
            Assert.AreEqual(3, words.Where(word => word.MatchesPinyin("wo")).Count());
            Assert.AreEqual(3, words.Where(word => word.MatchesPinyin("wo3")).Count());
            Assert.AreEqual(1, words.Where(word => word.MatchesPinyin("women")).Count());
            Assert.AreEqual(1, words.Where(word => word.MatchesPinyin("wo3men5")).Count());
            Assert.AreEqual(1, words.Where(word => word.Known).Count());
            var wo3men5 = words.Where(word => word.MatchesPinyin("wo3men5")).First();
            var a2 = words.Where(word => word.MatchesPinyin("a2")).First();
            Assert.AreEqual(false, a2.ShortEnglishGiven);
            Assert.AreEqual("interjection expressing doubt or requiring answer", a2.ShortEnglish);
            Assert.AreEqual(true, wo3men5.ShortEnglishGiven);
            Assert.AreEqual("we", wo3men5.ShortEnglish);
            a2.SetShortEnglish( "eh" );
            Assert.AreEqual(true, a2.ShortEnglishGiven);
            Assert.AreEqual("eh", a2.ShortEnglish);
            Assert.AreEqual(2, wo3men5.Characters.Length);
            Assert.AreEqual("wo3", wo3men5.Characters.First().Item2);
        }

        [TestMethod]
        public void TestParseSentence() {
            var db = new WordDatabase(WordDatabase.ParseCCLines(CC_LINES, INFO_DICT) );
            var words = db.HanyuToWords("啊 我女友QQ戒刀");
            Assert.AreEqual(5, words.Length);
        }
    }
}
