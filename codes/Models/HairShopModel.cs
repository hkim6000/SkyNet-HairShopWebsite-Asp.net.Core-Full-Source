namespace HairShop.Models
{
    public class SiteData
    {
        public string Brand { get; set; } = "HairShop";
        public string Tagline { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string MapNote { get; set; } = string.Empty;
        public List<DayHours> Hours { get; set; } = new List<DayHours>();
        public int SlotStep { get; set; } = 15;
        public int Buffer { get; set; } = 10;
        public int HorizonDays { get; set; } = 60;
        public int LeadMinutes { get; set; } = 60;
        public List<int> TipPresets { get; set; } = new List<int> { 15, 18, 20 };
        public int MaxVideoMb { get; set; } = 30;
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<SalonService> Services { get; set; } = new List<SalonService>();
        public List<Stylist> Stylists { get; set; } = new List<Stylist>();
        public List<GalleryTag> GalleryTags { get; set; } = new List<GalleryTag>();
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
    }

    public class DayHours
    {
        public bool Open { get; set; }
        public string From { get; set; } = "09:00";
        public string To { get; set; } = "18:00";
    }

    public class Category
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class GalleryTag
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class SalonService
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Minutes { get; set; } = 45;
        public decimal Price { get; set; }
        public bool From { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public bool Featured { get; set; }
        public int Order { get; set; }
    }

    public class Stylist
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Specialties { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public List<string> Services { get; set; } = new List<string>();
        public List<WorkDay> Week { get; set; } = new List<WorkDay>();
        public List<TimeOff> Off { get; set; } = new List<TimeOff>();
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class WorkDay
    {
        public bool On { get; set; }
        public string From { get; set; } = "09:00";
        public string To { get; set; } = "17:00";
        public string BreakFrom { get; set; } = string.Empty;
        public string BreakTo { get; set; } = string.Empty;
    }

    public class TimeOff
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class Guest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class Booking
    {
        public string Id { get; set; } = string.Empty;
        public string Ref { get; set; } = string.Empty;
        public string GuestId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public int Minutes { get; set; }
        public decimal Price { get; set; }
        public string StylistId { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public string Status { get; set; } = "booked";
        public string Note { get; set; } = string.Empty;
        public string Source { get; set; } = "online";
        public string Reason { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
        public DateTime? Arrived { get; set; }
        public DateTime? Finished { get; set; }
        public Payment? Paid { get; set; }
    }

    public class Payment
    {
        public List<PayLine> Lines { get; set; } = new List<PayLine>();
        public decimal ServiceTotal { get; set; }
        public decimal Discount { get; set; }
        public string TipMode { get; set; } = "none";
        public decimal Tip { get; set; }
        public decimal Total { get; set; }
        public string Method { get; set; } = "card";
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PayLine
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class ContactMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public bool Read { get; set; }
    }

    public class Staff
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool Admin { get; set; }
        public bool Active { get; set; } = true;
        public bool MustChange { get; set; }
        public string StylistId { get; set; } = string.Empty;
        public bool Own { get; set; }
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class Session
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
    }

    public class ActivityEntry
    {
        public DateTime When { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Name { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public bool Gallery { get; set; }
        public string Tag { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime Added { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PageDoc
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
        public List<PageVersion> Versions { get; set; } = new List<PageVersion>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PageVersion
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
