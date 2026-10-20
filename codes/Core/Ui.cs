using System.Globalization;
using System.Net;
using System.Text;

namespace HairShop.Core
{
    public static class Ui
    {
        public static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public static readonly string[] DayNames = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        public static string Esc(string? v)
        {
            return WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in v ?? string.Empty)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '"': sb.Append("\\\""); break;
                    case '<': sb.Append("\\u003C"); break;
                    case '>': sb.Append("\\u003E"); break;
                    case '&': sb.Append("\\u0026"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case (char)0x2028: sb.Append("\\u2028"); break;
                    case (char)0x2029: sb.Append("\\u2029"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        public static string Clip(string? v, int max)
        {
            string s = (v ?? string.Empty).Trim();
            return s.Length > max ? s.Substring(0, max).Trim() : s;
        }

        public static bool Has(string? text, string q)
        {
            return q.Length > 0 && (text ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        public static string Count(int n, string one, string many)
        {
            return n.ToString("#,0", Us) + " " + (n == 1 ? one : many);
        }

        public static string Money(decimal v)
        {
            return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("#,0.00", Us);
        }

        public static string Price(decimal v)
        {
            return v == Math.Floor(v) ? "$" + v.ToString("#,0", Us) : Money(v);
        }

        public static string Num(decimal v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string When(DateTime utc)
        {
            DateTime l = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            return l.ToString("MMM d, yyyy h:mm tt", Us);
        }

        public static string Day(DateTime d)
        {
            return d.ToString("ddd, MMM d", Us);
        }

        public static string LongDay(DateTime d)
        {
            return d.ToString("dddd, MMMM d, yyyy", Us);
        }

        public static string Time(DateTime d)
        {
            return d.ToString("h:mm tt", Us);
        }

        public static string Time(int minutes)
        {
            return DateTime.Today.AddMinutes(minutes).ToString("h:mm tt", Us);
        }

        public static string Iso(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static DateTime? ParseDay(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.Date : null;
        }

        public static int Int(string? v, int fallback)
        {
            return int.TryParse((v ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;
        }

        public static decimal? Dec(string? v)
        {
            string s = (v ?? string.Empty).Trim().TrimStart('$').Replace(",", string.Empty);
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? d : null;
        }

        public static string Minutes(int m)
        {
            if (m < 60)
            {
                return m + " min";
            }
            return m % 60 == 0 ? (m / 60) + " h" : (m / 60) + " h " + (m % 60) + " min";
        }

        public static string Initials(string name)
        {
            string[] parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        public static string Pager(string px, string fn, int page, int pages, int total, string one, string many)
        {
            StringBuilder sb = new StringBuilder("<div class=\"" + px + "-pager\"><span>Page " + page + " of " + Math.Max(1, pages) + " &middot; " + Count(total, one, many) + "</span><div>");
            sb.Append("<button type=\"button\" class=\"" + px + "-btn " + px + "-btn-ghost " + px + "-btn-sm\"" + (page <= 1 ? " disabled" : " onclick=\"" + fn + "(" + (page - 1) + ")\"") + ">Previous</button>");
            sb.Append("<button type=\"button\" class=\"" + px + "-btn " + px + "-btn-ghost " + px + "-btn-sm\"" + (page >= pages ? " disabled" : " onclick=\"" + fn + "(" + (page + 1) + ")\"") + ">Next</button>");
            return sb.Append("</div></div>").ToString();
        }

        public static string Status(string px, string status)
        {
            (string label, string cls) = status switch
            {
                "booked" => ("Booked", "new"),
                "arrived" => ("Arrived", "wait"),
                "done" => ("Done", "go"),
                "noshow" => ("No-show", "bad"),
                "cancelled" => ("Cancelled", "off"),
                _ => (status, "off")
            };
            return "<span class=\"" + px + "-pill " + px + "-pill-" + cls + "\">" + label + "</span>";
        }

        public static string StatusName(string status)
        {
            return status switch
            {
                "booked" => "Booked",
                "arrived" => "Arrived",
                "done" => "Done",
                "noshow" => "No-show",
                "cancelled" => "Cancelled",
                _ => status
            };
        }
    }
}
