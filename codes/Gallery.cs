using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class Gallery : WebPage
    {
        private string tag = string.Empty;
        private List<MediaItem> lib = new List<MediaItem>();

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            tag = PickTag(site, QueryValue("t"));
            lib = media;
            Prepare(site, media, "gallery", "Gallery", "Cuts, color and styling from " + site.Brand + ", plus photos and video of the salon.");
        }

        public async Task<ApiResponse> Filter()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            string t = PickTag(site, GetDataValue("tag"));
            response.SetElementContents("gl-chiprow", Chips(site, media, t));
            response.SetElementContents("gl-wall", WallHtml(site, media, t));
            response.ExecuteScript("GalleryJs.filtered('" + Ui.Js(t) + "');");
            return response;
        }

        private static string PickTag(SiteData site, string? v)
        {
            string t = (v ?? string.Empty).Trim().ToLowerInvariant();
            return site.GalleryTags.Any(x => x.Key == t) ? t : string.Empty;
        }

        private static List<MediaItem> Shown(List<MediaItem> media, string t)
        {
            return media.Where(m => m.Gallery && (t == string.Empty || m.Tag == t)).OrderByDescending(m => m.Added).ThenBy(m => m.Name, StringComparer.Ordinal).ToList();
        }

        private static string Chips(SiteData site, List<MediaItem> media, string cur)
        {
            StringBuilder sb = new StringBuilder("<button type=\"button\" class=\"gl-chip" + (cur == string.Empty ? " gl-act" : string.Empty) + "\" aria-pressed=\"" + (cur == string.Empty ? "true" : "false") + "\" onclick=\"GalleryJs.filter('')\">All <small>" + Shown(media, string.Empty).Count + "</small></button>");
            foreach (GalleryTag g in site.GalleryTags)
            {
                int n = Shown(media, g.Key).Count;
                if (n == 0)
                {
                    continue;
                }
                bool on = g.Key == cur;
                sb.Append("<button type=\"button\" class=\"gl-chip" + (on ? " gl-act" : string.Empty) + "\" aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"GalleryJs.filter('" + Ui.Js(g.Key) + "')\">" + Ui.Esc(g.Label) + " <small>" + n + "</small></button>");
            }
            return sb.ToString();
        }

        private static string WallHtml(SiteData site, List<MediaItem> media, string t)
        {
            List<MediaItem> list = Shown(media, t);
            if (list.Count == 0)
            {
                return "<p class=\"gl-none\">Nothing to show here yet.</p>";
            }
            StringBuilder sb = new StringBuilder();
            List<MediaItem> videos = list.Where(m => m.Kind == "video").ToList();
            foreach (MediaItem v in videos)
            {
                string poster = v.Path.Substring(0, v.Path.Length - 4) + ".webp";
                bool hasPoster = File.Exists(Path.Combine(Store.Root, poster));
                sb.Append("<figure class=\"gl-vid\"><video src=\"" + Ui.Esc(v.Path) + "\"" + (hasPoster ? " poster=\"" + Ui.Esc(poster) + "\"" : string.Empty) + " controls preload=\"metadata\" playsinline aria-label=\"" + Ui.Esc(v.Alt.Length > 0 ? v.Alt : v.Name) + "\"></video>" + (v.Alt.Length > 0 ? "<figcaption>" + Ui.Esc(v.Alt) + "</figcaption>" : string.Empty) + "</figure>");
            }
            sb.Append("<div class=\"gl-grid\">");
            int i = 0;
            foreach (MediaItem m in list.Where(x => x.Kind == "photo"))
            {
                string label = site.GalleryTags.FirstOrDefault(g => g.Key == m.Tag)?.Label ?? string.Empty;
                sb.Append("<button type=\"button\" class=\"gl-tile\" data-full=\"" + Ui.Esc(m.Path) + "\" data-alt=\"" + Ui.Esc(m.Alt) + "\" data-tag=\"" + Ui.Esc(label) + "\" onclick=\"GalleryJs.view(" + i + ")\" aria-label=\"Open " + Ui.Esc(m.Alt.Length > 0 ? m.Alt : m.Name) + "\"><img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"" + Ui.Esc(m.Alt) + "\" loading=\"lazy\"><span>" + Ui.Esc(label) + "</span></button>");
                i++;
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private string DataPart(SiteData site, Block b, bool first)
        {
            if (b.Source != "gallerygrid")
            {
                return string.Empty;
            }
            return Sec(b, Head(site, b, true, first ? "h1" : "h2") + "<div class=\"gl-chips gl-bar\" id=\"gl-chiprow\" role=\"group\" aria-label=\"Filter the gallery\">" + Chips(site, lib, tag) + "</div><div id=\"gl-wall\" aria-live=\"polite\">" + WallHtml(site, lib, tag) + "</div>");
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
            StringBuilder sb = new StringBuilder("<div class=\"gl-foot-in\"><div><a class=\"gl-logo\" href=\"Home\"><svg viewBox=\"0 0 40 40\" width=\"34\" height=\"34\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"11\" fill=\"#231A17\"/><path d=\"M14 31c-1-7 13-7 13-14 0-4-3-7-7.5-7\" fill=\"none\" stroke=\"#E8C9A9\" stroke-width=\"3.2\" stroke-linecap=\"round\"/><path d=\"M20 32c0-5 9-5.5 9.5-11\" fill=\"none\" stroke=\"#C2553A\" stroke-width=\"2.6\" stroke-linecap=\"round\"/></svg><span>" + Ui.Esc(site.Brand) + "</span></a><p>" + Ui.Esc(site.Tagline) + "</p></div>");
            sb.Append("<div><h3>Visit</h3><ul><li>" + Ui.Esc(site.Street) + "</li><li>" + Ui.Esc(site.City) + "</li><li><a href=\"" + Ui.Esc(Tel(site.Phone)) + "\">" + Ui.Esc(site.Phone) + "</a></li><li><a href=\"mailto:" + Ui.Esc(site.Email) + "\">" + Ui.Esc(site.Email) + "</a></li></ul></div>");
            sb.Append("<div><h3>Hours</h3><ul class=\"gl-fhours\">" + HoursRows(site) + "</ul></div>");
            sb.Append("<div><h3>Salon</h3><ul><li><a href=\"Services\">Services &amp; prices</a></li><li><a href=\"Stylists\">Stylists</a></li><li><a href=\"Gallery\">Gallery</a></li><li><a href=\"Book\">Book online</a></li><li><a href=\"Contact\">Contact</a></li><li><a href=\"AdminLogin\">Staff sign in</a></li></ul></div></div>");
            sb.Append("<div class=\"gl-foot-b\"><span>&copy; " + DateTime.Today.Year + " " + Ui.Esc(site.Brand) + "</span><span>Pay at the salon by card or cash. No deposits.</span></div>");
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"gl-btn" + (main ? string.Empty : " gl-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"gl-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"gl-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"gl-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"gl-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "gl-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"gl-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"gl-wrap gl-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            return "<section class=\"gl-bhero" + (mv == string.Empty ? " gl-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"gl-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"gl-wrap gl-bhero-in\"><div class=\"gl-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"gl-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "gl-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"gl-bsplit" + (b.Flip ? " gl-bflip" : string.Empty) + (b.Media == string.Empty ? " gl-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"gl-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"gl-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"gl-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"gl-bcards gl-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"gl-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"gl-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"gl-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"gl-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"gl-bsteps gl-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"gl-bstats gl-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"gl-bpeople gl-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"gl-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"gl-bnoface\">" + Ui.Esc(Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"gl-bboxes gl-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"gl-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"gl-bticks\">");
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
            StringBuilder sb = new StringBuilder("<div class=\"gl-bfaq\">" + Head(site, b, false) + "<div class=\"gl-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"gl-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"gl-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"gl-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"gl-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "gl-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"gl-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
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
