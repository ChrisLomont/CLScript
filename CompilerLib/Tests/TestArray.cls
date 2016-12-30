// test array and '.' in func locals and global and params
// sizes in stack slots
/*
[Entry]
export (i32) f()
   i32 a[2][2]
   a[1][1] = 5
   return a[1][1]
*/
/*
type t1
   i32 x,y,z
   
[Entry]   
(i32) func()
   t1 v[2][3]
   for i in 0..1
      for j in 0..2
         v[i][j].x = 100*(i)+10*(j)+1
         v[i][j].y = 100*(i)+10*(j)+2
         v[i][j].z = 100*(i)+10*(j)+3
   return v[1][0].y
*/

/*
[Entry]
(i32) fib(i32 n)
   if n < 2 
      return 1
   return fib(n-1)+fib(n-2)
*/
/*
[Entry1]
(i32,i32) func1(i32 x, i32 y, i32 z)
   i32 a,b = 2,3
   //a = 1
   //b = 2
   a,b += b,a
   z = 10
   return a,x+y+z
   //func2(10)
   //a += func2(3)
   //return func2(func2(a))
   

(i32) func2(i32 a)
   return a+2
*/
//i32 bb[10]
//i32 b=7
/*
[Entry]
(i32) testFor()
   i32 a = 0
   for j in 1..4
     for i in 1..6
          a += i+j*2
   return a
*/      

/*

[Entry]
(i32) func1()
   i32 a[3][4]
   a[1][1] = 45
   //a[2][3] = 1234
   //bb[1] = 4
   //func2(a,10)
   
   
   //a[b] = 1234 + a[1]//bb[1]
   a[1][1] = -3+func4()
   func2(a, a[1][1], 5+2)
   func3(a[2])
   
   for i in 1..10
      a[2][2] += i
      
   return 1000*a[2][2] + 100*a[1][2] + a[2][3]

() func2(i32 a[][], i32 b, i32 c)
    a[1][2] = 17 + b + c // 17+-2+5 = 20

() func3(i32 a[])
   a[3] = 34
   
(i32) func4()
   return 3
*/   

//() func2(i32 a[], i32 n)
//    a[0] = 123
    //for i in 0..n-1
    //   a[i] = i + 1


/*
type t1
   i32 a,b,c

[Entry]
(i32) test()
   i32 a[3]
   i32 b[2][3]
   t1 dd[2]
   for i in 1..10
      i32 c[2]
      a[1] += c[1]
   return a[1]
*/


type t1
   i32 x,y,z    // 3

type t2
   i32 a[3]     // 3 
   t1 tt[2]     // 3*2 = 6
   
t1 t13[3],t11   // size 12, addresses 0-11
t2 t21, t22[2]  // size 

i32 v = 1       // address 12
i32 v2[2]       // address 13
i32 v34[3][4]   // address 15, size 12

i32 w = 3 + v + t13[1].z + v2[v+3] + v34[1][v+2] + t11.x + t22[1].tt[1].y // address 27

/*
[Entry]
(i32) func1()
    i32 a //= func2(1,t21.a, v34[1], v34)
    return a

(i32) func2(i32 a, i32 b[], i32 c[], i32 d[][])
    i32 m = 0
    i32 cc[3][4]
    cc[1][2] = m + cc[2][3]
    m += a
    m += b[0]
    m += c[1]
    m += d[1][2]
    return m
*/

// end of file
