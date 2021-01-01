using UnityEngine;

namespace FirstGearGames.Utilities.Maths
{

    public static class Quaternions
    {

        /// <summary>
        /// Returns if a rotational value matches another. This method is preferred over Equals or == since those variations allow larger differences before returning false.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="target"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        public static bool Near(this Quaternion r, Quaternion target, float tolerance = 1f)
        {
            if (tolerance == 0f)
                tolerance = 0.01f;

            float a = Vectors.FastSqrMagnitude(r.eulerAngles - target.eulerAngles);
            return (a <= (tolerance * tolerance));
        }
    }

}