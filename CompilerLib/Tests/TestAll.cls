// code to test all

import "TestArray.cls"
import "TestSimple.cls"

[All]
export (bool,i32,i32) TestAll()
   bool b
   i32 p,t=0,0 // passed, tested
   i32 p1,t1   // temp
   
   b,p1,t1 = TestArray()
   p,t += p1,t1

   b,p1,t1 = TestIncDec()
   p,t += p1,t1

   return p==t,p,t