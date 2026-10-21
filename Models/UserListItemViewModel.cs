namespace FarmhouseBakery.Models
{
    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }

        // True for the admin who is looking at the page.
        public bool IsCurrentUser { get; set; }

        public bool IsLockedOut { get; set; }
    }
}
