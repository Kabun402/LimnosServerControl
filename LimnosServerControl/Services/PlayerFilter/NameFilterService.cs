using System.Text.RegularExpressions;

namespace LimnosServerControl.Services.PlayerFilter
{
    public static class NameFilterService
    {
        private static List<string> _NameBlacklist = new List<string>
        {
            "administrator",
            "admin",
            "server",
            "desktop",
            "user",
            "player",
            "spieler",
            "nutzer",
            "hitler",
            "göbbels",
            "goebbels",
            "mengele",
            "bundy",
            "himmler",
            "putin",
            "stalin",
            "flüchtling"
        };

        public static bool ContainsBlacklistEntry(string name)
        {
            name = name.ToLower();
            return _NameBlacklist.Any(x => name.Contains(x));
        }

        public static bool CharactersAllowed(string name)
        {
            return Regex.IsMatch(name, "^[a-zäöüß.'\\- ]+$", RegexOptions.IgnoreCase);
        }

        public static bool HasMinLength(string name)
        {
            return name.Length >= 5;
        }

        public static bool IsTrimmed(string name)
        {
            return name == name.Trim();
        }

        public static bool HasSurAndLastName(string name)
        {
            return Regex.IsMatch(name, "[A-ZÄÖÜ].+ [A-ZÄÖÜ].+");
        }
    }
}
