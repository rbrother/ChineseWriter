(ns WritingState
  (:require [clojure.string :as str])
  (:use Utils)
  (:use WordDatabase))

(def state (atom { :text [] :cursor-pos 0 }))

(defn cursor-pos [] (@state :cursor-pos))

(defn current-text [] (@state :text))

(defn set-state! [ writing-state ] (reset! state writing-state))

(defn clear-current-text! [] (set-state! { :text [] :cursor-pos 0 } ))

(defn load-current-text [path]
  (let [ text (load-from-file path) ]
    (set-state! { :text text :cursor-pos (count text) } )))

(defn word-deleted [ text pos ] 
  (concat
    (take pos text)
    (drop (inc pos) text)))

(defn delete-word! [ pos ]
  (if (and (>= pos 0) (< pos (count (current-text))))
    (let [ new (word-deleted (current-text) pos) ]
      (do (set-state! { :text new :cursor-pos (min pos (count new)) } )
        true ))
    false ))

(defn word-inserted [ original new-words position ]
  (concat 
    (take position original)
    (expand-all-words new-words)
    (drop position original)))

(defn insert-text-words! [ words ]
  (let [ pos (cursor-pos) ]
    (set-state!
      { :text (word-inserted (@state :text) words pos) 
        :cursor-pos (+ pos (count words)) } )))

(defn expand-text-words! [] 
  (set-state!
     (assoc @state :text (expand-all-words (@state :text))))) 

(defn moved-cursor-pos [ { :keys [ text cursor-pos ] } dir ]
  (case dir
    "Left" (max (dec cursor-pos) 0)
    "Right" (min (inc cursor-pos) (count text))
    "Home" 0
    "End" (count text)))

(defn moved-cursor-state [ state dir ]
  (assoc state :cursor-pos (moved-cursor-pos state dir)))

(defn move-cursor! [ dir ]
  (set-state! (moved-cursor-state @state dir))) 
