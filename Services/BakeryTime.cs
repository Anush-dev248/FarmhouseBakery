namespace FarmhouseBakery
{
    // Dates are saved in UTC. The Azure server also runs in UTC, so ToLocalTime() would
    // show UTC; this converts to the bakery's own time zone (Atlantic Time) instead.
    public static class BakeryTime
    {
        private static readonly TimeZoneInfo Zone =
            TimeZoneInfo.FindSystemTimeZoneById("America/Halifax");

        public static DateTime ToBakeryTime(this DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        }
    }
}
