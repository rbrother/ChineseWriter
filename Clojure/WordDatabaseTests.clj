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
  { :hanyu "我们",
    :pinyin "wo3 men5",
    :english "we, us, ourselves, our." })

(def man-word
  { :english "man, person, people, CL:个[ge4],位[wei4]"
    :pinyin "ren2"
    :hanyu "人" } )

(def wo-men-word-full
  {:known 4, :hsk-index 9, :hanzi-rarity 114, :short-english "we",
   :hanyu "我们", :pinyin "wo3 men5", :english "we, us, ourselves, our.",
   :finnish "me / meidän (gen) / meitä (part)" } )

(def madeira-breakdown
    [ { :known 1, :hanzi-rarity 4491, :short-english "Madeira", :hanyu "马德拉群岛",
        :pinyin "Ma3 de2 la1 Qun2 dao3", :english "Madeira, the Madeira islands" }
      { :english "horse, CL:匹[pi3], horse or cavalry piece in Chinese chess, knight in Western chess", :pinyin "ma3", :hanyu "马"}
      { :english "variant of 德[de2]. virtue, goodness, morality, ethics, kindness, favor, character, kind", :pinyin "de2", :hanyu "德"}
      { :english "to pull, to play (a bowed instrument), to drag, to draw, to chat", :pinyin "la1", :hanyu "拉"}
      { :english "group of islands, archipelago", :pinyin "qun2 dao3", :hanyu "群岛" } ] )

(load-database "C:\\github\\ChineseWriter\\cedict_ts.clj" "C:\\Google Drive\\Ann\\chinese study\\words.clj")

(def yi-dai {:hanyu "一代", :pinyin "yi1 dai4" } )

(update-word-props! yi-dai { :known 2 } )

(deftest cc-lines-test
  (are [ expected calculated ] (= expected calculated)
  107033 (count @all-words)
  131 (count (find-words "wo" false))
  7 (count (find-words "girlfriend" true))
  0 (count (find-words "zoobaba" true))
  wo-men-word (first (@hanyu-dict "我们"))
  wo-men-word-full (get-word "我们" "wo3 men5")
  "back, behind, rear, afterwards, after, later. empress, queen." (:english (get-word "后" "hou4"))
  man-word (simple-props (find-char "人" "ren2"))
  man-word (simple-props (find-char "人" "Ren2"))
  man-word (simple-props (find-char "人" "ren5"))
  2 (count (characters "爱人" "ai4 ren5"))
  2 (count (@hanyu-dict "向" ))
  2 (count (hanyu-to-words "我们女友" ))
  3 (count (hanyu-to-words "我们QQ女友" ))
  madeira-breakdown (word-breakdown "马德拉群岛" "Ma3 de2 la1 Qun2 dao3")

))

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
