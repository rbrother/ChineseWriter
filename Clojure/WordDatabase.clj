(ns WordDatabase
  (:use Utils)
  (:require [clojure.string :as str])
  (:use clojure.pprint)
  (:use clojure.set)
  (:use clojure.clr.io))

;----------------------- Atoms for the dictionary data etc. ----------------------------------

(def info-file-name (atom nil)) ; For storing info-file-name so at save we can use same name

(def all-words (atom [])) ; list for filtering suggestions

; dictionaries for all words. Immutable once set (could they be just defs?)
; Keyed by:
; { :hanyu hanyu }  (values are lists of words)
; { :hanyu hanyu :pinyin pinyin }  (values are single words)
(def word-dict (atom {}))

(def word-info-dict (atom {})) ; retain so that properties like :known can be changed at runtime

(def word-search-cache (atom {})) ; key: { :input pinyin-start :english bool }, value: raw words

(def add-diacritics-func (atom identity)) ; set this to proper diacritics expander from C#

(defn set-add-diacritics-func! [ f ] (reset! add-diacritics-func f))


;----------------------- Dictionary accessors ----------------------------------

(defn get-word
  ( [ key ] (@word-dict key))
  ( [ hanyu pinyin ] (get-word { :hanyu hanyu :pinyin pinyin } )))

(defn word-info [ hanyu-pinyin ] (get @word-info-dict hanyu-pinyin hanyu-pinyin ))

;--------------------------------------------------------------------------------

(defn database-info []
  (let [ known-level-count (fn [level] (count (filter #(= (:known %) level) (vals @word-info-dict))))
         known-level-str (fn [level] (str "level " level ": " (known-level-count level)))
         known-levels (str/join ", " (map known-level-str [4 3 2 1]) ) ]
    (str (count @all-words) " words, " known-levels)))

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

(defn suggestion-comparer [ { pinyin1 :pinyin :as word1} { pinyin2 :pinyin :as word2 } ]
  (let [ known1 (get word1 :known 0) known2 (get word2 :known 0) ]
    (cond
      (not= known1 known2) (if (> known1 known2) -1 1 )
      :else (compare pinyin1 pinyin2))))

(defn sort-suggestions [ words ] (sort suggestion-comparer words))

(defn index-hanyu-pinyin [ words ] (index words [ :hanyu :pinyin ]))

(defn create-word-dict [words]
  (merge
    (map-map-values sort-suggestions (index words [ :hanyu ]))
    (map-map-values first (index-hanyu-pinyin words))))

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
; for example 后 Combine those. TODO: We could combine them already at import if ccdict...
(defn combine-duplicates [values]
  (assoc (first values) :english (str/join ". " (map :english values))))

; TODO: After we get all attributes to words.clj as well (like full english), then we can
; immediately call (set-word-database-inner! info-list) and only then proceed with the
; slow step of merging the dictionaries (we can even only *load* the large dictionary after that).
; This should allow very quick starting of writing.
(defn set-word-database! [words-raw info-list]
  (let [ full-dict (map-map-values combine-duplicates (index-hanyu-pinyin words-raw))
         short-dict (map-map-values first (index-hanyu-pinyin info-list))
         words (vec (vals (merge-with merge full-dict short-dict))) ] ; merge-with merge :-)
    (reset! word-info-dict short-dict)
    (set-word-database-inner! (filter #(and (% :known) (> (% :known) 0 )) words))    ; Short word list for quickly getting writing
    (set-word-database-inner! words)))                              ; Full word list (slow to process)

; It's better to store the data at clojure-side. Casting the data to more C# usable
; format renders it less usable to clojure code
(defn load-database [ cc-dict-file info-file ]
  (do
    (reset! info-file-name info-file)
    (set-word-database!
      (load-from-file cc-dict-file)
      (load-from-file info-file))))

;----------------------- Updating word info  ---------------------

(defn get-word-prop [ hanyu pinyin prop-name ]
  (let [ key { :hanyu hanyu :pinyin pinyin }
         combined-properties (merge (get-word key) (word-info key)) ]
    (combined-properties (keyword prop-name))))

(defn swap-word-info! [ swap-func ]
  (do
    (swap! word-info-dict swap-func)
    (if @info-file-name
      (let [ words-str (pretty-pr (sort-suggestions (vals @word-info-dict))) ]
        (System.IO.File/WriteAllText @info-file-name words-str) nil ))))

(defn update-word-props! [ hanyu-pinyin new-props ]
  (swap-word-info!
    (fn [ dict ] (update-in dict [ hanyu-pinyin ] merge hanyu-pinyin (dict hanyu-pinyin) new-props) )))

(defn set-word-info-prop [hanyu pinyin prop-name value ]
    (update-word-props! { :hanyu hanyu :pinyin pinyin } { (keyword prop-name) value } ))

(defn delete-word-info! [ hanyu pinyin ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (swap-word-info! (fn [info-dict] (filter-map #(not= key %) info-dict)))))

;-------------------  Finding words   ----------------------------------------

(defn pinyin-matcher [ pinyin-start ]
  (fn [ { p1 :pinyin-no-spaces p2 :pinyin-no-spaces-no-tones } ]
    (or (starts-with p1 pinyin-start) (starts-with p2 pinyin-start))))

(defn english-matcher [ english-start ]
  (fn [ { english :english } ]
    (if english (some #(starts-with % english-start) (str/split english #" ")) false )))

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
; The key here is to have all-words pre-sorted in order of :known, so no new sorting is needed:
; Top results come quickly from top of the list.
; We could as well return all 100 000 items in whole dictionary, but no-one will need them so
; to consume less processor power, limit to 5000 (we will never expect to need more words)

(defn find-words [ input english ]
  (let [ matcher ((if english english-matcher pinyin-matcher) input) ]
    (->> (find-words-cached input english)
      (map expand-word)
      (take 5000))))

