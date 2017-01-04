using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{

    /* Array structure
     * Note an array entry may be another array - thus first data at
     * (array address + (dim-1)*header size, where dim is the number of dimensions 1+ of the type
     * 
     * 
     *  +-------+
     *  |  S    |  Stride between entries \
     *  +-------+                          | Header 2 entries
     *  |  N    |  Number of entries      /
     *  +-------+  
     *  |       |  <---  array name points to this address
     *  +-------+
     *  |  ...  |   
     *  +-------+
     *  |       |
     *  +-------+
     * 
     */



    // hold some array helper items
    static class ArrayTools
    {
        // holds a counter that indexes over (possibly partial) 
        // dimensions of an array
        internal class IndexCounter
        {
            int[] count;
            int [] max;

            internal IndexCounter(List<int> dimensions, int startIndex)
            {
                var d = dimensions.Count - startIndex;
                count = new int[d]; // zeroes on creation
                max = new int[d];
                for (var i = 0; i < d; ++i)
                {
                    count[i] = 0; // redundant, but explicit
                    max[i] = dimensions[i + startIndex];
                }
            }

            bool moreLeft = true;

            internal int Digit { get; private set; }

            // count to next index
            // return true if there are more
            // after call member 'Digit' is the last Digit incremented
            // if greater than 0, then some digits rolled over
            internal bool Next()
            {
                if (!moreLeft) return moreLeft;
                Digit = 0; // digit index to increment next
                do
                {
                    count[Digit]++;
                    if (count[Digit] >= max[Digit])
                    {
                        count[Digit] = 0;
                        Digit++;
                    }
                    else
                        break;
                } while (Digit < count.Length);

                moreLeft = Digit < count.Length;

                return moreLeft;
            }


        }
    }
}
