(ns WritingState
  (:require [clojure.string :as str])
  (:use Utils)
  (:use WordDatabase))

(def state (atom { :text [] :cursor-pos 0 }))

(defn cursor-pos [] (@state :cursor-pos))

(defn current-text [] (@state :text))

(defn clear-current-text! [] (reset! state { :text [] :cursor-pos 0 } ))

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
  { :text
   (concat
     (take cursor-pos text)
     (expand-all-words new-words)
     (drop cursor-pos text))
   :cursor-pos (+ cursor-pos (count new-words)) } )

(defn insert-words! [ words ]
  (swap! state insert-words words ))

(defn expand-words [ { :keys [ text ] :as original } ]
  (assoc original :text (expand-all-words text)))

(defn expand-words! []
  (swap! state expand-words))

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


