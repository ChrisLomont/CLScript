// test array and '.' in func locals and global and params

type t1
   i32 x,y,z  // size 3 stack
   
t1 tt[3],ttt  // size 12, addresses 0-11

i32 a = 1    // address 12
i32 b[2]     // address 13
i32 c[3][4]  // address 15, size 12

i32 d = tt[1].z + b[a+3] + c[1][a] + ttt.x + a // address 27

(i32) func()
   t1 lt
   lt.x,lt.y,lt.z = 4,5,6
   return 0




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