// testing mulitiple assignments, type assignments, type promotion

type RGB 
   r32 r,g,b
   
type t2
   RGB color
   r32 hsl[3]

() func()
   RGB red = 1.0,0.0,0.0
   r32 a = 4
   i32 b = 1.5
   
   t2 c2 = red,0.0,0.0,0.1  
   
   RGB c3 = red
   
   a,b = return2()
   
   i32 c[3][2], d = 1,2, 3,2+2, 5,6,  7
   
   
(i32,r32) return2()
   return 4,5.0
    