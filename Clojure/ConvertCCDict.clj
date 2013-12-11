; run with java clojure

(ns ConvertCC
  (:require [clojure.string :as str])
  (:use clojure.pprint)
  (:use clojure.java.io))

(defn regex-groups [ regex str ]
   (let [ matcher (re-matcher regex str) ]
    (if-not (re-find matcher) nil (re-groups matcher))))

(defn line-items-to-word [ line-items ]
  (let [ hanyu (nth line-items 2) pinyin (nth line-items 3) english (nth line-items 4) ]
    (if (or (not hanyu) (not pinyin) (not english)) (throw (Exception. "Invalid line"))
    { :hanyu hanyu
     :pinyin pinyin
     :english (str/replace english #"/" ", ") } )))

(def cc-line-regex #"(\S+)\s+(\S+)\s+\[([\w\:\s]+)\]\s+\/(.+)\/" )

(defn is-variant [word]
  (let [variant-regex #"^(variant of|old variant of|archaic variant of|Japanese variant of)"]
    (re-find variant-regex (word :english))))

(defn add-default-english [ {:keys [english short-english] :as word} ]
  (merge { :english (or short-english "") } word ))

(defn add-word-attributes [ { :keys [pinyin english] :as word } ]
  (let [pinyin-no-spaces (str/lower-case (str/replace pinyin #"[: ]" ""))
       remove-tone-numbers (fn [ s ] (str/replace s #"\d" "")) ]
    (merge
       word
       { :short-english (if english (first (str/split english #",")) "" )
         :pinyin-no-spaces pinyin-no-spaces
         :pinyin-no-spaces-no-tones (remove-tone-numbers pinyin-no-spaces) }
     )))

(defn parse-cc-lines [lines]
  (->> lines
    (map #(regex-groups cc-line-regex %))
    (remove nil?)
    (map line-items-to-word)
    (remove is-variant)
    (map add-word-attributes)
    (vec)))

(defn load-words [ file ]
  (with-open [rdr (reader file)]
    (parse-cc-lines (line-seq rdr))))

; :encoding "UTF-8"

; We would like to use pprint here, but it turns our to be ~200 times slower thn prn!
; So as a compromise use prn line-by-line and generate manually the syntax for the encoding list
(defn convert-dict [ infile outfile ]
  (with-open [wri (writer outfile)]
    (binding [*out* wri]
      (do
        (println "[")
        (dorun (map #(do (pr %) (println ",")) (load-words infile)))
        (println "]") ))))

(convert-dict (first *command-line-args*) (nth *command-line-args* 1) )

