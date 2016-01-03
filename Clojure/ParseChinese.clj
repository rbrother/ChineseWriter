(ns ParseChinese
  (:use clojure-common.utils)
  (:use WordDatabase))

(defn find-first-word-len [ chinese len ]
  (if (zero? len) { :text (subs chinese 0 1) }
    (let [words (@hanyu-dict (subs chinese 0 len) )]
      (if words (first words)
        (find-first-word-len chinese (dec len))))))

(def non-hanyu-regexp #"^[a-zA-Z0-9!！\?\？\.。,，\-\:\：\/=]+")

(defn find-first-word [ chinese max-word-len ]
  (let [non-hanyu (re-find non-hanyu-regexp chinese)]
    (if non-hanyu { :text non-hanyu }
      (find-first-word-len chinese max-word-len))))

(defn word-len [ { :keys [hanyu text] :as word } ]
  (if hanyu (count hanyu) (count text)))

; example: 很抱歉.没有信号 -> ["很","抱歉",".","没有","信号"]
(defn hanyu-to-words
  ( [ chinese ] (hanyu-to-words chinese (min (count chinese) 7)) )
  ( [ chinese max-word-len ]
  (cond
    (= chinese "") []
    (starts-with chinese " ") (hanyu-to-words (subs chinese 1))
    :else
    (let [ first-word (find-first-word chinese max-word-len)
          remaining-chinese (subs chinese (word-len first-word)) ]
      (cons first-word (hanyu-to-words remaining-chinese))))))

(defn word-breakdown [ hanyu pinyin ]
  (let [ word (@hanyu-pinyin-dict { :hanyu hanyu :pinyin pinyin } ) ]
    (concat
       [ word ]
       (if (= 1 (count hanyu)) [] (hanyu-to-words hanyu (dec (count hanyu)))))))

