(ns WordDatabaseTests
  (:use WordDatabase)
  (:use clojure.set)  
  (:use clojure.pprint)
  (:use clojure.test))

(deftest zip-test (is (= [ [1 :a] [2 :b] [3 :c] ] (zip [ 1 2 3 ] [ :a :b :c ] ))))

(deftest starts-with-test
  (is (starts-with "moikka" "moi"))
  (is (not (starts-with "moikka" "hei"))))

(def test-words-raw 
  [
   {:hanyu "一下子", :pinyin "yi1 xia4 zi5", :english "in a short while, all at once, all of a sudden"},
   {:hanyu "一世", :pinyin "yi1 shi4", :english "generation, period of 30 years, one's whole lifetime, lifelong, age, era, times, the whole world, the First (of numbered European kings)"},
   {:hanyu "一丘之貉", :pinyin "yi1 qiu1 zhi1 he2", :english "jackals of the same tribe (idiom); fig. They are all just as bad as each other."},
   {:hanyu "一中一台", :pinyin "yi1 Zhong1 yi1 Tai2", :english "one China and one Taiwan (policy)"},
   {:hanyu "一中原则", :pinyin "yi1 zhong1 yuan2 ze2", :english "One-China principle, the official doctrine that Taiwan is a province of China"},
   {:hanyu "一串", :pinyin "yi1 chuan4", :english "strand"},
   {:hanyu "一之为甚", :pinyin "yi1 zhi1 wei2 shen4", :english "Once is enough (idiom)"},
   {:hanyu "一之谓甚", :pinyin "yi1 zhi1 wei4 shen4", :english "see 一之為甚|一之为甚[yi1 zhi1 wei2 shen4]"},
   {:hanyu "一干二净", :pinyin "yi1 gan1 er4 jing4", :english "thoroughly (idiom), completely, one and all, very clean"},
   {:hanyu "一了百了", :pinyin "yi1 liao3 bai3 liao3", :english "once the main problem is solved, all troubles are solved, death ends all one's troubles"},
   {:hanyu "一事无成", :pinyin "yi1 shi4 wu2 cheng2", :english "to have achieved nothing, to be a total failure, to get nowhere"},
   {:hanyu "一二八事变", :pinyin "yi1 er4 ba1 shi4 bian4", :english "Shanghai incident of 28th January 1932, Chinese uprising against Japanese quarters of Shanghai"},
   {:hanyu "一五一十", :pinyin "yi1 wu3 yi1 shi2", :english "lit. count by fives and tens (idiom); to narrate systematically and in full detail"},
   {:hanyu "一些", :pinyin "yi1 xie1", :english "some, a few, a little"},
   {:hanyu "一代", :pinyin "yi1 dai4", :english "generation"},
   {:hanyu "一代不如一代", :pinyin "yi1 dai4 bu4 ru2 yi1 dai4", :english "to be getting worse with each generation"},
   {:hanyu "一并", :pinyin "yi1 bing4", :english "to lump together, to treat along with all the others"},
   {:hanyu "一来", :pinyin "yi1 lai2", :english "on one hand,..."},
   {:hanyu "一来二去", :pinyin "yi1 lai2 er4 qu4", :english "gradually, little by little, in the course of time"},
   {:hanyu "一个中国政策", :pinyin "yi1 ge4 Zhong1 guo2 zheng4 ce4", :english "one China policy"},
   {:hanyu "一个人", :pinyin "yi1 ge4 ren2", :english "alone"},
   {:hanyu "我", :pinyin "wo3", :english "I, me, my"},
   {:hanyu "们", :pinyin "men5", :english "plural marker for pronouns, and nouns referring to individuals"},
   {:hanyu "我们", :pinyin "wo3 men5", :english "we, us, ourselves, our"},
   {:hanyu "女友", :pinyin "nu:3 you3", :english "girlfriend"},
   {:hanyu "爱人", :pinyin "ai4 ren5", :english "spouse, husband, wife, sweetheart, CL:個|个[ge4]"}
   {:hanyu "爱", :pinyin "ai4", :english "to love, affection, to be fond of, to like"}
   {:hanyu "人", :pinyin "ren2", :english "man, person, people, CL:個|个[ge4],位[wei4]"}
])

(def test-word-info
  [ { :pinyin "wo3", :hanyu "我", :short-english "I", :known true, :usage-count 112 }
    { :pinyin "wo3 men5", :hanyu "我们", :known true, :usage-count 7 } ])

(def wo-men-word
  {:hanyu "我们",
   :pinyin "wo3 men5",
   :english "we, us, ourselves, our"
   :pinyin-no-spaces "wo3men5"
   :pinyin-no-spaces-no-tones "women"
   :known true 
   :usage-count 7 
   :pinyin-start "wo" })

(def wo-expanded
  {:hanyu "我", :pinyin "wo3", :pinyin-start "wo", :english "I, me, my",
   :pinyin-no-spaces-no-tones "wo",
   :pinyin-no-spaces "wo3",
   :short-english "I", :known true, :usage-count 112,
   :characters 
   [ {:hanyu "我", :pinyin "wo3", :pinyin-start "wo", :english "I, me, my",
      :pinyin-no-spaces-no-tones "wo",
      :pinyin-no-spaces "wo3",
      :short-english "I", :known true, :usage-count 112} ]} )
  
(def wo-men-word-expanded 
  {:pinyin-no-spaces-no-tones "women",
   :pinyin-no-spaces "wo3men5",
   :pinyin-start "wo",
   :hanyu "我们",
   :pinyin "wo3 men5",
   :english "we, us, ourselves, our",
   :known true,
   :usage-count 7,   
   :characters
   [{:pinyin-start "wo",
     :pinyin-no-spaces-no-tones "wo",
     :pinyin-no-spaces "wo3",
     :known true,
     :short-english "I",
     :hanyu "我",
     :pinyin "wo3",
     :english "I, me, my",
     :usage-count 112 }
    {:pinyin-no-spaces-no-tones "men",
     :pinyin-no-spaces "men5",
     :pinyin-start "me",
     :hanyu "们",
     :pinyin "men5",
     :english
     "plural marker for pronouns, and nouns referring to individuals"}]})

(set-word-database! test-words-raw test-word-info)

(def women-word-calculated (first (filter #(= (% :pinyin) "wo3 men5") @word-database)))

(def airen-chars ((expanded-word "爱人" "ai4 ren5") :characters))

(def second-airen-char (nth airen-chars 1))

(deftest cc-lines-test
  (is (= 28 (count @word-database)))
  (is (= 2 (count (find-words "wo3") )))
  (is (= wo-men-word women-word-calculated))
  (is (= wo-men-word-expanded (expanded-word "我们" "wo3 men5")))
  (is (= "ren2" ((find-char "人" "ren2") :pinyin)))
  (is (= "ren2" ((find-char "人" "Ren2") :pinyin)))
  (is (= "ren2" ((find-char "人" "ren5") :pinyin)))
  (is (= 2 (count airen-chars)))
  (is (= "人" (second-airen-char :hanyu)))
  (is (= wo-expanded (expanded-word "我" "wo3")))
  (is (= (count (hanyu-to-words "我们女友" )) 2 ))
  (is (= (count (hanyu-to-words "我们QQ女友" )) 3 )))

(run-tests)