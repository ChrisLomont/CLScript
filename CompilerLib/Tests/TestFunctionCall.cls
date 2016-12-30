// test comments can be misaligned
() err1()         
  /* comment */ 
   return
() err2()         
     /* comment */ 
   return
() err3()         
/* comment */ 
   return

i32 G = 123

(i32) addToG(i32 v)
   G += v
   return G
  
(i32) fib(i32 n)
   if n < 3 
      return 1
   return fib(n-1) + fib(n-2)

(i32,i32) f2()
   return 4,7

(r32,bool,i32) f3()
   return 5.0,false,9

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
  
   // discard - ensures stack cleaned correctly
   f2()
   f3()
   fib(5)
  
   i32 c,d = f2()
  
   b &= c == 4 && d == 7
   
   r32 r
   bool b2
   r,b2,c = f3()
   
   b &= r==5.0&&b2==false&&c==9
   
   a = 1 + 2*fib(2)
   b &= a==3
   
   
   return b,1,1
