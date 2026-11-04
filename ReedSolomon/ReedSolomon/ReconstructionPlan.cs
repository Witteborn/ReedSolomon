namespace Witteborn.ReedSolomon;

internal sealed record ReconstructionPlan(int[] MissingDataIndices, int[] InputShardIndices, byte[][] CoefficientRows);
