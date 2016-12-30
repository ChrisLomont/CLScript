// testing mulitiple assignments, type assignments, type promotion

(i32,i32) Make2()
   return 3,5
   
[Entry]
export (i32) func()
   i32 a,b
   a,b = Make2()
   
//   tri t
//   t = make3()
//   a += t.a
//   i32 x,y,z = make3()
//   b += z
   
   return a+b
   
//type tri
//   i32 a,b,c
//   
//(tri) make3()
//   tri t
//   t.a,t.b,t.c = 1,2,3
//   return t
   
   
   

/*
enum e1
   val1 = 1
   val2 = 2

type RGB 
   r32 r,g,b

type t2
   RGB color
   r32 hsv[3]
   
type t3 
   i32 i
   r32 r
   bool b

() func()
   // Array assignment
   r32 r1[3] = 1.0,1.0,1.0
   i32 a2[3][2] =  1,2, 3,2+2, 5,6 // e1.val1

   // assign multiple things
   i32 a1[3],d,a3[2] = 1,2,3, 4, 5,6
   
   // struct assignment
   RGB c1 = 1.0,0.0,0.0
   RGB c2 = c1
   t3  vv = 2,3.0,false
   
   for k in 0..12
      break
     
   // complex struct assignment
   t2  c3 = c1,0.0,0.0,0.1
   RGB cc[2] = c1,c2

   // swap
   c1,c2 = c2,c1
   
   // copy as long as underlying types ok
   c1,c2 = r1,1.0,0.0,1.0
   
   // function settings
   i32 pair[2] = func2()
   // r1 = Zero()
   // vv = returnSome()
   // a1 = 1,func2()

   
   // type promotion
   // r32 a = 4
   // i32 b = 1.5
   //
   //
   //
   //a,b = return2()
   //

(i32,i32) func2()
   return 1,2
   
(r32,r32,r32) Zero()
   return 0.0,0.0,0.0
   
(i32,r32,bool) returnC()
   return 4,5.0,false
*/    