// playground for testing code

type RGB
   i32 r,g,b
   
type RGB2
   RGB a
   RGB b
   
type RGBd
   RGB2 a
   RGB  b

(i32,i32) Test(i32 p, i32 t, bool b)
   if b
      p++
   t++
   return p,t

[Entry]
export (bool,i32,i32) Play()
   i32 p,t = 0,0 // passed, tested
   
   RGB c1 = 4,6,9
   p,t = Test(p,t,c1.r == 4 && c1.g == 6 && c1.b == 9)
   
   
   RGB c2 = return3()
   p,t = Test(p,t,c2.r == -4 && c2.g == 15 && c2.b == 3)
   
   i32 a,b,c = 3,4,-7
   p,t = Test(p,t,a==3&&b==4&&c==-7)
   
   a,c2,b = -11,return3(),19
   p,t = Test(p,t, a == -11 && b == 19 && c2.r == -4 && c2.g == 15 && c2.b == 3)

//   RGB2 x = 1,return3(),9,2
//   p,t = Test(p,t,x.a.r == 1 && x.a.g == -4 && x.a.b == 15 && x.b.r == 3 && x.b.g == 9 && x.b.b == 2)  
   
   return p==t,p,t
   
(i32,i32,i32) return3()
   return -4,15,3
   