// testing mulitiple assignments, type assignments, type promotion

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
   i32 a2[3][2] =  1,2, 3,2+2, 5,6

   // assign multiple things
   i32 a1[3],d,a3[2] = 1,2,3, 4, 5,6
   
   // struct assignment
   RGB c1 = 1.0,0.0,0.0
   RGB c2 = c1
   t3  vv = 2,3.0,false
     
   // complex struct assignment
   t2  c3 = c1,0.0,0.0,0.1  

   // swap
   c1,c2 = c2,c1
   
   // copy as long as underlying types ok
   c1,c2 = r1,1.0,0.0,1.0
   
   // function settings
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
   
(i32,r32,bool) returnSome()
   return 4,5.0,false
    