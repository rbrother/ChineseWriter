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

; retain so that properties like :known can be changed at runtime.
; Keyed by { :hanyu hanyu :pinyin pinyin }
(def word-info-dict (atom {}))

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
      ; If we are using reduced dictionary, character might truly not be found. Then just construct it from hanyu and pinyin
      { :hanyu hanyu :pinyin pinyin :english "?" :short-english "?" } )))

(defn characters
  ( [ { hanyu :hanyu pinyin :pinyin } ] (characters hanyu pinyin) )
  ( [ hanyu pinyin ]
    (->> (zip (map str hanyu) (str/split pinyin #" "))
      (map (fn [ [h p] ] (find-char h p))))))

(defn word-breakdown [ hanyu pinyin ]
  (let [ word (get-word { :hanyu hanyu :pinyin pinyin } ) ]
    (concat
       [ word ]
       (if (= 1 (count hanyu)) [] (characters hanyu pinyin)))))

;--------------- Loading database -------------------------------------

(defn suggestion-comparer [
     { hanyu1 :hanyu pinyin1 :pinyin known1 :known }
     { hanyu2 :hanyu pinyin2 :pinyin known2 :known } ]
  (cond
    (not= known1 known2) (if (> (or known1 0) (or known2 0)) -1 1 )
    (not= (count hanyu1) (count hanyu2)) (if (> (count hanyu1) (count hanyu2)) 1 -1 )
    (not= pinyin1 pinyin2) (compare pinyin1 pinyin2)
    :else (compare hanyu1 hanyu2)))

(defn sort-suggestions [ words ] (sort suggestion-comparer words))

(defn index-hanyu-pinyin [ words ] (index words [ :hanyu :pinyin ]))

(defn create-hanyu-pinyin-dict [words] (map-map-values first (index-hanyu-pinyin words)))

(defn create-combined-dict [words]
  (merge
    (map-map-values sort-suggestions (index words [ :hanyu ]))
    (create-hanyu-pinyin-dict words)))

; TODO: Now these global fields are set almost at the same time, so
; possibility of mismatch should be minimal,
; but consider if we should set all of the following atoms in one go, i.e. make them
; part of a single global atom that is reset in one operation.
(defn set-word-database! [ sorted-words ]
  (let [ dictionary (create-combined-dict sorted-words) ]
    (reset! all-words sorted-words)
    (reset! word-dict dictionary)))

(defn load-database [ cc-dict-file short-dict-file ]
  (let [ short-words (load-from-file short-dict-file)
         short-dict (create-hanyu-pinyin-dict short-words) ]
    (reset! info-file-name short-dict-file)
    (reset! word-info-dict short-dict)
    (set-word-database! short-words) ; words.clj is sorted upon saving, so no need to sort here
    ; At this point user can start writing with the short dictionary, rest is slower...
    (let [ full-words (load-from-file cc-dict-file)
           full-dict (create-hanyu-pinyin-dict full-words)
           merged-words (sort-suggestions (vals (merge-with merge full-dict short-dict))) ] ; merge-with merge :-)
      (set-word-database! merged-words))))

;----------------------- Updating word info  ---------------------

(defn combined-properties [ hanyu-pinyin ] (merge (get-word hanyu-pinyin) (word-info hanyu-pinyin)))

(defn get-word-prop [ hanyu pinyin prop-name ]
  ((combined-properties { :hanyu hanyu :pinyin pinyin }) (keyword prop-name)))

(defn swap-word-info! [ swap-func ]
  (do
    (swap! word-info-dict swap-func)
    (if @info-file-name
      (let [ words-str (pretty-pr (sort-suggestions (vals @word-info-dict))) ]
        (System.IO.File/WriteAllText @info-file-name words-str) nil ))))

(defn update-word-props! [ hanyu-pinyin new-props ]
  (swap-word-info! (fn [ dict ] (assoc dict hanyu-pinyin new-props))))

(defn set-word-info-prop [hanyu pinyin prop-name value ]
  (let [ key { :hanyu hanyu :pinyin pinyin }
         old-props (combined-properties key) ]
    (update-word-props! key (assoc old-props (keyword prop-name) value))))

(defn delete-word-info! [ hanyu pinyin ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (swap-word-info! (fn [info-dict] (filter-map #(not= key %) info-dict)))))

;-------------------  Finding suggestions based on starting pinyin or english  -----------------------

(defn pinyin-matcher [ pinyin-start ]
  (fn [ { pinyin :pinyin } ]
    (let [ pinyin-no-spaces (str/lower-case (str/replace pinyin #"[: ]" ""))
           pinyin-no-tones (remove-tone-numbers pinyin-no-spaces) ]
      (or
       (starts-with pinyin-no-spaces pinyin-start)
       (starts-with pinyin-no-tones pinyin-start)))))

(defn english-matcher [ english-start ]
  (fn [ { english :english } ]
    (if english (some #(starts-with % english-start) (str/split english #" ")) false )))

; Although filtering the whole dictionary is slowish, this function quickly returns
; a lazy-seq which we process in background-thread in the UI, so no delay is noticeable.
; The key here is to have all-words pre-sorted in order of :known, so no new sorting is needed:
; Top results come quickly from top of the list.
; We could as well return all 100 000 items in whole dictionary, but no-one will need them so
; to consume less processor power, limit to 5000 (we never expect to need more words)

(defn find-words [ input english ]
  (let [ matcher ((if english english-matcher pinyin-matcher) input) ]
    (take 5000 (filter matcher @all-words))))


