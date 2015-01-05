(ns WritingState
  (:require [clojure.string :as str])
  (:use utils)
  (:use WordDatabase)
  (:use ParseChinese))

(def state (atom { :text [] :cursor-pos 0 }))

(defn cursor-pos [] (@state :cursor-pos))

(defn current-text [] (@state :text))

(defn clear-current-text! [] (reset! state { :text [] :cursor-pos 0 } ))

(defn pinyin-text [] (map :pinyin (current-text)))

(defn load-current-text [ path ]
  (let [ text (load-from-file path) ]
    (reset! state { :text text :cursor-pos (count text) } )))

(defn save-current-text [ path ]
  (System.IO.File/WriteAllText path (pretty-pr (current-text))))

(defn delete-word [ { :keys [ text cursor-pos ] :as original } delete-pos ]
  (if (and (>= delete-pos 0) (< delete-pos (count text)))
    { :text
     (concat
       (take delete-pos text)
       (drop (inc delete-pos) text))
     :cursor-pos cursor-pos }
    original ))

(defn delete-word! [ pos ]
  (swap! state delete-word pos ))

(defn insert-words [ { :keys [ text cursor-pos ] } new-words ]
  (let [ new-text (concat (take cursor-pos text) new-words (drop cursor-pos text))
         new-cursor-pos (+ cursor-pos (count new-words)) ]
    { :text new-text :cursor-pos new-cursor-pos } ))

(defn insert-words! [ words ] (swap! state insert-words words) )

(defn insert-word!
  ( [ word ] (insert-words! [ word ] ))
  ( [ hanyu pinyin ] (insert-word! { :hanyu hanyu :pinyin pinyin } )))

(defn literal-input! [ text ]
  (insert-word! { :text text } ))

(defn insert-chinese! [ text ]
  (insert-words! (hanyu-to-words text)))

(defn moved-cursor-pos [ { :keys [ text cursor-pos ] } dir ]
  (case dir
    "Left" (max (dec cursor-pos) 0)
    "Right" (min (inc cursor-pos) (count text))
    "Home" 0
    "End" (count text)))

(defn move-cursor [ state dir ]
  (assoc state :cursor-pos (moved-cursor-pos state dir)))

(defn move-cursor! [ dir ]
  (swap! state move-cursor dir ))

(defn set-cursor [ { :keys [ text ] } pos ]
  { :text text :cursor-pos (max 0 (min (count text) pos)) } )

(defn reset-cursor! [ pos ]
  (swap! state set-cursor pos ))
