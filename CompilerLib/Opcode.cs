
namespace Lomont.ClScript.CompilerLib
{
    public enum Opcode
    {
        // first letter of allowed operand types

        // stack
        Push,               // [BIF] read bytes from code, expanded to stack entry size, pushed onto stack
        Pop,                // [   ] pop entry from stack
        //Pick,               // [   ] push stack value from n back onto stack
        Dup,                // [   ] copy top stack value
        Swap,               // [   ] swap top two stack values
        Rot3,               // [   ] rotate top 3 stack items, bottom becomes top
        ClearStack,         // [   ] add n zeroes to stack (used for function stack frames)
        PopStack,           // [   ] single int from code pops this many from stack
        Reverse,            // [   ] n = single int from code, reverse this many on stack

        // mem   
        // todo - these will need sized to handle byte accesses later
        Load,               // [GLC] push value from code memory location onto stack (same as PUSH+READ)
        Read,               // [GLC] Push value onto stack whose address on stack top
                            // push address, then value, then writes value into address

        Write,              // [BIF] push value, then address. Write stores value into addr. Note addr creates absolute addresses on stack

        Update,             // [BIF] push value, then address. This is used to update a value with a supported update opcode
                            //       Valid updates are all supported, i.e., *=, -=, etc. '=' is this opcode again
                            //       Following instruction is a byte opcode giving the operation, then a pre-increment signed byte B in -128 to 127
                            //       If B >= 0, add to address, leave resulting address on stack.
                            //       If B < 0, add (-B-1) to address, do not leave resulting address on stack

        Addr,               // [GLC] push physical address of variable. Global/const are absolute, local computed relative to base pointer

        // array
        Array,              // [   ] checked array access: takes k indices on stack, reverse order, then address of array, 
                            //       k is in code after opcode, then n = total array dimension of symbol (k <= n) 
                            //       Computes address of item, checking bounds along the way
                            //       Array in memory has length at position -1, and a stack size of rest in -2 (header size 2)
        MakeArr,            // [GL ] make an array by filling in values. 
                            //       values in code, in order: address a, # dims n, s total size, dims in order x1,x2,...,xn
                            //       total size s is h + x1(h+x2(h+x3...(h+xn*t)..) where t is base type size

        // label/branch/call/ret
        Call,               // [ LC] relative call address if local, else import # if const
        Return,             // [   ] two values (parameter entries, local stack entries) for cleaning stack after call
        BrTrue ,            // [   ] pop stack. If 1, branch to relative address
        BrFalse,            // [   ] pop stack. If 0, branch to relative address
        BrAlways,           // [   ] always branch to relative address
        ForStart,           // [   ] start, end, delta values on stack. If delta = 0, compute delta +1 or -1
                            //       store start at local memory location in code, delta at location +1
                            //       pops 3 from stack.
                            //       takes address as operand for frame
        ForLoop,            // [   ] update for stack frame, branch if more to do
                            //       Stack has end value, code has local offset to for frame (counter, delta), then delta address to jump on loop
                            //       pops end value after comparison
                            //bitwise           
        Or,                 // [   ]
        And,                // [   ]
        Xor,                // [   ]
        Not,                // [   ]
        RightShift,         // [   ]
        LeftShift,          // [   ]
        RightRotate,        // [   ] 
        LeftRotate,         // [   ]

        // comparison
        NotEqual,           // [BIF]
        IsEqual,            // [BIF]
        GreaterThan,        // [BIF]
        GreaterThanOrEqual, // [BIF]
        LessThanOrEqual,    // [BIF]
        LessThan,           // [BIF]

        //arithmetic       
        Neg,                // [BIF]
        Add,                // [BIF]
        Sub,                // [BIF]
        Mul,                // [BIF]
        Div,                // [BIF]
        Mod,                // [BI ]
        // todo - add inc, dec that take an address for ++ and -- ops, and versions leaving value on stack
        // end

        // pseudo-ops - take no space, merely placeholders
        Label,
        Symbol

    }


}
