(ns WordDatabase
  (:use Utils)
  (:require [clojure.string :as str])
  (:use clojure.pprint)
  (:use clojure.set)
  (:use clojure.clr.io))

;----------------------- Atoms for the dictionary data ----------------------------------

(def all-words (atom []))

; dictionaries for all words. Immutable once set (could they be just defs?)
; Keyed by:
; { :hanyu hanyu }  (values are lists of words)
; { :hanyu hanyu :pinyin pinyin }  (values are single words)
(def word-dict (atom {}))

(def word-info-dict (atom {})) ; retain so that properties like usage-count can be changed at runtime

;----------------------- Dictionary accessors ----------------------------------

(defn get-word [ key ] (@word-dict key))

(defn word-info [ hanyu-pinyin ] (get @word-info-dict hanyu-pinyin hanyu-pinyin ))

;--------------------- Expanding word characters ------------------------------------

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
          (not-empty hanyu-matches) (first hanyu-matches)
          ; If we are using reduced dictionary, character might truly not be found. Then just construct it from hanyu and pinyin
          :else { :hanyu hanyu :pinyin pinyin :english "?" :short-english "?" } )))))

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
      { :characters (characters key )})))

(defn expand-char [ {:keys [hanyu pinyin] :as word} ]
  (if (and hanyu pinyin)
    (expanded-word hanyu pinyin)
    word )) ; can be literal text

(defn expand-all-words [ words ]
  (map expand-char words))

;--------------- Loading database -------------------------------------

(defn merge-info-word [ {:keys [hanyu pinyin english] :as word} ]
  (let [pinyin-no-spaces (str/lower-case (str/replace pinyin #"[: ]" "")) ]
    (merge word
      ; Default values of attributes, can be overridden in info
      { :usage-count 0
        :known false
        :pinyin-no-spaces pinyin-no-spaces 
        :pinyin-no-spaces-no-tones (remove-tone-numbers pinyin-no-spaces)  
        :short-english (first (str/split english #",")) }  ; Default short english, can be overwritten by value in dict-entry
      (word-info { :hanyu hanyu :pinyin pinyin } ))))

(defn suggestion-comparer [ { hanyu1 :hanyu pinyin1 :pinyin :as word1} { hanyu2 :hanyu pinyin2 :pinyin :as word2 } ]
  (let [ uc1 (word1 :usage-count) uc2 (word2 :usage-count) ]
    (cond
      (not= uc1 uc2) (if (> uc1 uc2) -1 1 )
      (not= (count hanyu1) (count hanyu2)) (if (< (count hanyu1) (count hanyu2)) -1 1 )
      :else (compare pinyin1 pinyin2))))

(defn sort-suggestions [ suggestions ] (sort suggestion-comparer suggestions))

(defn create-word-dict [words]
  (merge
    (map-values sort-suggestions (index words [ :hanyu ]))
    (map-values first (index words [ :hanyu :pinyin ]))))

(defn set-word-database-inner! [words]
  (reset! all-words (sort-suggestions words))
  (reset! word-dict (create-word-dict words)))

(defn set-word-database! [words-raw info-dict]
  (do
    (reset! word-info-dict (map-values first (index info-dict [ :hanyu :pinyin ] )))
    (let [ words (map merge-info-word words-raw) ]
      ; Short word list for quickly getting writing
      (set-word-database-inner! (filter #(> (% :usage-count) 0 ) words))
      ; Full word list (slow to process)
      (set-word-database-inner! words))))

(defn set-default-usage-count [word] (merge { :usage-count 1 } word ))

; It's better to store the data at clojure-side. Casting the data to more C# usable
; format renders it less usable to clojure code
(defn load-database [ cc-dict-file info-file ]
  (set-word-database!
    (load-from-file cc-dict-file) 
    (map set-default-usage-count (load-from-file info-file))))

;----------------------- Updating word info  ---------------------

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

(defn pinyin-matcher [ pinyin-start ]
  (fn [ { p1 :pinyin-no-spaces p2 :pinyin-no-spaces-no-tones } ]
    (or (starts-with p1 pinyin-start) (starts-with p2 pinyin-start))))

(defn english-matcher [ english-start ]
  (fn [ { english :english } ]
    (some #(starts-with % english-start) (str/split english #" "))))

; Although filtering the whole dictionary is slowish, this function quickly returns
; a lazy-seq which we process in background-thread in the UI, so no delay is noticeable.
; The key here is to have all-words pre-sorted in order of usage-count, so no new sorting is needed:
; Top results come quickly from top of the list.
; We could as well return all 100 000 items in whole dictionary, but no-one will need them so
; to consume processor power, limit to 1000
(defn find-words [ pinyin ] (take 1000 (filter (pinyin-matcher pinyin) @all-words)))

(defn find-words-english [ english ] (take 1000 (filter (english-matcher english) @all-words)))

; -------------------- Parsing chinese text to words ---------------------

(defn find-first-word-len [ chinese len ]
  (if (zero? len) { :text (subs chinese 0 1) }
    (let [words (get-word { :hanyu (subs chinese 0 len) } )]
      (if words (first words) 
        (find-first-word-len chinese (dec len))))))

(defn find-first-word [ chinese ]
  (let [non-hanyu-regexp #"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=]+"
        non-hanyu (re-find non-hanyu-regexp chinese)]
    (if non-hanyu { :text non-hanyu }
      (find-first-word-len chinese (min (count chinese) 7)))))

(defn word-len [ { :keys [hanyu text] :as word } ]
  (if hanyu (count hanyu) (count text)))

; example: 很抱歉.没有信号 -> ["很","抱歉",".","没有","信号"]
(defn hanyu-to-words [ chinese ]
  (cond 
    (= chinese "") []
    (starts-with chinese " ") (hanyu-to-words (subs chinese 1))
    :else 
    (let [ first-word (find-first-word chinese )
          remaining-chinese (subs chinese (word-len first-word)) ]    
      (cons first-word (hanyu-to-words remaining-chinese)))))
