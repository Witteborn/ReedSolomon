namespace CompatibilityValidation;

/// <summary>Creates payload bytes that do not depend on the runtime's random number generator.</summary>
internal static class Payload
{
    internal static byte[] Create(int lengthBytes, uint seed)
    {
        const uint multiplier = 1664525;
        const uint increment = 1013904223;
        const int highByteShift = 24;

        var payload = new byte[lengthBytes];
        var generatorState = seed;

        for (var byteIndex = 0; byteIndex < lengthBytes; byteIndex++)
        {
            generatorState = unchecked(generatorState * multiplier + increment);
            payload[byteIndex] = (byte)(generatorState >> highByteShift);
        }

        return payload;
    }
}
