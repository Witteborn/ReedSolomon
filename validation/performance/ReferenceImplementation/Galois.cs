using System;
using System.Collections.Generic;

namespace ReferenceImplementation;

/// <summary>
///  8-bit Galois Field
///
/// This class implements multiplication, division, addition,
/// subtraction, and exponentiation.
///
/// Multiplication uses logarithm and exponent lookup tables.
/// </summary>
public class Galois
{
    private const int MultiplicativeGroupOrder = FIELD_SIZE - 1;
    private const int FieldElementMask = FIELD_SIZE - 1;
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

    private static short[] Logarithms
    {
        get
        {
            return GaloisTables.LOG_TABLE;
        }
    }

    private static sbyte[] Exponents
    {
        get
        {
            return GaloisTables.EXP_TABLE;
        }
    }

    /// <summary>
    /// Adds two elements of the field.  If you're in an inner loop,
    /// you should inline this function: it's just XOR.
    /// </summary>
    public static sbyte Add(sbyte a, sbyte b)
    {
        return (sbyte)(a ^ b);
    }

    /// <summary>
    /// Inverse of addition.  If you're in an inner loop,
    /// you should inline this function: it's just XOR.
    /// </summary>
    public static sbyte Subtract(sbyte a, sbyte b)
    {
        return (sbyte)(a ^ b);
    }

    /// <summary>
    /// Multiplies to elements of the field.
    /// </summary>
    public static sbyte Multiply(sbyte a, sbyte b)
    {
        var hasZeroOperand = a == 0 || b == 0;

        if (hasZeroOperand)
        {
            return 0;
        }

        var firstLogarithm = (int)Logarithms[a & FieldElementMask];
        var secondLogarithm = (int)Logarithms[b & FieldElementMask];
        var resultLogarithm = firstLogarithm + secondLogarithm;

        return Exponents[resultLogarithm];
    }

    /// <summary>
    /// Inverse of multiplication.
    /// </summary>
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

        var firstLogarithm = (int)Logarithms[a & FieldElementMask];
        var secondLogarithm = (int)Logarithms[b & FieldElementMask];
        var resultLogarithm = firstLogarithm - secondLogarithm;

        if (resultLogarithm < 0)
        {
            resultLogarithm += MultiplicativeGroupOrder;
        }

        return Exponents[resultLogarithm];
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

        if (a == 0)
        {
            return 0;
        }

        var firstLogarithm = (int)Logarithms[a & FieldElementMask];
        var resultLogarithm = firstLogarithm * n;

        resultLogarithm %= MultiplicativeGroupOrder;

        return Exponents[resultLogarithm];
    }

    /// <summary>
    /// Generates a logarithm table given a starting polynomial.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static short[] GenerateLogTable(int polynomial)
    {
        const short unassignedLogarithm = -1;

        var logarithms = new short[FIELD_SIZE];

        for (var fieldElementIndex = 0; fieldElementIndex < FIELD_SIZE; fieldElementIndex++)
        {
            logarithms[fieldElementIndex] = unassignedLogarithm;
        }

        var fieldElement = 1;

        for (var exponent = 0; exponent < FIELD_SIZE - 1; exponent++)
        {
            if (logarithms[fieldElement] != unassignedLogarithm)
            {
                throw new InvalidOperationException("Duplicate logarithm detected (invalid polynomial: " + polynomial + ")");
            }

            logarithms[fieldElement] = (short)exponent;
            fieldElement = fieldElement << 1;

            if (FIELD_SIZE <= fieldElement)
            {
                fieldElement = fieldElement - FIELD_SIZE ^ polynomial;
            }
        }

        return logarithms;
    }

    /// <summary>
    /// Generates the inverse log table.
    /// </summary>
    public static sbyte[] GenerateExpTable(short[] logTable)
    {
        const int repeatedLookupCycles = 2;

        var exponents = new sbyte[repeatedLookupCycles * MultiplicativeGroupOrder];

        for (var fieldElement = 1; fieldElement < FIELD_SIZE; fieldElement++)
        {
            var exponent = (int)logTable[fieldElement];

            exponents[exponent] = (sbyte)fieldElement;
            exponents[exponent + FIELD_SIZE - 1] = (sbyte)fieldElement;
        }

        return exponents;
    }

    /// <summary>
    /// Returns a list of all polynomials that can be used to generate
    /// the field.
    ///
    /// This is never used in the code; it's just here for completeness.
    /// </summary>
    public static int[] AllPossiblePolynomials()
    {
        var validPolynomials = new List<int>();

        for (var polynomial = 0; polynomial < FIELD_SIZE; polynomial++)
        {
            try
            {
                GenerateLogTable(polynomial);
                validPolynomials.Add(polynomial);
            }
            catch (InvalidOperationException)
            {
                // This polynomial does not generate a valid field.
            }
        }

        return validPolynomials.ToArray();
    }
}
