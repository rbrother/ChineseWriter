(ns ExportText
  (:use WordDatabase)
  (:use WritingState)
  (:require [clojure.string :as str]))

(def add-diacritics-func (atom identity)) ; set this to proper diacritics expander from C#

(defn set-add-diacritics-func! [ f ] (reset! add-diacritics-func f))

(defn get-pinyin-diacritics [ word ] (@add-diacritics-func (word :pinyin)))

(defn html-color [ pinyin ]
  (case (last pinyin)
    \1 "#FF0000"
    \2 "#A0A000"
    \3 "#00B000"
    \4 "#0000FF"
    "#808080" )) ; default

(defn html-part [ word separator char-to-part ]
  (or
    (word :text) ; literal words, just take the text
    (->> (characters word)
        (map #(format "<span style='color: %s;'>%s</span>" (html-color (% :pinyin)) (char-to-part %)))
        (interleave (repeat separator))
        (rest)
        (apply str))))

(defn word-hanyu-html [ word ] (html-part word "" :hanyu))

(defn word-pinyin-html [ word ] (html-part word " " get-pinyin-diacritics))

(defn word-english-html [ full-english? ]
  (fn [ word ]
    (cond
      full-english? (word :english)
      (word :known) ""
      true (or (word :short-english) "") )))

(defn html-row [ words selector attr ]
  (->> words
    (map selector)
    (map #(format "<td style='%s'>%s </td>" attr %))
    (apply str)))

(defn html
  ([ english? full-english? ] (html (current-text) english? full-english?))
  ([ words english? full-english? ]
    (let [ html-row2 (fn [selector attr] (html-row words selector attr)) ]
      (format
        "<table style='border: 1px solid #d0d0d0; border-collapse:collapse;' cellpadding='4'>
         <tr>%s</tr> <tr>%s</tr> <tr>%s</tr>
         </table>"
        (html-row2 word-hanyu-html "font-size:20pt;")
        (html-row2 word-pinyin-html "")
        (if english? (html-row2 (word-english-html full-english?) "color:#808080; font-size:9pt;") "" )))))

