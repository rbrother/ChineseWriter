(ns Utils
  (:require [clojure.string :as str]))

(defn zip [list1 list2] (map vec (partition 2 (interleave list1 list2) )))

(defn starts-with [str start] (if (> (count start) (count str)) false (= start (subs str 0 (count start)))))

(defn equal-caseless [ str1 str2 ] (= (str/lower-case str1) (str/lower-case str2)))

(defn single? [coll] (= (count coll) 1 ) )

(defn write-to-file [ path value ] (spit path value :encoding "UTF-8" :append false))

(defn load-from-file [ path ] (read-string (slurp path :encoding "UTF-8")))

(defn map-values [ f m ] (zipmap (keys m) (map f (vals m))))

; Use ordered serialization for word-maps for easier merging 
(defn map-to-str [ m ]
  (let [ item-str (fn [key] (str key " " (pr-str (m key))) )
        map-to-str-inner (fn [keys] (str/join ", " (map item-str keys))) ]
    (str "{ " (map-to-str-inner (sort (keys m))) " }" )))

; pprint is very slow on large lists, so use this instead
(defn list-to-str [ list-of-maps ] 
  (str "[\r\n" (str/join "\r\n" (map map-to-str list-of-maps)) "]\r\n"))
