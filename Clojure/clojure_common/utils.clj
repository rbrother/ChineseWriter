; General utility functions usable in any application.
; Move this to some general location ( %GD%/scripts...?) and user from everywere (eg. ChineseWriter)

(ns clojure-common.utils
  (:require [clojure.string :as str])
  (:use clojure.set)
  (:use clojure.test))

; General helpers

(defn single?
    { :test (fn [] (are [x y] (= x y)
        false (single? [])
        true  (single? [666])
        false (single? [111 666]))) }
    [coll] (= (count coll) 1 ) )

(defn list-contains?
  { :test (fn [] (is (= true (list-contains? [ 1 7 6 6] 7)))) }
  [ arr item ] (some #(= % item) arr))

; From set or map it's easy to remove one item, but it's sometimes needed
; also from list when we have use case with duplicates (like TI3 AC pack)
(defn remove-single
  { :test (fn [] (is (= [ 1 6 6 5 2 ] (remove-single [ 1 6 5 6 5 2 ] 5))))}
  [ arr item ]
  (let [ not-item-pred (fn [i] (not= i item))]
    (concat
      (take-while not-item-pred arr)
      (rest (drop-while not-item-pred arr)))    ))

(defn- pairs-to-map [ list-of-pairs ]
  (let [ pair-to-map (fn [[key,value]] {key value}) ]
    (apply merge (map pair-to-map list-of-pairs))))

(defn filter-map [ key-pred m ]
  (let [ pred (fn [[key value]] (key-pred key)) ]
    (pairs-to-map (filter pred m))))

(defn map-map-keys-values [ f-keys f-values m ]
  (let [ f (fn [ [k,v] ] { (f-keys k) (f-values v) } ) ]
    (apply merge (map f (seq m)))))

(defn index-single [ list-of-maps key ]
  { :test (fn [] (is (= {:a {:id :a, :val 6}, :b {:id :b, :val 88}}
                        (index-simple [ {:id :a :val 6} { :id :b :val 88 } ] :id ) ))) }
  (map-map-keys-values key first (index list-of-maps [ key ] )))

(defn index-by-id [items] (index-single items :id))

(defn- amend-with-id [ [id attr-map] ] (assoc attr-map :id id) )

(defn vals-with-id [ big-map ] (set (map amend-with-id (seq big-map))))

(defn map-map-values
    { :test (fn [] (is (= { :a 11 :b 21 } (map-map-values (partial + 1) { :a 10 :b 20 })))) }
    [ f-values m ] (map-map-keys-values identity f-values m))

; Clojure has already zipmap, but that does not preserve the order of items and
; does not work if there are identical items (can't have two different keys) which is sometimes important
(defn zip
    { :test (fn [] (are [x y] (= x y)
        [ [1 :a] [2 :b] [3 :c] ]    (zip [ 1 2 3 ] [ :a :b :c ] )
        [ [1 :a] [2 :b] ]           (zip [ 1 2 3 ] [ :a :b ] )
        [ [1 :a] [2 :b] ]           (zip [ 1 2 ] [ :a :b :c ] ) )) }
    [list1 list2] (map vec (partition 2 (interleave list1 list2) )))

(defn range2d
  { :test (fn [] (is (= 9 (count (range2d (range 3) (range 3)))))) }
  [ range1 range2 ]
    (let [ combine (fn [value1] (map #(vector value1 %) range2)) ]
      (mapcat combine range1)))

; Math

(defn round-any [ value ] (if (integer? value) value (Math/round value)))

(defn min-pos
  { :test (fn [] (is (= [ 7 -4 ] (min-pos [ [ 7 12 ] [ 8 -4 ] ] )))) }
  [ vectors ] (apply mapv min vectors))

(defn max-pos
  { :test (fn [] (is (= [ 8 12 ] (max-pos [ [ 7 12 ] [ 8 -4 ] ] )))) }
  [ vectors ] (apply mapv max vectors))

(defn pos> [ [ x1 y1 ] [ x2 y2 ] ] (and (> x1 x2) (> y1 y2)))
(defn pos< [ [ x1 y1 ] [ x2 y2 ] ] (and (< x1 x2) (< y1 y2)))

(defn distance [ vec1 ] (Math/sqrt (apply + (map * vec1 vec1))))

(defn mul-vec [ vec1 scalar ] (map * vec1 (repeat scalar)))

(defn inside-rect? [ pos [ small-corner large-corner ] ]
  (and (pos> pos small-corner) (pos< pos large-corner)))

(defn rect-size
  { :test (fn [] (is (= (rect-size [ [ -10.0 -12.0 ] [ 14.0 7.0 ] ]) [ 24.0 19.0 ] ))) }
  [ [ min-corner max-corner ] ] (map - max-corner min-corner))

(defn polar [ degrees distance ]
  (let [ radians (/ degrees 57.2958) ]
    (mul-vec [ (Math/sin radians) (Math/cos radians) ] distance)))

; String utils

(defn starts-with
    { :test (fn [] (are [x y] (= x y)
        true (starts-with "moikka" "moi")
        false (starts-with "moikka" "hei") )) }
    [str start] (if (> (count start) (count str)) false (= start (subs str 0 (count start)))))

(defn equal-caseless?
    { :test (fn [] (are [x y] (= x y)
        true (equal-caseless? "fOoBar" "FOObar")
        false (equal-caseless? "fOoBar" "FOObarz") )) }
    [ str1 str2 ] (= (str/lower-case str1) (str/lower-case str2)))

; Clojure pretty-printing
; Performance notes:
; Tried printing with multi-method based on item class. However, performance was bad,
; On RJB laptop, takes about 20 sec to save VAL register (2500 persons, 75 000 lines).
; Now back on regular logic and simple method, 4 sec and consise code.
; Adding special test (string? item) goes to 1.5 sec :-)

(defn- indent-str-raw
    { :test (fn [] (are [x y] (= x y)
        "\n" (indent-str-raw 0)
        "\n    " (indent-str-raw 1)
        "\n        " (indent-str-raw 2) )) }
    [level] (str "\n" (apply str (repeat level "    "))))

(def indent-str (memoize indent-str-raw))

(defn sorted-map-items
    { :test (fn [] (are [x y] (= x y)
        [[:a 3] [:b 1] [:x 5] [:y 7]] (sorted-map-items { :x 5 :a 3 :y 7 :b 1 }) )) }
    [m] (seq (apply sorted-map (apply concat (seq m)))))

(def pretty-pr)

(defn- get-separator [ string-values child-indent ]
  (let [ lengths (map count string-values)
         is-simple (and (every? #(< % 120) lengths) (< (apply + lengths) 120)) ]
    (if is-simple " " (indent-str child-indent))))

(defn pretty-map-content [ m child-indent ]
  (let [ values-resolved (sorted-map-items (map-map-values (fn [ value ] (pretty-pr value child-indent)) m))
         resolved-values (map last values-resolved)
         separator (get-separator resolved-values child-indent)
         starter (if (= separator " ") " " "   ")
         pr-entry (fn [ [ key value ] ] (str key " " value)) ]
    (str starter (str/join separator (map pr-entry values-resolved)))))

(defn pretty-arr-content [ arr child-indent ]
  (let [ resolved-values (map #(pretty-pr % child-indent) arr)
         separator (get-separator resolved-values child-indent)
         starter (if (= separator " ") " " "   ") ]
    (str starter (str/join separator resolved-values))))

(defn pretty-pr
  ( [item] (pretty-pr item 0) )
  ( [item indent]
    (let [ child-indent (inc indent) ]
      (cond
        (string? item) (str "\"" item "\"") ; Special case for string to avoid performance hit of pr-str for this common case
        (not (coll? item)) (pr-str item)
        (and (map? item) (empty? item)) "{ }"
        (map? item) (str "{" (pretty-map-content item child-indent) " }" )
        (set? item) (str "#{" (pretty-arr-content item child-indent) " }" )
        (vector? item) (str "[" (pretty-arr-content item child-indent) " ]" )
        :else (str "(list " (pretty-arr-content item child-indent) " )" )))))

; File utils

(defn write-to-file [ path value ] (spit path (pretty-pr value) :encoding "UTF-8" :append false))

(defn load-from-file [ path ] (read-string (slurp path :encoding "UTF-8")))
