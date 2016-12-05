i32 G = 123

(i32) addToG(i32 v)
   G += v
   return G
  
(i32) fib(i32 n)
   if n < 3 
      return 1
   return fib(n-1) + fib(n-2)