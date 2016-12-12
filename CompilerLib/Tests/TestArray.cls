// test array and '.' in func locals and global and params

i32 a
i32 b[10]
i32 c = b[3]+a

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