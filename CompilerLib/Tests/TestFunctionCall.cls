/*
[Entry]
export (i32) func()
   i32 a = f()
   return a
   
(i32) f ()
   return 6
*/

i32 G = 123

(i32) addToG(i32 v)
   G += v
   return G
  
(i32) fib(i32 n)
   if n < 3 
      return 1
   return fib(n-1) + fib(n-2)


[Entry]
export (bool,i32,i32) func()
   i32 a = addToG(2) // now 125
   bool b = a==125 && G == 125
   a = a + 1
   b &= a==126 && G == 125
   b &= fib(3) == 2 
   b &= fib(4) == 3 
   b &= fib(5) == 5 
   b &= fib(6) == 8 
   b &= fib(7) == 13 
   b &= fib(8) == 21 
   b &= fib(9) == 34  
   
   return b,1,1
