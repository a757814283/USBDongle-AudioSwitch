using System;
using System.Globalization;
using System.Text;

namespace USBDongle_AudioSwitch
{
    /// <summary>
    /// Conversion between the byte arrays used for report matching and the
    /// space-separated hex strings shown in the configuration UI.
    /// </summary>
    internal static class Hex
    {
        /// <summary>
        /// Parses "08 c0 09 03 00 01 cc 0f". Tolerates commas, "0x" prefixes,
        /// dashes and arbitrary whitespace so pasted values from a serial
        /// terminal or a hex dump still work.
        /// </summary>
        public static bool TryParse(string text, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string cleaned = text.Replace("0x", " ").Replace("0X", " ")
                                  .Replace(",", " ").Replace("-", " ")
                                  .Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

            string[] parts = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            byte[] result = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                byte value;
                if (!byte.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    return false;
                }
                result[i] = value;
            }

            bytes = result;
            return true;
        }

        public static byte[] ParseOrEmpty(string text)
        {
            byte[] bytes;
            return TryParse(text, out bytes) ? bytes : new byte[0];
        }

        /// <summary>Renders at most <paramref name="maxBytes"/> bytes as lowercase hex.</summary>
        public static string Format(byte[] buffer, int length, int maxBytes)
        {
            if (buffer == null || length <= 0)
            {
                return string.Empty;
            }

            int count = Math.Min(Math.Min(length, buffer.Length), maxBytes);
            StringBuilder sb = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(buffer[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static string Format(byte[] bytes)
        {
            return bytes == null ? string.Empty : Format(bytes, bytes.Length, bytes.Length);
        }

        /// <summary>
        /// XOR of the preceding bytes. The receiver's 8th byte is the checksum of
        /// the first seven, so this validates that a signature is self-consistent.
        /// </summary>
        public static byte XorChecksum(byte[] bytes, int count)
        {
            byte checksum = 0;
            int limit = Math.Min(count, bytes == null ? 0 : bytes.Length);
            for (int i = 0; i < limit; i++)
            {
                checksum ^= bytes[i];
            }
            return checksum;
        }
    }
}
