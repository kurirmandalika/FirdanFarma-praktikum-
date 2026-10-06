namespace ApotekFirdanFarma.Models
{
    public class AppUser
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public bool CanPurchase { get; set; }
        public bool CanAdjustStock { get; set; }
        public bool IsSuperAdmin => string.Equals(Role, "SUPER_ADMIN", System.StringComparison.OrdinalIgnoreCase);
        public bool CanPurchaseEffective => IsSuperAdmin || CanPurchase;
        public bool CanAdjustStockEffective => IsSuperAdmin || CanAdjustStock;
    }
}
