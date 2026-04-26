namespace PoliCoLauncherApp.Models
{
    public class UserSession
    {
        public string Name { get; set; } = "";
        public string LastName { get; set; } = "";
        public string SteamURL { get; set; } = "";
        public string Key { get; set; } = "";
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public UserSession? Session { get; set; }

        public static LoginResult Ok(UserSession session) =>
            new() { Success = true, Session = session };

        public static LoginResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };
    }
}
