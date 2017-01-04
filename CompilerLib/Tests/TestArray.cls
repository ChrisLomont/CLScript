// test array and '.' in func locals and global and params
// sizes in stack slots

(i32,i32) Update(bool b, i32 p1, i32 t1, i32 p, i32 t)
   return p+p1,t+t1

[Entry]
export (bool,i32,i32) TestArray()
   i32 p,t = 0,0 // passed,tested
   
   bool b
   i32 p1,t1
   
   b,p1,t1 = TestSingleArray()
   p,t = Update(b,p1,t1,p,t)

   b,p1,t1 = TestDoubleArray()
   p,t = Update(b,p1,t1,p,t)

   b,p1,t1 = TestMultiArray()
   p,t = Update(b,p1,t1,p,t)
   
   return p==t,p,t

/****************** various arrays ******************/
(bool,i32,i32) TestMultiArray()
   i32 p,t = 0,0 // passed,tested
   
   i32 a[2][3][2] = 1,2,3,4,5,6,7,8,9,10,11,12
   // todo - all these fail - perhaps init overwriting array info  
   
   //if Check2x3x2(a,1,2,3,4,5,6,7,8,9,10,11,12)
   //   p++
   //t++
//   a[1]    = 1,2,3,4
//   a[0][1] = 7,9
//   a[0][0] = TwoInt()
   return p==t,p,t
   


(bool,i32,i32) TestDoubleArray()
   i32 p,t = 0,0 // passed,tested
   
   
   i32 b[2][2] = 3,5,7,9
   if Check2x2(b,3,5,7,9) && !Check2x2(b,5,4,6,1)
      p++
   t++
   
   b = 5,6,98,1
   if Check2x2(b,5,6,98,1)
      p++
   t++
   
   b[0] = 17,-4 // partial  
   if Check2x2(b,17,-4,98,1)
      p++
   t++
   
   b[1] = 8,3 // partial  
   if Check2x2(b,17,-4,8,3)
      p++
   t++
   
   Fill_4_7x3_12(b)   
   if Check2x2(b,4,7,3,12)
      p++
   t++
   
   b[1] = Int_5_3()
   if Check2x2(b,4,7,5,3)
      p++
   t++

   b = Int_4_9_3_11()
   if Check2x2(b,4,9,3,11)
      p++
   t++

   b = Int_9_2_5(),8
   if Check2x2(b,9,2,5,8)
      p++
   t++
   
   i32 a[3][3] = 1,2,3,4,5,6,7,8,9
   if Check3x3(a,1,2,3,4,5,6,7,8,9) && !Check3x3(a,1,2,3,4,5,6,7,8,100)
      p++
   t++
   
   a[0] = 5,6,7
   if Check3x3(a,5,6,7,4,5,6,7,8,9)
      p++
   t++
   
   a[2] = Int_5_3(),18
   if Check3x3(a,5,6,7,4,5,6,5,3,18)
      p++
   t++
   
   Fill_I_Plus_J(a,3,3)
   if Check3(a[0],0,1,2) && Check3(a[1],1,2,3) && Check3(a[2],2,3,4)
      p++
   t++
   
   
   return p==t,p,t
   
(bool,i32,i32) TestSingleArray()
   i32 p,t = 0,0 // passed,tested
   
   i32 a[4] = 7,6,4,9
   if Check4(a,7,6,4,9)
      p++
   t++
   if !Check4(a,7,6,4,1+9)
      p++
   t++

   i32 b[3]
   b = 5,6,7
   if Check3(b,5,6,7)
      p++
   t++
   
   a = Int_5_3(),17,13
   if Check4(a,5,3,17,13)
      p++
   t++

   a = 1,Int_5_3(),8
   if Check4(a,1,5,3,8)
      p++
   t++

   a = 51,7,Int_5_3()
   if Check4(a,51,7,5,3)
      p++
   t++

   a = Int_5_3(),Int_5_3()
   if Check4(a,5,3,5,3)
      p++
   t++

   b = Int_5_3(),76
   if Check3(b,5,3,76)
      p++
   t++

   b = 76,Int_5_3()
   if Check3(b,76,5,3)
      p++
   t++
   
   a = Int_4_9_3_11()
   if Check4(a,4,9,3,11)
      p++
   t++

   a /= 5,3,2,3
   if Check4(a,0,3,1,3)
      p++
   t++


   b = Int_9_2_5() 
   if Check3(b,9,2,5)
      p++
   t++ 

   Fill_4_7_12(b)
   if Check3(b,4,7,12)
      p++
   t++ 
   
   b += 4,3,8
   if Check3(b,8,10,20)
      p++
   t++ 

   b %= 3,2,1
   if Check3(b,2,0,0)
      p++
   t++ 

   //pass to func to read	
   //pass to func to change
   // a = b,9 cannot expand on RHS yet
   // decompose structure into array
   // decompose array into ints
   
   return p==t,p,t
   
(i32,i32) Int_5_3()
   return 5,3
(i32,i32,i32) Int_9_2_5()
   return 9,2,5
(i32,i32,i32,i32) Int_4_9_3_11()
   return 4,9,3,11


(bool) Check2x2(i32 a[][], i32 p1, i32 p2, i32 p3, i32 p4)
    return a[0][0] == p1 && a[0][1] == p2 && a[1][0] == p3 && a[1][1] == p4
(bool) Check3(i32 a[], i32 p1, i32 p2, i32 p3)
    return a[0] == p1 && a[1] == p2 && a[2] == p3
(bool) Check4(i32 a[], i32 p1, i32 p2, i32 p3, i32 p4)
    return a[0] == p1 && a[1] == p2 && a[2] == p3 && a[3] == p4  
(bool) Check3x3(i32 a[][],               \
                i32 p1, i32 p2, i32 p3,  \
                i32 p4, i32 p5, i32 p6,  \
                i32 p7, i32 p8, i32 p9   \
                )    
    return Check3(a[0],p1,p2,p3) && Check3(a[1],p4,p5,p6) && Check3(a[2],p7,p8,p9)
    
(bool) Check3x2(i32 a[][], i32 p1, i32 p2, i32 p3, i32 p4, i32 p5, i32 p6)
    return a[0][0] == p1 && a[0][1] == p2 && \ 
           a[1][0] == p3 && a[1][1] == p4 && \
           a[2][0] == p5 && a[2][1] == p6

(bool) Check2x3x2(                       \            
                i32 a[][][],             \
                i32 p1, i32 p2, i32 p3,  \
                i32 p4, i32 p5, i32 p6,  \
                i32 p7, i32 p8, i32 p9,  \
                i32 p10, i32 p11, i32 p12)                
    // todo - fails, but should work?
    //return \ 
    //   Check3x2(a[0],p1,p2,p3,p4,p5,p6) && \
    //   Check3x2(a[1],p7,p8,p9,p10,p11,p12)
    return \ 
       a[0][0][0] == p1 && a[0][0][1] == p2 && \  
       a[0][1][0] == p3 && a[0][1][1] == p4 && \  
       a[0][2][0] == p5 && a[0][2][1] == p6 && \  
       a[1][0][0] == p7 && a[1][0][1] == p8 && \  
       a[1][1][0] == p9 && a[1][1][1] == p10 && \  
       a[1][2][0] == p11 && a[1][2][1] == p12  


() Fill_4_7_12(i32 a[])
   // cannot do this since don't know size a = 4,7,12
   // use a type if desired to do such things
   a[0] = 4
   a[1] = 7
   a[2] = 12
   
() Fill_4_7x3_12(i32 a[][])
   // cannot do this since don't know size a = 4,7,3,12
   // use a type if desired to do such things
   a[0][0] = 4
   a[0][1] = 7
   a[1][0] = 3
   a[1][1] = 12
   
() Fill_I_Plus_J(i32 a[][], i32 i, i32 j)
   for x in 0..i-1
      for y in 0..j-1
         a[x][y] = x+y
   

/*
todo - test these also

/****************** simple arrays ******************/

() ff()
  i32 zz = t22[1].tt[0+1].y
  zz = t22[1].tt[0+1].y
  i32 a[2][2],b[1],c = 1,2,3,4,5,6
  a = 1,2,3,4
  a[1] = 3,4

type RGB 
   i32 r[2]

() f()
   
   RGB c[2] = 1,2,3,4
   c[0] = 1,2




(i32,i32) TwoInt()
   return 8,11

/************** testing global expressions *********/

type t1
   i32 x,y,z    

type t2
   i32 a[3]      
   t1 tt[2]     

i32 v2[3] = 3,4,5       

t1 t11 = 1,2,3   
t1 t13[3] = t11,4,5,6,7,8,9   
t2 t21 = v2,t11,t13[1]  
t2 t22[2] = t21,t21

i32 vv = 1       
i32 v34[3][4] = 1,2,3,4,5,6,7,8,9,10,TwoInt()

i32 w = 3 + vv + t13[1].z + v2[vv+3] + v34[1][vv+2] + t11.x + t22[1].tt[1].y


/************** testing global expressions *********/

type t3
   i32 x,y,z  

(i32,i32) TestArray3()
   t3 v[2][3]
   for i in 0..1
      for j in 0..2
         v[i][j].x = 100*(i)+10*(j)+1
         v[i][j].y = 100*(i)+10*(j)+2
         v[i][j].z = 100*(i)+10*(j)+3
   return v[1][0].y,v[0][1].x
   
   
 
i32 bb[10]
i32 bg=7

(i32) TestArray4()
   i32 a[3][4]
   a[1][1] = 45
   a[2][3] = 1234
   bb[1] = 4
   func2(a[3],10)
   
   
   a[bg] = 1234 + a[1][2],bb[1],0,0
   a[1][1] = -3+func4()
   func2a(a, a[1][1], 5+2)
   func3(a[2])
   
   for i in 1..10
      a[2][2] += i
      
   return 1000*a[2][2] + 100*a[1][2] + a[2][3]

() func2a(i32 a[][], i32 b, i32 c)
    a[1][2] = 17 + b + c // 17+-2+5 = 20

() func3(i32 a[])
   a[3] = 34
   
(i32) func4()
   return 3
   

() func2(i32 a[], i32 n)
    a[0] = 123
    for i in 0..n-1
       a[i] = i + 1



type t4
   i32 a,b,c

(i32) test()
   i32 a[3]
   i32 b[2][3]
   t4 dd[2]
   for i in 1..10
      i32 c[2]
      a[1] += c[1]
   return a[1]




(i32) func1a()
    i32 a = func5(1,t21.a, v34[1], v34)
    return a

(i32) func5(i32 a, i32 b[], i32 c[], i32 d[][])
    i32 m = 0
    i32 cc[3][4]
    cc[1][2] = m + cc[2][3]
    m += a
    m += b[0]
    m += c[1]
    m += d[1][2]
    return m

// end of file
*/
