using System.Linq;

namespace TimestampService.Functions
{
    public static class HashValidator
    {
        public static bool IsValidHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            var hexChars = new char[] {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                'a', 'b', 'c', 'd', 'e', 'f', 'A', 'B', 'C', 'D', 'E', 'F'};

            if (!hash.All(ch => hexChars.Contains(ch)))
            {
                return false;
            }

            if (hash.Length % 2 != 0)
            {
                return false;
            }

            return true;
        }
    }
}
