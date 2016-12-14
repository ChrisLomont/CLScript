// test array and '.' in func locals and global and params
// sizes in stack slots


//i32 bb[10]
//i32 b=7

[Entry]
(i32) func()
   i32 c,d
   i32 a[13]
   //bb[1] = 4
   //func2(a,10)
   
   //i32 b = 7
   //for i in 1..10
   //   b += a[i-1]

   //a[b] = 1234 + a[1]//bb[1]
      
   return d // a[7]

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

/*
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
