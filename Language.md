# CL Script
Chris Lomont's script

Simple language
1. Designed for small resource systems such as embedded systems
2. Wanted for use in Hypnocube items

somewhat like http://arduino.cc/en/Reference/HomePage

Ideas:
- Python/F# like grouping (no braces)
- Function overloading on parameters allowed
- All type casts explicit.
- No pointers
- operator overloading to allow nice vector, color types?
- include header
- no tabs since spacing is important
- enums ?
- const on global vars to put them in ROM instead of RAM to save space




Basic types

    bool     - true/false
    i32      - signed 32 bit
    r32      - real number, IEEE where available, else s15.16 fixed point?!
    string   - ASCII, null terminated, immutable
    char     - read a string value
    array    - has form "type [size] name". Bounds checked, allow negative slicing, other?

Not needed anymore

    null     - empty type
    fixed8   - fixed point 24.8 bit, signed
    fixed16  - fixed point 16.16 bit, signed
    fixed24  - fixed point 8.24 bit, signed

Composite - from header

    RGB      - color has Red, Green, Blue components, of type r32
    HSL      - color has Hue Sat, Lightness, of type r32
    vector2D - 2d/3d vector? Not in the linear version.
    fixed point types if needed

Control
    
    if, if..else, for, while, break, continue
    return

Syntax

    // comment till end of line
    /* comment till matching */, allow nesting
    [] array
    \ - continues a line for nicer formatting

Arithmetic
  
    =,+,-,*,/,%,<<,>> (shift), >>>, <<< rotate
  
Compare

    ==,!=,<,>,<=,>=

Boolean 

    &&,||,!
    true,false

Bitwise
  
    &,|,~,^

Casting

    type2 = Cast(type1)

Optional

    ++,--,+=,-=,*=,/=,&=,|=,^=
   
Operator overload on +-*/
    
    type vec
       r32 x y = 0.0 0.0

    vec (+) vec a vec b
       return vec (a.x+b.x)  (a.y+b.y)
   

Items for Hypnocube needs - put in header
Library

  Set/Get pixel
  formatted string format (c# style)
  drawing: line, plane, etc., depending on item in?
  string stuff
  timing or frame passed to each item?
  array ops: splice, index, rest, most, apply func, sort, min, max,  
         append,remove,insert, 
  time, delay, 
  math: min,max,abs,pow,sqrt,sin,cos,tan,atan2,acos,asin
  rand, randseed
  memory used, memory left
  read/write file on card?
  get input info / write input info

Identifier

  C-style: start A-Za-z,allow 0-9, _ inside

Examples

  type OldType
    int x = 0
    int y = 0
  
  // default values optional - get other default otherwise
  type NewType
    type1   name1 = default
    type2   name2 = default
    int[10] name3 = 1 2 3 4 3 5 1 1 9 10 
    OldType point = 0 0 // fills in values in order  

  // function prototype ends with colon. Allows multi line versions
  int SumColor RGB color  
    return color.Red + color.Green + color.Blue
  
  Function int param1 NewObj param2 string name  
    int bob = 10
    NewType obj
    int [10] array
    obj.name1 = param1
    if bob == 10 then
      do1
      do2
    else if bob != 10 then
      do3
      do4
    else
      do4
    // comment - i is a new integer here, 0 and 10 inclusive
    int sum = 0
    for i = 0 to 10 do
      sum += SumColor obj.name2
      

All items passed by reference, including integers, reals, etc. allows updating, consistent
To make new value, create new one and set components equal
Example

  // all parameters are by reference
  Modify int a int b
    a++
    b--
    int c = b; // local copy
    c++;

  int a = 10
  int b = 10
  Modify a b
  // now a is 11, b is 9.
 
  
// loop ideas - declares variable right there  
  for i in 1..10 do
  
  for j in 11..-1..1 do // (counts down)
  
  for a in array do

  for i in a..2..b do // a to b by 2, up 
  
  


Default interface for a visualization and an object passed to it. 
Draws a frame given a frame count and time elapsed? Also given time and 
frames left?

Visualization has three parts - update. Can do intro /exit based on frame 
and framesLeft.

Make global object for all your globals, access through there.
    
Compiles into what? A stack language?

Interpreted language? tokenize it all?
   
# grammar

# Ideas

    For i, male in freemales ; i=index of male (not needed) AutoHotKey language
    var for variable, type from initializer
    arrays have a .length field 
    

# Binary blob format
Needs
   - Import/Export tables allow linking....
   - Attributes to know how to export items
      - for example, our visualizations tagged with [Vis "Vis name here"]
  -      
   
# Bytecode 
  
Needs 
   - misc
       - NOP
       - breakpoint?
   - const load
       - short, long, signed int, float  
   - Branch/jump 
       - : >,>=,=,<,<=, != signed int, perhaps unsigned int?  
       - : >,>=,=,<,<=, != float
       - branch true, branch false
       - short and far
       - always
   - Math
       - +,-,*, % / signed int
       - +,-,*,/ float
       - add 1, sub 1 for loops, add n, sub n short
       - negate
       - shift, rotate
   - bitwise
       - NOT, AND, OR, XOR
   - boolean        
       - NOT, AND, OR, XOR
   - call/return
       - call short, far
       - return
   - read/store
   - stack
      - forth like: DUP, DUP2, SWAP, etc.
      - push pop
   - array
      - check bounds read
      - check bounds write
   - convert
      - float <-> signed int
   - memcpy
      - copy items in memory
   - get len of type      
   

# Forth #

Built in functions

  DUP, 
  SWAP, 
  DROP, 
  = (comparison, result in 0 or 1)
  . pops value off stack (usually prints - can log?)
  .S shows whole stack (to a log?), leaves unchanged
  0SP zeros the stack pointer (clears stack)
  OVER - copies second item to top
  ROT ( a b c -- b c a , ROTate third item to top )
  PICK ( ... v3 v2 v1 v0 N -- ... v3 v2 v1 v0 vN ) 
  DROP ( n -- , remove top of stack )  
  ?DUP ( n -- n n | 0 , duplicate only if non-zero, '|' means OR ) 
  -ROT ( a b c -- c a b , rotate top to third position )
  2SWAP ( a b c d -- c d a b , swap pairs )  
  2OVER ( a b c d -- a b c d a b , leapfrog pair )
  2DUP ( a b -- a b a b , duplicate pair )  
  2DROP ( a b -- , remove pair )  
  NIP ( a b -- b , remove second item from stack )
  TUCK ( a b -- b a b , copy top item to third position )
  +-*/
  some built in for speed like 2*,3*,4*,5*,6*,7*, 2+ 3+ etc?
  MIN, MAX, ABS, SIN, COS, TAN, ACOS, ASIN, ATAN2
  LSHIFT, RSHIFT, ARSHIFT, NEGATE

  INCLUDE filename/location?  INCLUDE sample.fth

  Dictionary sizes, memory sizes, etc.

  +! add to variable in memory

  TRUE is 1, FALSE is 0

  0> 0< 0= quick compares to 0

  AND OR NOT XOR - bitwise and logical

  String to int, int to string?


Case

      CASE
          0 OF ." Just a zero!" ENDOF
          1 OF ." All is ONE!" ENDOF
          2 OF WORDS ENDOF
          DUP . ." Invalid Input!"
      ENDCASE CR
  
  
Comment, use as a stack diagram ,in ()

  ( n -- n')

Constants
    128 CONSTANT NAME_OF_CONSTANT
    


Function example, start with ": name", finish with ";" 
  
  : negate 0 swap - ;


Loop

  # BEGIN ... UNTIL
  5 begin dup doSomething 1 - 0 = until 

  # DO .. LOOP
  : SPELL
      ." ba"
      4 0 DO
          ." na"
      LOOP
  ;

  #begin while repeat
  : SUM.OF.N ( N -- SUM[N] , calculate sum of N integers )
      0  \ starting value of SUM
      BEGIN
          OVER 0>   \ Is N greater than zero?
      WHILE
          OVER +  \ add N to sum
          SWAP 1- SWAP  \ decrement N
      REPEAT
      SWAP DROP  \ get rid on N
  ;

  LEAVE exits a loop


Factorial

  : fact 
    0 swap  # place 0 terminator on stack below n on stack
    begin dup 1 - dup 1 = until # make stack 0 n n-1 ... 3 2 1
    begin dump * over 0 = until # multiply until see 0 at end
    swap drop ;                 # delete final 0, result on stack

If/then/else as "condition if true clause then" or 
"condition if true clause else false clause then" Weird :)

Factorial, recursive
  
  : fact2
    dup 1 > if dup 1 - fact * then
    ;       

    
Creating values on heap, @ and ! set and get values. Name puts address on stack

  create v1 4 allot # set v1 on heap, named value, reserves 4 spots 
  4 v1 !            # v1[0] now holds 4
  5 v1 1 + !        # v1[1] now holds 5
  v1 @              # 4 now on stack
  v1 1 + @          # 5 on stack, 4 below it


C like data structures?  

  'C' like Structures. :STRUCT
  
  You can define 'C' like data structures in pForth using :STRUCT. For example: 
  :STRUCT  SONG
      LONG     SONG_NUMNOTES  \ define 32 bit structure member named SONG_NUMNOTES
      SHORT    SONG_SECONDS   \ define 16 bit structure member
      BYTE     SONG_QUALITY   \ define 8 bit member
      LONG     SONG_NUMBYTES  \ auto aligns after SHORT or BYTE
      RPTR     SONG_DATA      \ relocatable pointer to data
  ;STRUCT
  SONG  HAPPY   \ define a song structure called happy
  400  HAPPY  S!  SONG_NUMNOTES  \ set number of notes to 400
  17   HAPPY  S!  SONG_SECONDS   \ S! works with all size members
  CREATE  SONG-DATA  23 , 17 , 19 , 27 ,
  SONG-DATA  HAPPY S! SONG_DATA  \ store pointer in relocatable form
  HAPPY  DST  SONG    \ dump HAPPY as a SONG structure
  HAPPY   S@  SONG_NUMNOTES .  \ fetch numnotes and print


Internally, all compiled to 

    ... DUP 6 < IF DROP 5 ELSE 1 - THEN ...

to 

  ... DUP LIT 6 < ?BRANCH 5  DROP LIT 5  BRANCH 3  LIT 1 - ...

where BRANCH is a relative token branch, and ?branch is a conditional version.
LIT is the word to push a literal on the stack

http://en.wikipedia.org/wiki/Forth_(programming_language)
Tokens [ and ] enter and exit compilation mode from literal mode?



end of file
