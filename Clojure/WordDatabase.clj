(ns WordDatabase
  (:use Utils)
  (:require [clojure.string :as str])
  (:use clojure.pprint)
  (:use clojure.set)
  (:use clojure.clr.io))

;----------------------- Atoms for the dictionary data etc. ----------------------------------

(def all-words (atom []))

; dictionaries for all words. Immutable once set (could they be just defs?)
; Keyed by:
; { :hanyu hanyu }  (values are lists of words)
; { :hanyu hanyu :pinyin pinyin }  (values are single words)
(def word-dict (atom {}))

(def word-info-dict (atom {})) ; retain so that properties like usage-count can be changed at runtime

(def word-search-cache (atom {})) ; key: { :input pinyin-start :english bool }, value: raw words

(def add-diacritics-func (atom identity)) ; set this to proper diacritics expander from C#

(defn set-add-diacritics-func! [ f ] (reset! add-diacritics-func f))


;----------------------- Dictionary accessors ----------------------------------

(defn get-word [ key ] (@word-dict key))

(defn word-info [ hanyu-pinyin ] (get @word-info-dict hanyu-pinyin hanyu-pinyin ))

;--------------------- Expanding word characters ------------------------------------

(defn remove-tone-numbers [ s ] (str/replace s #"\d" ""))

(defn toneless-equal [ a b ] (= (remove-tone-numbers a) (remove-tone-numbers b) ))

(defn word-pinyin-matcher [ pinyin compare-func ]
  (fn [ { word-pinyin :pinyin } ]
    (compare-func word-pinyin pinyin)))

(defn pinyin-matching-word [ compare-func pinyin words ]
  (first (filter (word-pinyin-matcher pinyin compare-func) words)))

(defn find-char [ hanyu pinyin ]
  (let [ hanyu-matches (get-word { :hanyu hanyu }) ]
    (or
      (get-word { :hanyu hanyu :pinyin pinyin }) ; exact match?
      (pinyin-matching-word equal-caseless pinyin hanyu-matches) ; only case difference?
      (pinyin-matching-word toneless-equal pinyin hanyu-matches) ; Tone changes of char as part of a word
      (first hanyu-matches)
      ; If we are using reduced dictionary, character might truly not be found. Then just construct it from hanyu and pinyin
      { :hanyu hanyu :pinyin pinyin :english "?" :short-english "?" } )))

(defn add-diacritics-to-word [ { pinyin :pinyin :as word } ]
  (assoc word :pinyin-diacritics (@add-diacritics-func pinyin)))

; This is slow, so do only for a word when needed (mainly when fetched to form current sentence words), not for all words in dictionary
(defn characters [ { hanyu :hanyu pinyin :pinyin } ]
  (->> (zip (map str hanyu) (str/split pinyin #" "))
    (map (fn [ [h p] ] (find-char h p)))
    (map add-diacritics-to-word)
    (vec)))

(defn expand-hanyu-word [ { :keys [hanyu pinyin] } ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (merge
      (get-word key)
      { :pinyin-diacritics (@add-diacritics-func pinyin) }
      (word-info key) ; info has been already merged at load, but re-merge as it might change runtime
      { :characters (characters key) })))

(defn expand-word [ {:keys [hanyu pinyin] :as word} ]
  (if (and hanyu pinyin)
    (expand-hanyu-word word)
    word )) ; can be literal text

(defn expand-all-words [ words ] (map expand-word words))

;--------------- Loading database -------------------------------------

(defn add-word-attributes [ {:keys [hanyu pinyin english] :as word} usage-count known ]
  (let [pinyin-no-spaces (str/lower-case (str/replace pinyin #"[: ]" "")) ]
    (merge
      (if english { :short-english (first (str/split english #",")) } {})
      ; Default values of attributes, can be overridden in info
      { :usage-count usage-count
        :known known
        :pinyin-no-spaces pinyin-no-spaces
        :pinyin-no-spaces-no-tones (remove-tone-numbers pinyin-no-spaces) }
      word
      )))

(defn add-default-english [ {:keys [english short-english] :as word} ]
    (merge { :english (or short-english "") } word ))

(defn suggestion-comparer [ { hanyu1 :hanyu pinyin1 :pinyin :as word1} { hanyu2 :hanyu pinyin2 :pinyin :as word2 } ]
  (let [ uc1 (word1 :usage-count) uc2 (word2 :usage-count) ]
    (cond
      (not= uc1 uc2) (if (> uc1 uc2) -1 1 )
      (not= (count hanyu1) (count hanyu2)) (if (< (count hanyu1) (count hanyu2)) -1 1 )
      :else (compare pinyin1 pinyin2))))

(defn sort-suggestions [ suggestions ] (sort suggestion-comparer suggestions))

(defn index-hanyu-pinyin [ words ] (index words [ :hanyu :pinyin ]))

(defn create-word-dict [words]
  (merge
    (map-values sort-suggestions (index words [ :hanyu ]))
    (map-values first (index-hanyu-pinyin words))))

; TODO: Now these global fields are set almost at the same time, so
; possibility of mismatch should be minimal,
; but consider if we should set all of the following atoms in one go, i.e. make them
; part of a single global atom that is reset in one operation.
(defn set-word-database-inner-2! [ sorted-words dictionary ]
  (reset! word-search-cache {})
  (reset! all-words sorted-words)
  (reset! word-dict dictionary))

(defn set-word-database-inner! [words]
  (set-word-database-inner-2!
    (sort-suggestions words)
    (create-word-dict words)))

; cc-dict can contain multiple entries with same hanyu+pinyin but different english,
; for example 后 Combine those.
(defn combine-duplicates [values]
  (assoc (first values) :english (str/join ". " (map :english values))))

(defn set-word-database! [words-raw info-list]
  (let [ raw-dict (map-values combine-duplicates (index-hanyu-pinyin words-raw))
        dict (map-values #(add-word-attributes % 0 false) raw-dict)
        raw-info-dict (map-values first (index-hanyu-pinyin info-list))
        info-dict (map-values #(add-word-attributes % 1 true) raw-info-dict)
        words (vec (map add-default-english (vals (merge-with merge dict info-dict)))) ]

    ; stupid to make again list in preceding when we have dict already....

    (reset! word-info-dict info-dict)
    ; Short word list for quickly getting writing
    (set-word-database-inner! (filter #(> (% :usage-count) 0 ) words))
    ; Full word list (slow to process)
    (set-word-database-inner! words)))

; It's better to store the data at clojure-side. Casting the data to more C# usable
; format renders it less usable to clojure code
(defn load-database [ cc-dict-file info-file ]
  (set-word-database!
    (load-from-file cc-dict-file)
    (load-from-file info-file)))

;----------------------- Updating word info  ---------------------

(defn update-word-props! [ hanyu-pinyin new-props ]
  (let [ amend-word-info (fn [ dict hanyu-pinyin new-props ]
         (update-in dict [ hanyu-pinyin ] merge hanyu-pinyin (dict hanyu-pinyin) new-props) ) ]
      (swap! word-info-dict amend-word-info hanyu-pinyin new-props)))

(defn usage-count [ hanyu-pinyin ] (get (word-info hanyu-pinyin) :usage-count 0))

(defn inc-usage-count [ hanyu pinyin ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (update-word-props! key { :usage-count (inc (usage-count key)) } )))

(defn set-word-info [hanyu pinyin short-english known ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (update-word-props! key { :short-english short-english :known known })))

(defn word-info-string [ ]
  (pretty-pr (vals @word-info-dict)))

;-------------------  Finding words   ----------------------------------------

(defn pinyin-matcher [ pinyin-start ]
  (fn [ { p1 :pinyin-no-spaces p2 :pinyin-no-spaces-no-tones } ]
    (or (starts-with p1 pinyin-start) (starts-with p2 pinyin-start))))

(defn english-matcher [ english-start ]
  (fn [ { english :english } ]
    (some #(starts-with % english-start) (str/split english #" "))))

(defn find-words-cached [ input english ]
  (let [ key { :input input :english english } ]
    (or (@word-search-cache key)
      (let [ key-shorter { :input (apply str (drop-last input)) :english english }
             source (or (@word-search-cache key-shorter) @all-words)
             matcher ((if english english-matcher pinyin-matcher) input)
             res (filter matcher source) ]
        (do
          ; TODO: Now we "memoize" without limit. If this seems to lead to too high memory consumption,
          ; make a more intelligent cache limiting the contained data
          (swap! word-search-cache #(assoc % key res))
          res )))))

; Although filtering the whole dictionary is slowish, this function quickly returns
; a lazy-seq which we process in background-thread in the UI, so no delay is noticeable.
; The key here is to have all-words pre-sorted in order of usage-count, so no new sorting is needed:
; Top results come quickly from top of the list.
; We could as well return all 100 000 items in whole dictionary, but no-one will need them so
; to consume processor power, limit to 1000

;(def word-search-cache (atom {})) ; key: { :input pinyin-start :english bool }, value: raw words

(defn find-words [ input english ]
  (let [ matcher ((if english english-matcher pinyin-matcher) input) ]
    (->> (find-words-cached input english)
      (map expand-word)
      (take 1000))))

