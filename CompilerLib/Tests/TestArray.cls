// test array and '.' in func locals and global and params
// sizes in stack slots

[Entry]
export (i32,i32,i32,i32,i32,i32,i32,i32,i32) TestArray()
   //i32 cat1 = 0xFF
   i32 a[3][3]// = 6,2,3,4
   //i32 cat2 = 0xFE
   
   //a = 1,2,3,4
   
   // not in array walker...
   //a[0][0] = 5
   //a[0][1] = 7
   //a[1][0] = 11
   //a[1][1] = 13
   
   a[0] = 14,18,22
   a[1] = 24,28,32
   a[2] = 36,40,44
   //a[1] = func()
   
   return a[0][0],a[0][1],a[0][2] \
          a[1][0],a[1][1],a[1][2] \
          a[2][0],a[2][1],a[2][2]
   //,cat1,cat2

(i32,i32) func()
   return 14,76

/*
[Entry]
export (bool,i32,i32) TestArray()
   i32 p,t = 0,0 // passed,tested
   return p==t,p,t

/****************** simple arrays ******************/

(bool,i32,i32) TestArray1()
   i32 p,t = 0,0 // passed,tested
   i32 a[2][2][2] = 1,2,3,4,5,6,7,8
   a[1]    = 1,2,3,4
   a[0][1] = 7,9
   a[0][0] = TwoInt()
   
   i32 b[2][2]
   b[1][1] = 5
   return true,1,1 
   // b[1][1] == 5 && a[0][1][0] == 7 && a[0][1][0] = 9

(bool,i32,i32) TestArray2()
   i32 p,t = 0,0 // passed,tested
   
   i32 a[3] = 1,2,3
   a[1] = 5
   a = 1,2,3
   i32 b[2][2] = 0,TwoInt(),3
   b[1] = 4,5
   b = 4,5,6,7
   return true,1,a[1]

// todo - test function calls and setting values and returns 

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