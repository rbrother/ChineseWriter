(ns WordDatabaseTests
  (:use Utils)
  (:use WordDatabase)
  (:require WritingState)
  (:require ExportText)
  (:use ParseChinese)
  (:use clojure.set)
  (:use clojure.pprint)
  (:use clojure.test))

(def wo-men-word
  {:hanyu "我们",
   :pinyin "wo3 men5",
   :english "we, us, ourselves, our." })

(def wo-men-word-full
  {:known 4, :hsk-index 9, :hanzi-rarity 114, :short-english "we",
   :hanyu "我们", :pinyin "wo3 men5", :english "we, us, ourselves, our."} )

(load-database "C:\\github\\ChineseWriter\\cedict_ts.clj" "C:\\Google Drive\\Ann\\chinese study\\words.clj")

(def yi-dai {:hanyu "一代", :pinyin "yi1 dai4" } )

(update-word-props! yi-dai { :known 2 } )

(deftest cc-lines-test
  (are [ expected calculated ] (= expected calculated)
  131 (count (find-words "wo" false))
  7 (count (find-words "girlfriend" true))
  0 (count (find-words "zoobaba" true))
  wo-men-word (first (@hanyu-dict "我们"))
  wo-men-word-full (get-word "我们" "wo3 men5")
  "back, behind, rear, afterwards, after, later. empress, queen." (:english (get-word "后" "hou4"))
  "ren2" ((find-char "人" "ren2") :pinyin)
  "ren2" ((find-char "人" "Ren2") :pinyin)
  "ren2" ((find-char "人" "ren5") :pinyin)
  2 (count (characters "爱人" "ai4 ren5"))
  2 (count (@hanyu-dict "向" ))
  2 (count (hanyu-to-words "我们女友" ))
  3 (count (hanyu-to-words "我们QQ女友" ))
))
;"we, us, ourselves, our"

(def current-text-data { :text [ nil nil nil nil nil ] :cursor-pos 2 } )

(deftest current-text-test
  (are [ expected calculated ] (= expected calculated)
     3 (WritingState/moved-cursor-pos current-text-data "Right" )
     3 (WritingState/moved-cursor-pos current-text-data "Right" )
     1 (WritingState/moved-cursor-pos current-text-data "Left" )
     1 (WritingState/moved-cursor-pos current-text-data "Left" )
     0 (WritingState/moved-cursor-pos current-text-data "Home" )
     5 (WritingState/moved-cursor-pos current-text-data "End" )
))

(deftest export-test
  (are [ expected calculated ] (= expected calculated)
       "<span style='color: #00B000;'>我</span><span style='color: #808080;'>们</span>"
       (ExportText/word-hanyu-html wo-men-word)
       "<span style='color: #00B000;'>wo3</span> <span style='color: #808080;'>men5</span>"
       (ExportText/word-pinyin-html wo-men-word)
))

(run-tests)


