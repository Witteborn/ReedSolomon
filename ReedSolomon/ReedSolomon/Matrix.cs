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
    private int Rows { get; set; }

    /// <summary>
    /// The number of columns in the matrix.
    /// </summary>
    private int Columns { get; set; }

    /// <summary>
    /// The data in the matrix, in row major form. <br/>
    ///<br/>
    /// To get element(r, c) : data[r][c]<br/>
    ///<br/>
    /// Because this is computer science, and not math,<br/>
    /// the indices for both the row and column start at 0.
    /// </summary>
    private sbyte[][] Data { get; set; }

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
        for (int r = 0; r < Rows; r++)
        {
            Data[r] = new sbyte[Columns];
        }
    }

    ///// <summary>
    ///// Initializes a matrix with the given row-major data.
    ///// </summary>
    ///// <param name="data"></param>
    ///// <exception cref="ArgumentException"></exception>
    //public Matrix(byte[][] data) : this(
    //      Array.ConvertAll(
    //          Array.ConvertAll(
    //              data,
    //              array => unchecked((sbyte[])array)),
    //          b => unchecked((sbyte)b
    //          )
    //          )
    //          )
    //{
    //}

    /// <summary>
    /// Initializes a matrix with the given row-major data.
    /// </summary>
    /// <param name="data"></param>
    /// <exception cref="ArgumentException"></exception>
    public Matrix(sbyte[][] data)
    {
        Rows = data.Length;
        Columns = data[0].Length;
        Data = new sbyte[Rows][];
        for (int r = 0; r < Rows; r++)
        {
            if (data[r].Length != Columns)
            {
                throw new ArgumentException("Not all rows have the same number of columns");
            }
            data[r] = new sbyte[Columns];
            for (int c = 0; c < Columns; c++)
            {
                data[r][c] = data[r][c];
            }
        }
    }

    /// <summary>
    /// Returns an identity matrix of the given size.
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    public static Matrix Identity(int size)
    {
        Matrix result = new Matrix(size, size);
        for (int i = 0; i < size; i++)
        {
            result.Set(i, i, 1);
        }
        return result;
    }

    /// <summary>
    /// Returns a human-readable string of the matrix contents.<br/>
    ///<br/>
    /// Example: [[1, 2], [3, 4]]
    /// </summary>
    /// <returns></returns>
    public override string? ToString()
    {
        StringBuilder result = new StringBuilder();
        result.Append('[');
        for (int r = 0; r < Rows; r++)
        {
            if (r != 0)
            {
                result.Append(", ");
            }
            result.Append('[');
            for (int c = 0; c < Columns; c++)
            {
                if (c != 0)
                {
                    result.Append(", ");
                }
                result.Append(Data[r][c] & 0xFF);
            }
            result.Append(']');
        }
        result.Append(']');
        return result.ToString();
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
    /// <returns></returns>
    public string ToBigString()
    {
        StringBuilder result = new StringBuilder();
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                int value = Get(r, c);
                if (value < 0)
                {
                    value += 256;
                }
                result.Append(string.Format("%02x ", value));
            }
            result.Append("\n");
        }
        return result.ToString();
    }

    /// <summary>
    /// Returns the value at row r, column c.
    /// </summary>
    /// <param name="r"></param>
    /// <param name="c"></param>
    /// <returns></returns>
    public sbyte Get(int r, int c)
    {
        if (r < 0 || Rows <= r)
        {
            throw new ArgumentException("Row index out of range: " + r);
        }
        if (c < 0 || Columns <= c)
        {
            throw new ArgumentException("Column index out of range: " + c);
        }
        return Data[r][c];
    }

    /// <summary>
    /// Sets the value at row r, column c.
    /// </summary>
    /// <param name="r"></param>
    /// <param name="c"></param>
    /// <param name="value"></param>
    public void Set(int r, int c, sbyte value)
    {
        if (r < 0 || Rows <= r)
        {
            throw new ArgumentException("Row index out of range: " + r);
        }
        if (c < 0 || Columns <= c)
        {
            throw new ArgumentException("Column index out of range: " + c);
        }
        Data[r][c] = value;
    }

    /// <summary>
    /// Returns true iff this matrix is identical to the other.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object? other)
    {
        if (other is not Matrix)
        {
            return false;
        }
        for (int r = 0; r < Rows; r++)
        {
            if (!Data[r].Equals(((Matrix)other).Data[r]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Multiplies this matrix (the one on the left) by another
    /// matrix(the one on the right).
    /// </summary>
    /// <param name="right"></param>
    /// <returns></returns>
    public Matrix Times(Matrix right)
    {
        if (Columns != right.Rows)
        {
            throw new ArgumentException(
                    "Columns on left (" + Columns + ") " +
                    "is different than rows on right (" + right.Rows + ")");
        }
        Matrix result = new Matrix(Rows, right.Columns);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < right.Columns; c++)
            {
                sbyte value = 0;
                for (int i = 0; i < Columns; i++)
                {
                    value ^= Galois.Multiply(Get(r, i), right.Get(i, c));
                }
                result.Set(r, c, value);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns the concatenation of this matrix and the matrix on the right.
    /// </summary>
    /// <param name="right"></param>
    /// <returns></returns>
    public Matrix Augment(Matrix right)
    {
        if (Rows != right.Rows)
        {
            throw new ArgumentException("Matrices don't have the same number of rows");
        }
        Matrix result = new Matrix(Rows, Columns + right.Columns);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                result.Data[r][c] = Data[r][c];
            }
            for (int c = 0; c < right.Columns; c++)
            {
                result.Data[r][Columns + c] = right.Data[r][c];
            }
        }
        return result;
    }

    /// <summary>
    /// Returns a part of this matrix.
    /// </summary>
    /// <param name="rmin"></param>
    /// <param name="cmin"></param>
    /// <param name="rmax"></param>
    /// <param name="cmax"></param>
    /// <returns></returns>
    public Matrix Submatrix(int rmin, int cmin, int rmax, int cmax)
    {
        Matrix result = new Matrix(rmax - rmin, cmax - cmin);
        for (int r = rmin; r < rmax; r++)
        {
            for (int c = cmin; c < cmax; c++)
            {
                result.Data[r - rmin][c - cmin] = Data[r][c];
            }
        }
        return result;
    }

    /// <summary>
    /// Returns one row of the matrix as a byte array.
    /// </summary>
    /// <param name="row"></param>
    /// <returns></returns>
    public sbyte[] GetRow(int row)
    {
        sbyte[] result = new sbyte[Columns];
        for (int c = 0; c < Columns; c++)
        {
            result[c] = Get(row, c);
        }
        return result;
    }

    /// <summary>
    /// Exchanges two rows in the matrix.
    /// </summary>
    /// <param name="r1"></param>
    /// <param name="r2"></param>
    public void SwapRows(int r1, int r2)
    {
        if (r1 < 0 || Rows <= r1 || r2 < 0 || Rows <= r2)
        {
            throw new ArgumentException("Row index out of range");
        }
        sbyte[] tmp = Data[r1];
        Data[r1] = Data[r2];
        Data[r2] = tmp;
    }

    /// <summary>
    /// Returns the inverse of this matrix.
    ///
    /// throws ArgumentException when the matrix is singular and
    /// doesn't have an inverse.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="IllegalArgumentException"></exception>
    public Matrix Invert()
    {
        // Sanity check.
        if (Rows != Columns)
        {
            throw new ArgumentException("Only square matrices can be inverted");
        }

        // Create a working matrix by augmenting this one with
        // an identity matrix on the right.
        Matrix work = Augment(Identity(Rows));

        // Do Gaussian elimination to transform the left half into
        // an identity matrix.
        work.GaussianElimination();

        // The right half is now the inverse.
        return work.Submatrix(0, Rows, Columns, Columns * 2);
    }

    /// <summary>
    /// Does the work of matrix inversion.
    ///
    /// Assumes that this is an r by 2r matrix.
    /// </summary>
    /// <exception cref="IllegalArgumentException"></exception>
    private void GaussianElimination()
    {
        // Clear out the part below the main diagonal and scale the main
        // diagonal to be 1.
        for (int r = 0; r < Rows; r++)
        {
            // If the element on the diagonal is 0, find a row below
            // that has a non-zero and swap them.
            if (Data[r][r] == 0)
            {
                for (int rowBelow = r + 1; rowBelow < Rows; rowBelow++)
                {
                    if (Data[rowBelow][r] != 0)
                    {
                        SwapRows(r, rowBelow);
                        break;
                    }
                }
            }
            // If we couldn't find one, the matrix is singular.
            if (Data[r][r] == 0)
            {
                throw new ArgumentException("Matrix is singular");
            }
            // Scale to 1.
            if (Data[r][r] != 1)
            {
                sbyte scale = Galois.Divide(1, Data[r][r]);
                for (int c = 0; c < Columns; c++)
                {
                    Data[r][c] = Galois.Multiply(Data[r][c], scale);
                }
            }
            // Make everything below the 1 be a 0 by subtracting
            // a multiple of it.  (Subtraction and addition are
            // both exclusive or in the Galois field.)
            for (int rowBelow = r + 1; rowBelow < Rows; rowBelow++)
            {
                if (Data[rowBelow][r] != 0)
                {
                    sbyte scale = Data[rowBelow][r];
                    for (int c = 0; c < Columns; c++)
                    {
                        Data[rowBelow][c] ^= Galois.Multiply(scale, Data[r][c]);
                    }
                }
            }
        }

        // Now clear the part above the main diagonal.
        for (int d = 0; d < Rows; d++)
        {
            for (int rowAbove = 0; rowAbove < d; rowAbove++)
            {
                if (Data[rowAbove][d] != 0)
                {
                    sbyte scale = Data[rowAbove][d];
                    for (int c = 0; c < Columns; c++)
                    {
                        Data[rowAbove][c] ^= Galois.Multiply(scale, Data[d][c]);
                    }
                }
            }
        }
    }
}