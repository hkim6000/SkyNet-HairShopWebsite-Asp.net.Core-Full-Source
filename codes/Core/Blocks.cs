using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HairShop.Models;

namespace HairShop.Core
{
    public sealed class PageSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Href { get; init; } = string.Empty;
        public string[] Data { get; init; } = Array.Empty<string>();
    }

    public sealed class PartSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Desc { get; init; } = string.Empty;
        public string Fields { get; init; } = string.Empty;
        public string ItemFields { get; init; } = string.Empty;
    }

    public static class Blocks
    {
        public static readonly PageSpec[] Pages =
        {
            new PageSpec { Key = "home", Name = "Home", Href = "Home", Data = new[] { "featured", "team", "visit" } },
            new PageSpec { Key = "services", Name = "Services & prices", Href = "Services", Data = new[] { "pricelist" } },
            new PageSpec { Key = "stylists", Name = "Stylists", Href = "Stylists", Data = new[] { "stylistlist" } },
            new PageSpec { Key = "gallery", Name = "Gallery", Href = "Gallery", Data = new[] { "gallerygrid" } },
            new PageSpec { Key = "book", Name = "Book online", Href = "Book", Data = new[] { "bookform" } },
            new PageSpec { Key = "contact", Name = "Contact", Href = "Contact", Data = new[] { "contactform" } }
        };

        public static readonly PartSpec[] Types =
        {
            new PartSpec { Key = "hero", Name = "Page top", Desc = "Big title, text, buttons and a picture or video.", Fields = "kicker,title,text,media,buttons" },
            new PartSpec { Key = "text", Name = "Text", Desc = "A title and paragraphs.", Fields = "kicker,title,text" },
            new PartSpec { Key = "split", Name = "Picture and text", Desc = "A picture or video beside the text.", Fields = "kicker,title,text,media,button,flip" },
            new PartSpec { Key = "cards", Name = "Cards", Desc = "A row of cards with an icon, title, text and link.", Fields = "kicker,title,text,items", ItemFields = "icon,title,text,link" },
            new PartSpec { Key = "steps", Name = "Steps", Desc = "Numbered steps, for how a visit works.", Fields = "kicker,title,text,items", ItemFields = "title,text" },
            new PartSpec { Key = "stats", Name = "Numbers", Desc = "A row of big numbers with a label under each.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "boxes", Name = "Checklists", Desc = "Boxes with a title and one tick per line.", Fields = "kicker,title,items", ItemFields = "title,lines" },
            new PartSpec { Key = "people", Name = "People", Desc = "Portraits with a name and a role.", Fields = "kicker,title,text,items", ItemFields = "media,title,text" },
            new PartSpec { Key = "faq", Name = "Questions", Desc = "Questions that open to show the answer.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "quote", Name = "Quote", Desc = "A quote with a name and a small photo.", Fields = "quote,name,media" },
            new PartSpec { Key = "media", Name = "Picture or video", Desc = "One wide picture or video with a caption.", Fields = "media,caption" },
            new PartSpec { Key = "cta", Name = "Call to action", Desc = "A colored band with a title, text and buttons.", Fields = "kicker,title,text,buttons" }
        };

        public static readonly PartSpec[] Parts =
        {
            new PartSpec { Key = "featured", Name = "Popular services", Desc = "Services marked Popular on the Services page, with prices and a Book button.", Fields = "kicker,title,text" },
            new PartSpec { Key = "team", Name = "The team", Desc = "Every active stylist with their portrait. Change them on the Stylists page.", Fields = "kicker,title,text" },
            new PartSpec { Key = "visit", Name = "Opening hours and address", Desc = "Hours, address and phone from Settings, with a Book button.", Fields = "kicker,title,text" },
            new PartSpec { Key = "pricelist", Name = "Price list", Desc = "Every active service by category, with time, price and a Book button.", Fields = "kicker,title,text" },
            new PartSpec { Key = "stylistlist", Name = "Stylist cards", Desc = "Every active stylist with bio, specialties and working days.", Fields = "kicker,title,text" },
            new PartSpec { Key = "gallerygrid", Name = "Gallery grid", Desc = "Photos and videos marked Show in gallery in the Media library, with tag filters.", Fields = "kicker,title,text" },
            new PartSpec { Key = "bookform", Name = "Booking form", Desc = "Service, stylist, day, free times and the guest details.", Fields = "kicker,title,text" },
            new PartSpec { Key = "contactform", Name = "Contact details and form", Desc = "Address, phone, email and hours from Settings, beside the message form.", Fields = "kicker,title,text" }
        };

        public static readonly (string Key, string Name)[] Icons = { ("scissors", "Scissors"), ("drop", "Color drop"), ("spark", "Sparkle"), ("leaf", "Leaf"), ("cup", "Coffee cup"), ("clock", "Clock"), ("pin", "Map pin"), ("cal", "Calendar"), ("heart", "Heart"), ("star", "Star"), ("users", "People"), ("check", "Tick"), ("phone", "Phone"), ("mail", "Email"), ("card", "Card") };

        private static readonly string[] Allowed = { "p", "br", "b", "i", "ul", "ol", "li", "a" };

        public static PageSpec? PageOf(string? key)
        {
            return Pages.FirstOrDefault(p => p.Key == (key ?? string.Empty).Trim().ToLowerInvariant());
        }

        public static PartSpec? Spec(Block b)
        {
            return b.Type == "data" ? Parts.FirstOrDefault(p => p.Key == b.Source) : Types.FirstOrDefault(t => t.Key == b.Type);
        }

        public static bool Has(PartSpec s, string field)
        {
            return ("," + s.Fields + ",").Contains("," + field + ",", StringComparison.Ordinal);
        }

        public static bool IsVideo(string src)
        {
            return src.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        public static string SafeLink(string? value)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Length == 0 || v.Length > 300)
            {
                return string.Empty;
            }
            if (Regex.IsMatch(v, "^https?://[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^mailto:[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^tel:[0-9+() .-]+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^#[A-Za-z0-9_-]+$") || Regex.IsMatch(v, "^[A-Za-z][A-Za-z0-9_-]*(\\?[A-Za-z0-9=&%._+-]*)?(#[A-Za-z0-9_-]+)?$"))
            {
                return v;
            }
            return string.Empty;
        }

        public static string Sanitize(string? html, int max)
        {
            string src = Regex.Replace(html ?? string.Empty, "<(script|style|template|iframe|object)[\\s\\S]*?</\\1\\s*>", string.Empty, RegexOptions.IgnoreCase);
            src = Regex.Replace(src, "<!--[\\s\\S]*?-->", string.Empty);
            StringBuilder sb = new StringBuilder();
            List<string> open = new List<string>();
            foreach (Match m in Regex.Matches(src, "<[^>]*>|[^<]+|<"))
            {
                string t = m.Value;
                if (!t.StartsWith('<') || t == "<")
                {
                    sb.Append(Ui.Esc(WebUtility.HtmlDecode(t)));
                    continue;
                }
                Match tag = Regex.Match(t, "^<\\s*(/?)\\s*([a-zA-Z][a-zA-Z0-9]*)([^>]*)>$");
                if (!tag.Success)
                {
                    continue;
                }
                bool close = tag.Groups[1].Value == "/";
                string name = tag.Groups[2].Value.ToLowerInvariant() switch
                {
                    "strong" => "b",
                    "em" => "i",
                    "div" => "p",
                    "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "p",
                    string n => n
                };
                if (!Allowed.Contains(name))
                {
                    continue;
                }
                if (name == "br")
                {
                    if (!close)
                    {
                        sb.Append("<br>");
                    }
                    continue;
                }
                if (close)
                {
                    int at = open.LastIndexOf(name);
                    if (at < 0)
                    {
                        continue;
                    }
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                    continue;
                }
                if (name == "p" && open.Contains("p"))
                {
                    int at = open.LastIndexOf("p");
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                }
                if (name == "a")
                {
                    Match h = Regex.Match(tag.Groups[3].Value, "href\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);
                    string href = h.Success ? SafeLink(WebUtility.HtmlDecode(h.Groups[2].Value + h.Groups[3].Value + h.Groups[4].Value)) : string.Empty;
                    if (href == string.Empty || open.Contains("a"))
                    {
                        continue;
                    }
                    bool outside = href.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                    sb.Append("<a href=\"" + Ui.Esc(href) + "\"" + (outside ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">");
                    open.Add("a");
                    continue;
                }
                sb.Append("<" + name + ">");
                open.Add(name);
            }
            for (int i = open.Count - 1; i >= 0; i--)
            {
                sb.Append("</" + open[i] + ">");
            }
            string outHtml = Regex.Replace(sb.ToString(), "<(p|b|i|li|ul|ol)>(\\s|<br>)*</\\1>", string.Empty);
            outHtml = Regex.Replace(outHtml, "(<br>\\s*){3,}", "<br><br>").Trim();
            return outHtml.Length > max ? string.Empty : outHtml;
        }

        public static string Plain(string? value, int max)
        {
            string v = Regex.Replace((value ?? string.Empty).Replace("\r", string.Empty), "[ \\t]+", " ");
            v = Regex.Replace(v, "\\n{3,}", "\n\n").Trim();
            return v.Length > max ? v.Substring(0, max).Trim() : v;
        }

        public static string NewId()
        {
            return "b" + Guid.NewGuid().ToString("N").Substring(0, 9);
        }

        public static (List<Block>? Blocks, string Error) Clean(List<MediaItem> library, PageSpec page, string? json)
        {
            List<Block>? raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<Block>>(json ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return (null, "The page could not be read. Reload and try again.");
            }
            if (raw == null || raw.Count == 0 || raw.Count > 40)
            {
                return (null, "A page needs between 1 and 40 blocks.");
            }
            HashSet<string> media = library.Select(m => m.Path).ToHashSet(StringComparer.Ordinal);
            HashSet<string> photos = library.Where(m => m.Kind == "photo").Select(m => m.Path).ToHashSet(StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<Block> list = new List<Block>();
            foreach (Block r in raw)
            {
                string type = r.Type ?? string.Empty;
                PartSpec? spec = type == "data" ? Parts.FirstOrDefault(p => p.Key == r.Source && page.Data.Contains(p.Key)) : Types.FirstOrDefault(t => t.Key == type);
                if (spec == null)
                {
                    return (null, "That page has a block it cannot use. Reload and try again.");
                }
                if (type == "data" && list.Any(x => x.Type == "data" && x.Source == spec.Key))
                {
                    return (null, spec.Name + " is on the page twice.");
                }
                string id = Regex.IsMatch(r.Id ?? string.Empty, "^b[a-z0-9]{3,12}$") && !ids.Contains(r.Id!) ? r.Id! : NewId();
                ids.Add(id);
                Block b = new Block { Id = id, Type = type, Source = type == "data" ? spec.Key : string.Empty, Hidden = r.Hidden };
                b.Kicker = Has(spec, "kicker") ? Plain(r.Kicker, 80) : string.Empty;
                b.Title = Has(spec, "title") || Has(spec, "name") ? Plain(r.Title, 140).Replace("\n", " ") : string.Empty;
                if (Has(spec, "text"))
                {
                    b.Text = Sanitize(r.Text, 6000);
                    if (b.Text == string.Empty && Regex.Replace(r.Text ?? string.Empty, "<[^>]*>|\\s|&nbsp;", string.Empty).Length > 0)
                    {
                        return (null, "The text in " + spec.Name + " is too long.");
                    }
                }
                else if (Has(spec, "quote") || Has(spec, "caption"))
                {
                    b.Text = Plain(r.Text, Has(spec, "quote") ? 600 : 240);
                }
                if (Has(spec, "media"))
                {
                    string m = (r.Media ?? string.Empty).Trim();
                    bool photoOnly = type == "quote";
                    if (m != string.Empty && !(photoOnly ? photos.Contains(m) : media.Contains(m)))
                    {
                        return (null, "A picture in " + spec.Name + " is no longer in the media library. Choose it again.");
                    }
                    b.Media = m;
                }
                if (Has(spec, "buttons") || Has(spec, "button"))
                {
                    b.Label = Plain(r.Label, 40).Replace("\n", " ");
                    b.Link = SafeLink(r.Link);
                    if (b.Label != string.Empty && b.Link == string.Empty)
                    {
                        return (null, "Check the link of the button “" + b.Label + "”. Use a page name like Shop, Contact or a full https:// address.");
                    }
                    if (Has(spec, "buttons"))
                    {
                        b.Label2 = Plain(r.Label2, 40).Replace("\n", " ");
                        b.Link2 = SafeLink(r.Link2);
                        if (b.Label2 != string.Empty && b.Link2 == string.Empty)
                        {
                            return (null, "Check the link of the button “" + b.Label2 + "”.");
                        }
                    }
                }
                b.Flip = Has(spec, "flip") && r.Flip;
                if (Has(spec, "items"))
                {
                    foreach (BlockItem it in (r.Items ?? new List<BlockItem>()).Take(12))
                    {
                        BlockItem c = new BlockItem { Title = Plain(it.Title, 120).Replace("\n", " "), Text = Plain(it.Text, 600) };
                        if (spec.ItemFields.Contains("icon"))
                        {
                            c.Icon = Icons.Any(x => x.Key == it.Icon) ? it.Icon : string.Empty;
                        }
                        if (spec.ItemFields.Contains("media"))
                        {
                            string m = (it.Media ?? string.Empty).Trim();
                            if (m != string.Empty && !photos.Contains(m))
                            {
                                return (null, "A photo in " + spec.Name + " is no longer in the media library. Choose it again.");
                            }
                            c.Media = m;
                        }
                        if (spec.ItemFields.Contains("link"))
                        {
                            c.Link = SafeLink(it.Link);
                            c.Label = c.Link != string.Empty ? Plain(it.Label, 40).Replace("\n", " ") : string.Empty;
                            if ((it.Link ?? string.Empty).Trim() != string.Empty && c.Link == string.Empty)
                            {
                                return (null, "Check the link on the card “" + c.Title + "”.");
                            }
                        }
                        if (c.Title != string.Empty || c.Text != string.Empty || c.Media != string.Empty)
                        {
                            b.Items.Add(c);
                        }
                    }
                }
                list.Add(b);
            }
            string? missing = page.Data.FirstOrDefault(k => !list.Any(x => x.Type == "data" && x.Source == k));
            if (missing != null)
            {
                return (null, (Parts.FirstOrDefault(p => p.Key == missing)?.Name ?? missing) + " must stay on the page. Hide it instead.");
            }
            return (list, string.Empty);
        }

        private static Block Data(string key, string kicker = "", string title = "", string text = "")
        {
            return new Block { Id = NewId(), Type = "data", Source = key, Kicker = kicker, Title = title, Text = text };
        }

        private static BlockItem It(string title, string text, string icon = "", string link = "", string label = "")
        {
            return new BlockItem { Title = title, Text = text, Icon = icon, Link = link, Label = label };
        }

        public static bool Ensure(SiteData site)
        {
            bool changed = false;
            foreach (PageSpec p in Pages)
            {
                if (!site.Pages.TryGetValue(p.Key, out PageDoc? doc) || doc == null)
                {
                    doc = new PageDoc { Blocks = Seed(p.Key) };
                    site.Pages[p.Key] = doc;
                    changed = true;
                }
                foreach (string k in p.Data)
                {
                    if (!doc.Blocks.Any(b => b.Type == "data" && b.Source == k))
                    {
                        doc.Blocks.Add(Data(k, string.Empty, Parts.First(x => x.Key == k).Name));
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private static List<Block> Seed(string key)
        {
            switch (key)
            {
                case "home":
                    return new List<Block>
                    {
                        new Block { Id = NewId(), Type = "hero", Kicker = "Neighborhood salon · Cuts, color, care", Title = "Good hair, kept simple.", Text = "<p>Honest advice, clear prices and a chair that is ready when you are. Book online in a minute and pay at the salon after your visit.</p>", Media = "images/hero.webp", Label = "Book online", Link = "Book", Label2 = "See prices", Link2 = "Services" },
                        new Block { Id = NewId(), Type = "cards", Items = new List<BlockItem> { It("Book in a minute", "Pick a service, a stylist and a free time. We confirm it on the spot.", "cal"), It("Pay at the chair", "No deposit and no card online. Pay by card or cash when you are done.", "card"), It("Clear prices", "Every price is on the menu. If your hair needs more time, we tell you first.", "check") } },
                        Data("featured", "Popular right now", "Our most booked services", "<p>Prices include a consultation, a wash and a finish.</p>"),
                        new Block { Id = NewId(), Type = "split", Kicker = "About the salon", Title = "Six chairs, good coffee, no rush", Text = "<p>{name} is a small salon on a quiet corner. We keep the book light so every appointment starts on time and nobody is hurried out of the chair.</p><p>Bring a photo, bring a question, or just tell us how you want to feel when you leave.</p>", Media = "images/salon/space.webp", Label = "Meet the team", Link = "Stylists" },
                        Data("team", "The team", "Meet your stylists", string.Empty),
                        new Block { Id = NewId(), Type = "media", Media = "images/video/salon-loop.mp4", Text = "A slow look at the color bar." },
                        Data("visit", "Visit us", "Opening hours", "<p>Walk-ins are welcome when a chair is free. Booking is the surest way to get the time you want.</p>")
                    };
                case "services":
                    return new List<Block>
                    {
                        Data("pricelist", "Services & prices", "Our menu", "<p>Every price includes a consultation and a finish. Longer or thicker hair may need more time; we always tell you before we start.</p>"),
                        new Block { Id = NewId(), Type = "split", Kicker = "First visit?", Title = "Not sure what to book?", Text = "<p>Book a consultation. It is free, takes fifteen minutes, and your stylist will suggest the right service and give you an exact price.</p>", Media = "images/salon/chair.webp", Label = "Book a consultation", Link = "Book", Flip = true },
                        new Block { Id = NewId(), Type = "faq", Title = "Before you book", Items = new List<BlockItem> { It("Do I need a patch test for color?", "Yes, if you are new to us or have not had color with us for six months. Book a free patch test at least 48 hours before your color."), It("What does a \"from\" price mean?", "The price starts there. It depends on length and thickness. Your stylist confirms the price before starting."), It("Can I pay by card?", "Yes. We take card or cash at the salon after your visit. We never ask for a deposit.") } }
                    };
                case "stylists":
                    return new List<Block>
                    {
                        Data("stylistlist", "The team", "Meet your stylists", "<p>Every stylist does a full consultation first. Pick someone whose work you like, or choose Any stylist when you book.</p>"),
                        new Block { Id = NewId(), Type = "cta", Title = "Not sure who to book?", Text = "<p>Choose Any stylist and we will match you with whoever is free.</p>", Label = "Book online", Link = "Book", Label2 = "Ask us", Link2 = "Contact" }
                    };
                case "gallery":
                    return new List<Block>
                    {
                        Data("gallerygrid", "Gallery", "Recent work and the salon", "<p>Cuts, color and styling from the last few months, and a look around the salon.</p>")
                    };
                case "book":
                    return new List<Block>
                    {
                        Data("bookform", "Book online", "Choose a time that suits you", "<p>No deposit and no card needed. You pay at the salon after your visit.</p>"),
                        new Block { Id = NewId(), Type = "faq", Title = "Good to know", Items = new List<BlockItem> { It("Running late?", "Call us. We will do our best, but if you are more than 15 minutes late we may need to shorten the service or move it."), It("Need to cancel?", "Call or email at least 24 hours before so someone else can have the time."), It("Booking for two?", "Book each person separately. Choose the same time with two stylists to sit side by side.") } }
                    };
                case "contact":
                    return new List<Block>
                    {
                        Data("contactform", "Contact", "Come and see us", "<p>Questions about a service, a wedding party or a gift card? Send us a note and we will reply by email, usually the same day.</p>")
                    };
                default:
                    return new List<Block>();
            }
        }

        public static List<string> Uses(SiteData site, string src)
        {
            List<string> u = new List<string>();
            foreach (PageSpec p in Pages)
            {
                if (site.Pages.TryGetValue(p.Key, out PageDoc? doc) && doc.Blocks.Any(b => b.Media == src || b.Items.Any(i => i.Media == src)))
                {
                    u.Add("Page: " + p.Name);
                }
            }
            return u;
        }
    }
}
