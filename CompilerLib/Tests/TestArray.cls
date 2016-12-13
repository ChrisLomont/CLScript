// test array and '.' in func locals and global and params
// sizes in stack slots

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

i32 d = 3 + v + t13[1].z + v2[v+3] + v34[1][v+2] + t11.x + t22[1].tt[1].y // address 27

[Entry]
(i32) func1()
    i32 a //= func2(1,t21.a, v34[1], v34)
    return a
    */

(i32) func2(i32 a, i32 b[], i32 c[], i32 d[][])
    i32 m = 0
    i32 cc[3][4]
    cc[1][2] = m + cc[2][3]
    m += a
    m += b[0]
    m += c[1][2]
    return m


/*
type t1
   i32 a
   i32 b
type t2
   i32 d
   t1 c
   
t2 tb // 0, length 3
t1 ta // 3, length 2
i32 e // 5, length 1

i32 z = e + ta.a + ta.b + tb.c.a

() func()
   t1 aa
   aa.a = 1
*/


/*

i32 a[10][0b10], b[0x10]

byte b1[10][3],c1

type t1
    i32 a[10]
    r32 b

t1 t1s[20]

//enum vals
//   val = 100
   
//t1 t1s[vals.val]

//i32 aa = b[2] + a[3]

//const i32 arrSize = 10
//t1 tss[arrSize]



*/