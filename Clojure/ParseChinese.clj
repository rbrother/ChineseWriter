(ns ParseChinese
  (:use Utils)
  (:use WordDatabase))

(defn find-first-word-len [ chinese len ]
  (if (zero? len) { :text (subs chinese 0 1) }
    (let [words (@hanyu-dict { :hanyu (subs chinese 0 len) } )]
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
