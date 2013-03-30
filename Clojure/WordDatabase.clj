(ns WordDatabase
  (:use Utils)
  (:require [clojure.string :as str])
  (:use clojure.pprint)
  (:use clojure.set)
  (:use clojure.clr.io))

;----------------------- Atoms for the dictionary data ----------------------------------

; dictionaries for all words. Immutable once set (could they be just defs?)
; Keyed by:
; { :hanyu hanyu }  (values are lists of words)
; { :hanyu hanyu :pinyin pinyin }  (values are single words)
; { :pinyin-start pinyin-start  }  (values are lists of words)
(def word-dict (atom {}))

(def word-info-dict (atom {})) ; retain so that properties like usage-count can be changed at runtime

;----------------------- Dictionary accessors ----------------------------------

(defn get-word [ key ] (@word-dict key))

(defn word-info [ hanyu-pinyin ] (get @word-info-dict hanyu-pinyin hanyu-pinyin ))

;---------------------------------------------------------

(defn remove-tone-numbers [ s ] (str/replace s #"\d" ""))

(defn toneless-equal [ a b ] (= (remove-tone-numbers a) (remove-tone-numbers b) ))

(defn find-char [ hanyu pinyin ]
  (let [ exact-match (get-word { :hanyu hanyu :pinyin pinyin }) ]
    (if exact-match exact-match
      ; Look for non-exact matches that might happen because of tone changes of char as part of a word
      (let [ hanyu-matches (get-word { :hanyu hanyu }) 
            caseless-match (filter #(equal-caseless (% :pinyin) pinyin) hanyu-matches)
            toneless-matches (filter #(toneless-equal (% :pinyin) pinyin) hanyu-matches)]
        (cond
          (not-empty caseless-match) (first caseless-match)
          (not-empty toneless-matches) (first toneless-matches)
          :else (first hanyu-matches) )))))

; This is slow, so do only for a word when needed (mainly when fetched to form current sentence words), not for all words in dictionary
(defn characters [ { hanyu :hanyu pinyin :pinyin } ] 
  (->> (zip (map str hanyu) (str/split pinyin #" "))
    (map (fn [ [h p] ] (find-char h p)))
    (vec)))

(defn expanded-word [ hanyu pinyin ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (merge 
      (get-word key)
      (word-info key) ; info has been already merged at load, but re-merge as it might change runtime
      { :characters (characters key)})))

;--------------- Loading database -------------------------------------

(defn merge-info-word [ {:keys [hanyu pinyin english] :as word} ]
  (let [pinyin-no-spaces (str/lower-case (str/replace pinyin #"[: ]" "")) ]
    (merge word
      ; Default values of attributes, can be overridden in info
      { :usage-count 0
        :known false
        :pinyin-no-spaces pinyin-no-spaces 
        :pinyin-no-spaces-no-tones (remove-tone-numbers pinyin-no-spaces) 
        :pinyin-start (subs pinyin-no-spaces 0 2) 
        :short-english (first (str/split english #",")) }  ; Default short english, can be overwritten by value in dict-entry
      (word-info { :hanyu hanyu :pinyin pinyin } ))))

(defn suggestion-comparer [ { hanyu1 :hanyu pinyin1 :pinyin :as word1} { hanyu2 :hanyu pinyin2 :pinyin :as word2 } ]
  (let [ uc1 (word1 :usage-count) uc2 (word2 :usage-count) ]
    (cond
      (not= uc1 uc2) (if (> uc1 uc2) -1 1 )
      (not= (count hanyu1) (count hanyu2)) (if (< (count hanyu1) (count hanyu2)) -1 1 )
      :else (compare pinyin1 pinyin2))))

(defn sort-suggestions [ suggestions ] (sort suggestion-comparer suggestions))

(defn set-word-database! [words-raw info-dict]
  (do
    (reset! word-info-dict (map-values first (index info-dict [ :hanyu :pinyin ] )))
    (let [ word-database (map merge-info-word words-raw) ]
      (reset! word-dict
        (merge
          (index word-database [ :hanyu ])
          (map-values first (index word-database [ :hanyu :pinyin ]))
          (map-values sort-suggestions (index word-database [ :pinyin-start ])))))))

(defn set-default-usage-count [word] (merge { :usage-count 1 } word ))

; It's better to store the data at clojure-side. Casting the data to more C# usable
; format renders it less usable to clojure code
(defn load-database [ cc-dict-file info-file ]
  (set-word-database!
    (load-from-file cc-dict-file) 
    (map set-default-usage-count (load-from-file info-file))))

(defn update-word-props [ hanyu-pinyin new-props ]
  (let [ new-word (merge (word-info hanyu-pinyin) new-props) ]
    (reset! word-info-dict (assoc @word-info-dict hanyu-pinyin new-word ))))

(defn usage-count [ hanyu-pinyin ] (get (word-info hanyu-pinyin) :usage-count 0))

(defn inc-usage-count [ hanyu pinyin ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (update-word-props key { :usage-count (inc (usage-count key)) } )))

(defn set-word-info [hanyu pinyin short-english known ]  
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (update-word-props key { :short-english short-english :known known })))

(defn word-info-string [ ] 
  (let [ sortfn (fn [ { hanyu :hanyu pinyin :pinyin } ] [ pinyin hanyu ] ) ]
    (list-to-str (sort-by sortfn (vals @word-info-dict)))))

;-------------------  Finding words   ----------------------------------------

(defn find-words [ pinyin ]
  (let [ pattern (starts-with-pattern pinyin)
        pinyin-matcher (fn [ { p1 :pinyin-no-spaces p2 :pinyin-no-spaces-no-tones } ]
                         (or (re-find pattern p1) (re-find pattern p2) )) ] 
    (if (< (count pinyin) 2) [] ; only suggest for 2+ letter of pinyin
      (->> (get-word { :pinyin-start (subs pinyin 0 2) } )
        (filter pinyin-matcher)))))

; -------------------- Parsing chinese text to words ---------------------

(defn most-common-word [words]
  (apply max-key #(% :usage-count) words))

(defn find-first-word-len [ chinese len ]
  (if (= len 0) { :text (subs chinese 0 1) }
    (let [words (get-word { :hanyu (subs chinese 0 len) } )]
      (if words (most-common-word words) 
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
      
