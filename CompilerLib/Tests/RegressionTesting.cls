// regression testing of features

// Call with "Entry" as the entry point, no parameters, 3 return values
[Entry]
// return (success, # passed, # tested)
export (bool,i32,i32) RunTests()
     
   i32 passed = 0
   i32 tested = 0
   //++passed
   
   tested = tested + 1
   if TestBool()
      passed = passed + 1

   tested = tested + 1
   if TestInt()
      passed = passed + 1

   tested = tested + 1
   if TestFlow()
      passed = passed + 1

   return tested==passed,passed,tested
   
/*****************basic language features testing **************/



// test basic flow control structures
(bool) TestFlow()
   i32 a = 0
   i32 c = 0

   for i in 1..10
      a = a + i
   if a == 55
      c = c + 1
   
   a = 0
   for i in 6..3
      a = a + i
   if a == 18
      c = c + 1
   
   a = 0
   for i in 3..8 by 3
      a = a + i
   if a == 9
      c = c + 1

   a = 0
   for i in 3..9 by 3
      a = a + i
   if a == 18
      c = c + 1
      
   a = 0
   for i in 1..5
      if i == 4
         break
      a = a + i
   if a == 6
      c = c + 1

   a = 0
   for i in 1..5
      if (i%3)==1
         continue
      a = a + i
   if a == 10
      c = c + 1

      
      
   a = 0
   for i in 1..5
      if (i%2) == 1
         for j in 1..5
            if (j & 2) != 0
               continue
            for k in 1..5
               if k == 4
                  break
               a = a + i*j*k
   if a == 540
      c = c + 1  
      
   // sum of odds is a square
   // sum of evens 2*square
   a = 0
   i32 b,d = 0,0
   for i in 1..8
      d = d + i
      if i%2 == 0
         a = a + i
      else
         b = b + i
   if b == 16 && a == d-b && a == 20
      c = c + 1   
      
   return c == 8

// test basic boolean expressions
(bool) TestBool()

   i32 c = 0
   
   // test logical AND and OR, so we can test lots at one
   if true || false
      c = c + 1
   if (true && false) == false
      c = c + 1
   if (false || true)
      c = c + 1
   if true && !false
      c = c + 1
   if true || false
      c = c + 1
   
   // some variables
   bool a,b = true,false
   
   if (a && !b) && (a == !b) && (a == true) && (b == false)
      c = c + 1

   return c == 6

// test integer operators
(bool) TestInt()
   i32 a,b,d,e = +1, -2, 0x1_00, 0b10_01
   i32 c = 0
   
   if a == 1 && b == -2 && d == 256 && e == 9
      c = c + 1     
   if a > b && d/256 == a && d/((e-1)*(e-1)) == 4
      c = c + 1      
   if a != b && e != d
      c = c + 1

   // div rounds to 0
   a,b,d,e = 12,5,-12,9
   if a/b == 2 && b / a == 0 
      c = c + 1
   if a/d == -1 && d/b == -2
      c = c + 1
   
   // mod (todo - define mod of - values!)
   if a%b == 2
      c = c + 1
   if 34%b == 4
      c = c + 1
   
   // arithmetic order
   if a*b+d+-e == 39 && a-b*e == -33 
      c = c + 1
      
   // ordering
   a,b,d,e = 1,2,3,4
   if a < b && a+1 == b && a <= b && !(a>b)
      c = c + 1
   if 0 < a && 1 <= a && a < 2 && a <= 2 && a == 1
      c = c + 1
   if 2 > a && 1 >= a && a > -2 && a >= 1 && a != 2
      c = c + 1

   // bitwise
   a,b,d,e = 0,8+4,4+32,-1
   if ~a == 0xFFFFFFFF && ~a == e && ~~a == 0
      c = c + 1
   if (b|d) == 44 && (b&d) == 4 && (b^d) == 40
      c = c + 1
      
   // shift
   a,b,d,e = 1,2,4,8
   if a << 4 == 0b1_0000 && a << b == 4
      c = c + 1
   if e >> 2 == 2 && e >> d-1 == 1
      c = c + 1
      
   // rotate
   a = 0b1000_0000_0000000000000000_0000_0101
   b,d,e = 2,3,4
   if (a>>>2) == 0b0110_0000_0000000000000000_0000_0001
      c = c + 1
   if (a>>>b) == 0b0110_0000__0000000000000000_0000_0001
      c = c + 1
   if (a<<<-2) == 0b0110_0000_0000000000000000_0000_0001
      c = c + 1
   if (a<<<-b) == 0b0110_0000__0000000000000000_0000_0001
      c = c + 1
   if (a<<<3) == 0b0_0000_0000000000000000_0000_0101100
      c = c + 1
   if (a<<<d) == 0b0_0000_0000000000000000_0000_0101100
      c = c + 1
   if (a>>>-3) == 0b0_0000_0000000000000000_0000_0101100
      c = c + 1
   if (a>>>-d) == 0b0_0000_0000000000000000_0000_0101100
      c = c + 1

   return c == 23
   
//TestAssign   
 