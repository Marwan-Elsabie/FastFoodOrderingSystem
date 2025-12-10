using System.Text.RegularExpressions;

namespace FastFoodOrderingSystem.Helpers
{
    public static class SecurityHelper
    {
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove potentially dangerous characters
            return Regex.Replace(input, @"[<>""'&]", string.Empty);
        }

        public static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}