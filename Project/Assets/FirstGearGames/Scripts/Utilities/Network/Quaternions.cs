using UnityEngine;

namespace FirstGearGames.Utilities.Networks
{

    public static class Quaternions
    {
        private const float MIN_VALUE = -1f / 1.414214f;
        private const float MAX_VALUE = 1f / 1.414214f;
        private const uint UINT_RANGE = (1 << 10) - 1;

        /// <summary>
        /// Used to Compress Quaternion into 4 bytes
        /// </summary>
        public static uint CompressQuaternion(Quaternion value)
        {
            value = value.normalized;

            int largestIndex = FindLargestIndex(value);
            Vector3 small = GetSmallerDimensions(largestIndex, value);
            // largest needs to be positive to be calculated by reader 
            // if largest is negative flip sign of others because Q = -Q
            if (value[largestIndex] < 0)
            {
                small *= -1;
            }

            uint a = ScaleToUInt(small.x, MIN_VALUE, MAX_VALUE, 0, UINT_RANGE);
            uint b = ScaleToUInt(small.y, MIN_VALUE, MAX_VALUE, 0, UINT_RANGE);
            uint c = ScaleToUInt(small.z, MIN_VALUE, MAX_VALUE, 0, UINT_RANGE);

            // pack each 10 bits and extra 2 bits into uint32
            uint packed = a | b << 10 | c << 20 | (uint)largestIndex << 30;

            return packed;
        }

        private static int FindLargestIndex(Quaternion q)
        {
            int index = default;
            float current = default;

            for (int i = 0; i < 4; i++)
            {
                float next = Mathf.Abs(q[i]);
                if (next > current)
                {
                    index = i;
                    current = next;
                }
            }

            return index;
        }

        private static Vector3 GetSmallerDimensions(int largestIndex, Quaternion value)
        {
            float x = value.x;
            float y = value.y;
            float z = value.z;
            float w = value.w;

            switch (largestIndex)
            {
                case 0:
                    return new Vector3(y, z, w);
                case 1:
                    return new Vector3(x, z, w);
                case 2:
                    return new Vector3(x, y, w);
                case 3:
                    return new Vector3(x, y, z);
                default:
                    Debug.LogError("Invalid quaternion index.");
                    return Vector3.zero;
            }
        }


        /// <summary>
        /// Used to read a Compressed Quaternion from 4 bytes
        /// <para>Quaternion is normalized</para>
        /// </summary>
        public static Quaternion DecompressQuaternion(uint packed)
        {
            const uint mask = 0b11_1111_1111;

            uint a = packed & mask;
            uint b = (packed >> 10) & mask;
            uint c = (packed >> 20) & mask;
            uint largestIndex = (packed >> 30) & mask;

            float x = ScaleFromUInt(a, MIN_VALUE, MAX_VALUE, 0, UINT_RANGE);
            float y = ScaleFromUInt(b, MIN_VALUE, MAX_VALUE, 0, UINT_RANGE);
            float z = ScaleFromUInt(c, MIN_VALUE, MAX_VALUE, 0, UINT_RANGE);

            Vector3 small = new Vector3(x, y, z);
            return FromSmallerDimensions(largestIndex, small);
        }

        private static Quaternion FromSmallerDimensions(uint largestIndex, Vector3 smallest)
        {
            float a = smallest.x;
            float b = smallest.y;
            float c = smallest.z;

            float largest = Mathf.Sqrt(1 - a * a - b * b - c * c);
            switch (largestIndex)
            {
                case 0:
                    return new Quaternion(largest, a, b, c).normalized;
                case 1:
                    return new Quaternion(a, largest, b, c).normalized;
                case 2:
                    return new Quaternion(a, b, largest, c).normalized;
                case 3:
                    return new Quaternion(a, b, c, largest).normalized;
                default:
                    Debug.LogError("Invalid quaternion index.");
                    return new Quaternion();

            }
        }


        /// <summary>
        /// Scales float from minFloat->maxFloat to minUint->maxUint
        /// <para>values out side of minFloat/maxFloat will return either 0 or maxUint</para>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minFloat"></param>
        /// <param name="maxFloat"></param>
        /// <param name="minUint">should be a power of 2, can be 0</param>
        /// <param name="maxUint">should be a power of 2, for example 1 &lt;&lt; 8 for value to take up 8 bytes</param>
        /// <returns></returns>
        public static uint ScaleToUInt(float value, float minFloat, float maxFloat, uint minUint, uint maxUint)
        {
            // if out of range return min/max
            if (value > maxFloat) { return maxUint; }
            if (value < minFloat) { return minUint; }

            float rangeFloat = maxFloat - minFloat;
            uint rangeUint = maxUint - minUint;

            // scale value to 0->1 (as float)
            float valueRelative = (value - minFloat) / rangeFloat;
            // scale value to uMin->uMax
            float outValue = valueRelative * rangeUint + minUint;

            return (uint)outValue;
        }

        /// <summary>
        /// Scales uint from minUint->maxUint to minFloat->maxFloat 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minFloat"></param>
        /// <param name="maxFloat"></param>
        /// <param name="minUint">should be a power of 2, can be 0</param>
        /// <param name="maxUint">should be a power of 2, for example 1 &lt;&lt; 8 for value to take up 8 bytes</param>
        /// <returns></returns>
        public static float ScaleFromUInt(uint value, float minFloat, float maxFloat, uint minUint, uint maxUint)
        {
            // if out of range return min/max
            if (value > maxUint) { return maxFloat; }
            if (value < minUint) { return minFloat; }

            float rangeFloat = maxFloat - minFloat;
            uint rangeUint = maxUint - minUint;

            // scale value to 0->1 (as float)
            // make sure divide is float
            float valueRelative = (value - minUint) / (float)rangeUint;
            // scale value to fMin->fMax
            float outValue = valueRelative * rangeFloat + minFloat;
            return outValue;
        }
    }

}
