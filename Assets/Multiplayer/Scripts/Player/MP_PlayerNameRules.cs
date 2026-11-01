using System.Text;

public static class MP_PlayerNameRules
{
    public const int MaxLength = 16;

    public static bool TryNormalize(string input, out string name, out string reason)
    {
        name = "";
        reason = "";
        if (string.IsNullOrWhiteSpace(input))
        {
            reason = "Enter a name before starting.";
            return false;
        }
        if (input.Length > 64)
        {
            reason = "Name must be 16 characters or fewer.";
            return false;
        }
        if (MP_PlayerBlockedTerms.ContainsIn(input))
        {
            reason = "That name isn't allowed. Please choose another.";
            return false;
        }

        StringBuilder builder = new StringBuilder(MaxLength);
        bool pendingSpace = false;
        foreach (char character in input)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            bool letter = character >= 'A' && character <= 'Z' ||
                character >= 'a' && character <= 'z';
            bool digit = character >= '0' && character <= '9';
            if (!letter && !digit && character != '_' && character != '-')
            {
                reason = "Use only letters A-Z, numbers, spaces, _ and -.";
                return false;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
            if (builder.Length > MaxLength)
            {
                reason = "Name must be 16 characters or fewer.";
                return false;
            }
        }

        name = builder.ToString();
        if (name.Length > 0)
            return true;

        reason = "Enter a name before starting.";
        return false;
    }
}
