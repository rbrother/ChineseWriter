; run with clojure-clr. Note that uses my own Utils in clojurecommon.

(ns ConvertCC
  (:require [clojure.string :as str])
  (:use clojure.pprint)
  (:use clojure.set)
  (:use utils))

(defn regex-groups [ regex str ]
   (let [ matcher (re-matcher regex str) ]
    (if-not (re-find matcher) nil (re-groups matcher))))

(defn line-items-to-word [ [ all traditional hanyu pinyin english ] ]
    (if (or (not hanyu) (not pinyin) (not english)) (throw (Exception. "Invalid line"))
    { :hanyu hanyu
     :pinyin pinyin
     :english (str/replace (str/replace english #"/" ", ") #"\"" "'" ) } ))

(def cc-line-regex #"(\S+)\s+(\S+)\s+\[([\w\:\s]+)\]\s+\/(.+)\/" )

(defn add-default-english [ {:keys [english short-english] :as word} ]
  (merge { :english (or short-english "") } word ))

(defn hanyu-rarity [ hanyu freqs ]
  (->> hanyu
    (seq)
    (map str)
    (map #(get freqs % 10000000.0))
    (apply +)
    (int)))

(defn add-word-attributes [ { :keys [pinyin english hanyu] :as word } freqs hsk-freqs ]
  (let [ pinyin-nospace (str/replace pinyin " " "")
         hsk-index (get hsk-freqs { :hanyu hanyu :pinyin pinyin-nospace }) ]
    (merge word
      { :short-english (if english (first (str/split english #",")) "" )
        :hanzi-rarity (hanyu-rarity hanyu freqs) }
      (if hsk-index { :hsk-index hsk-index} {} ))))

; cc-dict can contain multiple entries with same hanyu+pinyin but different english,
; for example 后 Combine those.
(defn combine-duplicates-inner [values]
  (assoc (first values) :english (str/join ". " (map :english values))))

(defn combine-duplicates [word-list]
  (let [ dict (index word-list [ :hanyu :pinyin ]) ]
     (map-map-values combine-duplicates-inner dict))))

(defn parse-cc-lines [ lines freqs hsk-freqs ]
  (let [ add-word-attributes-freq (fn [word] (add-word-attributes word freqs hsk-freqs)) ]
    (->> lines
      (map (partial regex-groups cc-line-regex))
      (remove nil?)
      (map line-items-to-word)
      (map add-word-attributes-freq) )))

(defn load-words [ file freqs hsk-freqs ]
  (parse-cc-lines (System.IO.File/ReadAllLines file) freqs hsk-freqs))

(defn make-freq-item [ [ all hanzi count ] ]
  (let [ rarity (/ 79226840.0 (read-string count)) ]  ; de5 = 10
    { hanzi rarity } ))

(defn parse-freqs [freq-lines]
  (->> freq-lines
    (map (partial regex-groups #"^\d+\t([^\t])\t(\d+)"))
    (remove nil?)
    (map make-freq-item)
    (apply merge)))

(defn parse-hsk [hsk-lines]
  (->> hsk-lines
    (map (partial regex-groups #"^([^\t]+)\t[^\t]+\t([^\t]+)"))
    (map-indexed (fn [ index [all hanyu pinyin] ] { { :hanyu hanyu :pinyin pinyin } (inc index) } ))
    (apply merge)))

; We would like to use pprint here, but it turns our to be ~200 times slower thn prn!
; So as a compromise use prn line-by-line and generate manually the syntax for the encoding list
(defn convert-dict [ infile freq-file hsk-freq-file outfile ]
  (let [ freqs (parse-freqs (System.IO.File/ReadAllLines freq-file))
         hsk-freqs (parse-hsk (System.IO.File/ReadAllLines hsk-freq-file))
         converted-words (sort-by :hanzi-rarity (combine-duplicates (load-words infile freqs hsk-freqs)))
         lines (concat ["["] (map pr-str converted-words) ["]"]) ]
    (System.IO.File/WriteAllLines outfile lines)))

(let [ cedict-file (first *command-line-args*)
       frequency-file (nth *command-line-args* 1)
       hsk-freq-file (nth *command-line-args* 2)
       output-file (nth *command-line-args* 3) ]
   (convert-dict cedict-file frequency-file hsk-freq-file output-file))

