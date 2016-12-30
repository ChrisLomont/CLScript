/*
export (i32) f()
   i32 a = 1
   if a == 1
      return 0
   return 1
*/

type RGB 
   r32 r,g,b 
   
() Check(RGB color)
   r32 aa = 0.0
   color.r = 1.0 + aa
   color.g = 0.0
   color.b = 0.0
   RGB c2
   c2.r = 1.0
   c2.g = 0.5
   c2.b = 0.25
   // return 0
   
[Entry]
export (i32,i32) Runme()
   RGB c1
   Check(c1)
   return 78,13


/*
import "Imports.cls"

// call this, returns value on stack
import (i32) ImportSquare(i32 value)

import (r32) ImportSquareRoot(r32 value)

[Entry]
export (r32) func(i32 a)
   a += TestImport(1)
   return ImportSquareRoot(99.0)
   // return ImportSquare(a)
*/   
   
   
/*
type t1
   i32 a
   i32 b
type t2
   i32 d
   t1 c
   
t2 tb // 0, length 3
t1 ta // 3, length 2
i32 e // 5, length 1

i32 z = e + ta.a + ta.b + tb.c.a
*/
/*
type t1
   i32 e[5]

type t2
   t1 d

type t3   
   t2 c[4]
   
t3 b[2][3]   

i32 a = b[1][2].c[3].d.e[4]
*/

//() func()
//   i32 aa
//   aa = 4
//   b[1][2].c[3].d.e[4] = 1
   

/*
type t11
   i32 d
type t21
   i32 a
   i32 b[10][4]
   
t21 tt   
i32 c = tt.a + tt.b[1] + tt.c[1].d


export (i32) f1(i32 a1,bool b, r32 cs)
   return 0




type type1
   i32 a
   i32 b
type type2
   type1 a[10]
   i32 b
   
type2 t2a
i32 aaa[10],bbb[4][3]

i32 v0 = v1+2*v0
i32 v1 = 2*aaa[3]+bbb[3][v0]
i32 v2 = 1+t2a.a[2].a
i32 v3 = 1+f(1,true,3.0)

(i32) f(i32 a1,bool b, r32 c)
   return 0



() func1()
   i32 a,b,c = 1,2,3
   a,b,c += 2,3,4

[Entry "param1"]
(i32,i32) test(i32 p1, i32 p2, i32 p3, i32 p4)
    i32 a=2
    return a+1,(a*a)/5//a+3//a,a*a



i32 g = 1
r32 bx = 1.0

type myType 
   i32 v1,v2


(i32) total(i32 p1, byte bb, i32 p2)
    i32 loc = 0
    myType t
    //t.v1 = 0
    //t.v2 = 1
    if p1 == 5
       return 0
    //for i in 1..10
    //   i32 a = i+2*loc
    //   // pass in constant, local, parameter, global, expression, type
    //   loc += func(1,i,loc,p1,g,i-loc*2,t)
    return loc
    
(i32) func(i32 p1, i32 p2, i32 p3, i32 p4, i32 p5, i32 p6, myType p7)
   i32 v = 0//p1+p2+p3+p4+p5+p6
   return v*v
*/