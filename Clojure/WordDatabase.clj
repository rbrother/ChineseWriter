(ns WordDatabase
  (:use Utils)
  (:require [clojure.string :as str])
  (:use clojure.pprint)
  (:use clojure.set)
  (:use clojure.clr.io))

;----------------------- Atoms for the dictionary data etc. ----------------------------------

(def info-file-name (atom nil)) ; For storing info-file-name so at save we can use same name

(def all-words (atom [])) ; list for filtering suggestions. ONLY { :hanyu :pinyin } pairs

; dictionaries for all words. Immutable once set (could they be just defs?)
; Keyed by: hanyu  (values are lists of words, ONLY { :hanyu :pinyin } pairs)
(def hanyu-dict (atom {}))

; Keyed by: { :hanyu hanyu :pinyin pinyin }  (values are full word properties)
(def hanyu-pinyin-dict (atom {}))

;----------------------- Dictionary accessors (from C#) ----------------------------------

(defn get-word [ hanyu pinyin ] (@hanyu-pinyin-dict { :hanyu hanyu :pinyin pinyin } ))

;--------------------------------------------------------------------------------

(defn database-info [] "***" )
;  (let [ known-level-count (fn [level] (count (filter #(= (:known %) level) (vals @word-info-dict))))
;         known-level-str (fn [level] (str "level " level ": " (known-level-count level)))
;         known-levels (str/join ", " (map known-level-str [4 3 2 1]) ) ]
;    (str (count @all-words) " words, " known-levels)))

;--------------------- Expanding word characters ------------------------------------

(defn remove-tone-numbers [ s ] (str/replace s #"\d" ""))

(defn toneless-equal [ a b ] (= (remove-tone-numbers a) (remove-tone-numbers b) ))

(defn word-pinyin-matcher [ pinyin compare-func ]
  (fn [ { word-pinyin :pinyin } ]
    (compare-func word-pinyin pinyin)))

(defn pinyin-matching-word [ compare-func pinyin words ]
  (first (filter (word-pinyin-matcher pinyin compare-func) words)))

(defn find-char [ hanyu pinyin ]
  (let [ hanyu-matches (@hanyu-dict hanyu) ]
    (or
      (@hanyu-pinyin-dict { :hanyu hanyu :pinyin pinyin }) ; exact match?
      (pinyin-matching-word equal-caseless pinyin hanyu-matches) ; only case difference?
      (pinyin-matching-word toneless-equal pinyin hanyu-matches) ; Tone changes of char as part of a word
      ; If we are using reduced dictionary, character might truly not be found. Then just construct it from hanyu and pinyin
      { :hanyu hanyu :pinyin pinyin } )))

(defn characters
  ( [ { hanyu :hanyu pinyin :pinyin } ] (characters hanyu pinyin) )
  ( [ hanyu pinyin ]
    (->> (zip (map str hanyu) (str/split pinyin #" "))
      (map (fn [ [h p] ] (find-char h p))))))

(defn word-breakdown [ hanyu pinyin ]
  (let [ word (@hanyu-pinyin-dict { :hanyu hanyu :pinyin pinyin } ) ]
    (concat
       [ word ]
       (if (= 1 (count hanyu)) [] (characters hanyu pinyin)))))

;--------------- Loading database -------------------------------------

(defn suggestion-comparer [
     { pinyin1 :pinyin known1 :known hsk-index1-raw :hsk-index rarity1 :hanzi-rarity }
     { pinyin2 :pinyin known2 :known hsk-index2-raw :hsk-index rarity2 :hanzi-rarity } ]
  (let [ hsk-index1 (or hsk-index1-raw 10000) hsk-index2 (or hsk-index2-raw 10000) ]
    (cond
      ; anything with known-level first
      (and known1 (not known2)) -1
      (and known2 (not known1)) 1
      (and known1 known2)
        (cond
          ; shorter words priority for known words. This must be done because unfortunately HSK does
          ; often not have entries for *parts* of words (eg. hai2shi4 is 333, but there is no
          ; entry at all for hai2, although that is commonly used separately). This is done
          ; by pinyin so that fully written pinyin (eg. "xi") would take predecense over partial (ef. "xin")
          (not= (count pinyin1) (count pinyin2)) (compare (count pinyin1) (count pinyin2))
          ; For *known* words, use rarity directly. This is done to avoid problem
          ; of HSK not often having entries for *parts* of words
          (not= rarity1 rarity2) (compare rarity1 rarity2)
          ; Identical rarity usually means that the hanzi are identical but correspond to
          ; different possible pinyin interpretations. Use hsk-index as "desperate" attempt
          ; to find better of them.
          (not= hsk-index1 hsk-index2) (compare hsk-index1 hsk-index2)
          ; for cases with same Hanzi but different pinyin sort alphabetically, so order does not randomly change
          :else (compare pinyin1 pinyin2))
      ; For unknown words (most likely searched with english?) use hsk-index before rarity
      ; and don't care of length directly (rarity will take care of that to some extent)
      (not= hsk-index1 hsk-index2) (compare hsk-index1 hsk-index2)
      (not= rarity1 rarity2) (compare rarity1 rarity2)
      :else (compare pinyin1 pinyin2))))

(defn sort-suggestions [ words ] (sort suggestion-comparer words))

(defn create-hanyu-pinyin-dict [ words ] (map-map-values first (index words [ :hanyu :pinyin ])))

(defn simple-props [ word ] (select-keys word [ :hanyu :pinyin :english ] ))

(defn load-database [ cc-dict-file short-dict-file ]
  (let [ short-dict (create-hanyu-pinyin-dict (load-from-file short-dict-file))
         large-dict (create-hanyu-pinyin-dict (load-from-file cc-dict-file))
         full-dict (merge-with merge large-dict short-dict)
         merged-words (sort-suggestions (vals full-dict))
         hanyu-indexed (index merged-words [ :hanyu ])
         sort-and-simplify (fn [word-list] (map simple-props (sort-suggestions word-list))) ]
      (reset! info-file-name short-dict-file)
      (reset! all-words (map simple-props merged-words))
      (reset! hanyu-pinyin-dict full-dict)
      (reset! hanyu-dict (map-map-keys-values :hanyu sort-and-simplify hanyu-indexed ))))

;----------------------- Updating word info  ---------------------

(defn get-word-prop
  ( [ hanyu pinyin prop-name ] (get-word-prop { :hanyu hanyu :pinyin pinyin } (keyword prop-name)) )
  ( [ hanyu-pinyin prop ]
      (let [ word (@hanyu-pinyin-dict hanyu-pinyin) ]
        (if word (word prop) nil))))

(defn known? [word] (> (get word :known 0) 0) )

(defn swap-hanyu-pinyin-dict! [ swap-func ]
  (do
    (swap! hanyu-pinyin-dict swap-func)
    (if @info-file-name
      (let [ words-str (pretty-pr (sort-suggestions (filter known? (vals @hanyu-pinyin-dict)))) ]
        (System.IO.File/WriteAllText @info-file-name words-str)))))

(defn update-word-props! [ hanyu-pinyin new-props ]
  (swap-hanyu-pinyin-dict! (fn [ dict ] (assoc dict hanyu-pinyin (merge (dict hanyu-pinyin) new-props)))))

(defn set-word-prop [hanyu pinyin prop-name value ]
  (let [ key { :hanyu hanyu :pinyin pinyin } ]
    (update-word-props! key { (keyword prop-name) value } )))

;(defn delete-word-info! [ hanyu pinyin ]
;  (let [ key { :hanyu hanyu :pinyin pinyin } ]
;    (swap-word-info! (fn [info-dict] (filter-map #(not= key %) info-dict)))))

(defn add-new-combination-word [ list-of-words ]
  (let [ hanyu (str/join "" (map :hanyu list-of-words))
         pinyin (str/join " " (map :pinyin list-of-words))
         key { :hanyu hanyu :pinyin pinyin }
         new-word { :hanyu hanyu :pinyin pinyin :english "?" :short-english "?" :known 1
                    :hanzi-rarity (apply + (map #(get-word-prop % :hanzi-rarity) list-of-words)) } ]
    (update-word-props! key new-word)))

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

