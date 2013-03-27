(ns WordDatabase
  (:require [clojure.string :as str])
  (:use clojure.set)
  (:use clojure.clr.io))

; helpers

(defn zip [list1 list2] (map vec (partition 2 (interleave list1 list2) )))

(defn starts-with-pattern [start] (re-pattern (str/join ["^" start]) ))

(defn starts-with [str start] (re-find (re-pattern (str/join ["^" start]) ) str ))

(defn equal-caseless [ str1 str2 ] (= (str/lower-case str1) (str/lower-case str2)))

(defn single? [coll] (= (count coll) 1 ) )

;(defn write-to-file [ path value ] (with-open [wri (text-writer path)] (pprint value wri)))

(defn load-from-file [ path ] (read-string (slurp path :encoding "UTF-8")))

(defn map-values [ f m ] (zipmap (keys m) (map f (vals m))))

(defn remove-tone-numbers [ s ] (str/replace s #"\d" ""))

;----------------------- Atoms for the dictionary data ----------------------------------

(def word-database (atom nil))

(def pinyin-start-dict (atom nil)) ; indexed by 2 first chars of pinyin

(def hanyu-dict (atom nil))

(def hanyu-pinyin-dict (atom nil))

;---------------------------------------------------------

(defn toneless-equal [ a b ] (= (remove-tone-numbers a) (remove-tone-numbers b) ))

(defn find-char [ hanyu pinyin ]
  (let [ exact-match (@hanyu-pinyin-dict { :hanyu hanyu :pinyin pinyin }) ]
    (if exact-match (first exact-match)
      ; Look for non-exact matches that might happen because of tone changes of char as part of a word
      (let [ hanyu-matches (@hanyu-dict hanyu) 
            caseless-match (filter #(equal-caseless (% :pinyin) pinyin) hanyu-matches)
            toneless-matches (filter #(toneless-equal (% :pinyin) pinyin) hanyu-matches)]
        (cond
          caseless-match (first caseless-match)
          toneless-matches (first toneless-matches)
          :else (first hanyu-matches) )))))

; This is slow, so do only for a word when needed, not for all words
(defn expanded-word [ hanyu pinyin ]
  (let [ word (first (@hanyu-pinyin-dict { :hanyu hanyu :pinyin pinyin } )) ]
    (->> 
      (zip (map str hanyu) (str/split pinyin #" "))
      (map (fn [ [h p] ] (find-char h p)))
      (vec)
      (assoc word :characters))))

;--------------- Loading database -------------------------------------

(defn merge-info-word [ {:keys [hanyu pinyin] :as word} info-dict ]
  (let [pinyin-no-spaces (str/lower-case (str/replace pinyin #"[: ]" ""))
        dict-entry (first (or (info-dict {:hanyu hanyu :pinyin pinyin}) [{}]))]
    (merge
      word
      (if (dict-entry :hanyu) { :usage-count 1 } {} ) ; ensure at least 1 usage count if entry in words.clj
      dict-entry
      {:pinyin-no-spaces pinyin-no-spaces 
       :pinyin-no-spaces-no-tones (remove-tone-numbers pinyin-no-spaces) 
       :pinyin-start (subs pinyin-no-spaces 0 2) })))

; Add extra properties from words.xml
(defn merge-info [words info]
  (let [ info-dict (index info [ :hanyu :pinyin ]) ]
    (->> words
      (map #(merge-info-word % info-dict)))))

(defn suggestion-comparer [ { hanyu1 :hanyu pinyin1 :pinyin :as word1} { hanyu2 :hanyu pinyin2 :pinyin :as word2 } ]
  (let [ uc1 (get word1 :usage-count 0) uc2 (get word2 :usage-count 0) ]
    (cond
      (not= uc1 uc2) (if (> uc1 uc2) -1 1 )
      (not= (count hanyu1) (count hanyu2)) (if (< (count hanyu1) (count hanyu2)) -1 1 )
      :else (compare pinyin1 pinyin2))))

(defn sort-suggestions [ suggestions ] (sort suggestion-comparer suggestions))

(defn create-pinyin-start-dict [ words ]
  (->> (index words [:pinyin-start])
    (map-values sort-suggestions))) ; pre-sort all suggestions, so no sorting at runtime

(defn set-word-database! [words-raw info-dict]
  (do
    (reset! word-database (merge-info words-raw info-dict))
    (reset! hanyu-dict (index @word-database [:hanyu]))
    (reset! hanyu-pinyin-dict (index @word-database [ :hanyu :pinyin ]))
    (reset! pinyin-start-dict (create-pinyin-start-dict @word-database))))

; It's better to store the data at clojure-side. Casting the data to more C# usable
; format renders it less usable to clojure code
(defn load-database [ cc-dict-file info-file ]
  (set-word-database!
    (load-from-file cc-dict-file) (load-from-file info-file)))

;-------------------  Finding words   ----------------------------------------

(defn find-words [ pinyin ]
  (let [ pattern (starts-with-pattern pinyin)
        pinyin-matcher (fn [ { p1 :pinyin-no-spaces p2 :pinyin-no-spaces-no-tones } ]
                         (or (re-find pattern p1) (re-find pattern p2) )) ] 
    (if (< (count pinyin) 2) [] ; only suggest for 2+ letter of pinyin
      (->> (@pinyin-start-dict { :pinyin-start (subs pinyin 0 2) } )
        (filter pinyin-matcher)
        ;(sort suggestion-comparer)
        (take 50)
        (vec)))))

; -------------------- Parsing chinese text to words ---------------------

(defn pick-word [words]
  (first words)) ; TODO: Implement selection of most likely word based on usage-counts etc.

(defn find-first-word-len [ chinese len ]
  (if (= len 0) { :text (subs chinese 0 1) }
    (let [words (@hanyu-dict { :hanyu (subs chinese 0 len) } )]
      (if words (pick-word words) 
        (find-first-word-len chinese (dec len))))))
      
(def non-hanyu-regexp #"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=]+")

(defn find-first-word [ chinese ]
  (let [non-hanyu (re-find non-hanyu-regexp chinese)]
    (if non-hanyu { :text non-hanyu }
      (find-first-word-len chinese (min (count chinese) 7)))))

(defn word-len [ { :keys [hanyu text] :as word } ]
  (cond 
    hanyu (count hanyu)
    text (count text)
    :else (throw (Exception. "Cannot determine length for word"))))

; example: 很抱歉.没有信号 -> ["很","抱歉",".","没有","信号"]
(defn hanyu-to-words [ chinese ]
  (cond 
    (= chinese "") []
    (starts-with chinese " ") (hanyu-to-words (subs chinese 1))
    :else 
    (let [ first-word (find-first-word chinese )
          remaining-chinese (subs chinese (word-len first-word)) ]    
      (cons first-word (hanyu-to-words remaining-chinese)))))

(defn find-word-for-hanyu-pinyin [ dict hanyu pinyin ]
  (let [words (dict { :hanyu hanyu }) 
        exact-matches (filter #(= (% :pinyin) pinyin )) 
        caseless-matches (filter #(equal-caseless (% :pinyin) pinyin )) ]
    (cond 
      (not (zero? (count exact-matches))) (first exact-matches)
      (not (zero? (count caseless-matches))) (first caseless-matches)
      :else (first words) ))) ; match based on Hanyi alone can be necessary when a character has changed from some tone to neutral tone when combined to a word.
      
