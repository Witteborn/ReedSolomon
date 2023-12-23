namespace Witteborn.ReedSolomon;

/// <summary>
///  Reed-Solomon Coding over 8-bit values.
/// </summary>
public class ReedSolomon
{
    private int DataShardCount { get; set; }
    private int ParityShardCount { get; set; }
    private int TotalShardCount { get; set; }
    private Matrix Matrix { get; set; }

    /// <summary>
    /// Rows from the matrix for encoding parity, each one as its own <br/>
    ///  sbyte array to allow for efficient access while encoding.
    /// </summary>
    private sbyte[][] ParityRows { get; set; }

    public ReedSolomon(int dataShardCount, int parityShardCount)
    {
        this.DataShardCount = dataShardCount;
        this.ParityShardCount = parityShardCount;
        this.TotalShardCount = dataShardCount + parityShardCount;
        this.Matrix = BuildMatrix(dataShardCount, this.TotalShardCount);
        this.ParityRows = new sbyte[parityShardCount][];
        for (int i = 0; i < parityShardCount; i++)
        {
            this.ParityRows[i] = this.Matrix.GetRow(dataShardCount + i);
        }
    }

    /// <summary>
    /// Encodes parity for a set of data shards.
    /// </summary>
    /// <param name="shards">An array containing data shards followed by parity shards. <br/>
    /// Each shard is a byte array, and they must all be the same size
    /// .</param>
    /// <param name="offset">The index of the first  sbyte in each shard to encode.</param>
    /// <param name="byteCount">The number of  sbytes to encode in each shard.</param>
    public void EncodeParity(sbyte[][] shards, int offset, int byteCount)
    {
        // Check arguments.
        CheckBuffersAndSizes(shards, offset, byteCount);

        // Build the array of output buffers.
        sbyte[][] outputs = new sbyte[ParityShardCount][];
        for (int i = 0; i < ParityShardCount; i++)
        {
            outputs[i] = shards[DataShardCount + i];
        }

        // Do the coding.
        CodeSomeShards(ParityRows, shards, outputs, ParityShardCount,
                offset, byteCount);
    }

    /// <summary>
    /// Returns true if the parity shards contain the right data.
    /// </summary>
    /// <param name="shards">An array containing data shards followed by parity shards. <br/>
    /// Each shard is a byte array, and they must all be the same size.</param>
    /// <param name="firstByte">The index of the first  sbyte in each shard to check.</param>
    /// <param name="byteCount">The number of  sbytes to check in each shard.</param>
    /// <returns></returns>
    public bool IsParityCorrect(sbyte[][] shards, int firstByte, int byteCount)
    {
        // Check arguments.
        CheckBuffersAndSizes(shards, firstByte, byteCount);

        // Build the array of buffers being checked.
        sbyte[][] toCheck = new sbyte[ParityShardCount][];
        for (int i = 0; i < ParityShardCount; i++)
        {
            toCheck[i] = shards[DataShardCount + i];
        }

        // Do the checking.
        return CheckSomeShards(ParityRows, shards, toCheck, ParityShardCount,
                firstByte, byteCount);
    }

    /// <summary>
    /// Given a list of shards, some of which contain data, fills in the <br/>
    /// ones that don't have data. <br/>
    ///<br/>
    /// Quickly does nothing if all of the shards are present. <br/>
    ///<br/>
    /// If any shards are missing (based on the flags in shardsPresent), <br/>
    /// the data in those shards is recomputed and filled in.
    /// </summary>
    /// <param name="shards"></param>
    /// <param name="shardPresent"></param>
    /// <param name="offset"></param>
    /// <param name="byteCount"></param>
    public void DecodeMissing(sbyte[][] shards,
                          bool[] shardPresent,
                          int offset,
                          int byteCount)
    {
        // Check arguments.
        CheckBuffersAndSizes(shards, offset, byteCount);

        // Quick check: are all of the shards present?  If so, there's
        // nothing to do.
        int numberPresent = 0;
        for (int i = 0; i < this.TotalShardCount; i++)
        {
            if (shardPresent[i])
            {
                numberPresent += 1;
            }
        }
        if (numberPresent == this.TotalShardCount)
        {
            // Cool.  All of the shards data data.  We don't
            // need to do anything.
            return;
        }

        // More complete sanity check
        if (numberPresent < this.DataShardCount)
        {
            throw new ArgumentException("Not enough shards present");
        }

        // Pull out the rows of the matrix that correspond to the
        // shards that we have and build a square matrix.  This
        // matrix could be used to generate the shards that we have
        // from the original data.
        //
        // Also, pull out an array holding just the shards that
        // correspond to the rows of the submatrix.  These shards
        // will be the input to the decoding process that re-creates
        // the missing data shards.
        Matrix subMatrix = new Matrix(this.DataShardCount, this.DataShardCount);
        sbyte[][] subShards = new sbyte[this.DataShardCount][];
        {
            int subMatrixRow = 0;
            for (int matrixRow = 0; matrixRow < this.TotalShardCount && subMatrixRow < this.DataShardCount; matrixRow++)
            {
                if (shardPresent[matrixRow])
                {
                    for (int c = 0; c < this.DataShardCount; c++)
                    {
                        subMatrix.Set(subMatrixRow, c, this.Matrix.Get(matrixRow, c));
                    }
                    subShards[subMatrixRow] = shards[matrixRow];
                    subMatrixRow += 1;
                }
            }
        }

        // Invert the matrix, so we can go from the encoded shards
        // back to the original data.  Then pull out the row that
        // generates the shard that we want to decode.  Note that
        // since this matrix maps back to the orginal data, it can
        // be used to create a data shard, but not a parity shard.
        Matrix dataDecodeMatrix = subMatrix.Invert();

        // Re-create any data shards that were missing.
        //
        // The input to the coding is all of the shards we actually
        // have, and the output is the missing data shards.  The computation
        // is done using the special decode matrix we just built.
        sbyte[][] outputs = new sbyte[this.ParityShardCount][];
        sbyte[][] matrixRows = new sbyte[this.ParityShardCount][];
        int outputCount = 0;
        for (int iShard = 0; iShard < this.DataShardCount; iShard++)
        {
            if (!shardPresent[iShard])
            {
                outputs[outputCount] = shards[iShard];
                matrixRows[outputCount] = dataDecodeMatrix.GetRow(iShard);
                outputCount += 1;
            }
        }
        CodeSomeShards(matrixRows, subShards, outputs, outputCount, offset, byteCount);

        // Now that we have all of the data shards intact, we can
        // compute any of the parity that is missing.
        //
        // The input to the coding is ALL of the data shards, including
        // any that we just calculated.  The output is whichever of the
        // data shards were missing.
        outputCount = 0;
        for (int iShard = this.DataShardCount; iShard < this.TotalShardCount; iShard++)
        {
            if (!shardPresent[iShard])
            {
                outputs[outputCount] = shards[iShard];
                matrixRows[outputCount] = this.ParityRows[iShard - this.DataShardCount];
                outputCount += 1;
            }
        }
        CodeSomeShards(matrixRows, shards, outputs, outputCount, offset, byteCount);
    }

    /// <summary>
    /// Checks the consistency of arguments passed to public methods.
    /// </summary>
    /// <param name="shards"></param>
    /// <param name="offset"></param>
    /// <param name="byteCount"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void CheckBuffersAndSizes(sbyte[][] shards, int offset, int byteCount)
    {
        // The number of buffers should be equal to the number of
        // data shards plus the number of parity shards.
        if (shards.Length != this.TotalShardCount)
        {
            throw new ArgumentException("wrong number of shards: " + shards.Length);
        }

        // All of the shard buffers should be the same length.
        int shardLength = shards[0].Length;
        for (int i = 1; i < shards.Length; i++)
        {
            if (shards[i].Length != shardLength)
            {
                throw new ArgumentException("Shards are different sizes");
            }
        }

        // The offset and byteCount must be non-negative and fit in the buffers.
        if (offset < 0)
        {
            throw new ArgumentException("offset is negative: " + offset);
        }
        if (byteCount < 0)
        {
            throw new ArgumentException("byteCount is negative: " + byteCount);
        }
        if (shardLength < offset + byteCount)
        {
            throw new ArgumentException("buffers to small: " + byteCount + offset);
        }
    }

    /// <summary>
    ///Multiplies a subset of rows from a coding matrix by a full set of <br/>
    ///input shards to produce some output shards.
    /// </summary>
    /// <param name="matrixRows">The rows from the matrix to use.</param>
    /// <param name="inputs">An array of byte arrays, each of which is one input shard. <br/>
    /// The inputs array may have extra buffers after the ones <br/>
    /// that are used.They will be ignored.The number of <br/>
    /// inputs used is determined by the length of the <br/>
    /// each matrix row.</param>
    /// <param name="outputs">Byte arrays where the computed shards are stored.
    /// The outputs array may also have extra, unused, elements <br/>
    /// at the end.The number of outputs computed, and the <br/>
    /// number of matrix rows used, is determined by <br/>
    /// outputCount.</param>
    /// <param name="outputCount">The number of outputs to compute.</param>
    /// <param name="offset">The index in the inputs and output of the first byte
    /// to process.</param>
    /// <param name="byteCount">The number of bytes to process.</param>
    /// <exception cref="NotImplementedException"></exception>
    private void CodeSomeShards(sbyte[][] matrixRows,
                             sbyte[][] inputs,
                             sbyte[][] outputs,
                            int outputCount,
                            int offset,
                            int byteCount)
    {
        // This is the inner loop.  It needs to be fast.  Be careful
        // if you change it.
        //
        // Note that dataShardCount is final in the class, so the
        // compiler can load it just once, before the loop.  Explicitly
        // adding a local variable does not make it faster.
        //
        // I have tried inlining Galois.multiply(), but it doesn't
        // make things any faster.  The JIT compiler is known to inline
        // methods, so it's probably already doing so.
        //
        // This method has been timed and compared with a C implementation.
        // This Java version is only about 10% slower than C.

        for (int iByte = offset; iByte < offset + byteCount; iByte++)
        {
            for (int iRow = 0; iRow < outputCount; iRow++)
            {
                sbyte[] matrixRow = matrixRows[iRow];
                int value = 0;
                for (int c = 0; c < this.DataShardCount; c++)
                {
                    value ^= Galois.Multiply(matrixRow[c], inputs[c][iByte]);
                }
                outputs[iRow][iByte] = (sbyte)value;
            }
        }
    }

    /// <summary>
    ///  Multiplies a subset of rows from a coding matrix by a full set of <br/>
    ///  input shards to produce some output shards, and checks that the <br/>
    ///  the data is those shards matches what's expected.
    /// </summary>
    /// <param name="matrixRows">The rows from the matrix to use.</param>
    /// <param name="inputs"> An array of byte arrays, each of which is one input shard. <br/>
    /// The inputs array may have extra buffers after the ones <br/>
    /// that are used.They will be ignored.The number of <br/>
    /// inputs used is determined by the length of the <br/>
    /// each matrix row.</param>
    /// <param name="toCheck">Byte arrays where the computed shards are stored.
    /// The outputs array may also have extra, unused, elements <br/>
    /// at the end.The number of outputs computed, and the <br/>
    /// number of matrix rows used, is determined by <br/>
    /// outputCount.</param>
    /// <param name="checkCount">The number of outputs to compute.</param>
    /// <param name="offset">The index in the inputs and output of the first byte
    /// to process.</param>
    /// <param name="byteCount">The number of bytes to process.</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private bool CheckSomeShards(sbyte[][] matrixRows,
                                sbyte[][] inputs,
                                sbyte[][] toCheck,
                                int checkCount,
                                int offset,
                                int byteCount)
    {
        // This is the inner loop.  It needs to be fast.  Be careful
        // if you change it.
        //
        // Note that dataShardCount is final in the class, so the
        // compiler can load it just once, before the loop.  Explicitly
        // adding a local variable does not make it faster.
        //
        // I have tried inlining Galois.multiply(), but it doesn't
        // make things any faster.  The JIT compiler is known to inline
        // methods, so it's probably already doing so.
        //
        // This method has been timed and compared with a C implementation.
        // This Java version is only about 10% slower than C.

        for (int iByte = offset; iByte < offset + byteCount; iByte++)
        {
            for (int iRow = 0; iRow < checkCount; iRow++)
            {
                sbyte[] matrixRow = matrixRows[iRow];
                int value = 0;
                for (int c = 0; c < this.DataShardCount; c++)
                {
                    value ^= Galois.Multiply(matrixRow[c], inputs[c][iByte]);
                }
                if (toCheck[iRow][iByte] != (sbyte)value)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Create the matrix to use for encoding, given the number of <br/>
    /// data shards and the number of total shards. <br/>
    ///<br/>
    /// The top square of the matrix is guaranteed to be an identity <br/>
    /// matrix, which means that the data shards are unchanged after encoding.
    /// </summary>
    /// <param name="dataShards"></param>
    /// <param name="totalShards"></param>
    /// <returns></returns>
    private static Matrix BuildMatrix(int dataShards, int totalShards)
    {
        // Start with a Vandermonde matrix.  This matrix would work,
        // in theory, but doesn't have the property that the data
        // shards are unchanged after encoding.
        Matrix vandermonde = Vandermonde(totalShards, dataShards);

        // Multiple by the inverse of the top square of the matrix.
        // This will make the top square be the identity matrix, but
        // preserve the property that any square subset of rows  is
        // invertible.
        Matrix top = vandermonde.Submatrix(0, 0, dataShards, dataShards);
        return vandermonde.Times(top.Invert());
    }

    /// <summary>
    /// Create a Vandermonde matrix, which is guaranteed to have the <br/>
    /// property that any subset of rows that forms a square matrix <br/>
    /// is invertible.
    /// </summary>
    /// <param name="rows">Number of rows in the result.</param>
    /// <param name="cols">Number of columns in the result.</param>
    /// <returns>A Matrix.</returns>
    private static Matrix Vandermonde(int rows, int cols)
    {
        Matrix result = new Matrix(rows, cols);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result.Set(r, c, Galois.Exp((sbyte)r, c));
            }
        }
        return result;
    }
}