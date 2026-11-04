using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Witteborn.ReedSolomon;

/// <summary>
/// A matrix over the 8-bit Galois field.
///
/// This class is not performance-critical, so the implementations
/// are simple and straightforward.
/// </summary>
public class Matrix
{
    /// <summary>
    /// The number of rows in the matrix.
    /// </summary>
    private int Rows
    {
        get;
        set;
    }

    /// <summary>
    /// The number of columns in the matrix.
    /// </summary>
    private int Columns
    {
        get;
        set;
    }

    /// <summary>Row-major values indexed by zero-based row and column.</summary>
    private sbyte[][] Data
    {
        get;
        set;
    }

    /// <summary>
    /// Initialize a matrix of zeros.
    /// </summary>
    /// <param name="rows">The number of rows in the matrix.</param>
    /// <param name="columns">The number of columns in the matrix.</param>
    public Matrix(int rows, int columns)
    {
        Rows = rows;
        Columns = columns;
        Data = new sbyte[Rows][];

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            Data[rowIndex] = new sbyte[Columns];
        }
    }

    /// <summary>
    /// Initializes a matrix with the given row-major data.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public Matrix(sbyte[][] data)
    {
        if (data is null)
        {
            throw new ArgumentException("data cannot be null");
        }

        if (data.Length == 0)
        {
            throw new ArgumentException("data cannot be empty");
        }

        Rows = data.Length;
        Columns = data[0].Length;
        Data = new sbyte[Rows][];

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            if (data[rowIndex].Length != Columns)
            {
                throw new ArgumentException("Not all rows have the same number of columns");
            }

            Data[rowIndex] = new sbyte[Columns];

            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                Data[rowIndex][columnIndex] = data[rowIndex][columnIndex];
            }
        }
    }

    /// <summary>
    /// Returns an identity matrix of the given size.
    /// </summary>
    public static Matrix Identity(int size)
    {
        var result = new Matrix(size, size);

        for (var diagonalIndex = 0; diagonalIndex < size; diagonalIndex++)
        {
            result.Set(diagonalIndex, diagonalIndex, 1);
        }

        return result;
    }

    /// <summary>
    /// Returns a human-readable string of the matrix contents.<br/>
    ///<br/>
    /// Example: [[1, 2], [3, 4]]
    /// </summary>
    public override string ToString()
    {
        const int fieldElementMask = Galois.FIELD_SIZE - 1;
        var builder = new StringBuilder();

        builder.Append('[');

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            if (rowIndex != 0)
            {
                builder.Append(", ");
            }

            builder.Append('[');

            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                if (columnIndex != 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Data[rowIndex][columnIndex] & fieldElementMask);
            }

            builder.Append(']');
        }

        builder.Append(']');

        return builder.ToString();
    }

    /// <summary>
    /// Returns a human-readable string of the matrix contents. <br/>
    ///<br/>
    /// Example: <br/>
    /// 00 01 02 <br/>
    /// 03 04 05 <br/>
    /// 06 07 08 <br/>
    /// 09 0a 0b
    /// </summary>
    public string ToBigString()
    {
        var builder = new StringBuilder();

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                var value = (int)Get(rowIndex, columnIndex);

                if (value < 0)
                {
                    value += Galois.FIELD_SIZE;
                }

                builder.Append(string.Format("{0:x2} ", value));
            }

            builder.Append("\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns the value at row r, column c.
    /// </summary>
    public sbyte Get(int r, int c)
    {
        var hasInvalidRowIndex = r < 0 || Rows <= r;

        if (hasInvalidRowIndex)
        {
            throw new ArgumentException("Row index out of range: " + r);
        }

        var hasInvalidColumnIndex = c < 0 || Columns <= c;

        if (hasInvalidColumnIndex)
        {
            throw new ArgumentException("Column index out of range: " + c);
        }

        return Data[r][c];
    }

    /// <summary>
    /// Sets the value at row r, column c.
    /// </summary>
    public void Set(int r, int c, sbyte value)
    {
        var hasInvalidRowIndex = r < 0 || Rows <= r;

        if (hasInvalidRowIndex)
        {
            throw new ArgumentException("Row index out of range: " + r);
        }

        var hasInvalidColumnIndex = c < 0 || Columns <= c;

        if (hasInvalidColumnIndex)
        {
            throw new ArgumentException("Column index out of range: " + c);
        }

        Data[r][c] = value;
    }

    /// <summary>
    /// Returns true iff this matrix is identical to the other.
    /// </summary>
    public override bool Equals(object? other)
    {
        if ((other is Matrix) == false)
        {
            return false;
        }

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            if (Data[rowIndex].SequenceEqual(((Matrix)other).Data[rowIndex]) == false)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a hash code for this matrix based on its contents.
    /// </summary>
    public override int GetHashCode()
    {
        const int hashMultiplier = 31;
        var hashCode = Rows * hashMultiplier + Columns;

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                hashCode = hashCode * hashMultiplier + Data[rowIndex][columnIndex];
            }
        }

        return hashCode;
    }

    /// <summary>
    /// Multiplies this matrix (the one on the left) by another
    /// matrix(the one on the right).
    /// </summary>
    public Matrix Times(Matrix right)
    {
        if (Columns != right.Rows)
        {
            throw new ArgumentException("Columns on left (" + Columns + ") " + "is different than rows on right (" + right.Rows + ")");
        }

        var result = new Matrix(Rows, right.Columns);

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < right.Columns; columnIndex++)
            {
                var value = (sbyte)0;

                for (var innerIndex = 0; innerIndex < Columns; innerIndex++)
                {
                    var leftElement = Get(rowIndex, innerIndex);
                    var rightElement = right.Get(innerIndex, columnIndex);
                    var product = Galois.Multiply(leftElement, rightElement);

                    value ^= product;
                }

                result.Set(rowIndex, columnIndex, value);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the concatenation of this matrix and the matrix on the right.
    /// </summary>
    public Matrix Augment(Matrix right)
    {
        if (Rows != right.Rows)
        {
            throw new ArgumentException("Matrices don't have the same number of rows");
        }

        var result = new Matrix(Rows, Columns + right.Columns);

        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                result.Data[rowIndex][columnIndex] = Data[rowIndex][columnIndex];
            }

            for (var columnIndex = 0; columnIndex < right.Columns; columnIndex++)
            {
                result.Data[rowIndex][Columns + columnIndex] = right.Data[rowIndex][columnIndex];
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a part of this matrix.
    /// </summary>
    public Matrix Submatrix(int rmin, int cmin, int rmax, int cmax)
    {
        var result = new Matrix(rmax - rmin, cmax - cmin);

        for (var rowIndex = rmin; rowIndex < rmax; rowIndex++)
        {
            for (var columnIndex = cmin; columnIndex < cmax; columnIndex++)
            {
                result.Data[rowIndex - rmin][columnIndex - cmin] = Data[rowIndex][columnIndex];
            }
        }

        return result;
    }

    /// <summary>
    /// Returns one row of the matrix as a byte array.
    /// </summary>
    public sbyte[] GetRow(int row)
    {
        var rowValues = new sbyte[Columns];

        for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
        {
            rowValues[columnIndex] = Get(row, columnIndex);
        }

        return rowValues;
    }

    /// <summary>
    /// Exchanges two rows in the matrix.
    /// </summary>
    public void SwapRows(int r1, int r2)
    {
        var hasInvalidRowIndices = r1 < 0 || Rows <= r1 || r2 < 0 || Rows <= r2;

        if (hasInvalidRowIndices)
        {
            throw new ArgumentException("Row index out of range");
        }

        var originalFirstRow = Data[r1];

        Data[r1] = Data[r2];
        Data[r2] = originalFirstRow;
    }

    /// <summary>
    /// Returns the inverse of this matrix.
    ///
    /// throws ArgumentException when the matrix is singular and
    /// doesn't have an inverse.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public Matrix Invert()
    {
        // Sanity check.
        if (Rows != Columns)
        {
            throw new ArgumentException("Only square matrices can be inverted");
        }

        // Create a working matrix by augmenting this one with
        // an identity matrix on the right.
        var identity = Identity(Rows);
        var augmentedMatrix = Augment(identity);

        // Do Gaussian elimination to transform the left half into
        // an identity matrix.
        augmentedMatrix.GaussianElimination();

        // The right half is now the inverse.
        return augmentedMatrix.Submatrix(0, Rows, Columns, Columns * 2);
    }

    /// <summary>
    /// Does the work of matrix inversion.
    ///
    /// Assumes that this is an r by 2r matrix.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    private void GaussianElimination()
    {
        for (var rowIndex = 0; rowIndex < Rows; rowIndex++)
        {
            EnsureDiagonalEntryIsNonZero(rowIndex);
            ScaleRowToMakeDiagonalOne(rowIndex);
            ClearColumnBelowDiagonal(rowIndex);
        }

        for (var diagonalIndex = 0; diagonalIndex < Rows; diagonalIndex++)
        {
            ClearColumnAboveDiagonal(diagonalIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureDiagonalEntryIsNonZero(int rowIndex)
    {
        if (Data[rowIndex][rowIndex] != 0)
        {
            return;
        }

        for (var rowBelow = rowIndex + 1; rowBelow < Rows; rowBelow++)
        {
            if (Data[rowBelow][rowIndex] != 0)
            {
                SwapRows(rowIndex, rowBelow);
                break;
            }
        }

        if (Data[rowIndex][rowIndex] == 0)
        {
            throw new ArgumentException("Matrix is singular");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScaleRowToMakeDiagonalOne(int rowIndex)
    {
        if (Data[rowIndex][rowIndex] == 1)
        {
            return;
        }

        var scale = Galois.Divide(1, Data[rowIndex][rowIndex]);

        for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
        {
            Data[rowIndex][columnIndex] = Galois.Multiply(Data[rowIndex][columnIndex], scale);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearColumnBelowDiagonal(int rowIndex)
    {
        for (var rowBelow = rowIndex + 1; rowBelow < Rows; rowBelow++)
        {
            if (Data[rowBelow][rowIndex] == 0)
            {
                continue;
            }

            var scale = Data[rowBelow][rowIndex];

            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                Data[rowBelow][columnIndex] ^= Galois.Multiply(scale, Data[rowIndex][columnIndex]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearColumnAboveDiagonal(int diagonalIndex)
    {
        for (var rowAbove = 0; rowAbove < diagonalIndex; rowAbove++)
        {
            if (Data[rowAbove][diagonalIndex] == 0)
            {
                continue;
            }

            var scale = Data[rowAbove][diagonalIndex];

            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                Data[rowAbove][columnIndex] ^= Galois.Multiply(scale, Data[diagonalIndex][columnIndex]);
            }
        }
    }
}
