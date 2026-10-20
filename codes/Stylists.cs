using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class Stylists : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            Prepare(site, media, "stylists", "Stylists", "Meet the stylists at " + site.Brand + ": their specialties, working days and services. Book with the one you like.");
            string w = (QueryValue("w") ?? string.Empty).Trim();
            Stylist? s = site.Stylists.FirstOrDefault(x => x.Active && x.Id == w);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_detail}", s == null ? string.Empty : DetailHtml(site, s)).Replace("{plhd_open}", s == null ? string.Empty : " st-open");
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            string id = Ui.Clip(GetDataValue("who"), 40);
            Stylist? s = site.Stylists.FirstOrDefault(x => x.Active && x.Id == id);
            if (s == null)
            {
                response.ExecuteScript("StylistsJs.toast('That stylist is no longer with us.');");
                return response;
            }
            response.SetElementContents("st-detail", DetailHtml(site, s));
            response.ExecuteScript("StylistsJs.shown('" + Ui.Js(s.Id) + "');");
            return response;
        }

        private static string Days(SiteData site, Stylist s)
        {
            List<string> parts = new List<string>();
            int i = 0;
            while (i < 7)
            {
                if (!s.Week[i].On || !site.Hours[i].Open)
                {
                    i++;
                    continue;
                }
                int j = i;
                while (j + 1 < 7 && s.Week[j + 1].On && site.Hours[j + 1].Open)
                {
                    j++;
                }
                string a = Ui.DayNames[i].Substring(0, 3);
                parts.Add(j == i ? a : j == i + 1 ? a + ", " + Ui.DayNames[j].Substring(0, 3) : a + "&ndash;" + Ui.DayNames[j].Substring(0, 3));
                i = j + 1;
            }
            return parts.Count == 0 ? "Not taking bookings" : string.Join(", ", parts);
        }

        private static string Face(Stylist s, int size)
        {
            return s.Photo.Length > 0 ? "<img src=\"" + Ui.Esc(s.Photo) + "\" alt=\"Portrait of " + Ui.Esc(s.Name) + "\" width=\"" + size + "\" height=\"" + size + "\" loading=\"lazy\">" : "<span class=\"st-noface\">" + Ui.Esc(Ui.Initials(s.Name)) + "</span>";
        }

        private static string DetailHtml(SiteData site, Stylist s)
        {
            StringBuilder sb = new StringBuilder("<div class=\"st-dtop\">" + Face(s, 140) + "<div><h2 id=\"st-dtitle\">" + Ui.Esc(s.Name) + "</h2><p class=\"st-role\">" + Ui.Esc(s.Role) + "</p><p class=\"st-days\">Works " + Days(site, s) + "</p></div></div>");
            sb.Append("<p class=\"st-bio\">" + Ui.Esc(s.Bio) + "</p>");
            if (s.Specialties.Length > 0)
            {
                sb.Append("<p class=\"st-spec\"><b>Loves:</b> " + Ui.Esc(s.Specialties) + "</p>");
            }
            List<SalonService> list = site.Services.Where(x => x.Active && s.Services.Contains(x.Id)).OrderBy(x => x.Order).ToList();
            sb.Append("<h3>Services</h3><ul class=\"st-svc\">");
            foreach (SalonService x in list)
            {
                sb.Append("<li><a href=\"Book?s=" + Ui.Esc(x.Id) + "&amp;p=" + Ui.Esc(s.Id) + "\"><span>" + Ui.Esc(x.Name) + "</span><b>" + (x.Price == 0 ? "Free" : (x.From ? "from " : string.Empty) + Ui.Price(x.Price)) + "</b></a></li>");
            }
            sb.Append("</ul><a class=\"st-btn\" href=\"Book?p=" + Ui.Esc(s.Id) + "\">Book with " + Ui.Esc(s.Name.Split(' ')[0]) + "</a>");
            return sb.ToString();
        }

        private string DataPart(SiteData site, Block b, bool first)
        {
            if (b.Source != "stylistlist")
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true, first ? "h1" : "h2") + "<div class=\"st-cards\">");
            List<Stylist> list = site.Stylists.Where(s => s.Active).OrderBy(s => s.Order).ToList();
            foreach (Stylist s in list)
            {
                string first1 = s.Name.Split(' ')[0];
                sb.Append("<article class=\"st-card\"><button type=\"button\" class=\"st-face\" onclick=\"StylistsJs.show('" + Ui.Js(s.Id) + "')\" aria-label=\"About " + Ui.Esc(s.Name) + "\">" + Face(s, 220) + "</button><div class=\"st-ct\"><h3>" + Ui.Esc(s.Name) + "</h3><p class=\"st-role\">" + Ui.Esc(s.Role) + "</p><p class=\"st-spec\">" + Ui.Esc(s.Specialties) + "</p><p class=\"st-days\">" + Days(site, s) + "</p></div><div class=\"st-cacts\"><a class=\"st-btn st-btn-sm\" href=\"Book?p=" + Ui.Esc(s.Id) + "\">Book with " + Ui.Esc(first1) + "</a><button type=\"button\" class=\"st-btn st-btn-ghost st-btn-sm\" onclick=\"StylistsJs.show('" + Ui.Js(s.Id) + "')\">About " + Ui.Esc(first1) + "</button></div></article>");
            }
            sb.Append("</div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"st-none\">The team will be introduced here soon.</p>");
            }
            return Sec(b, sb.ToString());
        }

        private void Prepare(SiteData site, List<MediaItem> media, string key, string title, string desc)
        {
            HtmlDoc.SetTitle(title + " | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", desc);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_foot}", FootHtml(site))
                .Replace("{plhd_blocks}", PageHtml(site, media, key));
        }

        private static string HourText(DayHours h)
        {
            int a = Diary.Min(h.From);
            int b = Diary.Min(h.To);
            return h.Open && a >= 0 && b > a ? Ui.Time(a) + " &ndash; " + Ui.Time(b) : "Closed";
        }

        private static string HoursRows(SiteData site)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 7; i++)
            {
                sb.Append("<li><b>" + Ui.DayNames[i].Substring(0, 3) + "</b><span>" + HourText(site.Hours[i]) + "</span></li>");
            }
            return sb.ToString();
        }

        private static string Tel(string phone)
        {
            return "tel:" + new string(phone.Where(ch => char.IsDigit(ch) || ch == '+').ToArray());
        }

        private static string FootHtml(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"st-foot-in\"><div><a class=\"st-logo\" href=\"Home\"><svg viewBox=\"0 0 40 40\" width=\"34\" height=\"34\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"11\" fill=\"#231A17\"/><path d=\"M14 31c-1-7 13-7 13-14 0-4-3-7-7.5-7\" fill=\"none\" stroke=\"#E8C9A9\" stroke-width=\"3.2\" stroke-linecap=\"round\"/><path d=\"M20 32c0-5 9-5.5 9.5-11\" fill=\"none\" stroke=\"#C2553A\" stroke-width=\"2.6\" stroke-linecap=\"round\"/></svg><span>" + Ui.Esc(site.Brand) + "</span></a><p>" + Ui.Esc(site.Tagline) + "</p></div>");
            sb.Append("<div><h3>Visit</h3><ul><li>" + Ui.Esc(site.Street) + "</li><li>" + Ui.Esc(site.City) + "</li><li><a href=\"" + Ui.Esc(Tel(site.Phone)) + "\">" + Ui.Esc(site.Phone) + "</a></li><li><a href=\"mailto:" + Ui.Esc(site.Email) + "\">" + Ui.Esc(site.Email) + "</a></li></ul></div>");
            sb.Append("<div><h3>Hours</h3><ul class=\"st-fhours\">" + HoursRows(site) + "</ul></div>");
            sb.Append("<div><h3>Salon</h3><ul><li><a href=\"Services\">Services &amp; prices</a></li><li><a href=\"Stylists\">Stylists</a></li><li><a href=\"Gallery\">Gallery</a></li><li><a href=\"Book\">Book online</a></li><li><a href=\"Contact\">Contact</a></li><li><a href=\"AdminLogin\">Staff sign in</a></li></ul></div></div>");
            sb.Append("<div class=\"st-foot-b\"><span>&copy; " + DateTime.Today.Year + " " + Ui.Esc(site.Brand) + "</span><span>Pay at the salon by card or cash. No deposits.</span></div>");
            return sb.ToString();
        }

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "scissors" => a + "<circle cx=\"6.5\" cy=\"17.5\" r=\"2.8\"/><circle cx=\"17.5\" cy=\"17.5\" r=\"2.8\"/><path d=\"M8.6 15.6L18 4M15.4 15.6L6 4\"/></svg>",
                "drop" => a + "<path d=\"M12 3.5s6 6.4 6 10.6a6 6 0 0 1-12 0C6 9.9 12 3.5 12 3.5z\"/><path d=\"M9.5 14.5a2.6 2.6 0 0 0 2.5 2.5\"/></svg>",
                "spark" => a + "<path d=\"M12 3v5M12 16v5M3 12h5M16 12h5M6.5 6.5l2.6 2.6M14.9 14.9l2.6 2.6M17.5 6.5l-2.6 2.6M9.1 14.9l-2.6 2.6\"/></svg>",
                "leaf" => a + "<path d=\"M5 19c0-8 5-13 14-13-0 9-5 14-14 13zM5 19c3-4 6-7 10-9\"/></svg>",
                "cup" => a + "<path d=\"M4.5 9h12v5a5 5 0 0 1-5 5h-2a5 5 0 0 1-5-5zM16.5 10.5h1.5a2.5 2.5 0 0 1 0 5h-1.8M8 3.5c0 1.5 1.5 1.5 1.5 3M12 3.5c0 1.5 1.5 1.5 1.5 3\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "pin" => a + "<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "cal" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "heart" => a + "<path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg>",
                "star" => a + "<path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg>",
                "users" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            if (Blocks.IsVideo(src))
            {
                string poster = src.Substring(0, src.Length - 4) + ".webp";
                return "<video src=\"" + Ui.Esc(src) + "\"" + (File.Exists(Path.Combine(Store.Root, poster)) ? " poster=\"" + Ui.Esc(poster) + "\"" : string.Empty) + " playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return "<img src=\"" + Ui.Esc(src) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"st-btn" + (main ? string.Empty : " st-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"st-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"st-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"st-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"st-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "st-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"st-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"st-wrap st-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            return "<section class=\"st-bhero" + (mv == string.Empty ? " st-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"st-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"st-wrap st-bhero-in\"><div class=\"st-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"st-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "st-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"st-bsplit" + (b.Flip ? " st-bflip" : string.Empty) + (b.Media == string.Empty ? " st-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"st-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"st-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"st-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"st-bcards st-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"st-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"st-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"st-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"st-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"st-bsteps st-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"st-bstats st-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Initials(string name)
        {
            string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"st-bpeople st-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"st-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"st-bnoface\">" + Ui.Esc(Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"st-bboxes st-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"st-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"st-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>" + Icon("check") + "<span>" + Ui.Esc(Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"st-bfaq\">" + Head(site, b, false) + "<div class=\"st-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"st-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"st-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"st-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"st-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "st-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(SiteData site, List<MediaItem> media, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(site, media, b, first),
                    "text" => Sec(b, "<div class=\"st-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(site, media, b),
                    "cards" => Cards(site, b),
                    "steps" => Steps(site, b),
                    "stats" => StatRow(site, b),
                    "people" => People(site, media, b),
                    "boxes" => Boxes(site, b),
                    "faq" => Faq(site, b),
                    "quote" => Quote(site, media, b),
                    "media" => Wide(site, media, b),
                    "cta" => Cta(site, b),
                    "data" => DataPart(site, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            return sb.ToString();
        }

    }
}
