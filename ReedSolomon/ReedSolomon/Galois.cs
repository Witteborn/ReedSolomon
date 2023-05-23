namespace Witteborn.ReedSolomon;

/// <summary>
///  8-bit Galois Field
///
/// This class implements multiplication, division, addition,
/// subtraction, and exponentiation.
///
/// The multiplication operation is in the inner loop of
/// erasure coding, so it's been optimized.  Having the
/// class be "final" helps a little, and having the EXP_TABLE
/// repeat the data, so there's no need to bound the sum
/// of two logarithms to 255 helps a lot.
/// </summary>
public class Galois
{
    /// <summary>
    /// The number of elements in the field.
    /// </summary>
    public const int FIELD_SIZE = 256;
    /// <summary>
    /// The polynomial used to generate the logarithm table.
    ///
    /// There are a number of polynomials that work to generate
    /// a Galois field of 256 elements.The choice is arbitrary,
    /// and we just use the first one.
    ///
    /// The possibilities are: 29, 43, 45, 77, 95, 99, 101, 105,
    /// 113, 135, 141, 169, 195, 207, 231, and 245.
    /// </summary>
    public const int GENERATING_POLYNOMIAL = 29;

    private static short[] LOG_TABLE => GaloisTables.LOG_TABLE;
    private static sbyte[] EXP_TABLE => GaloisTables.EXP_TABLE;


    /// <summary>
    /// Adds two elements of the field.  If you're in an inner loop,
    /// you should inline this function: it's just XOR.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static sbyte Add(sbyte a, sbyte b)
    {
        return (sbyte)(a ^ b);
    }

    /// <summary>
    /// Inverse of addition.  If you're in an inner loop,
    /// you should inline this function: it's just XOR.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static sbyte Subtract(sbyte a, sbyte b)
    {
        return (sbyte)(a ^ b);
    }

    /// <summary>
    /// Multiplies to elements of the field.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static sbyte Multiply(sbyte a, sbyte b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        int logA = LOG_TABLE[a & 0xFF];
        int logB = LOG_TABLE[b & 0xFF];
        int logResult = logA + logB;
        return EXP_TABLE[logResult];

    }

    /// <summary>
    /// Inverse of multiplication.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static sbyte Divide(sbyte a, sbyte b)
    {
        if (a == 0)
        {
            return 0;
        }

        if (b == 0)
        {
            throw new ArgumentException("Argument 'divisor' is 0");
        }

        int logA = LOG_TABLE[a & 0xFF];
        int logB = LOG_TABLE[b & 0xFF];
        int logResult = logA - logB;

        if (logResult < 0)
        {
            logResult += 255;
        }

        return EXP_TABLE[logResult];
    }

    /// <summary>
    /// Computes a**n.
    ///
    /// The result will be the same as multiplying a times itself n times.
    /// </summary>
    /// <param name="a">A member of the field.</param>
    /// <param name="n">A plain-old integer.</param>
    /// <returns>The result of multiplying a by itself n times.</returns>
    public static sbyte Exp(sbyte a, int n)
    {
        if (n == 0)
        {
            return 1;
        }
        else if (a == 0)
        {
            return 0;
        }

        int logA = LOG_TABLE[a & 0xFF];
        int logResult = logA * n;
        while (255 <= logResult)
        {
            logResult -= 255;
        }

        return EXP_TABLE[logResult];
    }

    /// <summary>
    /// Generates a logarithm table given a starting polynomial.
    /// </summary>
    /// <param name="polynomial"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static short[] GenerateLogTable(int polynomial)
    {
        short[] result = new short[FIELD_SIZE];
        for (int i = 0; i < FIELD_SIZE; i++)
        {
            result[i] = -1; // -1 means "not set"
        }
        int b = 1;
        for (int log = 0; log < FIELD_SIZE - 1; log++)
        {
            if (result[b] != -1)
            {
                throw new Exception("BUG: duplicate logarithm (bad polynomial?)");
            }
            result[b] = (short)log;
            b = b << 1;
            if (FIELD_SIZE <= b)
            {
                b = b - FIELD_SIZE ^ polynomial;
            }
        }
        return result;
    }

    /// <summary>
    /// Generates the inverse log table.
    /// </summary>
    /// <param name="logTable"></param>
    /// <returns></returns>
    public static sbyte[] GenerateExpTable(short[] logTable)
    {
        sbyte[] result = new sbyte[FIELD_SIZE * 2 - 2];
        for (int i = 1; i < FIELD_SIZE; i++)
        {
            int log = logTable[i];
            result[log] = (sbyte)i;
            result[log + FIELD_SIZE - 1] = (sbyte)i;
        }
        return result;
    }

    /// <summary>
    /// Returns a list of all polynomials that can be used to generate
    /// the field.
    ///
    /// This is never used in the code; it's just here for completeness.
    /// </summary>
    /// <returns></returns>
    public static int[] AllPossiblePolynomials()
    {
        List<int> result = new List<int>();
        for (int i = 0; i < FIELD_SIZE; i++)
        {
            try
            {
                GenerateLogTable(i);
                result.Add(i);
            }
            catch (Exception e)
            {
                // this one didn't work
            }
        }

        return result.ToArray();
        //return result.toArray(new Integer[result.size()]);
    }
}
