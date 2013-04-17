(ns ExportText
  (:use WritingState)
  (:require [clojure.string :as str]))

(defn html-color [ pinyin ]
  (case (last pinyin)
    \1 "#FF0000"
    \2 "#A0A000"
    \3 "#00B000"
    \4 "#0000FF"
    "#808080" )) ; default
    
(defn html-part [ word separator char-to-part ]
  (if (word :characters)
    (->> (word :characters)
      (map #(format "<span style='color: %s;'>%s</span>" (html-color (% :pinyin)) (char-to-part %)))
      (interleave (repeat separator))
      (rest)
      (apply str))
    (word :text)))

(defn word-hanyu-html [ word _ ] (html-part word "" #(% :hanyu)))

(defn word-pinyin-html [ word add-diacritics-func ] (html-part word " " #(add-diacritics-func (% :pinyin))))

(defn word-english-html [ word _ ] (if (word :known) "" (word :short-english)))

(defn html-row [ words selector attr add-diacritics-func ]
  (->> words
    (map #(selector % add-diacritics-func))
    (map #(format "<td style='%s'>%s </td>" attr %))
    (apply str)))

(defn html 
  ([ add-diacritics-func ] (html add-diacritics-func (current-text)))
  ([ add-diacritics-func words]
    (let [ html-row2 (fn [selector attr] (html-row words selector attr add-diacritics-func)) ]
      (format 
        "<table style='border: 1px solid #d0d0d0; border-collapse:collapse;' cellpadding='4'>
         <tr>%s</tr> <tr>%s</tr> <tr>%s</tr>
         </table>" 
        (html-row2 word-hanyu-html "font-size:20pt;") 
        (html-row2 word-pinyin-html "") 
        (html-row2 word-english-html "color:#808080; font-size:9pt;")))))
