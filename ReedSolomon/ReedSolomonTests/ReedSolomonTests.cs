using Witteborn.ReedSolomon;

namespace ReedSolomonTests;

[TestClass]
public class ReedSolomonTests
{
    [TestMethod]
    public void MatrixEquals_ReturnsTrue_WhenValuesMatch()
    {
        // Two separately constructed matrices with identical values should be equal
        var firstMatrix = new Matrix(2, 2);

        firstMatrix.Set(0, 0, 1);
        firstMatrix.Set(0, 1, 2);
        firstMatrix.Set(1, 0, 3);
        firstMatrix.Set(1, 1, 4);

        var secondMatrix = new Matrix(2, 2);

        secondMatrix.Set(0, 0, 1);
        secondMatrix.Set(0, 1, 2);
        secondMatrix.Set(1, 0, 3);
        secondMatrix.Set(1, 1, 4);
        Assert.IsTrue(firstMatrix.Equals(secondMatrix));
    }

    [TestMethod]
    public void MatrixConstructor_PreservesData_WhenGivenSignedBytes()
    {
        var data = new sbyte[][]
        {
            new sbyte[]
            {
                1,
                2
            },
            new sbyte[]
            {
                3,
                4
            }

};
        var matrix = new Matrix(data);

        Assert.AreEqual((sbyte)1, matrix.Get(0, 0));
        Assert.AreEqual((sbyte)2, matrix.Get(0, 1));
        Assert.AreEqual((sbyte)3, matrix.Get(1, 0));
        Assert.AreEqual((sbyte)4, matrix.Get(1, 1));
    }

    [TestMethod]
    public void GaloisDivide_Throws_WhenDivisorIsZero()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Galois.Divide(1, 0));
    }

    [TestMethod]
    public void GaloisDivide_ReturnsZero_WhenNumeratorIsZero()
    {
        Assert.AreEqual((sbyte)0, Galois.Divide(0, 1));
    }

    [TestMethod]
    public void GaloisMultiply_ReturnsZero_WhenEitherOperandIsZero()
    {
        Assert.AreEqual((sbyte)0, Galois.Multiply(0, 42));
        Assert.AreEqual((sbyte)0, Galois.Multiply(42, 0));
        Assert.AreEqual((sbyte)0, Galois.Multiply(0, 0));
    }

    [TestMethod]
    public void GaloisMultiply_ReturnsFieldProduct_WhenBytesAreNegative()
    {
        // -1 as sbyte = 0xFF as unsigned, should work with 0xFF masking
        var result = Galois.Multiply(-1, 1);

        Assert.AreEqual((sbyte)-1, result);
    }

    [TestMethod]
    public void GaloisExp_ReturnsOne_WhenExponentIsZero()
    {
        Assert.AreEqual((sbyte)1, Galois.Exp(42, 0));
        Assert.AreEqual((sbyte)1, Galois.Exp(0, 0));
    }

    [TestMethod]
    public void GaloisExp_ReturnsZero_WhenBaseIsZero()
    {
        Assert.AreEqual((sbyte)0, Galois.Exp(0, 5));
    }

    [TestMethod]
    public void GaloisDivide_RestoresFirstOperand_WhenDividingItsProduct()
    {
        // a * b / b == a for all non-zero values
        var firstValue = (sbyte)42;
        var secondValue = (sbyte)7;

        Assert.AreEqual(firstValue, Galois.Divide(Galois.Multiply(firstValue, secondValue), secondValue));
    }

    [TestMethod]
    public void GaloisAdd_MatchesSubtraction_WhenGivenTheSameOperands()
    {
        // In GF(256), add and subtract are both XOR
        Assert.AreEqual(Galois.Add(42, 17), Galois.Subtract(42, 17));
    }

    [TestMethod]
    public void MatrixInvert_Throws_WhenMatrixIsNotSquare()
    {
        var matrix = new Matrix(3, 4);

        Assert.ThrowsExactly<ArgumentException>(() => matrix.Invert());
    }

    [TestMethod]
    public void MatrixConstructor_Throws_WhenRowsHaveDifferentLengths()
    {
        var data = new sbyte[][]
        {
            new sbyte[]
            {
                1,
                2
            },
            new sbyte[]
            {
                3,
                4,
                5
            }

};

        Assert.ThrowsExactly<ArgumentException>(() => new Matrix(data));
    }

    [TestMethod]
    public void MatrixEquals_ReturnsFalse_WhenOtherIsNull()
    {
        var matrix = new Matrix(2, 2);

        Assert.IsFalse(matrix.Equals(null));
    }

    [TestMethod]
    public void MatrixEquals_ReturnsFalse_WhenOtherHasADifferentType()
    {
        var matrix = new Matrix(2, 2);

        Assert.IsFalse(matrix.Equals("not a matrix"));
    }

    [TestMethod]
    public void MatrixEquals_ReturnsFalse_WhenValuesDiffer()
    {
        var firstMatrix = new Matrix(2, 2);

        firstMatrix.Set(0, 0, 1);

        var secondMatrix = new Matrix(2, 2);

        secondMatrix.Set(0, 0, 2);
        Assert.IsFalse(firstMatrix.Equals(secondMatrix));
    }

    [TestMethod]
    public void MatrixIdentity_ReturnsIdentity_WhenGivenASize()
    {
        var matrix = Matrix.Identity(3);

        for (var rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                Assert.AreEqual(rowIndex == columnIndex ? (sbyte)1 : (sbyte)0, matrix.Get(rowIndex, columnIndex));
            }
        }
    }

    [TestMethod]
    public void MatrixSwapRows_Throws_WhenRowIndexIsOutsideTheMatrix()
    {
        var matrix = new Matrix(3, 3);

        Assert.ThrowsExactly<ArgumentException>(() => matrix.SwapRows(-1, 0));
        Assert.ThrowsExactly<ArgumentException>(() => matrix.SwapRows(0, 3));
    }

    [TestMethod]
    public void MatrixGet_Throws_WhenIndexIsOutsideTheMatrix()
    {
        var matrix = new Matrix(3, 3);

        Assert.ThrowsExactly<ArgumentException>(() => matrix.Get(-1, 0));
        Assert.ThrowsExactly<ArgumentException>(() => matrix.Get(0, 3));
        Assert.ThrowsExactly<ArgumentException>(() => matrix.Get(3, 0));
    }

    [TestMethod]
    public void GaloisMultiply_ReturnsTheSameProduct_WhenOperandsAreSwapped()
    {
        var values = new sbyte[]
{0,1,2,3,7,15,16,31,32,63,64,100,127,-128,-1,-64,-32,-16,-2,42,99,-99,50,-50,126};

        foreach (var firstValue in values)
        {
            foreach (var secondValue in values)
            {
                Assert.AreEqual(
                    Galois.Multiply(firstValue, secondValue),
                    Galois.Multiply(secondValue, firstValue),
                    $"Commutativity failed for a={firstValue}, b={secondValue}");
            }
        }
    }

    [TestMethod]
    public void GaloisMultiply_ReturnsTheSameProduct_WhenGroupingChanges()
    {
        var values = new sbyte[]
{1,2,3,7,15,42,100,127,-128,-1};

        foreach (var firstValue in values)
        {
            foreach (var secondValue in values)
            {
                foreach (var thirdValue in values)
                {
                    Assert.AreEqual(
                        Galois.Multiply(Galois.Multiply(firstValue, secondValue), thirdValue),
                        Galois.Multiply(firstValue, Galois.Multiply(secondValue, thirdValue)),
                        $"Associativity failed for a={firstValue}, b={secondValue}, c={thirdValue}");
                }
            }
        }
    }

    [TestMethod]
    public void GaloisMultiply_DistributesOverAddition_WhenCombiningOperands()
    {
        var values = new sbyte[]
{1,2,3,7,15,42,100,127,-128,-1};

        foreach (var firstValue in values)
        {
            foreach (var secondValue in values)
            {
                foreach (var thirdValue in values)
                {
                    Assert.AreEqual(
                        Galois.Multiply(firstValue, Galois.Add(secondValue, thirdValue)),
                        Galois.Add(
                            Galois.Multiply(firstValue, secondValue),
                            Galois.Multiply(firstValue, thirdValue)),
                        $"Distributivity failed for a={firstValue}, b={secondValue}, c={thirdValue}");
                }
            }
        }
    }

    [TestMethod]
    public void GaloisMultiply_ReturnsOriginalElement_WhenMultipliedByOne()
    {
        for (var fieldValue = -128; fieldValue <= 127; fieldValue++)
        {
            var fieldElement = (sbyte)fieldValue;

            Assert.AreEqual(
                fieldElement,
                Galois.Multiply(fieldElement, 1),
                $"Multiply({fieldElement}, 1) != {fieldElement}");
            Assert.AreEqual(
                fieldElement,
                Galois.Multiply(1, fieldElement),
                $"Multiply(1, {fieldElement}) != {fieldElement}");
        }
    }

    [TestMethod]
    public void GaloisDivide_ReturnsMultiplicativeInverse_WhenNumeratorIsOne()
    {
        for (var fieldValue = -128; fieldValue <= 127; fieldValue++)
        {
            var fieldElement = (sbyte)fieldValue;

            if (fieldElement == 0)
            {
                continue;
            }

            var inverse = Galois.Divide(1, fieldElement);

            Assert.AreEqual(
                (sbyte)1,
                Galois.Multiply(fieldElement, inverse),
                $"x={fieldElement}: x * (1/x) should be 1, got {Galois.Multiply(fieldElement, inverse)}");
        }
    }

    [TestMethod]
    public void GaloisAdd_ReturnsZero_WhenAddingAnElementToItself()
    {
        for (var fieldValue = -128; fieldValue <= 127; fieldValue++)
        {
            var fieldElement = (sbyte)fieldValue;

            Assert.AreEqual(
                (sbyte)0,
                Galois.Add(fieldElement, fieldElement),
                $"Add({fieldElement}, {fieldElement}) should be 0");
        }
    }

    [TestMethod]
    public void GaloisExp_MatchesRepeatedMultiplication_WhenExponentIsPositive()
    {
        var bases = new sbyte[]
{1,2,3,7,42,127,-1,-128};

        foreach (var firstValue in bases)
        {
            for (var exponent = 1; exponent <= 10; exponent++)
            {
                var expected = firstValue;

                for (var multiplicationIndex = 1; multiplicationIndex < exponent; multiplicationIndex++)
                {
                    expected = Galois.Multiply(expected, firstValue);
                }

                Assert.AreEqual(
                    expected,
                    Galois.Exp(firstValue, exponent),
                    $"Exp({firstValue}, {exponent}) != repeated multiply");
            }
        }
    }

    [TestMethod]
    public void GaloisExp_MatchesRepeatedMultiplication_WhenExponentIsLarge()
    {
        // Verify large exponents don't throw and produce consistent results
        var result255 = Galois.Exp(2, 255);
        var result510 = Galois.Exp(2, 510);
        var result1000 = Galois.Exp(3, 1000);
        // Verify via repeated multiply for Exp(2, 255)
        var repeatedProduct = (sbyte)1;

        for (var multiplicationIndex = 0; multiplicationIndex < 255; multiplicationIndex++)
        {
            repeatedProduct = Galois.Multiply(repeatedProduct, 2);
        }

        Assert.AreEqual(repeatedProduct, result255, "Exp(2, 255) mismatch");
        // Exp(2, 510) = Exp(2, 255) * Exp(2, 255) since GF(256) has order 255
        Assert.AreEqual(Galois.Multiply(result255, result255), result510, "Exp(2, 510) mismatch");
        // Verify Exp(3, 1000) via repeated multiply
        repeatedProduct = 1;

        for (var multiplicationIndex = 0; multiplicationIndex < 1000; multiplicationIndex++)
        {
            repeatedProduct = Galois.Multiply(repeatedProduct, 3);
        }

        Assert.AreEqual(repeatedProduct, result1000, "Exp(3, 1000) mismatch");
    }

    [TestMethod]
    public void GaloisMultiply_RestoresNumerator_WhenMultiplyingItsQuotient()
    {
        var values = new sbyte[]
{1,2,3,7,15,42,100,127,-128,-1,-64,-32,-2,50,99};

        foreach (var firstValue in values)
        {
            foreach (var secondValue in values)
            {
                var quotient = Galois.Divide(firstValue, secondValue);
                var result = Galois.Multiply(quotient, secondValue);

                Assert.AreEqual(firstValue, result, $"(a/b)*b != a for a={firstValue}, b={secondValue}");
            }
        }
    }

    [TestMethod]
    public void GaloisMultiply_PreservesFieldRules_WhenOperandsAreBoundaryValues()
    {
        // Test all sbyte boundary combinations
        var boundaries = new sbyte[]
{-128,-1,0,1,127};

        foreach (var firstValue in boundaries)
        {
            foreach (var secondValue in boundaries)
            {
                // Should not throw
                var result = Galois.Multiply(firstValue, secondValue);

                // Zero property
                var hasZeroOperand = firstValue == 0 || secondValue == 0;

                if (hasZeroOperand)
                {
                    Assert.AreEqual((sbyte)0, result, $"Multiply({firstValue}, {secondValue}) should be 0");
                }

                // Identity property
                if (secondValue == 1)
                {
                    Assert.AreEqual(firstValue, result, $"Multiply({firstValue}, 1) should be {firstValue}");
                }

                if (firstValue == 1)
                {
                    Assert.AreEqual(secondValue, result, $"Multiply(1, {secondValue}) should be {secondValue}");
                }

                // Commutativity at boundaries
                Assert.AreEqual(
                    result,
                    Galois.Multiply(secondValue, firstValue),
                    $"Commutativity failed for a={firstValue}, b={secondValue}");
            }
        }
    }

    [TestMethod]
    public void GenerateLogTable_MatchesStoredValues_WhenUsingTheGeneratingPolynomial()
    {
        var generated = Galois.GenerateLogTable(Galois.GENERATING_POLYNOMIAL);
        var hardcoded = GaloisTables.LOG_TABLE;

        Assert.AreEqual(hardcoded.Length, generated.Length, "LOG_TABLE length mismatch");

        for (var tableIndex = 0; tableIndex < generated.Length; tableIndex++)
        {
            Assert.AreEqual(
                hardcoded[tableIndex],
                generated[tableIndex],
                $"LOG_TABLE mismatch at index {tableIndex}");
        }
    }

    [TestMethod]
    public void GenerateExpTable_MatchesStoredValues_WhenUsingTheGeneratedLogTable()
    {
        var logTable = Galois.GenerateLogTable(Galois.GENERATING_POLYNOMIAL);
        var generated = Galois.GenerateExpTable(logTable);
        var hardcoded = GaloisTables.EXP_TABLE;

        Assert.AreEqual(hardcoded.Length, generated.Length, "EXP_TABLE length mismatch");

        for (var tableIndex = 0; tableIndex < generated.Length; tableIndex++)
        {
            Assert.AreEqual(
                hardcoded[tableIndex],
                generated[tableIndex],
                $"EXP_TABLE mismatch at index {tableIndex}");
        }
    }

    [TestMethod]
    public void GenerateLogTable_Throws_WhenPolynomialDoesNotGenerateTheField()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Galois.GenerateLogTable(0));
    }

    [TestMethod]
    public void AllPossiblePolynomials_ContainsKnownValues_WhenEnumerated()
    {
        var polynomials = Galois.AllPossiblePolynomials();
        var knownPolynomials = new int[]
{29,43,45,77,95,99,101,105,113,135,141,169,195,207,231,245};

        foreach (var polynomial in knownPolynomials)
        {
            Assert.IsTrue(polynomials.Contains(polynomial), $"Missing known polynomial: {polynomial}");
        }
    }

    [TestMethod]
    public void AllPossiblePolynomials_ReturnsSixteenValues_WhenEnumerated()
    {
        var polynomials = Galois.AllPossiblePolynomials();

        Assert.AreEqual(16, polynomials.Length, $"Expected 16 valid polynomials, got {polynomials.Length}");
    }

    [TestMethod]
    public void MatrixTimes_ReturnsOriginalValues_WhenMultipliedByIdentity()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 1, 2, 3 },
new sbyte[] { 4, 5, 6 },
new sbyte[] { 7, 8, 9 }
};

        var matrix = new Matrix(matrixValues);
        var identity = Matrix.Identity(3);
        var result = matrix.Times(identity);

        Assert.IsTrue(matrix.Equals(result));
    }

    [TestMethod]
    public void MatrixTimes_ReturnsExpectedProduct_WhenGivenKnownMatrices()
    {
        // 2x2 multiplication over GF(256)
        var firstMatrixValues = new sbyte[][] {
new sbyte[] { 1, 2 },
new sbyte[] { 3, 4 }
};

        var firstMatrix = new Matrix(firstMatrixValues);
        var secondMatrixValues = new sbyte[][] {
new sbyte[] { 5, 6 },
new sbyte[] { 7, 8 }
};

        var secondMatrix = new Matrix(secondMatrixValues);
        var result = firstMatrix.Times(secondMatrix);
        // result[0][0] = Multiply(1,5) XOR Multiply(2,7) = 5 XOR 14 = 11
        // result[0][1] = Multiply(1,6) XOR Multiply(2,8) = 6 XOR 16 = 22
        // result[1][0] = Multiply(3,5) XOR Multiply(4,7) = 15 XOR 28 = 19
        // result[1][1] = Multiply(3,6) XOR Multiply(4,8) = 10 XOR 32 = 42
        var expected00 = (sbyte)(Galois.Multiply(1, 5) ^ Galois.Multiply(2, 7));
        var expected01 = (sbyte)(Galois.Multiply(1, 6) ^ Galois.Multiply(2, 8));
        var expected10 = (sbyte)(Galois.Multiply(3, 5) ^ Galois.Multiply(4, 7));
        var expected11 = (sbyte)(Galois.Multiply(3, 6) ^ Galois.Multiply(4, 8));

        Assert.AreEqual(expected00, result.Get(0, 0));
        Assert.AreEqual(expected01, result.Get(0, 1));
        Assert.AreEqual(expected10, result.Get(1, 0));
        Assert.AreEqual(expected11, result.Get(1, 1));
    }

    [TestMethod]
    public void MatrixTimes_Throws_WhenInnerDimensionsDiffer()
    {
        var firstMatrix = new Matrix(2, 3);
        var secondMatrix = new Matrix(2, 3);

        Assert.ThrowsExactly<ArgumentException>(() => firstMatrix.Times(secondMatrix));
    }

    [TestMethod]
    public void MatrixAugment_ConcatenatesRows_WhenDimensionsMatch()
    {
        var leftValues = new sbyte[][] {
new sbyte[] { 1, 2 },
new sbyte[] { 3, 4 }
};

        var left = new Matrix(leftValues);
        var rightValues = new sbyte[][] {
new sbyte[] { 5, 6, 7 },
new sbyte[] { 8, 9, 10 }
};

        var right = new Matrix(rightValues);
        var result = left.Augment(right);

        Assert.AreEqual((sbyte)1, result.Get(0, 0));
        Assert.AreEqual((sbyte)2, result.Get(0, 1));
        Assert.AreEqual((sbyte)5, result.Get(0, 2));
        Assert.AreEqual((sbyte)6, result.Get(0, 3));
        Assert.AreEqual((sbyte)7, result.Get(0, 4));
        Assert.AreEqual((sbyte)3, result.Get(1, 0));
        Assert.AreEqual((sbyte)10, result.Get(1, 4));
    }

    [TestMethod]
    public void MatrixAugment_Throws_WhenRowCountsDiffer()
    {
        var firstMatrix = new Matrix(2, 2);
        var secondMatrix = new Matrix(3, 2);

        Assert.ThrowsExactly<ArgumentException>(() => firstMatrix.Augment(secondMatrix));
    }

    [TestMethod]
    public void MatrixSubmatrix_ReturnsRequestedRegion_WhenBoundsAreValid()
    {
        var matrix = new Matrix(4, 4);

        for (var rowIndex = 0; rowIndex < 4; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < 4; columnIndex++)
            {
                matrix.Set(rowIndex, columnIndex, (sbyte)(rowIndex * 4 + columnIndex));
            }
        }

        // Extract rows 1-2, columns 1-2
        var submatrix = matrix.Submatrix(1, 1, 3, 3);

        Assert.AreEqual((sbyte)5, submatrix.Get(0, 0)); // m[1][1]
        Assert.AreEqual((sbyte)6, submatrix.Get(0, 1)); // m[1][2]
        Assert.AreEqual((sbyte)9, submatrix.Get(1, 0)); // m[2][1]
        Assert.AreEqual((sbyte)10, submatrix.Get(1, 1)); // m[2][2]
    }

    [TestMethod]
    public void MatrixGetRow_ReturnsExpectedValues_WhenRowExists()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 1, 2, 3 },
new sbyte[] { 4, 5, 6 },
new sbyte[] { 7, 8, 9 }
};

        var matrix = new Matrix(matrixValues);
        var row1 = matrix.GetRow(1);

        Assert.IsTrue(row1.SequenceEqual(new sbyte[] { 4, 5, 6 }));
    }

    [TestMethod]
    public void MatrixSwapRows_ExchangesOnlyRequestedRows_WhenIndicesAreValid()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 1, 2 },
new sbyte[] { 3, 4 },
new sbyte[] { 5, 6 }
};

        var matrix = new Matrix(matrixValues);

        matrix.SwapRows(0, 2);
        Assert.AreEqual((sbyte)5, matrix.Get(0, 0));
        Assert.AreEqual((sbyte)6, matrix.Get(0, 1));
        Assert.AreEqual((sbyte)1, matrix.Get(2, 0));
        Assert.AreEqual((sbyte)2, matrix.Get(2, 1));
        // Middle row unchanged
        Assert.AreEqual((sbyte)3, matrix.Get(1, 0));
    }

    [TestMethod]
    public void MatrixInvert_ReturnsInverse_WhenMatrixHasOneElement()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 5 }
};

        var matrix = new Matrix(matrixValues);
        var inverseMatrix = matrix.Invert();
        var product = matrix.Times(inverseMatrix);

        Assert.IsTrue(product.Equals(Matrix.Identity(1)));
    }

    [TestMethod]
    public void MatrixInvert_ProducesIdentity_WhenMultipliedByOriginal()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 1, 2 },
new sbyte[] { 3, 4 }
};

        var matrix = new Matrix(matrixValues);
        var inverseMatrix = matrix.Invert();
        var product = matrix.Times(inverseMatrix);

        Assert.IsTrue(product.Equals(Matrix.Identity(2)));
    }

    [TestMethod]
    public void MatrixInvert_ReturnsInverse_WhenMatrixUsesDistinctVandermondeRows()
    {
        // Vandermonde matrices are guaranteed invertible when rows are distinct
        // Build a 5x5 matrix manually using Galois.Exp
        var data = new sbyte[5][];

        for (var rowIndex = 0; rowIndex < 5; rowIndex++)
        {
            data[rowIndex] = new sbyte[5];

            for (var columnIndex = 0; columnIndex < 5; columnIndex++)
            {
                data[rowIndex][columnIndex] = Galois.Exp((sbyte)(rowIndex + 1), columnIndex);
            }
        }

        var matrix = new Matrix(data);
        var inverseMatrix = matrix.Invert();
        var product = matrix.Times(inverseMatrix);

        Assert.IsTrue(product.Equals(Matrix.Identity(5)));
    }

    [TestMethod]
    public void MatrixInvert_Throws_WhenRowsAreLinearlyDependent()
    {
        // Row 1 is a GF(256) multiple of row 0
        var scale = (sbyte)3;
        var matrixValues = new sbyte[][] {
new sbyte[] { 1, 2 },
new sbyte[] { Galois.Multiply(
            1,
            scale), Galois.Multiply(
            2,
            scale) }
};

        var matrix = new Matrix(matrixValues);

        Assert.ThrowsExactly<ArgumentException>(() => matrix.Invert());
    }

    [TestMethod]
    public void MatrixToString_ReturnsRowMajorText_WhenFormattingKnownValues()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 1, 2 },
new sbyte[] { 3, 4 }
};

        var matrix = new Matrix(matrixValues);
        var formattedMatrix = matrix.ToString();

        Assert.AreEqual("[[1, 2], [3, 4]]", formattedMatrix);
    }

    [TestMethod]
    public void MatrixToBigString_ReturnsHexadecimalBytes_WhenFormattingSignedValues()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 0, -1 },
new sbyte[] { 16, 15 }
};

        var matrix = new Matrix(matrixValues);
        var formattedMatrix = matrix.ToBigString();

        // -1 as sbyte = 0xFF → "ff", 0 → "00", 16 → "10", 15 → "0f"
        Assert.IsTrue(formattedMatrix.Contains("00"));
        Assert.IsTrue(formattedMatrix.Contains("ff"));
        Assert.IsTrue(formattedMatrix.Contains("10"));
        Assert.IsTrue(formattedMatrix.Contains("0f"));
    }

    [TestMethod]
    public void MatrixEquals_ReturnsTrue_WhenComparedWithTheSameInstance()
    {
        var matrixValues = new sbyte[][] {
new sbyte[] { 1, 2 },
new sbyte[] { 3, 4 }
};

        var matrix = new Matrix(matrixValues);

        Assert.IsTrue(matrix.Equals(matrix));
    }

    [TestMethod]
    public void GaloisExp_Throws_WhenExponentIsNegative()
    {
        // Negative exponent causes logResult to be negative, leading to IndexOutOfRangeException
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => Galois.Exp(2, -1));
    }
}
